use std::sync::Arc;

use async_trait::async_trait;
use tracing::{debug, warn};
use uuid::Uuid;

use agent_core::models::message::{Message, MessageRole};
use agent_core::traits::llm::{CompletionRequest, LLMClient};

use crate::error::{ExtractionError, Result};
use crate::models::{SkillDefinition, SkillParameter};

const MIN_MESSAGES_FOR_EXTRACTION: usize = 4;

const EXTRACTION_SYSTEM_PROMPT: &str = r#"你是一个技能抽取专家。分析用户与助手之间的对话，识别可复用的交互模式并生成技能定义。

技能是一种可参数化的提示词模板，能够将重复的对话模式转化为可一键调用的快捷方式。

请按以下 JSON 格式输出（不要添加其他文字）：

```json
{
  "name": "技能短名（英文，snake_case）",
  "namespace": "命名空间（如 personal, work, coding）",
  "description": "技能描述（中文）",
  "parameters": [
    {
      "name": "参数名",
      "param_type": "string",
      "required": true,
      "description": "参数描述",
      "default_value": null
    }
  ],
  "template": "模板内容，使用 {{ param_name }} 作为占位符"
}
```

规则：
1. name 必须是英文 snake_case，简洁且描述性
2. 从对话中提取可变部分作为参数
3. template 使用 {{ }} 双花括号作为占位符
4. 只在识别到明确的可复用模式时才生成技能
5. 如果对话没有可复用模式，返回空 JSON: {}
"#;

#[async_trait]
pub trait SkillExtractorTrait: Send + Sync {
    async fn extract_from_messages(&self, messages: &[Message]) -> Result<Option<SkillDefinition>>;
    async fn extract_with_hint(&self, messages: &[Message], hint: &str) -> Result<Option<SkillDefinition>>;
}

pub struct LlmSkillExtractor {
    llm_client: Arc<dyn LLMClient>,
    model: String,
}

impl LlmSkillExtractor {
    pub fn new(llm_client: Arc<dyn LLMClient>, model: String) -> Self {
        Self { llm_client, model }
    }

    fn build_extraction_messages(&self, messages: &[Message], hint: Option<&str>) -> Vec<Message> {
        let session_id = Uuid::nil();

        let mut conversation_text = String::new();
        for msg in messages {
            let role_label = match msg.role {
                MessageRole::User => "用户",
                MessageRole::Assistant => "助手",
                MessageRole::System => "系统",
            };
            conversation_text.push_str(&format!("[{}]: {}\n\n", role_label, msg.content));
        }

        let user_content = match hint {
            Some(h) => format!(
                "请分析以下对话并提取可复用技能。\n\n提示：{}\n\n---\n\n{}",
                h, conversation_text
            ),
            None => format!(
                "请分析以下对话并提取可复用技能。\n\n---\n\n{}",
                conversation_text
            ),
        };

        vec![Message::new(
            session_id,
            MessageRole::User,
            user_content,
        )]
    }

    fn parse_skill_from_response(&self, response: &str) -> Result<Option<SkillDefinition>> {
        let json_str = extract_json_block(response);
        let json_str = json_str.trim();

        if json_str.is_empty() || json_str == "{}" {
            debug!("LLM 未识别到可复用模式");
            return Ok(None);
        }

        let raw: serde_json::Value = serde_json::from_str(json_str)
            .map_err(|e| ExtractionError::ParseError(format!("JSON 解析失败: {}", e)))?;

        let name = raw.get("name")
            .and_then(|v| v.as_str())
            .ok_or_else(|| ExtractionError::ParseError("缺少 name 字段".into()))?
            .to_string();

        let namespace = raw.get("namespace")
            .and_then(|v| v.as_str())
            .map(|s| s.to_string());

        let description = raw.get("description")
            .and_then(|v| v.as_str())
            .unwrap_or("")
            .to_string();

        let template = raw.get("template")
            .and_then(|v| v.as_str())
            .unwrap_or("")
            .to_string();

        let parameters = match raw.get("parameters").and_then(|v| v.as_array()) {
            Some(params) => params.iter().filter_map(|p| {
                let param_name = p.get("name")?.as_str()?.to_string();
                let param_type = p.get("param_type")
                    .or_else(|| p.get("type"))
                    .and_then(|v| v.as_str())
                    .unwrap_or("string")
                    .to_string();
                let required = p.get("required")
                    .and_then(|v| v.as_bool())
                    .unwrap_or(false);
                let param_desc = p.get("description")
                    .and_then(|v| v.as_str())
                    .unwrap_or("")
                    .to_string();
                let default_value = p.get("default_value")
                    .or_else(|| p.get("default"))
                    .and_then(|v| {
                        if v.is_null() { None } else { Some(v.as_str().unwrap_or("").to_string()) }
                    });

                Some(SkillParameter {
                    name: param_name,
                    param_type,
                    required,
                    description: param_desc,
                    default_value,
                })
            }).collect(),
            None => vec![],
        };

        if name.is_empty() || template.is_empty() {
            warn!("技能定义不完整: name={}, template_len={}", name, template.len());
            return Err(ExtractionError::ValidationError(
                "技能名称和模板不能为空".into(),
            ));
        }

        Ok(Some(SkillDefinition {
            name,
            namespace,
            description,
            parameters,
            template,
        }))
    }
}

#[async_trait]
impl SkillExtractorTrait for LlmSkillExtractor {
    async fn extract_from_messages(&self, messages: &[Message]) -> Result<Option<SkillDefinition>> {
        if messages.len() < MIN_MESSAGES_FOR_EXTRACTION {
            return Err(ExtractionError::InsufficientMessages);
        }

        let extraction_messages = self.build_extraction_messages(messages, None);
        let request = CompletionRequest::new(extraction_messages, self.model.clone())
            .with_system_prompt(EXTRACTION_SYSTEM_PROMPT.to_string())
            .with_temperature(0.3)
            .with_max_tokens(2000);

        let response = self.llm_client.complete(request).await
            .map_err(|e| ExtractionError::LlmError(e.to_string()))?;

        debug!("LLM 抽取响应长度: {} 字符", response.content.len());
        self.parse_skill_from_response(&response.content)
    }

    async fn extract_with_hint(&self, messages: &[Message], hint: &str) -> Result<Option<SkillDefinition>> {
        if messages.len() < MIN_MESSAGES_FOR_EXTRACTION {
            return Err(ExtractionError::InsufficientMessages);
        }

        let extraction_messages = self.build_extraction_messages(messages, Some(hint));
        let request = CompletionRequest::new(extraction_messages, self.model.clone())
            .with_system_prompt(EXTRACTION_SYSTEM_PROMPT.to_string())
            .with_temperature(0.3)
            .with_max_tokens(2000);

        let response = self.llm_client.complete(request).await
            .map_err(|e| ExtractionError::LlmError(e.to_string()))?;

        self.parse_skill_from_response(&response.content)
    }
}

fn extract_json_block(text: &str) -> &str {
    if let Some(start) = text.find("```json") {
        let content_start = start + 7;
        if let Some(end) = text[content_start..].find("```") {
            return &text[content_start..content_start + end];
        }
    }
    if let Some(start) = text.find("```") {
        let content_start = start + 3;
        if let Some(newline) = text[content_start..].find('\n') {
            let after_lang = content_start + newline + 1;
            if let Some(end) = text[after_lang..].find("```") {
                return &text[after_lang..after_lang + end];
            }
        }
    }
    if let Some(start) = text.find('{') {
        if let Some(end) = text.rfind('}') {
            return &text[start..=end];
        }
    }
    text
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_extractor() -> LlmSkillExtractor {
        LlmSkillExtractor {
            llm_client: Arc::new(MockLlmClient),
            model: "test-model".to_string(),
        }
    }

    struct MockLlmClient;

    #[async_trait]
    impl LLMClient for MockLlmClient {
        async fn complete(&self, _request: CompletionRequest) -> agent_core::error::Result<agent_core::traits::llm::CompletionResponse> {
            Ok(agent_core::traits::llm::CompletionResponse {
                content: r#"```json
{
  "name": "code_review",
  "namespace": "coding",
  "description": "代码审查请求",
  "parameters": [
    {
      "name": "language",
      "param_type": "string",
      "required": true,
      "description": "编程语言",
      "default_value": null
    },
    {
      "name": "focus",
      "param_type": "string",
      "required": false,
      "description": "审查重点",
      "default_value": "general"
    }
  ],
  "template": "请审查以下 {{ language }} 代码，重点关注 {{ focus }} 方面。"
}
```"#.to_string(),
                model: "test".to_string(),
                usage: agent_core::traits::llm::TokenUsage {
                    prompt_tokens: 100,
                    completion_tokens: 50,
                    total_tokens: 150,
                },
                finish_reason: Some("stop".to_string()),
            })
        }

        async fn stream(&self, _request: CompletionRequest) -> agent_core::error::Result<Box<dyn agent_core::traits::llm::CompletionStream>> {
            unimplemented!()
        }

        async fn list_models(&self) -> agent_core::error::Result<Vec<agent_core::traits::llm::ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    fn test_messages(count: usize) -> Vec<Message> {
        let session_id = Uuid::new_v4();
        (0..count).map(|i| {
            let role = if i % 2 == 0 { MessageRole::User } else { MessageRole::Assistant };
            Message::new(session_id, role, format!("消息 {}", i))
        }).collect()
    }

    #[test]
    fn test_extract_json_block_from_markdown() {
        let text = "分析结果：\n```json\n{\"name\": \"test\"}\n```\n完成。";
        assert_eq!(extract_json_block(text).trim(), "{\"name\": \"test\"}");
    }

    #[test]
    fn test_extract_json_block_raw() {
        let text = "这是结果 {\"name\": \"test\"} 完毕";
        assert_eq!(extract_json_block(text), "{\"name\": \"test\"}");
    }

    #[test]
    fn test_extract_json_block_empty() {
        let text = "没有 JSON";
        assert_eq!(extract_json_block(text), "没有 JSON");
    }

    #[test]
    fn test_parse_complete_skill() {
        let extractor = make_extractor();
        let json = r#"```json
{
  "name": "greet",
  "namespace": "personal",
  "description": "问候用户",
  "parameters": [
    {"name": "user_name", "param_type": "string", "required": true, "description": "用户名"}
  ],
  "template": "你好 {{ user_name }}！"
}
```"#;
        let result = extractor.parse_skill_from_response(json).unwrap().unwrap();
        assert_eq!(result.name, "greet");
        assert_eq!(result.namespace, Some("personal".to_string()));
        assert_eq!(result.parameters.len(), 1);
        assert_eq!(result.parameters[0].name, "user_name");
        assert!(result.parameters[0].required);
        assert!(result.template.contains("{{ user_name }}"));
    }

    #[test]
    fn test_parse_empty_response() {
        let extractor = make_extractor();
        let result = extractor.parse_skill_from_response("{}").unwrap();
        assert!(result.is_none());
    }

    #[test]
    fn test_parse_missing_name() {
        let extractor = make_extractor();
        let json = r#"{"description": "test", "template": "hello"}"#;
        let result = extractor.parse_skill_from_response(json);
        assert!(result.is_err());
    }

    #[test]
    fn test_parse_with_type_fallback() {
        let extractor = make_extractor();
        let json = r#"{
            "name": "test",
            "description": "测试",
            "parameters": [{"name": "x", "type": "number", "required": true, "description": "数字"}],
            "template": "值: {{ x }}"
        }"#;
        let result = extractor.parse_skill_from_response(json).unwrap().unwrap();
        assert_eq!(result.parameters[0].param_type, "number");
    }

    #[tokio::test]
    async fn test_extract_insufficient_messages() {
        let extractor = make_extractor();
        let messages = test_messages(2);
        let result = extractor.extract_from_messages(&messages).await;
        assert!(matches!(result, Err(ExtractionError::InsufficientMessages)));
    }

    #[tokio::test]
    async fn test_extract_from_messages_success() {
        let extractor = make_extractor();
        let messages = test_messages(6);
        let result = extractor.extract_from_messages(&messages).await.unwrap().unwrap();
        assert_eq!(result.name, "code_review");
        assert_eq!(result.namespace, Some("coding".to_string()));
        assert_eq!(result.parameters.len(), 2);
    }

    #[tokio::test]
    async fn test_extract_with_hint() {
        let extractor = make_extractor();
        let messages = test_messages(6);
        let result = extractor.extract_with_hint(&messages, "关注代码审查模式").await.unwrap().unwrap();
        assert_eq!(result.name, "code_review");
    }
}
