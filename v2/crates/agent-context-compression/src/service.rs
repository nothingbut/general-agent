use crate::models::{CompressionConfig, CompressionRecord, CompressionResult};
use crate::strategies::{
    CompressionStrategy, HierarchicalStrategy, SemanticStrategy, SlidingWindowStrategy,
    StrategyType,
};
use crate::token_counter::TokenCounter;
use crate::Result;
use agent_core::models::Message;
use agent_core::traits::llm::LLMClient;
use std::collections::HashMap;
use std::sync::Arc;

/// 压缩服务
///
/// 提供消息压缩的核心服务，支持：
/// - 自动压缩（基于配置阈值）
/// - 手动压缩（指定策略）
/// - 压缩历史记录
///
/// # 示例
///
/// ```rust,ignore
/// let service = CompressionService::new(llm_client, config)?;
///
/// // 自动压缩（根据配置决定是否压缩）
/// let result = service.auto_compress(&messages).await?;
///
/// // 手动压缩（指定策略）
/// let result = service.compress_with_strategy(
///     &messages,
///     StrategyType::Semantic
/// ).await?;
/// ```
pub struct CompressionService {
    config: CompressionConfig,
    strategies: HashMap<StrategyType, Box<dyn CompressionStrategy>>,
    token_counter: TokenCounter,
    compression_history: Vec<CompressionRecord>,
}

impl CompressionService {
    /// 创建新的压缩服务
    ///
    /// # 参数
    ///
    /// - `llm_client`: LLM 客户端（用于语义压缩）
    /// - `config`: 压缩配置
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let config = CompressionConfig::default();
    /// let service = CompressionService::new(llm_client, config)?;
    /// ```
    pub fn new(llm_client: Arc<dyn LLMClient>, config: CompressionConfig) -> Result<Self> {
        let token_counter = TokenCounter::new_for_claude()?;

        // 初始化所有策略
        let mut strategies: HashMap<StrategyType, Box<dyn CompressionStrategy>> = HashMap::new();

        strategies.insert(
            StrategyType::SlidingWindow,
            Box::new(SlidingWindowStrategy::new(config.sliding_window_size)?),
        );

        strategies.insert(
            StrategyType::Semantic,
            Box::new(SemanticStrategy::new(
                llm_client.clone(),
                config.semantic_target_tokens,
            )?),
        );

        strategies.insert(
            StrategyType::Hierarchical,
            Box::new(HierarchicalStrategy::new(
                llm_client.clone(),
                config.sliding_window_size,
                config.semantic_target_tokens,
            )?),
        );

        Ok(Self {
            config,
            strategies,
            token_counter,
            compression_history: Vec::new(),
        })
    }

    /// 自动压缩消息
    ///
    /// 根据配置的阈值自动决定是否需要压缩。
    /// 如果消息数量少于阈值，直接返回原消息。
    ///
    /// # 参数
    ///
    /// - `messages`: 待压缩的消息列表
    ///
    /// # 返回
    ///
    /// 压缩结果，包含压缩后的消息和元数据
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let result = service.auto_compress(&messages).await?;
    /// println!("压缩比例: {:.2}%", result.compression_ratio * 100.0);
    /// ```
    pub async fn auto_compress(&mut self, messages: &[Message]) -> Result<CompressionResult> {
        let original_count = messages.len();
        let original_tokens = self.token_counter.count_messages(messages);

        // 检查是否需要压缩
        if original_count < self.config.auto_trigger_threshold {
            return Ok(CompressionResult {
                compressed_messages: messages.to_vec(),
                original_count,
                compressed_count: original_count,
                original_tokens,
                compressed_tokens: original_tokens,
                compression_ratio: 1.0,
                strategy_used: None,
            });
        }

        // 使用默认策略（Hierarchical）进行压缩
        self.compress_with_strategy(messages, StrategyType::Hierarchical)
            .await
    }

    /// 使用指定策略压缩消息
    ///
    /// # 参数
    ///
    /// - `messages`: 待压缩的消息列表
    /// - `strategy_type`: 压缩策略类型
    ///
    /// # 返回
    ///
    /// 压缩结果，包含压缩后的消息和元数据
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let result = service.compress_with_strategy(
    ///     &messages,
    ///     StrategyType::Semantic
    /// ).await?;
    /// ```
    pub async fn compress_with_strategy(
        &mut self,
        messages: &[Message],
        strategy_type: StrategyType,
    ) -> Result<CompressionResult> {
        let original_count = messages.len();
        let original_tokens = self.token_counter.count_messages(messages);

        // 获取策略并执行压缩
        let strategy = self
            .strategies
            .get(&strategy_type)
            .ok_or_else(|| crate::CompressionError::StrategyNotFound(strategy_type.as_str().to_string()))?;

        let compressed_messages = strategy.compress(messages).await?;

        let compressed_count = compressed_messages.len();
        let compressed_tokens = self.token_counter.count_messages(&compressed_messages);

        let compression_ratio = if original_tokens > 0 {
            compressed_tokens as f64 / original_tokens as f64
        } else {
            1.0
        };

        // 记录压缩历史
        let record = CompressionRecord {
            timestamp: chrono::Utc::now(),
            strategy_used: strategy_type.as_str().to_string(),
            original_message_count: original_count,
            compressed_message_count: compressed_count,
            original_token_count: original_tokens,
            compressed_token_count: compressed_tokens,
            compression_ratio,
        };
        self.compression_history.push(record);

        Ok(CompressionResult {
            compressed_messages,
            original_count,
            compressed_count,
            original_tokens,
            compressed_tokens,
            compression_ratio,
            strategy_used: Some(strategy_type.as_str().to_string()),
        })
    }

    /// 获取压缩配置
    pub fn config(&self) -> &CompressionConfig {
        &self.config
    }

    /// 获取压缩历史
    pub fn history(&self) -> &[CompressionRecord] {
        &self.compression_history
    }

    /// 清除压缩历史
    pub fn clear_history(&mut self) {
        self.compression_history.clear();
    }

    /// 获取最近一次压缩记录
    pub fn last_compression(&self) -> Option<&CompressionRecord> {
        self.compression_history.last()
    }

    /// 估算消息的 token 数
    pub fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }

    /// 检查是否需要压缩
    pub fn should_compress(&self, messages: &[Message]) -> bool {
        messages.len() >= self.config.auto_trigger_threshold
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{CompletionRequest, CompletionResponse, CompletionStream, ModelInfo, TokenUsage};
    use async_trait::async_trait;
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
    async fn test_new_service() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let service = CompressionService::new(client, config);
        assert!(service.is_ok());
    }

    #[tokio::test]
    async fn test_auto_compress_below_threshold() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig {
            auto_trigger_threshold: 15,
            ..Default::default()
        };
        let mut service = CompressionService::new(client, config).unwrap();

        // 只有 10 条消息，低于阈值
        let messages = create_messages(10);
        let result = service.auto_compress(&messages).await.unwrap();

        // 应该不压缩
        assert_eq!(result.compressed_count, 10);
        assert_eq!(result.compression_ratio, 1.0);
        assert!(result.strategy_used.is_none());
    }

    #[tokio::test]
    async fn test_auto_compress_above_threshold() {
        let client = MockLLMClient::new("对话摘要");
        let config = CompressionConfig {
            auto_trigger_threshold: 15,
            ..Default::default()
        };
        let mut service = CompressionService::new(client, config).unwrap();

        // 20 条消息，超过阈值
        let messages = create_messages(20);
        let result = service.auto_compress(&messages).await.unwrap();

        // 应该压缩
        assert!(result.compressed_count < result.original_count);
        assert!(result.compression_ratio < 1.0);
        assert!(result.strategy_used.is_some());
        assert_eq!(result.strategy_used.as_ref().unwrap(), "hierarchical");
    }

    #[tokio::test]
    async fn test_compress_with_sliding_window() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        let messages = create_messages(20);
        let result = service
            .compress_with_strategy(&messages, StrategyType::SlidingWindow)
            .await
            .unwrap();

        assert_eq!(result.original_count, 20);
        assert_eq!(result.compressed_count, 10); // 默认窗口大小
        assert!(result.compression_ratio < 1.0);
        assert_eq!(result.strategy_used.as_ref().unwrap(), "sliding_window");
    }

    #[tokio::test]
    async fn test_compress_with_semantic() {
        let client = MockLLMClient::new("用户和助手进行了对话。");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        let messages = vec![
            create_message(MessageRole::System, "System"),
            create_message(MessageRole::User, "Q1"),
            create_message(MessageRole::Assistant, "A1"),
            create_message(MessageRole::User, "Q2"),
            create_message(MessageRole::Assistant, "A2"),
        ];

        let result = service
            .compress_with_strategy(&messages, StrategyType::Semantic)
            .await
            .unwrap();

        assert_eq!(result.original_count, 5);
        assert_eq!(result.compressed_count, 2); // 系统消息 + 摘要
        assert!(result.compression_ratio < 1.0);
        assert_eq!(result.strategy_used.as_ref().unwrap(), "semantic");
    }

    #[tokio::test]
    async fn test_compress_with_hierarchical() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        let messages = create_messages(30);
        let result = service
            .compress_with_strategy(&messages, StrategyType::Hierarchical)
            .await
            .unwrap();

        assert_eq!(result.original_count, 30);
        assert!(result.compressed_count < result.original_count);
        assert!(result.compression_ratio < 1.0);
        assert_eq!(result.strategy_used.as_ref().unwrap(), "hierarchical");
    }

    #[tokio::test]
    async fn test_compression_history() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        assert_eq!(service.history().len(), 0);

        let messages = create_messages(20);
        service
            .compress_with_strategy(&messages, StrategyType::SlidingWindow)
            .await
            .unwrap();

        assert_eq!(service.history().len(), 1);
        assert!(service.last_compression().is_some());

        let last = service.last_compression().unwrap();
        assert_eq!(last.strategy_used, "sliding_window");
        assert_eq!(last.original_message_count, 20);
    }

    #[tokio::test]
    async fn test_clear_history() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        let messages = create_messages(20);
        service
            .compress_with_strategy(&messages, StrategyType::SlidingWindow)
            .await
            .unwrap();

        assert_eq!(service.history().len(), 1);

        service.clear_history();
        assert_eq!(service.history().len(), 0);
    }

    #[tokio::test]
    async fn test_estimate_tokens() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let service = CompressionService::new(client, config).unwrap();

        let messages = vec![
            create_message(MessageRole::User, "Hello world"),
            create_message(MessageRole::Assistant, "Hi there!"),
        ];

        let token_count = service.estimate_tokens(&messages);
        assert!(token_count > 0);
        assert!(token_count < 100);
    }

    #[tokio::test]
    async fn test_should_compress() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig {
            auto_trigger_threshold: 15,
            ..Default::default()
        };
        let service = CompressionService::new(client, config).unwrap();

        let small_messages = create_messages(10);
        assert!(!service.should_compress(&small_messages));

        let large_messages = create_messages(20);
        assert!(service.should_compress(&large_messages));
    }

    #[tokio::test]
    async fn test_config_access() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig {
            auto_trigger_threshold: 20,
            sliding_window_size: 15,
            semantic_target_tokens: 3000,
            auto_compression_enabled: true,
        };
        let service = CompressionService::new(client, config).unwrap();

        let retrieved_config = service.config();
        assert_eq!(retrieved_config.auto_trigger_threshold, 20);
        assert_eq!(retrieved_config.sliding_window_size, 15);
        assert_eq!(retrieved_config.semantic_target_tokens, 3000);
    }

    #[tokio::test]
    async fn test_multiple_compressions() {
        let client = MockLLMClient::new("Summary");
        let config = CompressionConfig::default();
        let mut service = CompressionService::new(client, config).unwrap();

        // 第一次压缩
        let messages1 = create_messages(20);
        service
            .compress_with_strategy(&messages1, StrategyType::SlidingWindow)
            .await
            .unwrap();

        // 第二次压缩
        let messages2 = create_messages(30);
        service
            .compress_with_strategy(&messages2, StrategyType::Hierarchical)
            .await
            .unwrap();

        // 历史记录应该有 2 条
        assert_eq!(service.history().len(), 2);
        assert_eq!(service.history()[0].strategy_used, "sliding_window");
        assert_eq!(service.history()[1].strategy_used, "hierarchical");
    }
}
