use crate::error::{MemoryError, Result};
use crate::models::{Memory, MemoryType};
use agent_core::models::{Message, MessageRole};
use agent_core::traits::llm::{CompletionRequest, LLMClient};
use serde::{Deserialize, Serialize};
use std::sync::Arc;
use tracing::{debug, info, warn};
use uuid::Uuid;

const EXTRACTION_SYSTEM_PROMPT: &str = r#"你是一个记忆提取助手。分析用户和助手的对话，提取值得长期保存的记忆片段。

你需要识别以下 5 种记忆类型：
1. **user** - 用户的角色、偏好、能力、目标等个人信息
2. **feedback** - 用户对工作方式的反馈和偏好（做什么/不做什么）
3. **project** - 项目的目标、截止日期、决策、状态等信息
4. **reference** - 外部资源的链接和位置信息
5. **knowledge** - 领域知识、技术事实、最佳实践

规则：
- 只提取非显而易见的、对未来对话有价值的信息
- 不要提取可以从代码或 git 历史中推导的信息
- 不要提取临时性的调试过程
- 每条记忆应该是独立的、可理解的
- 将相对时间转换为绝对时间（如果有上下文的话）

返回 JSON 数组格式：
```json
[
  {
    "type": "user",
    "content": "记忆内容",
    "source": "来源描述"
  }
]
```

如果对话中没有值得提取的记忆，返回空数组 `[]`。
只返回 JSON，不要添加任何其他文字。"#;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExtractionResult {
    pub memories: Vec<ExtractedMemory>,
    pub message_count: usize,
    pub extraction_model: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExtractedMemory {
    #[serde(rename = "type")]
    pub memory_type: String,
    pub content: String,
    pub source: Option<String>,
}

impl ExtractedMemory {
    pub fn to_memory(&self, session_id: Option<Uuid>) -> Option<Memory> {
        let memory_type = MemoryType::from_str(&self.memory_type)?;
        let mut memory = Memory::new(memory_type, self.content.clone());
        if let Some(ref source) = self.source {
            memory = memory.with_source(source.clone());
        }
        if let Some(sid) = session_id {
            memory = memory.with_session(sid);
        }
        Some(memory)
    }
}

pub struct MemoryExtractor {
    llm_client: Arc<dyn LLMClient>,
    model: String,
    max_messages: usize,
}

impl MemoryExtractor {
    pub fn new(llm_client: Arc<dyn LLMClient>, model: String) -> Self {
        Self {
            llm_client,
            model,
            max_messages: 50,
        }
    }

    pub fn with_max_messages(mut self, max: usize) -> Self {
        self.max_messages = max;
        self
    }

    pub async fn extract_from_messages(
        &self,
        messages: &[Message],
        session_id: Option<Uuid>,
    ) -> Result<ExtractionResult> {
        if messages.is_empty() {
            return Ok(ExtractionResult {
                memories: Vec::new(),
                message_count: 0,
                extraction_model: self.model.clone(),
            });
        }

        let truncated = if messages.len() > self.max_messages {
            &messages[messages.len() - self.max_messages..]
        } else {
            messages
        };

        let conversation_text = Self::format_messages(truncated);
        debug!("Extracting memories from {} messages", truncated.len());

        let user_message = Message::new(
            session_id.unwrap_or_else(Uuid::new_v4),
            MessageRole::User,
            format!(
                "请分析以下对话，提取值得长期保存的记忆片段：\n\n{}",
                conversation_text
            ),
        );

        let request = CompletionRequest::new(vec![user_message], self.model.clone())
            .with_system_prompt(EXTRACTION_SYSTEM_PROMPT.to_string())
            .with_temperature(0.3)
            .with_max_tokens(2000);

        let response = self
            .llm_client
            .complete(request)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("LLM extraction failed: {}", e)))?;

        let extracted = self.parse_extraction_response(&response.content)?;

        info!(
            "Extracted {} memories from {} messages",
            extracted.len(),
            truncated.len()
        );

        Ok(ExtractionResult {
            memories: extracted,
            message_count: truncated.len(),
            extraction_model: response.model,
        })
    }

    fn format_messages(messages: &[Message]) -> String {
        messages
            .iter()
            .map(|m| {
                let role = match m.role {
                    MessageRole::User => "用户",
                    MessageRole::Assistant => "助手",
                    MessageRole::System => "系统",
                };
                format!("[{}]: {}", role, m.content)
            })
            .collect::<Vec<_>>()
            .join("\n")
    }

    fn parse_extraction_response(&self, response: &str) -> Result<Vec<ExtractedMemory>> {
        let trimmed = response.trim();

        // 尝试直接解析
        if let Ok(memories) = serde_json::from_str::<Vec<ExtractedMemory>>(trimmed) {
            return Ok(memories);
        }

        // 尝试提取 JSON 块（```json ... ```）
        if let Some(json_str) = Self::extract_json_block(trimmed) {
            if let Ok(memories) = serde_json::from_str::<Vec<ExtractedMemory>>(&json_str) {
                return Ok(memories);
            }
        }

        // 尝试找到第一个 [ 和最后一个 ]
        if let (Some(start), Some(end)) = (trimmed.find('['), trimmed.rfind(']')) {
            let json_str = &trimmed[start..=end];
            if let Ok(memories) = serde_json::from_str::<Vec<ExtractedMemory>>(json_str) {
                return Ok(memories);
            }
        }

        warn!("Failed to parse extraction response: {}", trimmed);
        Ok(Vec::new())
    }

    fn extract_json_block(text: &str) -> Option<String> {
        let start_markers = ["```json\n", "```json\r\n", "```\n"];
        for marker in &start_markers {
            if let Some(start) = text.find(marker) {
                let json_start = start + marker.len();
                if let Some(end) = text[json_start..].find("```") {
                    return Some(text[json_start..json_start + end].to_string());
                }
            }
        }
        None
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, ModelInfo, TokenUsage,
    };
    use async_trait::async_trait;

    struct MockLLMClient {
        response: String,
    }

    impl MockLLMClient {
        fn new(response: &str) -> Arc<Self> {
            Arc::new(Self {
                response: response.to_string(),
            })
        }
    }

    #[async_trait]
    impl LLMClient for MockLLMClient {
        async fn complete(
            &self,
            _request: CompletionRequest,
        ) -> agent_core::Result<CompletionResponse> {
            Ok(CompletionResponse {
                content: self.response.clone(),
                model: "mock-model".to_string(),
                usage: TokenUsage::new(100, 50),
                finish_reason: Some("stop".to_string()),
            })
        }

        async fn stream(
            &self,
            _request: CompletionRequest,
        ) -> agent_core::Result<Box<dyn CompletionStream>> {
            unimplemented!()
        }

        async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    #[tokio::test]
    async fn test_extract_empty_messages() {
        let client = MockLLMClient::new("[]");
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let result = extractor.extract_from_messages(&[], None).await.unwrap();
        assert!(result.memories.is_empty());
        assert_eq!(result.message_count, 0);
    }

    #[tokio::test]
    async fn test_extract_user_memory() {
        let response = r#"[{"type": "user", "content": "用户是一名数据科学家", "source": "对话"}]"#;
        let client = MockLLMClient::new(response);
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "我是一名数据科学家".to_string()),
            Message::new(Uuid::new_v4(), MessageRole::Assistant, "好的，我会根据您的背景调整回答".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(result.memories.len(), 1);
        assert_eq!(result.memories[0].memory_type, "user");
        assert_eq!(result.memories[0].content, "用户是一名数据科学家");
    }

    #[tokio::test]
    async fn test_extract_multiple_types() {
        let response = r#"[
            {"type": "user", "content": "用户熟悉 Rust", "source": "对话"},
            {"type": "feedback", "content": "不要使用 mock 测试", "source": "用户指示"},
            {"type": "project", "content": "2026-04-30 前完成记忆系统", "source": "计划"}
        ]"#;
        let client = MockLLMClient::new(response);
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "test".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(result.memories.len(), 3);
        assert_eq!(result.memories[0].memory_type, "user");
        assert_eq!(result.memories[1].memory_type, "feedback");
        assert_eq!(result.memories[2].memory_type, "project");
    }

    #[tokio::test]
    async fn test_extract_with_json_block() {
        let response = "分析结果如下：\n```json\n[{\"type\": \"reference\", \"content\": \"Bug tracker: Linear INGEST\", \"source\": \"对话\"}]\n```\n以上是提取的记忆。";
        let client = MockLLMClient::new(response);
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "test".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(result.memories.len(), 1);
        assert_eq!(result.memories[0].memory_type, "reference");
    }

    #[tokio::test]
    async fn test_extract_with_prefix_text() {
        let response = "以下是提取结果：\n[{\"type\": \"knowledge\", \"content\": \"Rust 的所有权系统保证内存安全\", \"source\": \"讨论\"}]";
        let client = MockLLMClient::new(response);
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "test".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(result.memories.len(), 1);
        assert_eq!(result.memories[0].memory_type, "knowledge");
    }

    #[tokio::test]
    async fn test_extract_unparseable_response() {
        let client = MockLLMClient::new("这不是一个有效的 JSON 响应");
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "test".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert!(result.memories.is_empty());
    }

    #[tokio::test]
    async fn test_extract_empty_array() {
        let client = MockLLMClient::new("[]");
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "你好".to_string()),
            Message::new(Uuid::new_v4(), MessageRole::Assistant, "你好！".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert!(result.memories.is_empty());
        assert_eq!(result.message_count, 2);
    }

    #[tokio::test]
    async fn test_extracted_memory_to_memory() {
        let extracted = ExtractedMemory {
            memory_type: "user".to_string(),
            content: "User is a Rust developer".to_string(),
            source: Some("conversation".to_string()),
        };

        let sid = Uuid::new_v4();
        let memory = extracted.to_memory(Some(sid)).unwrap();
        assert_eq!(memory.memory_type, MemoryType::User);
        assert_eq!(memory.content, "User is a Rust developer");
        assert_eq!(memory.source.as_deref(), Some("conversation"));
        assert_eq!(memory.session_id, Some(sid));
    }

    #[tokio::test]
    async fn test_extracted_memory_invalid_type() {
        let extracted = ExtractedMemory {
            memory_type: "invalid_type".to_string(),
            content: "something".to_string(),
            source: None,
        };

        assert!(extracted.to_memory(None).is_none());
    }

    #[tokio::test]
    async fn test_max_messages_truncation() {
        let client = MockLLMClient::new("[]");
        let extractor = MemoryExtractor::new(client, "mock".to_string())
            .with_max_messages(5);

        let messages: Vec<Message> = (0..20)
            .map(|i| Message::new(Uuid::new_v4(), MessageRole::User, format!("msg {}", i)))
            .collect();

        let result = extractor.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(result.message_count, 5);
    }

    #[tokio::test]
    async fn test_extract_with_session_id() {
        let response = r#"[{"type": "project", "content": "项目进入测试阶段", "source": "会话"}]"#;
        let client = MockLLMClient::new(response);
        let extractor = MemoryExtractor::new(client, "mock".to_string());

        let sid = Uuid::new_v4();
        let messages = vec![
            Message::new(sid, MessageRole::User, "项目现在进入测试阶段了".to_string()),
        ];

        let result = extractor.extract_from_messages(&messages, Some(sid)).await.unwrap();
        assert_eq!(result.memories.len(), 1);

        let memory = result.memories[0].to_memory(Some(sid)).unwrap();
        assert_eq!(memory.session_id, Some(sid));
    }

    #[test]
    fn test_format_messages() {
        let sid = Uuid::new_v4();
        let messages = vec![
            Message::new(sid, MessageRole::User, "你好".to_string()),
            Message::new(sid, MessageRole::Assistant, "你好！有什么可以帮助你的？".to_string()),
        ];

        let formatted = MemoryExtractor::format_messages(&messages);
        assert!(formatted.contains("[用户]: 你好"));
        assert!(formatted.contains("[助手]: 你好！有什么可以帮助你的？"));
    }
}
