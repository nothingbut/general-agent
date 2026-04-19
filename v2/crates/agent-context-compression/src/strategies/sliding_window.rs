use super::CompressionStrategy;
use crate::token_counter::TokenCounter;
use crate::Result;
use agent_core::models::{Message, MessageRole};
use async_trait::async_trait;

/// 滑动窗口压缩策略
///
/// 保留系统消息和最近的 N 条消息，丢弃较早的对话历史。
///
/// # 工作原理
///
/// 1. 识别所有系统消息（role = "system"）
/// 2. 保留系统消息（始终保留）
/// 3. 从非系统消息中选择最近的 N 条
/// 4. 合并结果，系统消息在前
///
/// # 适用场景
///
/// - 简单对话场景
/// - 需要快速压缩（< 10ms）
/// - Token 使用量适中的场景
///
/// # 示例
///
/// ```rust,ignore
/// let strategy = SlidingWindowStrategy::new(10)?;
/// let compressed = strategy.compress(&messages).await?;
/// ```
pub struct SlidingWindowStrategy {
    window_size: usize,
    token_counter: TokenCounter,
}

impl SlidingWindowStrategy {
    /// 创建新的滑动窗口策略
    ///
    /// # 参数
    ///
    /// - `window_size`: 窗口大小（保留的总消息数，包括系统消息）
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let strategy = SlidingWindowStrategy::new(10)?;
    /// ```
    pub fn new(window_size: usize) -> Result<Self> {
        let token_counter = TokenCounter::new_for_claude()?;
        Ok(Self {
            window_size,
            token_counter,
        })
    }

    /// 使用指定模型创建策略
    pub fn new_with_model(window_size: usize, model: &str) -> Result<Self> {
        let token_counter = TokenCounter::new_for_model(model)?;
        Ok(Self {
            window_size,
            token_counter,
        })
    }

    /// 获取窗口大小
    pub fn window_size(&self) -> usize {
        self.window_size
    }
}

#[async_trait]
impl CompressionStrategy for SlidingWindowStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        // 1. 如果消息数量不超过窗口大小，直接返回
        if messages.len() <= self.window_size {
            return Ok(messages.to_vec());
        }

        // 2. 分离系统消息和非系统消息
        let (system_msgs, other_msgs): (Vec<_>, Vec<_>) = messages
            .iter()
            .partition(|m| matches!(m.role, MessageRole::System));

        // 3. 计算可以保留的非系统消息数量
        let keep_count = self.window_size.saturating_sub(system_msgs.len());

        // 4. 保留最近的 N 条非系统消息
        let kept_msgs: Vec<Message> = other_msgs
            .into_iter()
            .rev() // 反转以从最后开始
            .take(keep_count) // 取最近的 N 条
            .rev() // 恢复原顺序
            .cloned()
            .collect();

        // 5. 合并结果：系统消息在前，最近消息在后
        let mut result = system_msgs.into_iter().cloned().collect::<Vec<_>>();
        result.extend(kept_msgs);

        Ok(result)
    }

    fn name(&self) -> &str {
        "SlidingWindow"
    }

    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    fn create_message(role: MessageRole, content: &str) -> Message {
        Message::new(Uuid::new_v4(), role, content.to_string())
    }

    #[tokio::test]
    async fn test_new_strategy() {
        let strategy = SlidingWindowStrategy::new(10);
        assert!(strategy.is_ok());
        assert_eq!(strategy.unwrap().window_size(), 10);
    }

    #[tokio::test]
    async fn test_compress_under_threshold() {
        let strategy = SlidingWindowStrategy::new(10).unwrap();
        let messages = vec![
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 消息数少于窗口大小，不应压缩
        assert_eq!(compressed.len(), 2);
        assert_eq!(compressed[0].content, "msg 1");
        assert_eq!(compressed[1].content, "msg 2");
    }

    #[tokio::test]
    async fn test_compress_exact_threshold() {
        let strategy = SlidingWindowStrategy::new(3).unwrap();
        let messages = vec![
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
            create_message(MessageRole::User, "msg 3"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 消息数等于窗口大小，不应压缩
        assert_eq!(compressed.len(), 3);
    }

    #[tokio::test]
    async fn test_compress_over_threshold() {
        let strategy = SlidingWindowStrategy::new(5).unwrap();
        let messages = vec![
            create_message(MessageRole::System, "You are a helpful assistant"),
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
            create_message(MessageRole::User, "msg 3"),
            create_message(MessageRole::Assistant, "msg 4"),
            create_message(MessageRole::User, "msg 5"),
            create_message(MessageRole::Assistant, "msg 6"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 应该保留系统消息 + 最近 4 条
        assert_eq!(compressed.len(), 5);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert_eq!(compressed[1].content, "msg 3");
        assert_eq!(compressed[2].content, "msg 4");
        assert_eq!(compressed[3].content, "msg 5");
        assert_eq!(compressed[4].content, "msg 6");
    }

    #[tokio::test]
    async fn test_compress_multiple_system_messages() {
        let strategy = SlidingWindowStrategy::new(4).unwrap();
        let messages = vec![
            create_message(MessageRole::System, "System msg 1"),
            create_message(MessageRole::System, "System msg 2"),
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
            create_message(MessageRole::User, "msg 3"),
            create_message(MessageRole::Assistant, "msg 4"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 保留 2 个系统消息 + 最近 2 条
        assert_eq!(compressed.len(), 4);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert!(matches!(compressed[1].role, MessageRole::System));
        assert_eq!(compressed[2].content, "msg 3");
        assert_eq!(compressed[3].content, "msg 4");
    }

    #[tokio::test]
    async fn test_compress_no_system_messages() {
        let strategy = SlidingWindowStrategy::new(3).unwrap();
        let messages = vec![
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
            create_message(MessageRole::User, "msg 3"),
            create_message(MessageRole::Assistant, "msg 4"),
            create_message(MessageRole::User, "msg 5"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 保留最近 3 条
        assert_eq!(compressed.len(), 3);
        assert_eq!(compressed[0].content, "msg 3");
        assert_eq!(compressed[1].content, "msg 4");
        assert_eq!(compressed[2].content, "msg 5");
    }

    #[tokio::test]
    async fn test_compress_only_system_messages() {
        let strategy = SlidingWindowStrategy::new(5).unwrap();
        let messages = vec![
            create_message(MessageRole::System, "System msg 1"),
            create_message(MessageRole::System, "System msg 2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 所有都是系统消息，全部保留
        assert_eq!(compressed.len(), 2);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert!(matches!(compressed[1].role, MessageRole::System));
    }

    #[tokio::test]
    async fn test_compress_preserves_message_order() {
        let strategy = SlidingWindowStrategy::new(4).unwrap();
        let messages = vec![
            create_message(MessageRole::System, "System"),
            create_message(MessageRole::User, "Q1"),
            create_message(MessageRole::Assistant, "A1"),
            create_message(MessageRole::User, "Q2"),
            create_message(MessageRole::Assistant, "A2"),
            create_message(MessageRole::User, "Q3"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 验证顺序：System → Q2 → A2 → Q3
        assert_eq!(compressed.len(), 4);
        assert!(matches!(compressed[0].role, MessageRole::System));
        assert_eq!(compressed[1].content, "Q2");
        assert_eq!(compressed[2].content, "A2");
        assert_eq!(compressed[3].content, "Q3");
    }

    #[tokio::test]
    async fn test_estimate_tokens() {
        let strategy = SlidingWindowStrategy::new(10).unwrap();
        let messages = vec![
            create_message(MessageRole::User, "Hello"),
            create_message(MessageRole::Assistant, "Hi there!"),
        ];

        let token_count = strategy.estimate_tokens(&messages);

        // 应该返回合理的 token 数（> 0）
        assert!(token_count > 0);
        assert!(token_count < 50); // 简单消息不应太多 tokens
    }

    #[tokio::test]
    async fn test_name() {
        let strategy = SlidingWindowStrategy::new(10).unwrap();
        assert_eq!(strategy.name(), "SlidingWindow");
    }

    #[tokio::test]
    async fn test_window_size_zero() {
        let strategy = SlidingWindowStrategy::new(0).unwrap();
        let messages = vec![
            create_message(MessageRole::User, "msg 1"),
            create_message(MessageRole::Assistant, "msg 2"),
        ];

        let compressed = strategy.compress(&messages).await.unwrap();

        // 窗口为 0，应该返回空（除非有系统消息）
        assert_eq!(compressed.len(), 0);
    }
}
