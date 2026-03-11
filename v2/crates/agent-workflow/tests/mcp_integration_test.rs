//! ConversationFlow MCP 集成测试
//!
//! 测试 ConversationFlow 与 MCP 的集成

#[cfg(feature = "mcp")]
mod mcp_tests {
    use agent_core::{
        error::Result,
        traits::{
            llm::{CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage},
            MCPClient,
        },
    };
    use agent_storage::{repository::*, Database};
    use agent_workflow::{ConversationConfig, ConversationFlow, SessionManager};
    use async_trait::async_trait;
    use serde_json::json;
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

    /// Mock MCP 客户端
    struct MockMCPClient {
        name: String,
    }

    #[async_trait]
    impl MCPClient for MockMCPClient {
        async fn list_tools(
            &self,
        ) -> Result<Vec<agent_core::traits::ToolDefinition>> {
            Ok(vec![
                agent_core::traits::ToolDefinition {
                    name: "test_tool".to_string(),
                    description: "A test tool".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "param": { "type": "string" }
                        }
                    }),
                },
                agent_core::traits::ToolDefinition {
                    name: "another_tool".to_string(),
                    description: "Another test tool".to_string(),
                    input_schema: json!({
                        "type": "object",
                        "properties": {
                            "value": { "type": "number" }
                        }
                    }),
                },
            ])
        }

        async fn call_tool(
            &self,
            tool_name: &str,
            arguments: serde_json::Value,
        ) -> Result<serde_json::Value> {
            Ok(json!({
                "result": format!("Called {} with args: {}", tool_name, arguments),
                "success": true
            }))
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
            response: "Mock response with MCP tools".to_string(),
        });

        (db, session_manager, llm_client)
    }

    #[tokio::test]
    async fn test_conversation_flow_with_mcp() {
        let (_db, session_manager, llm_client) = setup().await;

        let mcp_client: Arc<dyn MCPClient> = Arc::new(MockMCPClient {
            name: "test-mcp".to_string(),
        });

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_mcp(vec![mcp_client]);

        let session = session_manager
            .create_session(Some("MCP Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Use MCP tools".to_string())
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
    async fn test_mcp_multiple_clients() {
        let (_db, session_manager, llm_client) = setup().await;

        let mcp1: Arc<dyn MCPClient> = Arc::new(MockMCPClient {
            name: "mcp-server-1".to_string(),
        });
        let mcp2: Arc<dyn MCPClient> = Arc::new(MockMCPClient {
            name: "mcp-server-2".to_string(),
        });

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_mcp(vec![mcp1, mcp2]);

        let session = session_manager
            .create_session(Some("Multi-MCP Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Test message".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());
    }

    #[tokio::test]
    async fn test_mcp_tool_listing() {
        // 验证 MCP 工具可以被列出
        let mcp_client = MockMCPClient {
            name: "test-mcp".to_string(),
        };

        let tools = mcp_client.list_tools().await.unwrap();
        assert_eq!(tools.len(), 2);
        assert_eq!(tools[0].name, "test_tool");
        assert_eq!(tools[1].name, "another_tool");
    }

    #[tokio::test]
    async fn test_mcp_tool_invocation() {
        // 测试 MCP 工具调用
        let mcp_client = MockMCPClient {
            name: "test-mcp".to_string(),
        };

        let result = mcp_client
            .call_tool("test_tool", json!({"param": "value"}))
            .await
            .unwrap();

        assert!(result.get("success").unwrap().as_bool().unwrap());
        assert!(result.get("result").is_some());
    }

    #[tokio::test]
    async fn test_flow_without_mcp() {
        let (_db, session_manager, llm_client) = setup().await;

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

        let session = session_manager
            .create_session(Some("No MCP Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Normal message".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());
    }

    #[tokio::test]
    #[cfg(feature = "rag")]
    async fn test_mcp_with_rag_combined() {
        use agent_core::traits::RAGRetriever;

        struct MockRAGRetriever;

        #[async_trait]
        impl RAGRetriever for MockRAGRetriever {
            async fn retrieve(
                &self,
                _query: &str,
                _top_k: usize,
            ) -> Result<Vec<agent_core::traits::Document>> {
                Ok(vec![])
            }

            async fn index_document(
                &self,
                _doc: agent_core::traits::Document,
            ) -> Result<()> {
                Ok(())
            }
        }

        let (_db, session_manager, llm_client) = setup().await;

        let mcp_client: Arc<dyn MCPClient> = Arc::new(MockMCPClient {
            name: "test-mcp".to_string(),
        });
        let rag_retriever: Arc<dyn RAGRetriever> = Arc::new(MockRAGRetriever);

        let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client)
            .with_mcp(vec![mcp_client])
            .with_rag(rag_retriever);

        let session = session_manager
            .create_session(Some("Combined Test".to_string()))
            .await
            .unwrap();

        let response = flow
            .send_message(session.id, "Test with both MCP and RAG".to_string())
            .await
            .unwrap();

        assert!(!response.is_empty());
    }
}

#[cfg(not(feature = "mcp"))]
mod placeholder {
    #[test]
    fn mcp_feature_not_enabled() {
        println!("MCP feature is not enabled. Enable with: cargo test --features mcp");
    }
}
