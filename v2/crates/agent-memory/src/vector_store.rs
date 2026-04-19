use crate::error::{MemoryError, Result};
use crate::models::{Memory, MemoryType};
use crate::repository::MemoryRepository;
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::Arc;
use tracing::{debug, info, warn};

const COLLECTION_NAME: &str = "memories";

#[async_trait]
pub trait EmbeddingProvider: Send + Sync {
    async fn embed(&self, text: &str) -> Result<Vec<f32>>;
    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>>;
    fn dimension(&self) -> usize;
}

#[async_trait]
pub trait VectorStoreProvider: Send + Sync {
    async fn ensure_collection(&self, dimension: usize) -> Result<()>;
    async fn upsert(&self, id: &str, vector: Vec<f32>, metadata: HashMap<String, String>) -> Result<()>;
    async fn search(&self, query_vector: Vec<f32>, top_k: usize) -> Result<Vec<VectorSearchResult>>;
    async fn delete(&self, id: &str) -> Result<()>;
    async fn health_check(&self) -> bool;
}

#[derive(Debug, Clone)]
pub struct VectorSearchResult {
    pub id: String,
    pub score: f32,
    pub metadata: HashMap<String, String>,
}

#[cfg(feature = "vector")]
pub struct RagEmbeddingAdapter {
    embedder: Arc<dyn agent_rag::Embedder>,
}

#[cfg(feature = "vector")]
impl RagEmbeddingAdapter {
    pub fn new(embedder: Arc<dyn agent_rag::Embedder>) -> Self {
        Self { embedder }
    }
}

#[cfg(feature = "vector")]
#[async_trait]
impl EmbeddingProvider for RagEmbeddingAdapter {
    async fn embed(&self, text: &str) -> Result<Vec<f32>> {
        self.embedder
            .embed(text)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("Embedding failed: {}", e)))
    }

    async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>> {
        self.embedder
            .embed_batch(texts)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("Batch embedding failed: {}", e)))
    }

    fn dimension(&self) -> usize {
        self.embedder.dimension()
    }
}

#[cfg(feature = "vector")]
pub struct RagVectorStoreAdapter {
    store: Arc<dyn agent_rag::VectorStore>,
}

#[cfg(feature = "vector")]
impl RagVectorStoreAdapter {
    pub fn new(store: Arc<dyn agent_rag::VectorStore>) -> Self {
        Self { store }
    }
}

#[cfg(feature = "vector")]
#[async_trait]
impl VectorStoreProvider for RagVectorStoreAdapter {
    async fn ensure_collection(&self, dimension: usize) -> Result<()> {
        let exists = self
            .store
            .collection_exists(COLLECTION_NAME)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("{}", e)))?;

        if !exists {
            self.store
                .create_collection(COLLECTION_NAME, dimension)
                .await
                .map_err(|e| MemoryError::Other(anyhow::anyhow!("{}", e)))?;
        }
        Ok(())
    }

    async fn upsert(&self, id: &str, vector: Vec<f32>, metadata: HashMap<String, String>) -> Result<()> {
        self.store
            .insert(COLLECTION_NAME, id.to_string(), vector, metadata)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("{}", e)))
    }

    async fn search(&self, query_vector: Vec<f32>, top_k: usize) -> Result<Vec<VectorSearchResult>> {
        let results = self
            .store
            .search(COLLECTION_NAME, query_vector, top_k)
            .await
            .map_err(|e| MemoryError::Other(anyhow::anyhow!("{}", e)))?;

        Ok(results
            .into_iter()
            .map(|r| VectorSearchResult {
                id: r.id,
                score: r.score,
                metadata: r.metadata,
            })
            .collect())
    }

    async fn delete(&self, _id: &str) -> Result<()> {
        Ok(())
    }

    async fn health_check(&self) -> bool {
        self.store
            .collection_exists(COLLECTION_NAME)
            .await
            .is_ok()
    }
}

pub struct VectorMemoryStore {
    repository: Arc<dyn MemoryRepository>,
    embedder: Option<Arc<dyn EmbeddingProvider>>,
    vector_store: Option<Arc<dyn VectorStoreProvider>>,
    vector_available: bool,
}

impl VectorMemoryStore {
    pub fn new(repository: Arc<dyn MemoryRepository>) -> Self {
        Self {
            repository,
            embedder: None,
            vector_store: None,
            vector_available: false,
        }
    }

    pub fn with_vector(
        mut self,
        embedder: Arc<dyn EmbeddingProvider>,
        vector_store: Arc<dyn VectorStoreProvider>,
    ) -> Self {
        self.embedder = Some(embedder);
        self.vector_store = Some(vector_store);
        self
    }

    pub async fn initialize(&mut self) -> Result<()> {
        if let (Some(ref embedder), Some(ref store)) = (&self.embedder, &self.vector_store) {
            if store.health_check().await {
                match store.ensure_collection(embedder.dimension()).await {
                    Ok(_) => {
                        self.vector_available = true;
                        info!("Vector store initialized (dim={})", embedder.dimension());
                    }
                    Err(e) => {
                        warn!("Vector store init failed, degrading to FTS: {}", e);
                        self.vector_available = false;
                    }
                }
            } else {
                warn!("Vector store unavailable, degrading to FTS");
                self.vector_available = false;
            }
        } else {
            debug!("No vector store configured, using FTS only");
            self.vector_available = false;
        }
        Ok(())
    }

    pub fn is_vector_available(&self) -> bool {
        self.vector_available
    }

    pub async fn store_memory(&self, memory: &Memory) -> Result<Memory> {
        let created = self.repository.create(memory.clone()).await?;

        if self.vector_available {
            if let Err(e) = self.index_memory(&created).await {
                warn!("Failed to index memory in vector store: {}", e);
            }
        }

        Ok(created)
    }

    pub async fn semantic_search(&self, query: &str, top_k: usize) -> Result<Vec<Memory>> {
        if self.vector_available {
            match self.vector_search(query, top_k).await {
                Ok(memories) => return Ok(memories),
                Err(e) => {
                    warn!("Vector search failed, falling back to FTS: {}", e);
                }
            }
        }

        debug!("Using FTS fallback for search: {}", query);
        self.repository.search(query, top_k as u32).await
    }

    pub async fn hybrid_search(&self, query: &str, top_k: usize) -> Result<Vec<Memory>> {
        let fts_results = self.repository.search(query, top_k as u32).await?;

        if !self.vector_available {
            return Ok(fts_results);
        }

        let vector_results = match self.vector_search(query, top_k).await {
            Ok(results) => results,
            Err(e) => {
                warn!("Vector search failed in hybrid mode: {}", e);
                return Ok(fts_results);
            }
        };

        Ok(Self::merge_results(fts_results, vector_results, top_k))
    }

    pub async fn find_relevant(&self, context: &str, top_k: usize) -> Result<Vec<Memory>> {
        self.semantic_search(context, top_k).await
    }

    pub async fn delete_memory(&self, memory: &Memory) -> Result<()> {
        self.repository.delete(memory.id).await?;

        if self.vector_available {
            if let Some(ref store) = self.vector_store {
                if let Err(e) = store.delete(&memory.id.to_string()).await {
                    warn!("Failed to delete from vector store: {}", e);
                }
            }
        }

        Ok(())
    }

    async fn index_memory(&self, memory: &Memory) -> Result<()> {
        let embedder = self.embedder.as_ref().ok_or_else(|| {
            MemoryError::Other(anyhow::anyhow!("Embedder not configured"))
        })?;
        let store = self.vector_store.as_ref().ok_or_else(|| {
            MemoryError::Other(anyhow::anyhow!("Vector store not configured"))
        })?;

        let vector = embedder.embed(&memory.content).await?;

        let mut metadata = HashMap::new();
        metadata.insert("memory_type".to_string(), memory.memory_type.as_str().to_string());
        metadata.insert("content".to_string(), memory.content.clone());
        if let Some(ref source) = memory.source {
            metadata.insert("source".to_string(), source.clone());
        }

        store.upsert(&memory.id.to_string(), vector, metadata).await
    }

    async fn vector_search(&self, query: &str, top_k: usize) -> Result<Vec<Memory>> {
        let embedder = self.embedder.as_ref().ok_or_else(|| {
            MemoryError::Other(anyhow::anyhow!("Embedder not configured"))
        })?;
        let store = self.vector_store.as_ref().ok_or_else(|| {
            MemoryError::Other(anyhow::anyhow!("Vector store not configured"))
        })?;

        let query_vector = embedder.embed(query).await?;
        let results = store.search(query_vector, top_k).await?;

        let mut memories = Vec::new();
        for result in results {
            let id = uuid::Uuid::parse_str(&result.id)
                .map_err(|e| MemoryError::Database(format!("Invalid UUID: {}", e)))?;

            if let Some(memory) = self.repository.find_by_id(id).await? {
                memories.push(memory);
            }
        }

        Ok(memories)
    }

    fn merge_results(fts: Vec<Memory>, vector: Vec<Memory>, top_k: usize) -> Vec<Memory> {
        let mut seen = std::collections::HashSet::new();
        let mut merged = Vec::new();

        let mut fts_iter = fts.into_iter();
        let mut vec_iter = vector.into_iter();

        loop {
            if merged.len() >= top_k {
                break;
            }

            match (fts_iter.next(), vec_iter.next()) {
                (Some(f), Some(v)) => {
                    if seen.insert(f.id) {
                        merged.push(f);
                    }
                    if merged.len() < top_k && seen.insert(v.id) {
                        merged.push(v);
                    }
                }
                (Some(f), None) => {
                    if seen.insert(f.id) {
                        merged.push(f);
                    }
                }
                (None, Some(v)) => {
                    if seen.insert(v.id) {
                        merged.push(v);
                    }
                }
                (None, None) => break,
            }
        }

        merged
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::MemoryType;
    use crate::repository::SqliteMemoryRepository;
    use sqlx::SqlitePool;
    use std::sync::atomic::{AtomicBool, Ordering};

    async fn setup_repo() -> (SqlitePool, Arc<SqliteMemoryRepository>) {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        let repo = Arc::new(SqliteMemoryRepository::new(pool.clone()));
        (pool, repo)
    }

    struct MockEmbedder {
        dimension: usize,
    }

    #[async_trait]
    impl EmbeddingProvider for MockEmbedder {
        async fn embed(&self, _text: &str) -> Result<Vec<f32>> {
            Ok(vec![0.1; self.dimension])
        }

        async fn embed_batch(&self, texts: &[String]) -> Result<Vec<Vec<f32>>> {
            Ok(texts.iter().map(|_| vec![0.1; self.dimension]).collect())
        }

        fn dimension(&self) -> usize {
            self.dimension
        }
    }

    struct MockVectorStore {
        healthy: AtomicBool,
        search_results: Vec<VectorSearchResult>,
    }

    impl MockVectorStore {
        fn new(healthy: bool) -> Self {
            Self {
                healthy: AtomicBool::new(healthy),
                search_results: Vec::new(),
            }
        }

        fn with_results(mut self, results: Vec<VectorSearchResult>) -> Self {
            self.search_results = results;
            self
        }
    }

    #[async_trait]
    impl VectorStoreProvider for MockVectorStore {
        async fn ensure_collection(&self, _dimension: usize) -> Result<()> {
            if self.healthy.load(Ordering::Relaxed) {
                Ok(())
            } else {
                Err(MemoryError::Other(anyhow::anyhow!("Store unavailable")))
            }
        }

        async fn upsert(&self, _id: &str, _vector: Vec<f32>, _metadata: HashMap<String, String>) -> Result<()> {
            Ok(())
        }

        async fn search(&self, _query_vector: Vec<f32>, _top_k: usize) -> Result<Vec<VectorSearchResult>> {
            Ok(self.search_results.clone())
        }

        async fn delete(&self, _id: &str) -> Result<()> {
            Ok(())
        }

        async fn health_check(&self) -> bool {
            self.healthy.load(Ordering::Relaxed)
        }
    }

    #[tokio::test]
    async fn test_store_without_vector() {
        let (_pool, repo) = setup_repo().await;
        let mut store = VectorMemoryStore::new(repo);
        store.initialize().await.unwrap();

        assert!(!store.is_vector_available());
    }

    #[tokio::test]
    async fn test_store_with_healthy_vector() {
        let (_pool, repo) = setup_repo().await;
        let embedder: Arc<dyn EmbeddingProvider> = Arc::new(MockEmbedder { dimension: 384 });
        let vector: Arc<dyn VectorStoreProvider> = Arc::new(MockVectorStore::new(true));

        let mut store = VectorMemoryStore::new(repo).with_vector(embedder, vector);
        store.initialize().await.unwrap();

        assert!(store.is_vector_available());
    }

    #[tokio::test]
    async fn test_store_with_unhealthy_vector_degrades() {
        let (_pool, repo) = setup_repo().await;
        let embedder: Arc<dyn EmbeddingProvider> = Arc::new(MockEmbedder { dimension: 384 });
        let vector: Arc<dyn VectorStoreProvider> = Arc::new(MockVectorStore::new(false));

        let mut store = VectorMemoryStore::new(repo).with_vector(embedder, vector);
        store.initialize().await.unwrap();

        assert!(!store.is_vector_available());
    }

    #[tokio::test]
    async fn test_store_memory_without_vector() {
        let (_pool, repo) = setup_repo().await;
        let mut store = VectorMemoryStore::new(repo.clone());
        store.initialize().await.unwrap();

        let memory = Memory::new(MemoryType::User, "user is a developer".to_string());
        let created = store.store_memory(&memory).await.unwrap();

        let found = repo.find_by_id(created.id).await.unwrap();
        assert!(found.is_some());
    }

    #[tokio::test]
    async fn test_store_memory_with_vector() {
        let (_pool, repo) = setup_repo().await;
        let embedder: Arc<dyn EmbeddingProvider> = Arc::new(MockEmbedder { dimension: 384 });
        let vector: Arc<dyn VectorStoreProvider> = Arc::new(MockVectorStore::new(true));

        let mut store = VectorMemoryStore::new(repo.clone()).with_vector(embedder, vector);
        store.initialize().await.unwrap();

        let memory = Memory::new(MemoryType::Feedback, "prefer immutable data".to_string());
        let created = store.store_memory(&memory).await.unwrap();

        let found = repo.find_by_id(created.id).await.unwrap();
        assert!(found.is_some());
    }

    #[tokio::test]
    async fn test_semantic_search_falls_back_to_fts() {
        let (_pool, repo) = setup_repo().await;

        repo.create(Memory::new(MemoryType::User, "user prefers dark mode".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "avoid global state".to_string())).await.unwrap();

        let mut store = VectorMemoryStore::new(repo);
        store.initialize().await.unwrap();

        let results = store.semantic_search("dark", 10).await.unwrap();
        assert_eq!(results.len(), 1);
        assert!(results[0].content.contains("dark"));
    }

    #[tokio::test]
    async fn test_semantic_search_with_vector() {
        let (_pool, repo) = setup_repo().await;

        let m1 = Memory::new(MemoryType::User, "user likes Rust".to_string());
        let m1_id = m1.id;
        repo.create(m1).await.unwrap();

        let embedder: Arc<dyn EmbeddingProvider> = Arc::new(MockEmbedder { dimension: 384 });
        let vector_results = vec![VectorSearchResult {
            id: m1_id.to_string(),
            score: 0.95,
            metadata: HashMap::new(),
        }];
        let vector: Arc<dyn VectorStoreProvider> = Arc::new(
            MockVectorStore::new(true).with_results(vector_results)
        );

        let mut store = VectorMemoryStore::new(repo).with_vector(embedder, vector);
        store.initialize().await.unwrap();

        let results = store.semantic_search("programming languages", 5).await.unwrap();
        assert_eq!(results.len(), 1);
        assert_eq!(results[0].id, m1_id);
    }

    #[tokio::test]
    async fn test_hybrid_search_merges_results() {
        let (_pool, repo) = setup_repo().await;

        let m1 = Memory::new(MemoryType::User, "user likes dark themes".to_string());
        let m1_id = m1.id;
        repo.create(m1).await.unwrap();

        let m2 = Memory::new(MemoryType::Feedback, "dark mode is preferred".to_string());
        let m2_id = m2.id;
        repo.create(m2).await.unwrap();

        let embedder: Arc<dyn EmbeddingProvider> = Arc::new(MockEmbedder { dimension: 384 });
        let vector_results = vec![
            VectorSearchResult {
                id: m2_id.to_string(),
                score: 0.9,
                metadata: HashMap::new(),
            },
            VectorSearchResult {
                id: m1_id.to_string(),
                score: 0.8,
                metadata: HashMap::new(),
            },
        ];
        let vector: Arc<dyn VectorStoreProvider> = Arc::new(
            MockVectorStore::new(true).with_results(vector_results)
        );

        let mut store = VectorMemoryStore::new(repo).with_vector(embedder, vector);
        store.initialize().await.unwrap();

        let results = store.hybrid_search("dark", 10).await.unwrap();
        assert!(results.len() >= 1);

        let ids: Vec<_> = results.iter().map(|m| m.id).collect();
        assert!(ids.contains(&m1_id) || ids.contains(&m2_id));
    }

    #[tokio::test]
    async fn test_hybrid_search_without_vector() {
        let (_pool, repo) = setup_repo().await;

        repo.create(Memory::new(MemoryType::Knowledge, "Rust is memory safe".to_string())).await.unwrap();

        let mut store = VectorMemoryStore::new(repo);
        store.initialize().await.unwrap();

        let results = store.hybrid_search("Rust", 10).await.unwrap();
        assert_eq!(results.len(), 1);
    }

    #[tokio::test]
    async fn test_delete_memory() {
        let (_pool, repo) = setup_repo().await;

        let memory = Memory::new(MemoryType::Project, "deadline Friday".to_string());
        let created = repo.create(memory).await.unwrap();

        let mut store = VectorMemoryStore::new(repo.clone());
        store.initialize().await.unwrap();

        store.delete_memory(&created).await.unwrap();
        let found = repo.find_by_id(created.id).await.unwrap();
        assert!(found.is_none());
    }

    #[tokio::test]
    async fn test_find_relevant_delegates_to_semantic() {
        let (_pool, repo) = setup_repo().await;

        repo.create(Memory::new(MemoryType::User, "user knows Python well".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "prefer Python style".to_string())).await.unwrap();

        let mut store = VectorMemoryStore::new(repo);
        store.initialize().await.unwrap();

        let results = store.find_relevant("Python", 10).await.unwrap();
        assert_eq!(results.len(), 2);
    }

    #[tokio::test]
    async fn test_merge_results_deduplicates() {
        let id1 = uuid::Uuid::new_v4();
        let id2 = uuid::Uuid::new_v4();
        let id3 = uuid::Uuid::new_v4();

        let m1 = Memory::new(MemoryType::User, "a".to_string());
        let m2 = Memory::new(MemoryType::User, "b".to_string());
        let m3 = Memory::new(MemoryType::User, "c".to_string());

        let mut m1c = m1.clone();
        m1c.id = id1;
        let mut m2c = m2.clone();
        m2c.id = id2;
        let mut m3c = m3.clone();
        m3c.id = id3;

        let mut m1d = m1.clone();
        m1d.id = id1; // duplicate

        let fts = vec![m1c, m2c.clone()];
        let vector = vec![m1d, m3c.clone()];

        let merged = VectorMemoryStore::merge_results(fts, vector, 10);
        let ids: Vec<_> = merged.iter().map(|m| m.id).collect();

        // id1 should only appear once
        assert_eq!(ids.iter().filter(|&&id| id == id1).count(), 1);
        assert!(ids.contains(&id2));
        assert!(ids.contains(&id3));
    }
}
