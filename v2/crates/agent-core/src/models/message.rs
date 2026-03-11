//! 消息实体定义

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

/// 消息实体
///
/// 表示对话中的一条消息，可以是用户消息、助手回复或系统消息
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Message {
    /// 消息唯一标识符
    pub id: Uuid,
    /// 所属会话 ID
    pub session_id: Uuid,
    /// 消息角色（用户/助手/系统）
    pub role: MessageRole,
    /// 消息内容
    pub content: String,
    /// 创建时间
    pub created_at: DateTime<Utc>,
    /// 元数据（可选）
    pub metadata: Option<MessageMetadata>,
}

impl Message {
    /// 创建新消息
    ///
    /// # Examples
    ///
    /// ```
    /// use agent_core::models::Message;
    /// use uuid::Uuid;
    ///
    /// let message = Message::new(
    ///     Uuid::new_v4(),
    ///     agent_core::models::MessageRole::User,
    ///     "Hello!".to_string(),
    /// );
    /// ```
    pub fn new(session_id: Uuid, role: MessageRole, content: String) -> Self {
        Self {
            id: Uuid::new_v4(),
            session_id,
            role,
            content,
            created_at: Utc::now(),
            metadata: None,
        }
    }

    /// 创建带元数据的消息
    pub fn with_metadata(
        session_id: Uuid,
        role: MessageRole,
        content: String,
        metadata: MessageMetadata,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            session_id,
            role,
            content,
            created_at: Utc::now(),
            metadata: Some(metadata),
        }
    }

    /// 检查是否为用户消息
    pub fn is_user(&self) -> bool {
        matches!(self.role, MessageRole::User)
    }

    /// 检查是否为助手消息
    pub fn is_assistant(&self) -> bool {
        matches!(self.role, MessageRole::Assistant)
    }

    /// 检查是否为系统消息
    pub fn is_system(&self) -> bool {
        matches!(self.role, MessageRole::System)
    }
}

/// 消息角色
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum MessageRole {
    /// 用户消息
    User,
    /// 助手回复
    Assistant,
    /// 系统消息
    System,
}

impl std::fmt::Display for MessageRole {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            MessageRole::User => write!(f, "user"),
            MessageRole::Assistant => write!(f, "assistant"),
            MessageRole::System => write!(f, "system"),
        }
    }
}

/// 消息元数据
///
/// 包含 LLM 响应的额外信息，如 token 使用量、模型名称等
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct MessageMetadata {
    /// 使用的 token 数量
    pub tokens: Option<u32>,
    /// 使用的模型名称
    pub model: Option<String>,
    /// 完成原因（stop, length, etc.）
    pub finish_reason: Option<String>,
    /// 执行时间（毫秒）
    pub execution_time_ms: Option<u64>,
    /// 额外的键值对
    #[serde(flatten)]
    pub extra: std::collections::HashMap<String, serde_json::Value>,
}

impl Default for MessageMetadata {
    fn default() -> Self {
        Self {
            tokens: None,
            model: None,
            finish_reason: None,
            execution_time_ms: None,
            extra: std::collections::HashMap::new(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_message_creation() {
        let session_id = Uuid::new_v4();
        let message = Message::new(session_id, MessageRole::User, "Hello".to_string());

        assert_eq!(message.session_id, session_id);
        assert_eq!(message.role, MessageRole::User);
        assert_eq!(message.content, "Hello");
        assert!(message.metadata.is_none());
    }

    #[test]
    fn test_message_with_metadata() {
        let session_id = Uuid::new_v4();
        let metadata = MessageMetadata {
            tokens: Some(100),
            model: Some("gpt-4".to_string()),
            finish_reason: Some("stop".to_string()),
            execution_time_ms: Some(500),
            extra: std::collections::HashMap::new(),
        };

        let message = Message::with_metadata(
            session_id,
            MessageRole::Assistant,
            "Response".to_string(),
            metadata.clone(),
        );

        assert_eq!(message.role, MessageRole::Assistant);
        assert!(message.metadata.is_some());
        assert_eq!(message.metadata.unwrap().tokens, Some(100));
    }

    #[test]
    fn test_message_role_checks() {
        let session_id = Uuid::new_v4();

        let user_msg = Message::new(session_id, MessageRole::User, "Hello".to_string());
        assert!(user_msg.is_user());
        assert!(!user_msg.is_assistant());
        assert!(!user_msg.is_system());

        let assistant_msg = Message::new(session_id, MessageRole::Assistant, "Hi".to_string());
        assert!(!assistant_msg.is_user());
        assert!(assistant_msg.is_assistant());
        assert!(!assistant_msg.is_system());

        let system_msg = Message::new(session_id, MessageRole::System, "Info".to_string());
        assert!(!system_msg.is_user());
        assert!(!system_msg.is_assistant());
        assert!(system_msg.is_system());
    }

    #[test]
    fn test_message_role_display() {
        assert_eq!(MessageRole::User.to_string(), "user");
        assert_eq!(MessageRole::Assistant.to_string(), "assistant");
        assert_eq!(MessageRole::System.to_string(), "system");
    }

    #[test]
    fn test_message_serialization() {
        let message = Message::new(
            Uuid::new_v4(),
            MessageRole::User,
            "Test message".to_string(),
        );

        let json = serde_json::to_string(&message).unwrap();
        let deserialized: Message = serde_json::from_str(&json).unwrap();

        assert_eq!(message.id, deserialized.id);
        assert_eq!(message.content, deserialized.content);
        assert_eq!(message.role, deserialized.role);
    }
}
