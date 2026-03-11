//! Ollama 客户端实现

use agent_core::{
    error::Result,
    models::{Message, MessageRole},
    traits::llm::{CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage},
};
use async_trait::async_trait;
use reqwest::Client;
use tracing::{debug, info};

use super::stream::OllamaStream;
use super::types::{ChatMessage, ChatRequest, ChatResponse, OllamaConfig};

pub struct OllamaClient {
    config: OllamaConfig,
    http_client: Client,
}

impl OllamaClient {
    pub fn new(config: OllamaConfig) -> Result<Self> {
        let http_client = Client::builder()
            .timeout(std::time::Duration::from_secs(300))
            .build()
            .map_err(|e| agent_core::Error::LLM(format!("Failed to create HTTP client: {}", e)))?;

        Ok(Self { config, http_client })
    }

    pub fn from_model(model: String) -> Result<Self> {
        Self::new(OllamaConfig::new(model))
    }

    fn convert_messages(&self, messages: &[Message]) -> Vec<ChatMessage> {
        messages
            .iter()
            .map(|msg| ChatMessage {
                role: match msg.role {
                    MessageRole::User => "user".to_string(),
                    MessageRole::Assistant => "assistant".to_string(),
                    MessageRole::System => "system".to_string(),
                },
                content: msg.content.clone(),
            })
            .collect()
    }
}

#[async_trait]
impl LLMClient for OllamaClient {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        info!("Sending request to Ollama");

        let chat_messages = self.convert_messages(&request.messages);

        let ollama_request = ChatRequest {
            model: self.config.model.clone(),
            messages: chat_messages,
            stream: Some(false),
        };

        let url = format!("{}/api/chat", self.config.base_url);

        let response = self
            .http_client
            .post(&url)
            .json(&ollama_request)
            .send()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("HTTP request failed: {}", e)))?;

        let status = response.status();
        if !status.is_success() {
            let body = response.text().await.unwrap_or_else(|_| "Unknown error".to_string());
            return Err(agent_core::Error::LLM(format!("API request failed with status {}: {}", status, body)));
        }

        let chat_response: ChatResponse = response
            .json()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("Failed to parse response: {}", e)))?;

        debug!("Received response from Ollama");

        Ok(CompletionResponse {
            content: chat_response.message.content,
            model: self.config.model.clone(),
            usage: TokenUsage::new(0, 0), // Ollama 不返回 token 统计
            finish_reason: Some("stop".to_string()),
        })
    }

    async fn stream(&self, request: CompletionRequest) -> Result<Box<dyn CompletionStream>> {
        info!("Sending streaming request to Ollama");

        let chat_messages = self.convert_messages(&request.messages);

        let ollama_request = ChatRequest {
            model: self.config.model.clone(),
            messages: chat_messages,
            stream: Some(true),
        };

        let url = format!("{}/api/chat", self.config.base_url);

        let response = self
            .http_client
            .post(&url)
            .json(&ollama_request)
            .send()
            .await
            .map_err(|e| agent_core::Error::LLM(format!("HTTP request failed: {}", e)))?;

        let status = response.status();
        if !status.is_success() {
            let body = response.text().await.unwrap_or_else(|_| "Unknown error".to_string());
            return Err(agent_core::Error::LLM(format!("API request failed with status {}: {}", status, body)));
        }

        debug!("Received streaming response from Ollama");

        Ok(Box::new(OllamaStream::new(response)))
    }

    async fn list_models(&self) -> Result<Vec<ModelInfo>> {
        Ok(vec![ModelInfo {
            id: self.config.model.clone(),
            name: self.config.model.clone(),
            provider: "ollama".to_string(),
            context_window: None,
            max_output_tokens: None,
            supports_streaming: true,
        }])
    }

    fn provider_name(&self) -> &str {
        "ollama"
    }
}
