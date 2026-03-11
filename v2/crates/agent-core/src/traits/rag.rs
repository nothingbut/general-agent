//! RAG (Retrieval Augmented Generation) trait 定义

use async_trait::async_trait;
use std::collections::HashMap;
use crate::error::Result;

/// 文档
#[derive(Debug, Clone)]
pub struct Document {
    pub id: String,
    pub content: String,
    pub metadata: HashMap<String, String>,
}

/// RAG 检索器抽象
#[async_trait]
pub trait RAGRetriever: Send + Sync {
    /// 检索相关文档
    async fn retrieve(&self, query: &str, top_k: usize) -> Result<Vec<Document>>;

    /// 索引新文档
    async fn index_document(&self, doc: Document) -> Result<()>;
}
