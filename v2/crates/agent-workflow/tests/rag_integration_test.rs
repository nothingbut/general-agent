//! ConversationFlow RAG 集成测试
//!
//! 测试 ConversationFlow 与 RAG 的集成

#[cfg(feature = "rag")]
mod rag_tests {
    use agent_core::{
        error::Result,
        traits::{
            llm::{CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, StreamChunk, TokenUsage},
            RAGRetriever,
        },
    };
    use agent_storage::{repository::*, Database};
    use agent_workflow::{ConversationConfig, ConversationFlow, SessionManager};
    use async_trait::async_trait;
    use std::sync::Arc;

    /// Mock LLM 客户端
    struct MockLLMClient {
        response: String,
    }

    #[async_trait]
    impl LLMClient for MockLLMClient {
        async fn complete(&self, _request: CompletionRequest) -> Result<CompletionResponse> {
            Ok(CompletionResponse {
                content: self.response.clone(),
                model: "mock-model".to_string(),
                usage: TokenUsage {
                    prompt_tokens: 10,
                    completion_tokens: 20,
                    total_tokens: 30,
                },
                finish_reason: Some("stop".to_string()),
            })
        }

        async fn stream(&self, _request: CompletionRequest) -> Result<Box<dyn CompletionStream>> {
            unimplemented!("Stream not needed for this test")
        }

        async fn list_models(&self) -> Result<Vec<ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    /// Mock RAG Retriever
    struct MockRAGRetriever {
        should_return_results: bool,
    }

    #[async_trait]
    impl RAGRetriever for MockRAGRetriever {
        async fn retrieve(
            &self,
            query: &str,
            _top_k: usize,
        ) -> Result<Vec<agent_core::traits::Document>> {
            if !self.should_return_results {
                return Ok(vec![]);
            }

            Ok(vec![
                agent_core::traits::Document {
                    id: "doc1".to_string(),
                    content: format!("Retrieved context for: {}", query),
                    metadata: std::collections::HashMap::new(),
                },
            ])
        }

        async fn index_document(
            &self,
            _doc: agent_core::traits::Document,
        ) -> Result<()> {
            Ok(())
        }
    }

    /// 测试辅助函数：创建测试环境
    async fn setup() -> (Database, Arc<SessionManager>, Arc<dyn LLMClient>) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient {
            response: "Mock response with RAG context".to_string(),
        });

        (db, session_manager, llm_client)
    }

    #[tokio::test]
    async fn test_conversation_flow_with_rag() {
        let (_db, session_manager, llm_client) = setup().await;

        let retriever: Arc<dyn RAGRetriever> = Arc::new(MockRAGRetriever {
            should_return_results: true,
        });

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_rag(retriever);

        let session = session_manager
            .create_session(Some("RAG Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "What is RAG?".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());

        let messages = session_manager
            .get_messages(session.id, None)
            .await
            .unwrap();
        assert_eq!(messages.len(), 2);
    }

    #[tokio::test]
    async fn test_rag_disabled_flow() {
        let (_db, session_manager, llm_client) = setup().await;

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

        let session = session_manager
            .create_session(Some("No RAG Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Test message".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());
    }

    #[tokio::test]
    async fn test_rag_with_empty_results() {
        let (_db, session_manager, llm_client) = setup().await;

        let retriever: Arc<dyn RAGRetriever> = Arc::new(MockRAGRetriever {
            should_return_results: false,
        });

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_rag(retriever);

        let session = session_manager
            .create_session(Some("Empty Results Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Query with no results".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());
    }

    #[tokio::test]
    async fn test_rag_multi_turn_conversation() {
        let (_db, session_manager, llm_client) = setup().await;

        let retriever: Arc<dyn RAGRetriever> = Arc::new(MockRAGRetriever {
            should_return_results: true,
        });

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_rag(retriever);

        let session = session_manager
            .create_session(Some("Multi-turn RAG Test".to_string()))
            .await
            .unwrap();

        let response1 = flow
            .send_message(session.id, "First question".to_string())
            .await
            .unwrap();
        assert!(!response1.is_empty());

        let response2 = flow
            .send_message(session.id, "Follow-up question".to_string())
            .await
            .unwrap();
        assert!(!response2.is_empty());

        let messages = session_manager
            .get_messages(session.id, None)
            .await
            .unwrap();
        assert_eq!(messages.len(), 4);
    }
}

#[cfg(not(feature = "rag"))]
mod placeholder {
    #[test]
    fn rag_feature_not_enabled() {
        println!("RAG feature is not enabled. Enable with: cargo test --features rag");
    }
}
