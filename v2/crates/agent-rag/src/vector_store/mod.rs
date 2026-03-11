//! 向量存储模块

pub mod qdrant;

use async_trait::async_trait;
use std::collections::HashMap;
use crate::Result;

/// 向量搜索结果
#[derive(Debug, Clone)]
pub struct SearchResult {
    /// 文档 ID
    pub id: String,
    /// 相似度分数
    pub score: f32,
    /// 元数据
    pub metadata: HashMap<String, String>,
    /// 文档内容（可选）
    pub content: Option<String>,
}

/// 向量存储 trait
#[async_trait]
pub trait VectorStore: Send + Sync {
    /// 创建集合
    async fn create_collection(&self, name: &str, dimension: usize) -> Result<()>;

    /// 删除集合
    async fn delete_collection(&self, name: &str) -> Result<()>;

    /// 插入向量
    async fn insert(
        &self,
        collection: &str,
        id: String,
        vector: Vec<f32>,
        metadata: HashMap<String, String>,
    ) -> Result<()>;

    /// 批量插入向量
    async fn insert_batch(
        &self,
        collection: &str,
        ids: Vec<String>,
        vectors: Vec<Vec<f32>>,
        metadatas: Vec<HashMap<String, String>>,
    ) -> Result<()>;

    /// 搜索相似向量
    async fn search(
        &self,
        collection: &str,
        query_vector: Vec<f32>,
        top_k: usize,
    ) -> Result<Vec<SearchResult>>;

    /// 检查集合是否存在
    async fn collection_exists(&self, name: &str) -> Result<bool>;
}
