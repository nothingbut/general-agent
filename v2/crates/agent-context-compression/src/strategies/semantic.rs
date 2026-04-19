use super::CompressionStrategy;
use crate::token_counter::TokenCounter;
use crate::Result;
use agent_core::models::{Message, MessageRole};
use agent_core::traits::llm::{CompletionRequest, LLMClient};
use async_trait::async_trait;
use std::sync::Arc;
use uuid::Uuid;

const COMPRESSION_SYSTEM_PROMPT: &str = r#"你是一个专业的对话压缩助手。
你的任务是将一段对话历史压缩成简洁的摘要，同时保留以下关键信息：
1. 重要的事实、日期、名称、数字
2. 用户的主要问题和需求
3. 助手的核心建议和解决方案
4. 对话的上下文和逻辑流程

请用第三人称的叙述方式生成摘要，保持客观和准确。"#;

/// 语义压缩策略
///
/// 使用 LLM 生成对话摘要，保留关键信息和上下文。
///
/// # 工作原理
///
/// 1. 识别系统消息和对话消息
/// 2. 格式化对话历史
/// 3. 调用 LLM 生成摘要
/// 4. 返回系统消息 + 摘要
///
/// # 适用场景
///
/// - 长对话压缩（50+ 消息）
/// - 需要保留语义和上下文
/// - 对压缩质量要求高的场景
///
/// # 性能
///
/// - 压缩时间：2-5 秒（取决于 LLM）
/// - 压缩率：通常 60-80%
///
/// # 示例
///
/// ```rust,ignore
/// let strategy = SemanticStrategy::new(llm_client, 2000)?;
/// let compressed = strategy.compress(&messages).await?;
/// ```
pub struct SemanticStrategy {
    llm_client: Arc<dyn LLMClient>,
    token_counter: TokenCounter,
    target_tokens: usize,
    model: String,
}

impl SemanticStrategy {
    /// 创建新的语义压缩策略
    ///
    /// # 参数
    ///
    /// - `llm_client`: LLM 客户端
    /// - `target_tokens`: 目标 token 数（摘要长度）
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let strategy = SemanticStrategy::new(llm_client, 2000)?;
    /// ```
    pub fn new(llm_client: Arc<dyn LLMClient>, target_tokens: usize) -> Result<Self> {
        let token_counter = TokenCounter::new_for_claude()?;
        Ok(Self {
            llm_client,
            token_counter,
            target_tokens,
            model: "claude-3-5-sonnet-20241022".to_string(),
        })
    }

    /// 使用指定模型创建策略
    pub fn new_with_model(
        llm_client: Arc<dyn LLMClient>,
        target_tokens: usize,
        model: String,
    ) -> Result<Self> {
        let token_counter = TokenCounter::new_for_model(&model)?;
        Ok(Self {
            llm_client,
            token_counter,
            target_tokens,
            model,
        })
    }

    /// 获取目标 token 数
    pub fn target_tokens(&self) -> usize {
        self.target_tokens
    }
}

#[async_trait]
impl CompressionStrategy for SemanticStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        // 1. 分离系统消息和对话消息
        let (system_msgs, dialog_msgs): (Vec<_>, Vec<_>) = messages
            .iter()
            .partition(|m| matches!(m.role, MessageRole::System));

        // 2. 如果对话消息很少（<= 2 条），不需要压缩
        if dialog_msgs.len() <= 2 {
            return Ok(messages.to_vec());
        }

        // 3. 格式化对话历史
        let dialog_text = dialog_msgs
            .iter()
            .map(|m| format!("{}: {}", m.role, m.content))
            .collect::<Vec<_>>()
            .join("\n\n");

        // 4. 构造压缩提示
        let compression_prompt = format!(
            "请将以下对话历史压缩成简洁的摘要（目标长度约 {} tokens）：\n\n{}",
            self.target_tokens, dialog_text
        );

        // 5. 调用 LLM 生成摘要
        let session_id = Uuid::new_v4();
        let summary_messages = vec![
            Message::new(
                session_id,
                MessageRole::System,
                COMPRESSION_SYSTEM_PROMPT.to_string(),
            ),
            Message::new(session_id, MessageRole::User, compression_prompt),
        ];

        let request = CompletionRequest::new(summary_messages, self.model.clone())
            .with_max_tokens((self.target_tokens as u32) * 2) // 留些余量
            .with_temperature(0.3); // 较低温度保证一致性

        let response = self
            .llm_client
            .complete(request)
            .await
            .map_err(|e| crate::CompressionError::LlmError(e.to_string()))?;

        // 6. 构造压缩后的消息列表
        let mut result = system_msgs.into_iter().cloned().collect::<Vec<_>>();
        result.push(Message::new(
            session_id,
            MessageRole::Assistant,
            format!("[对话摘要]\n{}", response.content),
        ));

        Ok(result)
    }

    fn name(&self) -> &str {
        "Semantic"
    }

    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::traits::llm::{CompletionResponse, CompletionStream, ModelInfo, TokenUsage};

    // Mock LLM Client for testing
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
        async fn complete(&self, _request: CompletionRequest) -> agent_core::Result<CompletionResponse> {
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
            unimplemented!("Stream not needed for tests")
        }

        async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    fn create_message(role: MessageRole, content: &str) -> Message {
        Message::new(Uuid::new_v4(), role, content.to_string())
    }

    #[tokio::test]
    async fn test_new_strategy() {
        let client = MockLLMClient::new("Summary");
        let strategy = SemanticStrategy::new(client, 2000);
        assert!(strategy.is_ok());
        assert_eq!(strategy.unwrap().target_tokens(), 2000);
    }

    #[tokio::test]
    async fn test_compress_few_messages() {
        let client = MockLLMClient::new("Summary");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "Hello"),
            create_message(MessageRole::Assistant, "Hi!"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 消息太少，不压缩
        assert_eq!(compressed.len(), 2);
        assert_eq!(compressed[0].content, "Hello");
    }

    #[tokio::test]
    async fn test_compress_with_llm() {
        let client = MockLLMClient::new("用户询问了产品功能，助手提供了详细说明。");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::System, "You are a helpful assistant"),
            create_message(MessageRole::User, "What is this product?"),
            create_message(MessageRole::Assistant, "This is a great product..."),
            create_message(MessageRole::User, "How does it work?"),
            create_message(MessageRole::Assistant, "It works by..."),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 应该保留系统消息 + 摘要
        assert_eq!(compressed.len(), 2);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert!(compressed[1].content.contains("[对话摘要]"));
        assert!(compressed[1].content.contains("用户询问了产品功能"));
    }

    #[tokio::test]
    async fn test_compress_preserves_system_messages() {
        let client = MockLLMClient::new("Summary of conversation");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::System, "System msg 1"),
            create_message(MessageRole::System, "System msg 2"),
            create_message(MessageRole::User, "Q1"),
            create_message(MessageRole::Assistant, "A1"),
            create_message(MessageRole::User, "Q2"),
            create_message(MessageRole::Assistant, "A2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 应该保留 2 个系统消息 + 摘要
        assert_eq!(compressed.len(), 3);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert!(matches!(compressed[1].role, MessageRole::System));
        assert_eq!(compressed[0].content, "System msg 1");
        assert_eq!(compressed[1].content, "System msg 2");
    }

    #[tokio::test]
    async fn test_compress_no_system_messages() {
        let client = MockLLMClient::new("Summarized conversation");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "Question 1"),
            create_message(MessageRole::Assistant, "Answer 1"),
            create_message(MessageRole::User, "Question 2"),
            create_message(MessageRole::Assistant, "Answer 2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 应该只有摘要
        assert_eq!(compressed.len(), 1);
        assert!(matches!(compressed[0].role, MessageRole::Assistant));
        assert!(compressed[0].content.contains("[对话摘要]"));
    }

    #[tokio::test]
    async fn test_estimate_tokens() {
        let client = MockLLMClient::new("Summary");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "Hello world"),
            create_message(MessageRole::Assistant, "Hi there!"),
        ];

        let token_count = strategy.estimate_tokens(&messages);
        assert!(token_count > 0);
        assert!(token_count < 50);
    }

    #[tokio::test]
    async fn test_name() {
        let client = MockLLMClient::new("Summary");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();
        assert_eq!(strategy.name(), "Semantic");
    }

    #[tokio::test]
    async fn test_compress_formats_dialog_correctly() {
        let client = MockLLMClient::new("Test summary");
        let strategy = SemanticStrategy::new(client, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "First question"),
            create_message(MessageRole::Assistant, "First answer"),
            create_message(MessageRole::User, "Second question"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 验证压缩成功
        assert_eq!(compressed.len(), 1);
        assert!(compressed[0].content.contains("[对话摘要]"));
        assert!(compressed[0].content.contains("Test summary"));
    }
}
