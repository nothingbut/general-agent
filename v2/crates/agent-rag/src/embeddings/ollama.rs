//! Ollama embeddings 实现

use async_trait::async_trait;
use reqwest::Client;
use serde::{Deserialize, Serialize};
use crate::{Result, RAGError};
use super::Embedder;

/// Ollama embedding 请求
#[derive(Debug, Serialize)]
struct EmbedRequest {
    model: String,
    prompt: String,
}

/// Ollama embedding 响应
#[derive(Debug, Deserialize)]
struct EmbedResponse {
    embedding: Vec<f32>,
}

/// Ollama embedder 配置
#[derive(Debug, Clone)]
pub struct OllamaConfig {
    /// Ollama 服务器地址
    pub base_url: String,
    /// 模型名称
    pub model: String,
    /// Embedding 维度
    pub dimension: usize,
}

impl Default for OllamaConfig {
    fn default() -> Self {
        Self {
            base_url: "http://localhost:11434".to_string(),
            model: "nomic-embed-text".to_string(),
            dimension: 768,
        }
    }
}

/// Ollama embedder
pub struct OllamaEmbedder {
    client: Client,
    config: OllamaConfig,
}

impl OllamaEmbedder {
    /// 创建新的 Ollama embedder
    pub fn new(config: OllamaConfig) -> Self {
        Self {
            client: Client::new(),
            config,
        }
    }

    /// 使用默认配置创建
    pub fn with_defaults() -> Self {
        Self::new(OllamaConfig::default())
    }

    /// 调用 Ollama API 生成 embedding
    async fn generate_embedding(&self, text: &str) -> Result<Vec<f32>> {
        let url = format!("{}/api/embeddings", self.config.base_url);

        let request = EmbedRequest {
            model: self.config.model.clone(),
            prompt: text.to_string(),
        };

        let response = self.client
            .post(&url)
            .json(&request)
            .send()
            .await
            .map_err(|e| RAGError::EmbeddingFailed(format!("HTTP request failed: {}", e)))?;

        if !response.status().is_success() {
            let status = response.status();
            let error_text = response.text().await.unwrap_or_else(|_| "Unknown error".to_string());
            return Err(RAGError::EmbeddingFailed(format!("Ollama API error {}: {}", status, error_text)));
        }

        let embed_response: EmbedResponse = response.json().await
            .map_err(|e| RAGError::EmbeddingFailed(format!("Failed to parse response: {}", e)))?;

        Ok(embed_response.embedding)
    }
}

#[async_trait]
impl Embedder for OllamaEmbedder {
    async fn embed(&self, text: &str) -> Result<Vec<f32>> {
        self.generate_embedding(text).await
    }

    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>> {
        let mut embeddings = Vec::with_capacity(texts.len());

        for text in texts {
            let embedding = self.generate_embedding(text).await?;
            embeddings.push(embedding);
        }

        Ok(embeddings)
    }

    fn dimension(&self) -> usize {
        self.config.dimension
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_ollama_embedder_creation() {
        let embedder = OllamaEmbedder::with_defaults();
        assert_eq!(embedder.dimension(), 768);
    }

    #[tokio::test]
    async fn test_ollama_config_default() {
        let config = OllamaConfig::default();
        assert_eq!(config.base_url, "http://localhost:11434");
        assert_eq!(config.model, "nomic-embed-text");
        assert_eq!(config.dimension, 768);
    }
}
