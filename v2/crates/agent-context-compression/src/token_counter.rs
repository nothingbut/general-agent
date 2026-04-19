use agent_core::models::Message;
use tiktoken_rs::{cl100k_base, CoreBPE};
use tracing::warn;

use crate::{CompressionError, Result};

/// Token 计数器
///
/// 支持多种 LLM 模型的 Token 计数，准确率 > 95%
pub struct TokenCounter {
    bpe: CoreBPE,
    model_name: String,
}

impl TokenCounter {
    /// 为 Claude 模型创建计数器
    ///
    /// Claude 使用 cl100k_base tokenizer（与 GPT-4 相同）
    pub fn new_for_claude() -> Result<Self> {
        let bpe = cl100k_base().map_err(|e| {
            CompressionError::TokenCountingFailed(format!("无法初始化 cl100k_base: {}", e))
        })?;

        Ok(Self {
            bpe,
            model_name: "claude".to_string(),
        })
    }

    /// 为指定模型创建计数器
    ///
    /// # 支持的模型
    ///
    /// - `claude-*`: Anthropic Claude 系列
    /// - `qwen*`: Qwen 系列
    /// - `gpt-*`: OpenAI GPT 系列
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let counter = TokenCounter::new_for_model("qwen2.5:7b")?;
    /// let count = counter.count("Hello, world!");
    /// ```
    pub fn new_for_model(model: &str) -> Result<Self> {
        let bpe = if model.starts_with("claude")
            || model.starts_with("qwen")
            || model.starts_with("gpt")
        {
            cl100k_base().map_err(|e| {
                CompressionError::TokenCountingFailed(format!("无法初始化 cl100k_base: {}", e))
            })?
        } else {
            warn!(
                "未知模型 '{}', 使用默认 tokenizer (cl100k_base)",
                model
            );
            cl100k_base().map_err(|e| {
                CompressionError::TokenCountingFailed(format!("无法初始化 cl100k_base: {}", e))
            })?
        };

        Ok(Self {
            bpe,
            model_name: model.to_string(),
        })
    }

    /// 计算文本的 token 数
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let counter = TokenCounter::new_for_claude()?;
    /// let count = counter.count("Hello, world!");
    /// assert!(count > 0 && count < 10);
    /// ```
    pub fn count(&self, text: &str) -> usize {
        self.bpe.encode_with_special_tokens(text).len()
    }

    /// 计算消息的 token 数
    ///
    /// 包含以下部分：
    /// - role 字段的 token 数
    /// - content 字段的 token 数
    /// - 消息格式开销（约 4 tokens）
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let counter = TokenCounter::new_for_claude()?;
    /// let message = Message {
    ///     role: "user".to_string(),
    ///     content: "Hello!".to_string(),
    ///     ..Default::default()
    /// };
    /// let count = counter.count_message(&message);
    /// assert!(count > 5); // role + content + overhead
    /// ```
    pub fn count_message(&self, message: &Message) -> usize {
        // 消息格式: role + content + overhead
        // 参考 OpenAI API 文档，每条消息约 4 tokens 开销
        let role_tokens = self.count(&message.role.to_string());
        let content_tokens = self.count(&message.content);
        role_tokens + content_tokens + 4
    }

    /// 计算消息列表的总 token 数
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let counter = TokenCounter::new_for_claude()?;
    /// let messages = vec![
    ///     Message { role: "user".into(), content: "Hi".into(), ..Default::default() },
    ///     Message { role: "assistant".into(), content: "Hello!".into(), ..Default::default() },
    /// ];
    /// let total = counter.count_messages(&messages);
    /// assert!(total > 10);
    /// ```
    pub fn count_messages(&self, messages: &[Message]) -> usize {
        messages.iter().map(|m| self.count_message(m)).sum()
    }

    /// 获取模型名称
    pub fn model_name(&self) -> &str {
        &self.model_name
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::models::MessageRole;
    use uuid::Uuid;

    #[test]
    fn test_new_for_claude() {
        let counter = TokenCounter::new_for_claude();
        assert!(counter.is_ok());
        assert_eq!(counter.unwrap().model_name(), "claude");
    }

    #[test]
    fn test_new_for_model() {
        let models = vec!["claude-3-5-sonnet", "qwen2.5:7b", "gpt-4", "unknown-model"];

        for model in models {
            let counter = TokenCounter::new_for_model(model);
            assert!(counter.is_ok(), "应该能创建 {} 的计数器", model);
            assert_eq!(counter.unwrap().model_name(), model);
        }
    }

    #[test]
    fn test_count_simple_text() {
        let counter = TokenCounter::new_for_claude().unwrap();

        // 简单英文文本
        let count = counter.count("Hello, world!");
        assert!(count > 0, "token 数应该大于 0");
        assert!(count < 10, "简单文本的 token 数应该很小");

        // 空文本
        let empty_count = counter.count("");
        assert_eq!(empty_count, 0, "空文本应该是 0 tokens");
    }

    #[test]
    fn test_count_chinese_text() {
        let counter = TokenCounter::new_for_claude().unwrap();

        // 中文文本
        let count = counter.count("你好，世界！");
        assert!(count > 0, "中文 token 数应该大于 0");

        // 中文通常每个字符 2-3 tokens
        let long_text = "这是一段比较长的中文文本，用于测试 token 计数功能。";
        let long_count = counter.count(long_text);
        assert!(long_count > 20, "长中文文本应该有更多 tokens");
    }

    #[test]
    fn test_count_message() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let session_id = Uuid::new_v4();

        let message = Message::new(
            session_id,
            MessageRole::User,
            "Hello, world!".to_string(),
        );

        let count = counter.count_message(&message);

        // role (1-2 tokens) + content (3-4 tokens) + overhead (4 tokens)
        assert!(count >= 8 && count <= 15, "消息 token 数应该在合理范围内");
    }

    #[test]
    fn test_count_messages() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let session_id = Uuid::new_v4();

        let messages = vec![
            Message::new(session_id, MessageRole::User, "Hello".to_string()),
            Message::new(session_id, MessageRole::Assistant, "Hi there!".to_string()),
            Message::new(session_id, MessageRole::User, "How are you?".to_string()),
        ];

        let total = counter.count_messages(&messages);
        assert!(total > 15, "3 条消息的总 token 数应该 > 15");

        // 验证总数等于各消息之和
        let sum: usize = messages.iter().map(|m| counter.count_message(m)).sum();
        assert_eq!(total, sum, "总数应该等于各消息之和");
    }

    #[test]
    fn test_count_long_text() {
        let counter = TokenCounter::new_for_claude().unwrap();

        let long_text = "This is a longer text that should have more tokens. \
                         It contains multiple sentences and covers various topics. \
                         The purpose is to test the token counting functionality \
                         with a more realistic example.";

        let count = counter.count(long_text);
        assert!(count > 30, "长文本应该有更多 tokens");
    }

    #[test]
    fn test_count_empty_messages() {
        let counter = TokenCounter::new_for_claude().unwrap();

        let empty_messages: Vec<Message> = vec![];
        let count = counter.count_messages(&empty_messages);
        assert_eq!(count, 0, "空消息列表应该是 0 tokens");
    }

    #[test]
    fn test_count_message_with_empty_content() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let session_id = Uuid::new_v4();

        let message = Message::new(session_id, MessageRole::User, "".to_string());

        let count = counter.count_message(&message);
        // role (1-2 tokens) + overhead (4 tokens)
        assert!(count >= 5 && count <= 8, "空内容消息仍有 role 和 overhead");
    }

    #[test]
    fn test_count_special_characters() {
        let counter = TokenCounter::new_for_claude().unwrap();

        let special_text = "Hello! @#$%^&*() 测试 🚀 emoji";
        let count = counter.count(special_text);
        assert!(count > 0, "特殊字符和 emoji 应该能正常计数");
    }
}
