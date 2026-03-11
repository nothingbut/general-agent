//! Anthropic Claude API 客户端实现

use agent_core::{
    error::Result,
    models::{Message, MessageRole},
    traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    },
};
use async_trait::async_trait;
use reqwest::Client;
use tracing::{debug, info};

use super::{
    stream::AnthropicStream,
    types::{AnthropicConfig, ApiMessage, ContentBlock, MessagesRequest, MessagesResponse, CLAUDE_MODELS},
};

/// Anthropic Claude API 客户端
pub struct AnthropicClient {
    config: AnthropicConfig,
    http_client: Client,
}

impl AnthropicClient {
    /// 创建新的 Anthropic 客户端
    ///
    /// # Arguments
    ///
    /// * `config` - 客户端配置
    pub fn new(config: AnthropicConfig) -> Result<Self> {
        let http_client = Client::builder()
            .timeout(std::time::Duration::from_secs(300))
            .build()
            .map_err(|e| agent_core::Error::LLM(format!("Failed to create HTTP client: {}", e)))?;

        Ok(Self {
            config,
            http_client,
        })
    }

    /// 从 API Key 创建客户端
    pub fn from_api_key(api_key: String) -> Result<Self> {
        Self::new(AnthropicConfig::new(api_key))
    }

    /// 从环境变量创建客户端
    pub fn from_env() -> Result<Self> {
        let api_key = std::env::var("ANTHROPIC_API_KEY")
            .map_err(|_| agent_core::Error::LLM(
                "ANTHROPIC_API_KEY environment variable not set".to_string()
            ))?;
        Self::from_api_key(api_key)
    }

    /// 转换消息格式
    fn convert_messages(&self, messages: &[Message]) -> (Option<String>, Vec<ApiMessage>) {
        let mut system_prompt = None;
        let mut api_messages = Vec::new();

        for msg in messages {
            match msg.role {
                MessageRole::System => {
                    // Anthropic 要求 system 作为单独的参数
                    system_prompt = Some(msg.content.clone());
                }
                MessageRole::User => {
                    api_messages.push(ApiMessage {
                        role: "user".to_string(),
                        content: msg.content.clone(),
                    });
                }
                MessageRole::Assistant => {
                    api_messages.push(ApiMessage {
                        role: "assistant".to_string(),
                        content: msg.content.clone(),
                    });
                }
            }
        }

        (system_prompt, api_messages)
    }

    /// 构建 API 请求
    fn build_request(&self, request: &CompletionRequest, stream: bool) -> MessagesRequest {
        let (system_prompt, api_messages) = self.convert_messages(&request.messages);

        // 使用请求中的 system_prompt 覆盖消息中的
        let system = request.system_prompt.clone().or(system_prompt);

        MessagesRequest {
            model: request.model.clone(),
            messages: api_messages,
            max_tokens: request.max_tokens.unwrap_or(4096),
            system,
            temperature: request.temperature,
            top_p: request.top_p,
            stop_sequences: request.stop.clone(),
            stream,
        }
    }

    /// 发送 API 请求
    async fn send_request(&self, api_request: &MessagesRequest) -> Result<MessagesResponse> {
        let url = format!("{}/v1/messages", self.config.base_url);

        debug!("Sending request to Anthropic API: {:?}", api_request);

        let response = self
            .http_client
            .post(&url)
            .header("x-api-key", &self.config.api_key)
            .header("anthropic-version", &self.config.api_version)
            .header("content-type", "application/json")
            .json(&api_request)
            .send()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("HTTP request failed: {}", e)))?;

        let status = response.status();
        if !status.is_success() {
            let body = response.text().await.unwrap_or_else(|_| "Unknown error".to_string());
            return Err(agent_core::Error::LLM(format!(
                "API request failed with status {}: {}",
                status, body
            )));
        }

        let api_response: MessagesResponse = response
            .json()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("Failed to parse response: {}", e)))?;

        debug!("Received response from Anthropic API");

        Ok(api_response)
    }

    /// 发送流式 API 请求
    async fn send_stream_request(&self, api_request: &MessagesRequest) -> Result<reqwest::Response> {
        let url = format!("{}/v1/messages", self.config.base_url);

        debug!("Sending stream request to Anthropic API");

        let response = self
            .http_client
            .post(&url)
            .header("x-api-key", &self.config.api_key)
            .header("anthropic-version", &self.config.api_version)
            .header("content-type", "application/json")
            .json(&api_request)
            .send()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("HTTP request failed: {}", e)))?;

        let status = response.status();
        if !status.is_success() {
            let body = response.text().await.unwrap_or_else(|_| "Unknown error".to_string());
            return Err(agent_core::Error::LLM(format!(
                "Stream request failed with status {}: {}",
                status, body
            )));
        }

        Ok(response)
    }

    /// 转换响应格式
    fn convert_response(&self, response: MessagesResponse) -> CompletionResponse {
        let content = response
            .content
            .into_iter()
            .filter_map(|block| match block {
                ContentBlock::Text { text } => Some(text),
            })
            .collect::<Vec<_>>()
            .join("");

        CompletionResponse {
            content,
            model: response.model,
            usage: TokenUsage::new(response.usage.input_tokens, response.usage.output_tokens),
            finish_reason: response.stop_reason,
        }
    }
}

#[async_trait]
impl LLMClient for AnthropicClient {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        info!("Sending completion request to Anthropic API");

        let api_request = self.build_request(&request, false);
        let api_response = self.send_request(&api_request).await?;
        let response = self.convert_response(api_response);

        info!(
            "Completion successful. Tokens: {} input, {} output",
            response.usage.prompt_tokens, response.usage.completion_tokens
        );

        Ok(response)
    }

    async fn stream(&self, request: CompletionRequest) -> Result<Box<dyn CompletionStream>> {
        info!("Sending stream request to Anthropic API");

        let api_request = self.build_request(&request, true);
        let http_response = self.send_stream_request(&api_request).await?;
        let stream = AnthropicStream::new(http_response);

        Ok(Box::new(stream))
    }

    async fn list_models(&self) -> Result<Vec<ModelInfo>> {
        let models = CLAUDE_MODELS
            .iter()
            .map(|(id, name, context_window, max_output)| ModelInfo {
                id: id.to_string(),
                name: name.to_string(),
                provider: "anthropic".to_string(),
                context_window: Some(*context_window),
                max_output_tokens: Some(*max_output),
                supports_streaming: true,
            })
            .collect();

        Ok(models)
    }

    fn provider_name(&self) -> &str {
        "anthropic"
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::models::Session;
    use uuid::Uuid;

    fn create_test_client() -> AnthropicClient {
        let config = AnthropicConfig::new("test-api-key".to_string());
        AnthropicClient::new(config).unwrap()
    }

    #[test]
    fn test_client_creation() {
        let client = create_test_client();
        assert_eq!(client.provider_name(), "anthropic");
    }

    #[test]
    fn test_convert_messages() {
        let client = create_test_client();
        let session_id = Uuid::new_v4();

        let messages = vec![
            Message::new(session_id, MessageRole::System, "You are helpful".to_string()),
            Message::new(session_id, MessageRole::User, "Hello".to_string()),
            Message::new(session_id, MessageRole::Assistant, "Hi there!".to_string()),
            Message::new(session_id, MessageRole::User, "How are you?".to_string()),
        ];

        let (system, api_messages) = client.convert_messages(&messages);

        assert_eq!(system, Some("You are helpful".to_string()));
        assert_eq!(api_messages.len(), 3);
        assert_eq!(api_messages[0].role, "user");
        assert_eq!(api_messages[0].content, "Hello");
        assert_eq!(api_messages[1].role, "assistant");
        assert_eq!(api_messages[2].role, "user");
    }

    #[test]
    fn test_build_request() {
        let client = create_test_client();
        let session_id = Uuid::new_v4();

        let messages = vec![
            Message::new(session_id, MessageRole::User, "Test".to_string()),
        ];

        let request = CompletionRequest::new(messages, "claude-3-5-sonnet-20241022".to_string())
            .with_temperature(0.7)
            .with_max_tokens(2000);

        let api_request = client.build_request(&request, false);

        assert_eq!(api_request.model, "claude-3-5-sonnet-20241022");
        assert_eq!(api_request.max_tokens, 2000);
        assert_eq!(api_request.temperature, Some(0.7));
        assert!(!api_request.stream);
    }

    #[tokio::test]
    async fn test_list_models() {
        let client = create_test_client();
        let models = client.list_models().await.unwrap();

        assert!(models.len() > 0);
        assert_eq!(models[0].provider, "anthropic");
        assert!(models[0].supports_streaming);
    }

    #[test]
    fn test_convert_response() {
        let client = create_test_client();

        let api_response = MessagesResponse {
            id: "msg_123".to_string(),
            model: "claude-3-5-sonnet-20241022".to_string(),
            role: "assistant".to_string(),
            content: vec![ContentBlock::Text {
                text: "Hello, world!".to_string(),
            }],
            stop_reason: Some("end_turn".to_string()),
            usage: super::super::types::Usage {
                input_tokens: 10,
                output_tokens: 20,
            },
        };

        let response = client.convert_response(api_response);

        assert_eq!(response.content, "Hello, world!");
        assert_eq!(response.model, "claude-3-5-sonnet-20241022");
        assert_eq!(response.usage.prompt_tokens, 10);
        assert_eq!(response.usage.completion_tokens, 20);
        assert_eq!(response.usage.total_tokens, 30);
        assert_eq!(response.finish_reason, Some("end_turn".to_string()));
    }
}
