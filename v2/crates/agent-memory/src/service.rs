use crate::error::Result;
use crate::extractor::MemoryExtractor;
use crate::models::{Memory, MemoryQuery, MemoryType};
use crate::repository::MemoryRepository;
use crate::vector_store::VectorMemoryStore;
use agent_core::models::Message;
use std::sync::Arc;
use tracing::info;
use uuid::Uuid;

pub struct MemoryService {
    repository: Arc<dyn MemoryRepository>,
    vector_store: VectorMemoryStore,
    extractor: MemoryExtractor,
}

impl MemoryService {
    pub fn new(
        repository: Arc<dyn MemoryRepository>,
        vector_store: VectorMemoryStore,
        extractor: MemoryExtractor,
    ) -> Self {
        Self {
            repository,
            vector_store,
            extractor,
        }
    }

    pub async fn initialize(&mut self) -> Result<()> {
        self.vector_store.initialize().await
    }

    pub fn is_vector_available(&self) -> bool {
        self.vector_store.is_vector_available()
    }

    // --- CRUD ---

    pub async fn create(&self, memory: Memory) -> Result<Memory> {
        self.vector_store.store_memory(&memory).await
    }

    pub async fn get(&self, id: Uuid) -> Result<Option<Memory>> {
        self.repository.find_by_id(id).await
    }

    pub async fn update(&self, memory: &Memory) -> Result<Memory> {
        self.repository.update(memory).await
    }

    pub async fn delete(&self, id: Uuid) -> Result<()> {
        if let Some(memory) = self.repository.find_by_id(id).await? {
            self.vector_store.delete_memory(&memory).await
        } else {
            Err(crate::error::MemoryError::NotFound(id.to_string()))
        }
    }

    pub async fn list(&self, query: &MemoryQuery) -> Result<Vec<Memory>> {
        self.repository.query(query).await
    }

    pub async fn list_by_type(&self, memory_type: MemoryType, limit: u32) -> Result<Vec<Memory>> {
        self.repository
            .query(&MemoryQuery::by_type(memory_type).with_limit(limit))
            .await
    }

    pub async fn count_by_type(&self, memory_type: MemoryType) -> Result<u64> {
        self.repository.count_by_type(memory_type).await
    }

    // --- Search ---

    pub async fn search_keyword(&self, keyword: &str, limit: u32) -> Result<Vec<Memory>> {
        self.repository.search(keyword, limit).await
    }

    pub async fn search_semantic(&self, query: &str, top_k: usize) -> Result<Vec<Memory>> {
        self.vector_store.semantic_search(query, top_k).await
    }

    pub async fn search_hybrid(&self, query: &str, top_k: usize) -> Result<Vec<Memory>> {
        self.vector_store.hybrid_search(query, top_k).await
    }

    pub async fn find_relevant(&self, context: &str, top_k: usize) -> Result<Vec<Memory>> {
        self.vector_store.find_relevant(context, top_k).await
    }

    // --- Extraction ---

    pub async fn extract_from_messages(
        &self,
        messages: &[Message],
        session_id: Option<Uuid>,
    ) -> Result<Vec<Memory>> {
        let result = self.extractor.extract_from_messages(messages, session_id).await?;

        let mut saved = Vec::new();
        for extracted in &result.memories {
            if let Some(memory) = extracted.to_memory(session_id) {
                match self.create(memory).await {
                    Ok(m) => saved.push(m),
                    Err(e) => {
                        tracing::warn!("Failed to save extracted memory: {}", e);
                    }
                }
            }
        }

        info!(
            "Extracted and saved {}/{} memories from {} messages",
            saved.len(),
            result.memories.len(),
            result.message_count
        );

        Ok(saved)
    }

    // --- Stats ---

    pub async fn stats(&self) -> Result<MemoryStats> {
        let mut type_counts = Vec::new();
        for mt in MemoryType::all() {
            let count = self.repository.count_by_type(*mt).await?;
            type_counts.push((*mt, count));
        }

        let total: u64 = type_counts.iter().map(|(_, c)| c).sum();

        Ok(MemoryStats {
            total_memories: total,
            type_counts,
            vector_available: self.is_vector_available(),
        })
    }
}

#[derive(Debug, Clone)]
pub struct MemoryStats {
    pub total_memories: u64,
    pub type_counts: Vec<(MemoryType, u64)>,
    pub vector_available: bool,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::repository::SqliteMemoryRepository;
    use crate::vector_store::VectorMemoryStore;
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    };
    use async_trait::async_trait;
    use sqlx::SqlitePool;

    struct MockLLMClient {
        response: String,
    }

    #[async_trait]
    impl LLMClient for MockLLMClient {
        async fn complete(&self, _req: CompletionRequest) -> agent_core::Result<CompletionResponse> {
            Ok(CompletionResponse {
                content: self.response.clone(),
                model: "mock".to_string(),
                usage: TokenUsage::new(10, 5),
                finish_reason: Some("stop".to_string()),
            })
        }
        async fn stream(&self, _req: CompletionRequest) -> agent_core::Result<Box<dyn CompletionStream>> {
            unimplemented!()
        }
        async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
            Ok(vec![])
        }
        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    async fn setup(llm_response: &str) -> MemoryService {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();

        let repo: Arc<dyn MemoryRepository> = Arc::new(SqliteMemoryRepository::new(pool));
        let vector_store = VectorMemoryStore::new(repo.clone());
        let llm: Arc<dyn LLMClient> = Arc::new(MockLLMClient {
            response: llm_response.to_string(),
        });
        let extractor = MemoryExtractor::new(llm, "mock".to_string());

        let mut service = MemoryService::new(repo, vector_store, extractor);
        service.initialize().await.unwrap();
        service
    }

    #[tokio::test]
    async fn test_create_and_get() {
        let service = setup("[]").await;

        let memory = Memory::new(MemoryType::User, "user is a developer".to_string());
        let id = memory.id;
        service.create(memory).await.unwrap();

        let found = service.get(id).await.unwrap().unwrap();
        assert_eq!(found.content, "user is a developer");
    }

    #[tokio::test]
    async fn test_update_memory() {
        let service = setup("[]").await;

        let memory = Memory::new(MemoryType::Feedback, "original".to_string());
        let created = service.create(memory).await.unwrap();

        let mut updated = created.clone();
        updated.content = "updated content".to_string();
        service.update(&updated).await.unwrap();

        let found = service.get(created.id).await.unwrap().unwrap();
        assert_eq!(found.content, "updated content");
    }

    #[tokio::test]
    async fn test_delete_memory() {
        let service = setup("[]").await;

        let memory = Memory::new(MemoryType::Project, "deadline info".to_string());
        let id = memory.id;
        service.create(memory).await.unwrap();

        service.delete(id).await.unwrap();
        assert!(service.get(id).await.unwrap().is_none());
    }

    #[tokio::test]
    async fn test_list_by_type() {
        let service = setup("[]").await;

        service.create(Memory::new(MemoryType::User, "u1".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::User, "u2".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::Feedback, "f1".to_string())).await.unwrap();

        let users = service.list_by_type(MemoryType::User, 50).await.unwrap();
        assert_eq!(users.len(), 2);
    }

    #[tokio::test]
    async fn test_keyword_search() {
        let service = setup("[]").await;

        service.create(Memory::new(MemoryType::User, "user likes Rust programming".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::Feedback, "avoid using Python".to_string())).await.unwrap();

        let results = service.search_keyword("Rust", 10).await.unwrap();
        assert_eq!(results.len(), 1);
    }

    #[tokio::test]
    async fn test_extract_and_save() {
        let response = r#"[{"type": "user", "content": "用户熟悉 Rust", "source": "对话"}]"#;
        let service = setup(response).await;

        let messages = vec![
            Message::new(Uuid::new_v4(), MessageRole::User, "我很熟悉 Rust".to_string()),
        ];

        let saved = service.extract_from_messages(&messages, None).await.unwrap();
        assert_eq!(saved.len(), 1);
        assert_eq!(saved[0].memory_type, MemoryType::User);

        let all_users = service.list_by_type(MemoryType::User, 50).await.unwrap();
        assert_eq!(all_users.len(), 1);
    }

    #[tokio::test]
    async fn test_stats() {
        let service = setup("[]").await;

        service.create(Memory::new(MemoryType::User, "u1".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::User, "u2".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::Feedback, "f1".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::Knowledge, "k1".to_string())).await.unwrap();

        let stats = service.stats().await.unwrap();
        assert_eq!(stats.total_memories, 4);
        assert!(!stats.vector_available);

        let user_count = stats.type_counts.iter().find(|(t, _)| *t == MemoryType::User).unwrap().1;
        assert_eq!(user_count, 2);
    }

    #[tokio::test]
    async fn test_semantic_search_degrades_to_fts() {
        let service = setup("[]").await;

        service.create(Memory::new(MemoryType::Knowledge, "Rust memory safety".to_string())).await.unwrap();

        let results = service.search_semantic("Rust", 10).await.unwrap();
        assert_eq!(results.len(), 1);
    }

    #[tokio::test]
    async fn test_find_relevant() {
        let service = setup("[]").await;

        service.create(Memory::new(MemoryType::User, "user is a data scientist".to_string())).await.unwrap();
        service.create(Memory::new(MemoryType::Feedback, "prefers Python".to_string())).await.unwrap();

        let results = service.find_relevant("data", 10).await.unwrap();
        assert_eq!(results.len(), 1);
    }
}
