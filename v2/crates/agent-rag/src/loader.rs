//! 文档加载器实现

use agent_core::traits::Document;
use async_trait::async_trait;
use std::collections::HashMap;
use tokio::fs;
use crate::{Result, RAGError};

/// 文档加载器 trait
#[async_trait]
pub trait DocumentLoader: Send + Sync {
    /// 从源加载文档
    async fn load(&self, source: &str) -> Result<Vec<Document>>;
}

/// Markdown 文档加载器
pub struct MarkdownLoader;

#[async_trait]
impl DocumentLoader for MarkdownLoader {
    async fn load(&self, source: &str) -> Result<Vec<Document>> {
        let content = fs::read_to_string(source).await
            .map_err(|e| RAGError::LoadingFailed(e.to_string()))?;

        let mut metadata = HashMap::new();
        metadata.insert("type".to_string(), "markdown".to_string());
        metadata.insert("source".to_string(), source.to_string());

        let doc = Document {
            id: source.to_string(),
            content,
            metadata,
        };

        Ok(vec![doc])
    }
}

/// 文本文档加载器
pub struct TextLoader;

#[async_trait]
impl DocumentLoader for TextLoader {
    async fn load(&self, source: &str) -> Result<Vec<Document>> {
        let content = fs::read_to_string(source).await
            .map_err(|e| RAGError::LoadingFailed(e.to_string()))?;

        let mut metadata = HashMap::new();
        metadata.insert("type".to_string(), "text".to_string());
        metadata.insert("source".to_string(), source.to_string());

        let doc = Document {
            id: source.to_string(),
            content,
            metadata,
        };

        Ok(vec![doc])
    }
}
