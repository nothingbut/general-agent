//! Anthropic API 类型定义

use serde::{Deserialize, Serialize};

/// Anthropic 客户端配置
#[derive(Debug, Clone)]
pub struct AnthropicConfig {
    /// API Key
    pub api_key: String,
    /// API 基础 URL
    pub base_url: String,
    /// API 版本
    pub api_version: String,
}

impl Default for AnthropicConfig {
    fn default() -> Self {
        Self {
            api_key: std::env::var("ANTHROPIC_API_KEY").unwrap_or_default(),
            base_url: "https://api.anthropic.com".to_string(),
            api_version: "2023-06-01".to_string(),
        }
    }
}

impl AnthropicConfig {
    /// 从 API Key 创建配置
    pub fn new(api_key: String) -> Self {
        Self {
            api_key,
            ..Default::default()
        }
    }

    /// 设置 base URL
    pub fn with_base_url(mut self, base_url: String) -> Self {
        self.base_url = base_url;
        self
    }

    /// 设置 API 版本
    pub fn with_api_version(mut self, api_version: String) -> Self {
        self.api_version = api_version;
        self
    }
}

/// Anthropic Messages API 请求
#[derive(Debug, Clone, Serialize)]
pub struct MessagesRequest {
    pub model: String,
    pub messages: Vec<ApiMessage>,
    pub max_tokens: u32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub system: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub temperature: Option<f32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub top_p: Option<f32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub stop_sequences: Option<Vec<String>>,
    pub stream: bool,
}

/// API 消息格式
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ApiMessage {
    pub role: String,
    pub content: String,
}

/// Messages API 响应
#[derive(Debug, Clone, Deserialize)]
pub struct MessagesResponse {
    pub id: String,
    pub model: String,
    pub role: String,
    pub content: Vec<ContentBlock>,
    pub stop_reason: Option<String>,
    pub usage: Usage,
}

/// 内容块
#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum ContentBlock {
    #[serde(rename = "text")]
    Text { text: String },
}

/// Token 使用统计
#[derive(Debug, Clone, Deserialize)]
pub struct Usage {
    pub input_tokens: u32,
    pub output_tokens: u32,
}

/// 流式响应事件
#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum StreamEvent {
    #[serde(rename = "message_start")]
    MessageStart { message: MessageStart },
    #[serde(rename = "content_block_start")]
    ContentBlockStart {
        index: u32,
        content_block: ContentBlock,
    },
    #[serde(rename = "content_block_delta")]
    ContentBlockDelta { index: u32, delta: Delta },
    #[serde(rename = "content_block_stop")]
    ContentBlockStop { index: u32 },
    #[serde(rename = "message_delta")]
    MessageDelta { delta: MessageDeltaData },
    #[serde(rename = "message_stop")]
    MessageStop,
    #[serde(rename = "ping")]
    Ping,
    #[serde(rename = "error")]
    Error { error: ApiError },
}

/// 消息开始事件数据
#[derive(Debug, Clone, Deserialize)]
pub struct MessageStart {
    pub id: String,
    pub model: String,
    pub role: String,
    pub usage: Usage,
}

/// 增量数据
#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum Delta {
    #[serde(rename = "text_delta")]
    TextDelta { text: String },
}

/// 消息增量数据
#[derive(Debug, Clone, Deserialize)]
pub struct MessageDeltaData {
    pub stop_reason: Option<String>,
    pub usage: Option<Usage>,
}

/// API 错误
#[derive(Debug, Clone, Deserialize)]
pub struct ApiError {
    #[serde(rename = "type")]
    pub error_type: String,
    pub message: String,
}

/// Claude 模型常量
pub const CLAUDE_MODELS: &[(&str, &str, u32, u32)] = &[
    // (model_id, display_name, context_window, max_output)
    ("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet", 200_000, 8_192),
    ("claude-3-5-haiku-20241022", "Claude 3.5 Haiku", 200_000, 8_192),
    ("claude-3-opus-20240229", "Claude 3 Opus", 200_000, 4_096),
    ("claude-3-sonnet-20240229", "Claude 3 Sonnet", 200_000, 4_096),
    ("claude-3-haiku-20240307", "Claude 3 Haiku", 200_000, 4_096),
];

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_config_default() {
        let config = AnthropicConfig::default();
        assert_eq!(config.base_url, "https://api.anthropic.com");
        assert_eq!(config.api_version, "2023-06-01");
    }

    #[test]
    fn test_config_builder() {
        let config = AnthropicConfig::new("test-key".to_string())
            .with_base_url("https://custom.api.com".to_string())
            .with_api_version("2024-01-01".to_string());

        assert_eq!(config.api_key, "test-key");
        assert_eq!(config.base_url, "https://custom.api.com");
        assert_eq!(config.api_version, "2024-01-01");
    }

    #[test]
    fn test_claude_models() {
        assert!(CLAUDE_MODELS.len() > 0);
        let (model_id, name, context, output) = CLAUDE_MODELS[0];
        assert!(!model_id.is_empty());
        assert!(!name.is_empty());
        assert!(context > 0);
        assert!(output > 0);
    }
}
