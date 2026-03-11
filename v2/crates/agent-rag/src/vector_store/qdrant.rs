//! Qdrant 向量存储实现

use async_trait::async_trait;
use qdrant_client::Qdrant;
use qdrant_client::qdrant::{
    CreateCollectionBuilder, Distance, PointStruct, SearchPointsBuilder, UpsertPointsBuilder, VectorParamsBuilder,
    Value,
};
use std::collections::HashMap;
use crate::{Result, RAGError};
use super::{SearchResult, VectorStore};

/// Qdrant 配置
#[derive(Debug, Clone)]
pub struct QdrantConfig {
    /// Qdrant 服务器地址
    pub url: String,
}

impl Default for QdrantConfig {
    fn default() -> Self {
        Self {
            url: "http://localhost:6334".to_string(),
        }
    }
}

/// Qdrant 向量存储
pub struct QdrantStore {
    client: Qdrant,
}

impl QdrantStore {
    /// 创建新的 Qdrant 存储
    pub async fn new(config: QdrantConfig) -> Result<Self> {
        let client = Qdrant::from_url(&config.url)
            .build()
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to connect to Qdrant: {}", e)))?;

        Ok(Self { client })
    }

    /// 使用默认配置创建
    pub async fn with_defaults() -> Result<Self> {
        Self::new(QdrantConfig::default()).await
    }

    /// 将元数据转换为 Qdrant payload
    fn metadata_to_payload(metadata: HashMap<String, String>) -> HashMap<String, Value> {
        metadata
            .into_iter()
            .map(|(k, v)| (k, Value::from(v)))
            .collect()
    }

    /// 从 Qdrant payload 提取元数据
    fn payload_to_metadata(payload: &HashMap<String, Value>) -> HashMap<String, String> {
        payload
            .iter()
            .filter_map(|(k, v)| {
                if let Some(s) = v.as_str() {
                    Some((k.clone(), s.to_string()))
                } else {
                    None
                }
            })
            .collect()
    }
}

#[async_trait]
impl VectorStore for QdrantStore {
    async fn create_collection(&self, name: &str, dimension: usize) -> Result<()> {
        self.client
            .create_collection(
                CreateCollectionBuilder::new(name)
                    .vectors_config(VectorParamsBuilder::new(dimension as u64, Distance::Cosine)),
            )
            .await
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to create collection: {}", e)))?;

        Ok(())
    }

    async fn delete_collection(&self, name: &str) -> Result<()> {
        self.client
            .delete_collection(name)
            .await
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to delete collection: {}", e)))?;

        Ok(())
    }

    async fn insert(
        &self,
        collection: &str,
        id: String,
        vector: Vec<f32>,
        metadata: HashMap<String, String>,
    ) -> Result<()> {
        let payload = Self::metadata_to_payload(metadata);
        let point = PointStruct::new(id, vector, payload);

        self.client
            .upsert_points(UpsertPointsBuilder::new(collection, vec![point]))
            .await
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to insert point: {}", e)))?;

        Ok(())
    }

    async fn insert_batch(
        &self,
        collection: &str,
        ids: Vec<String>,
        vectors: Vec<Vec<f32>>,
        metadatas: Vec<HashMap<String, String>>,
    ) -> Result<()> {
        if ids.len() != vectors.len() || ids.len() != metadatas.len() {
            return Err(RAGError::VectorStoreError(
                "IDs, vectors, and metadatas must have the same length".to_string(),
            ));
        }

        let points: Vec<PointStruct> = ids
            .into_iter()
            .zip(vectors)
            .zip(metadatas)
            .map(|((id, vector), metadata)| {
                let payload = Self::metadata_to_payload(metadata);
                PointStruct::new(id, vector, payload)
            })
            .collect();

        self.client
            .upsert_points(UpsertPointsBuilder::new(collection, points))
            .await
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to insert batch: {}", e)))?;

        Ok(())
    }

    async fn search(
        &self,
        collection: &str,
        query_vector: Vec<f32>,
        top_k: usize,
    ) -> Result<Vec<SearchResult>> {
        let search_result = self
            .client
            .search_points(
                SearchPointsBuilder::new(collection, query_vector, top_k as u64)
                    .with_payload(true),
            )
            .await
            .map_err(|e| RAGError::RetrievalFailed(format!("Failed to search: {}", e)))?;

        let results = search_result
            .result
            .into_iter()
            .map(|point| {
                let metadata = Self::payload_to_metadata(&point.payload);
                let content = metadata.get("content").cloned();

                SearchResult {
                    id: point.id.map(|id| format!("{:?}", id)).unwrap_or_default(),
                    score: point.score,
                    metadata,
                    content,
                }
            })
            .collect();

        Ok(results)
    }

    async fn collection_exists(&self, name: &str) -> Result<bool> {
        let result = self.client.collection_exists(name).await
            .map_err(|e| RAGError::VectorStoreError(format!("Failed to check collection: {}", e)))?;

        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_qdrant_config_default() {
        let config = QdrantConfig::default();
        assert_eq!(config.url, "http://localhost:6334");
    }

    #[test]
    fn test_metadata_conversion() {
        let mut metadata = HashMap::new();
        metadata.insert("key1".to_string(), "value1".to_string());
        metadata.insert("key2".to_string(), "value2".to_string());

        let payload = QdrantStore::metadata_to_payload(metadata.clone());
        let converted = QdrantStore::payload_to_metadata(&payload);

        assert_eq!(converted, metadata);
    }
}
