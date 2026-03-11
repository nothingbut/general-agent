//! 会话实体定义

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use uuid::Uuid;

/// 会话实体
///
/// 表示一个对话会话，包含多条消息和会话上下文
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Session {
    /// 会话唯一标识符
    pub id: Uuid,
    /// 会话标题（可选）
    pub title: Option<String>,
    /// 创建时间
    pub created_at: DateTime<Utc>,
    /// 最后更新时间
    pub updated_at: DateTime<Utc>,
    /// 会话上下文配置
    pub context: SessionContext,
}

impl Session {
    /// 创建新会话
    ///
    /// # Examples
    ///
    /// ```
    /// use agent_core::models::Session;
    ///
    /// let session = Session::new(None);
    /// assert!(session.title.is_none());
    /// ```
    pub fn new(title: Option<String>) -> Self {
        let now = Utc::now();
        Self {
            id: Uuid::new_v4(),
            title,
            created_at: now,
            updated_at: now,
            context: SessionContext::default(),
        }
    }

    /// 创建带上下文的会话
    pub fn with_context(title: Option<String>, context: SessionContext) -> Self {
        let now = Utc::now();
        Self {
            id: Uuid::new_v4(),
            title,
            created_at: now,
            updated_at: now,
            context,
        }
    }

    /// 更新会话标题
    pub fn update_title(&mut self, title: String) {
        self.title = Some(title);
        self.updated_at = Utc::now();
    }

    /// 更新上下文
    pub fn update_context(&mut self, context: SessionContext) {
        self.context = context;
        self.updated_at = Utc::now();
    }

    /// 标记为已更新
    pub fn mark_updated(&mut self) {
        self.updated_at = Utc::now();
    }
}

/// 会话上下文配置
///
/// 包含会话级别的配置参数和变量
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SessionContext {
    /// 自定义变量（键值对）
    pub variables: HashMap<String, String>,
    /// 最大 token 数量
    pub max_tokens: Option<u32>,
    /// 温度参数（0.0 - 2.0）
    pub temperature: Option<f32>,
    /// 使用的模型名称
    pub model: Option<String>,
    /// 系统提示词
    pub system_prompt: Option<String>,
    /// 额外配置
    #[serde(flatten)]
    pub extra: HashMap<String, serde_json::Value>,
}

impl Default for SessionContext {
    fn default() -> Self {
        Self {
            variables: HashMap::new(),
            max_tokens: None,
            temperature: None,
            model: None,
            system_prompt: None,
            extra: HashMap::new(),
        }
    }
}

impl SessionContext {
    /// 创建新的空上下文
    pub fn new() -> Self {
        Self::default()
    }

    /// 设置变量
    pub fn set_variable(&mut self, key: String, value: String) {
        self.variables.insert(key, value);
    }

    /// 获取变量
    pub fn get_variable(&self, key: &str) -> Option<&String> {
        self.variables.get(key)
    }

    /// 设置最大 token 数
    pub fn with_max_tokens(mut self, max_tokens: u32) -> Self {
        self.max_tokens = Some(max_tokens);
        self
    }

    /// 设置温度参数
    pub fn with_temperature(mut self, temperature: f32) -> Self {
        self.temperature = Some(temperature);
        self
    }

    /// 设置模型名称
    pub fn with_model(mut self, model: String) -> Self {
        self.model = Some(model);
        self
    }

    /// 设置系统提示词
    pub fn with_system_prompt(mut self, prompt: String) -> Self {
        self.system_prompt = Some(prompt);
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_session_creation() {
        let session = Session::new(None);

        assert!(session.title.is_none());
        assert_eq!(session.context.variables.len(), 0);
        assert!(session.created_at <= Utc::now());
    }

    #[test]
    fn test_session_with_title() {
        let session = Session::new(Some("Test Session".to_string()));

        assert_eq!(session.title, Some("Test Session".to_string()));
    }

    #[test]
    fn test_session_update_title() {
        let mut session = Session::new(None);
        let old_updated_at = session.updated_at;

        // 等待一点时间确保时间戳不同
        std::thread::sleep(std::time::Duration::from_millis(10));

        session.update_title("New Title".to_string());

        assert_eq!(session.title, Some("New Title".to_string()));
        assert!(session.updated_at > old_updated_at);
    }

    #[test]
    fn test_session_context_default() {
        let context = SessionContext::default();

        assert_eq!(context.variables.len(), 0);
        assert!(context.max_tokens.is_none());
        assert!(context.temperature.is_none());
        assert!(context.model.is_none());
    }

    #[test]
    fn test_session_context_builder() {
        let context = SessionContext::new()
            .with_max_tokens(4000)
            .with_temperature(0.7)
            .with_model("gpt-4".to_string())
            .with_system_prompt("You are a helpful assistant".to_string());

        assert_eq!(context.max_tokens, Some(4000));
        assert_eq!(context.temperature, Some(0.7));
        assert_eq!(context.model, Some("gpt-4".to_string()));
        assert_eq!(
            context.system_prompt,
            Some("You are a helpful assistant".to_string())
        );
    }

    #[test]
    fn test_session_context_variables() {
        let mut context = SessionContext::new();

        context.set_variable("user_id".to_string(), "123".to_string());
        context.set_variable("language".to_string(), "zh".to_string());

        assert_eq!(context.get_variable("user_id"), Some(&"123".to_string()));
        assert_eq!(context.get_variable("language"), Some(&"zh".to_string()));
        assert_eq!(context.get_variable("nonexistent"), None);
    }

    #[test]
    fn test_session_with_context() {
        let context = SessionContext::new()
            .with_max_tokens(2000)
            .with_temperature(0.5);

        let session = Session::with_context(Some("Test".to_string()), context.clone());

        assert_eq!(session.context.max_tokens, Some(2000));
        assert_eq!(session.context.temperature, Some(0.5));
    }

    #[test]
    fn test_session_serialization() {
        let session = Session::new(Some("Test".to_string()));

        let json = serde_json::to_string(&session).unwrap();
        let deserialized: Session = serde_json::from_str(&json).unwrap();

        assert_eq!(session.id, deserialized.id);
        assert_eq!(session.title, deserialized.title);
    }
}
