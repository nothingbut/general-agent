use super::{CompressionStrategy, SemanticStrategy, SlidingWindowStrategy};
use crate::token_counter::TokenCounter;
use crate::Result;
use agent_core::models::Message;
use agent_core::traits::llm::LLMClient;
use async_trait::async_trait;
use std::sync::Arc;

/// 分层压缩策略
///
/// 根据消息数量和 token 数智能选择压缩策略：
/// - 小对话（<= 20 消息或 < 3000 tokens）→ 滑动窗口
/// - 中等对话（21-50 消息）→ 滑动窗口
/// - 大对话（> 50 消息或 >= 8000 tokens）→ 语义压缩
///
/// # 工作原理
///
/// 1. 评估消息列表的大小（消息数 + token 数）
/// 2. 根据阈值选择最优策略
/// 3. 委托给选中的策略执行压缩
///
/// # 适用场景
///
/// - 需要自动适应不同对话长度
/// - 希望平衡速度和压缩质量
/// - 通用场景（推荐作为默认策略）
///
/// # 性能特点
///
/// - 小对话：< 10ms（滑动窗口）
/// - 大对话：2-5 秒（语义压缩）
/// - 自动选择最优策略
///
/// # 示例
///
/// ```rust,ignore
/// let strategy = HierarchicalStrategy::new(
///     llm_client,
///     10,    // 滑动窗口大小
///     2000   // 语义压缩目标 token 数
/// )?;
/// let compressed = strategy.compress(&messages).await?;
/// ```
pub struct HierarchicalStrategy {
    sliding_window: SlidingWindowStrategy,
    semantic: SemanticStrategy,
    token_counter: TokenCounter,
    // 阈值配置
    small_message_threshold: usize,  // 消息数阈值（小对话）
    large_message_threshold: usize,  // 消息数阈值（大对话）
    large_token_threshold: usize,    // token 数阈值（大对话）
}

impl HierarchicalStrategy {
    /// 创建新的分层压缩策略
    ///
    /// # 参数
    ///
    /// - `llm_client`: LLM 客户端（用于语义压缩）
    /// - `window_size`: 滑动窗口大小
    /// - `semantic_target_tokens`: 语义压缩目标 token 数
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let strategy = HierarchicalStrategy::new(llm_client, 10, 2000)?;
    /// ```
    pub fn new(
        llm_client: Arc<dyn LLMClient>,
        window_size: usize,
        semantic_target_tokens: usize,
    ) -> Result<Self> {
        let sliding_window = SlidingWindowStrategy::new(window_size)?;
        let semantic = SemanticStrategy::new(llm_client, semantic_target_tokens)?;
        let token_counter = TokenCounter::new_for_claude()?;

        Ok(Self {
            sliding_window,
            semantic,
            token_counter,
            small_message_threshold: 20,
            large_message_threshold: 50,
            large_token_threshold: 8000,
        })
    }

    /// 使用自定义阈值创建策略
    pub fn new_with_thresholds(
        llm_client: Arc<dyn LLMClient>,
        window_size: usize,
        semantic_target_tokens: usize,
        small_message_threshold: usize,
        large_message_threshold: usize,
        large_token_threshold: usize,
    ) -> Result<Self> {
        let sliding_window = SlidingWindowStrategy::new(window_size)?;
        let semantic = SemanticStrategy::new(llm_client, semantic_target_tokens)?;
        let token_counter = TokenCounter::new_for_claude()?;

        Ok(Self {
            sliding_window,
            semantic,
            token_counter,
            small_message_threshold,
            large_message_threshold,
            large_token_threshold,
        })
    }

    /// 选择最优压缩策略
    ///
    /// # 策略选择逻辑
    ///
    /// - 消息数 <= 20 → 滑动窗口（快速）
    /// - 消息数 > 50 或 tokens >= 8000 → 语义压缩（高质量）
    /// - 其他情况 → 滑动窗口（平衡）
    fn select_strategy(&self, messages: &[Message]) -> StrategyChoice {
        let message_count = messages.len();
        let token_count = self.token_counter.count_messages(messages);

        // 小对话：使用滑动窗口
        if message_count <= self.small_message_threshold {
            return StrategyChoice::SlidingWindow;
        }

        // 大对话：使用语义压缩
        if message_count > self.large_message_threshold || token_count >= self.large_token_threshold
        {
            return StrategyChoice::Semantic;
        }

        // 中等对话：使用滑动窗口（速度优先）
        StrategyChoice::SlidingWindow
    }

    /// 获取策略配置信息
    pub fn config(&self) -> HierarchicalConfig {
        HierarchicalConfig {
            small_message_threshold: self.small_message_threshold,
            large_message_threshold: self.large_message_threshold,
            large_token_threshold: self.large_token_threshold,
        }
    }
}

/// 策略选择结果
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum StrategyChoice {
    SlidingWindow,
    Semantic,
}

/// 分层策略配置
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct HierarchicalConfig {
    pub small_message_threshold: usize,
    pub large_message_threshold: usize,
    pub large_token_threshold: usize,
}

#[async_trait]
impl CompressionStrategy for HierarchicalStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        let choice = self.select_strategy(messages);

        match choice {
            StrategyChoice::SlidingWindow => self.sliding_window.compress(messages).await,
            StrategyChoice::Semantic => self.semantic.compress(messages).await,
        }
    }

    fn name(&self) -> &str {
        "Hierarchical"
    }

    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{CompletionRequest, CompletionResponse, CompletionStream, ModelInfo, TokenUsage};
    use uuid::Uuid;

    // Mock LLM Client
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

    fn create_messages(count: usize) -> Vec<Message> {
        (0..count)
            .map(|i| {
                let role = if i % 2 == 0 {
                    MessageRole::User
                } else {
                    MessageRole::Assistant
                };
                create_message(role, &format!("Message {}", i))
            })
            .collect()
    }

    #[tokio::test]
    async fn test_new_strategy() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 10, 2000);
        assert!(strategy.is_ok());
    }

    #[tokio::test]
    async fn test_small_conversation_uses_sliding_window() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();

        // 10 条消息 → 应该使用滑动窗口
        let messages = create_messages(10);
        let choice = strategy.select_strategy(&messages);

        assert_eq!(choice, StrategyChoice::SlidingWindow);
    }

    #[tokio::test]
    async fn test_large_conversation_uses_semantic() {
        let client = MockLLMClient::new("用户和助手进行了长时间对话。");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();

        // 60 条消息 → 应该使用语义压缩
        let messages = create_messages(60);
        let choice = strategy.select_strategy(&messages);

        assert_eq!(choice, StrategyChoice::Semantic);
    }

    #[tokio::test]
    async fn test_medium_conversation_uses_sliding_window() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();

        // 30 条消息 → 中等对话，使用滑动窗口（速度优先）
        let messages = create_messages(30);
        let choice = strategy.select_strategy(&messages);

        assert_eq!(choice, StrategyChoice::SlidingWindow);
    }

    #[tokio::test]
    async fn test_compress_small_conversation() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 5, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::System, "System message"),
            create_message(MessageRole::User, "Q1"),
            create_message(MessageRole::Assistant, "A1"),
            create_message(MessageRole::User, "Q2"),
            create_message(MessageRole::Assistant, "A2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 小对话，使用滑动窗口，保留系统消息 + 最近 5 条
        assert!(compressed.len() <= 6); // 系统消息 + 5 条
        assert!(compressed[0].role == MessageRole::System);
    }

    #[tokio::test]
    async fn test_compress_large_conversation() {
        let client = MockLLMClient::new("这是一段很长的对话摘要。");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();

        let mut messages = vec![create_message(MessageRole::System, "System")];
        messages.extend(create_messages(60)); // 总共 61 条

        let compressed = strategy.compress(&messages).await.unwrap();

        // 大对话，使用语义压缩，应该保留系统消息 + 摘要
        assert_eq!(compressed.len(), 2);
        assert!(compressed[0].role == MessageRole::System);
        assert!(compressed[1].content.contains("[对话摘要]"));
    }

    #[tokio::test]
    async fn test_custom_thresholds() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new_with_thresholds(
            client,
            10,
            2000,
            10,  // small_message_threshold
            30,  // large_message_threshold
            5000, // large_token_threshold
        )
        .unwrap();

        let config = strategy.config();
        assert_eq!(config.small_message_threshold, 10);
        assert_eq!(config.large_message_threshold, 30);
        assert_eq!(config.large_token_threshold, 5000);
    }

    #[tokio::test]
    async fn test_token_threshold_triggers_semantic() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new_with_thresholds(
            client,
            10,
            2000,
            5,   // small_message_threshold = 5（消息数少于5才用滑动窗口）
            50,  // large_message_threshold
            50,  // large_token_threshold（非常低的 token 阈值）
        )
        .unwrap();

        // 创建 10 条消息（超过 small_threshold 5，但少于 large_threshold 50）
        // 每条消息都很长，确保总 token 数超过 50
        let mut messages = vec![];
        for i in 0..10 {
            let role = if i % 2 == 0 {
                MessageRole::User
            } else {
                MessageRole::Assistant
            };
            messages.push(create_message(
                role,
                &"A very long message with many tokens ".repeat(10),
            ));
        }

        let choice = strategy.select_strategy(&messages);
        // 应该因为 token 数超过阈值而使用语义压缩
        assert_eq!(choice, StrategyChoice::Semantic);
    }

    #[tokio::test]
    async fn test_name() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();
        assert_eq!(strategy.name(), "Hierarchical");
    }

    #[tokio::test]
    async fn test_estimate_tokens() {
        let client = MockLLMClient::new("Summary");
        let strategy = HierarchicalStrategy::new(client, 10, 2000).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "Hello world"),
            create_message(MessageRole::Assistant, "Hi there!"),
        ];

        let token_count = strategy.estimate_tokens(&messages);
        assert!(token_count > 0);
        assert!(token_count < 100);
    }
}
