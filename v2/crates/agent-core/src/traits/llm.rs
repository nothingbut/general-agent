//! LLM 客户端 trait 定义
//!
//! 定义与各种 LLM 提供商交互的抽象接口

use crate::{error::Result, models::Message};
use async_trait::async_trait;
use serde::{Deserialize, Serialize};

/// LLM 客户端接口
///
/// 定义与 LLM 提供商的交互方法
#[async_trait]
pub trait LLMClient: Send + Sync {
    /// 发送完成请求
    ///
    /// # Arguments
    ///
    /// * `request` - 完成请求参数
    ///
    /// # Returns
    ///
    /// 完成响应
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse>;

    /// 发送流式完成请求
    ///
    /// # Arguments
    ///
    /// * `request` - 完成请求参数
    ///
    /// # Returns
    ///
    /// 流式响应的接收端
    async fn stream(&self, request: CompletionRequest) -> Result<Box<dyn CompletionStream>>;

    /// 列出可用模型
    async fn list_models(&self) -> Result<Vec<ModelInfo>>;

    /// 获取提供商名称
    fn provider_name(&self) -> &str;
}

/// 完成请求参数
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompletionRequest {
    /// 消息历史
    pub messages: Vec<Message>,
    /// 模型名称
    pub model: String,
    /// 温度参数（0.0 - 2.0）
    #[serde(skip_serializing_if = "Option::is_none")]
    pub temperature: Option<f32>,
    /// 最大 token 数
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_tokens: Option<u32>,
    /// Top-p 采样参数
    #[serde(skip_serializing_if = "Option::is_none")]
    pub top_p: Option<f32>,
    /// 停止序列
    #[serde(skip_serializing_if = "Option::is_none")]
    pub stop: Option<Vec<String>>,
    /// 系统提示词（覆盖消息中的系统提示）
    #[serde(skip_serializing_if = "Option::is_none")]
    pub system_prompt: Option<String>,
}

impl CompletionRequest {
    /// 创建新的完成请求
    pub fn new(messages: Vec<Message>, model: String) -> Self {
        Self {
            messages,
            model,
            temperature: None,
            max_tokens: None,
            top_p: None,
            stop: None,
            system_prompt: None,
        }
    }

    /// 设置温度参数
    pub fn with_temperature(mut self, temperature: f32) -> Self {
        self.temperature = Some(temperature);
        self
    }

    /// 设置最大 token 数
    pub fn with_max_tokens(mut self, max_tokens: u32) -> Self {
        self.max_tokens = Some(max_tokens);
        self
    }

    /// 设置 top-p 参数
    pub fn with_top_p(mut self, top_p: f32) -> Self {
        self.top_p = Some(top_p);
        self
    }

    /// 设置停止序列
    pub fn with_stop(mut self, stop: Vec<String>) -> Self {
        self.stop = Some(stop);
        self
    }

    /// 设置系统提示词
    pub fn with_system_prompt(mut self, prompt: String) -> Self {
        self.system_prompt = Some(prompt);
        self
    }
}

/// 完成响应
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompletionResponse {
    /// 生成的内容
    pub content: String,
    /// 使用的模型名称
    pub model: String,
    /// Token 使用情况
    pub usage: TokenUsage,
    /// 完成原因
    pub finish_reason: Option<String>,
}

/// Token 使用情况
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TokenUsage {
    /// 提示词使用的 token 数
    pub prompt_tokens: u32,
    /// 完成使用的 token 数
    pub completion_tokens: u32,
    /// 总 token 数
    pub total_tokens: u32,
}

impl TokenUsage {
    /// 创建新的 token 使用统计
    pub fn new(prompt_tokens: u32, completion_tokens: u32) -> Self {
        Self {
            prompt_tokens,
            completion_tokens,
            total_tokens: prompt_tokens + completion_tokens,
        }
    }
}

/// 流式响应 trait
#[async_trait]
pub trait CompletionStream: Send {
    /// 获取下一个流式数据块
    ///
    /// # Returns
    ///
    /// 如果有数据返回 Some(StreamChunk)，流结束返回 None
    async fn next(&mut self) -> Result<Option<StreamChunk>>;
}

/// 流式数据块
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct StreamChunk {
    /// 增量内容
    pub delta: String,
    /// 是否为最后一个块
    pub is_final: bool,
    /// 完成原因（仅在 is_final=true 时有值）
    pub finish_reason: Option<String>,
}

/// 模型信息
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ModelInfo {
    /// 模型 ID
    pub id: String,
    /// 模型名称
    pub name: String,
    /// 提供商
    pub provider: String,
    /// 上下文窗口大小
    pub context_window: Option<u32>,
    /// 最大输出 token 数
    pub max_output_tokens: Option<u32>,
    /// 是否支持流式
    pub supports_streaming: bool,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{MessageRole, Session};
    use uuid::Uuid;

    #[test]
    fn test_completion_request_builder() {
        let session_id = Uuid::new_v4();
        let message = Message::new(session_id, MessageRole::User, "Hello".to_string());

        let request = CompletionRequest::new(vec![message], "gpt-4".to_string())
            .with_temperature(0.7)
            .with_max_tokens(1000)
            .with_top_p(0.9)
            .with_stop(vec!["END".to_string()]);

        assert_eq!(request.model, "gpt-4");
        assert_eq!(request.temperature, Some(0.7));
        assert_eq!(request.max_tokens, Some(1000));
        assert_eq!(request.top_p, Some(0.9));
        assert!(request.stop.is_some());
    }

    #[test]
    fn test_token_usage() {
        let usage = TokenUsage::new(100, 50);

        assert_eq!(usage.prompt_tokens, 100);
        assert_eq!(usage.completion_tokens, 50);
        assert_eq!(usage.total_tokens, 150);
    }

    #[test]
    fn test_completion_request_serialization() {
        let session_id = Uuid::new_v4();
        let message = Message::new(session_id, MessageRole::User, "Test".to_string());

        let request = CompletionRequest::new(vec![message], "gpt-4".to_string());

        let json = serde_json::to_string(&request).unwrap();
        let deserialized: CompletionRequest = serde_json::from_str(&json).unwrap();

        assert_eq!(request.model, deserialized.model);
        assert_eq!(request.messages.len(), deserialized.messages.len());
    }
}
