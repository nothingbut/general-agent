//! Embeddings 生成模块

pub mod ollama;

use async_trait::async_trait;
use crate::Result;

/// Embedder trait
#[async_trait]
pub trait Embedder: Send + Sync {
    /// 为单个文本生成 embedding
    async fn embed(&self, text: &str) -> Result<Vec<f32>>;

    /// 为多个文本批量生成 embeddings
    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>>;

    /// 获取 embedding 维度
    fn dimension(&self) -> usize;
}
