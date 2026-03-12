use agent_workflow::runtime::AgentRuntime;
use agent_storage::Database;
use agent_llm::OllamaClient;
use std::sync::Arc;

#[tokio::test]
async fn test_runtime_creation() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let llm_client = Arc::new(
        OllamaClient::new(
            agent_llm::ollama::OllamaConfig::new("test".to_string())
        ).unwrap()
    );

    let runtime = AgentRuntime::new(db, llm_client, None).await.unwrap();

    // 验证可以访问各个组件
    assert!(runtime.session_manager().list_sessions(10, 0).await.is_ok());
    assert_eq!(runtime.orchestrator().active_count(), 0);
}
