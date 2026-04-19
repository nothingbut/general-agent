use agent_api::{AppState, build_router};
use agent_core::traits::llm::{
    CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, StreamChunk,
    TokenUsage,
};
use agent_storage::{repository::*, Database};
use agent_workflow::{AgentRuntime, ConversationConfig, ConversationFlow, SessionManager};
use async_trait::async_trait;
use axum_test::TestServer;
use serde_json::{json, Value};
use std::sync::Arc;

struct MockLLMClient;

#[async_trait]
impl LLMClient for MockLLMClient {
    async fn complete(
        &self,
        _request: CompletionRequest,
    ) -> agent_core::Result<CompletionResponse> {
        Ok(CompletionResponse {
            content: "这是模拟回复".to_string(),
            model: "mock".to_string(),
            usage: TokenUsage {
                prompt_tokens: 10,
                completion_tokens: 20,
                total_tokens: 30,
            },
            finish_reason: Some("stop".to_string()),
        })
    }

    async fn stream(
        &self,
        _request: CompletionRequest,
    ) -> agent_core::Result<Box<dyn CompletionStream>> {
        Ok(Box::new(MockStream { done: false }))
    }

    async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
        Ok(vec![])
    }

    fn provider_name(&self) -> &str {
        "mock"
    }
}

struct MockStream {
    done: bool,
}

#[async_trait]
impl CompletionStream for MockStream {
    async fn next(&mut self) -> agent_core::Result<Option<StreamChunk>> {
        if self.done {
            return Ok(None);
        }
        self.done = true;
        Ok(Some(StreamChunk {
            delta: "模拟流式回复".to_string(),
            is_final: true,
            finish_reason: Some("stop".to_string()),
        }))
    }
}

use std::sync::atomic::{AtomicU32, Ordering};

static TEST_DB_COUNTER: AtomicU32 = AtomicU32::new(0);

async fn setup_test_server() -> TestServer {
    let n = TEST_DB_COUNTER.fetch_add(1, Ordering::SeqCst);
    let tmp = tempfile::tempdir().unwrap();
    let db_path = tmp.path().join(format!("test_{}.db", n));
    let db = Database::from_path(&db_path).await.unwrap();
    db.migrate().await.unwrap();

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient);
    let runtime = Arc::new(AgentRuntime::new(db, llm_client.clone(), None).await.unwrap());

    let config = ConversationConfig::default();
    let flow = Arc::new(ConversationFlow::new(
        runtime.session_manager().clone(),
        llm_client,
        config,
    ));

    let state = AppState::new(runtime, flow);
    let router = build_router(state);

    // Keep tempdir alive by leaking it (tests are short-lived)
    std::mem::forget(tmp);

    TestServer::new(router).unwrap()
}

#[tokio::test]
async fn test_health_check() {
    let server = setup_test_server().await;
    let response = server.get("/health").await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert_eq!(body["status"], "ok");
}

#[tokio::test]
async fn test_create_and_list_sessions() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "测试会话"}))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["success"].as_bool().unwrap());
    assert_eq!(body["data"]["title"], "测试会话");

    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server.get("/api/v1/sessions").await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["success"].as_bool().unwrap());
    assert!(body["data"].as_array().unwrap().len() >= 1);
    assert_eq!(body["total"], 1);

    let response = server
        .get(&format!("/api/v1/sessions/{}", session_id))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert_eq!(body["data"]["id"], session_id);
}

#[tokio::test]
async fn test_update_session() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "原始标题"}))
        .await;
    let body: Value = response.json();
    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server
        .put(&format!("/api/v1/sessions/{}", session_id))
        .json(&json!({"title": "新标题"}))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert_eq!(body["data"]["title"], "新标题");
}

#[tokio::test]
async fn test_delete_session() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "待删除"}))
        .await;
    let body: Value = response.json();
    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server
        .delete(&format!("/api/v1/sessions/{}", session_id))
        .await;
    response.assert_status_ok();

    let response = server
        .get(&format!("/api/v1/sessions/{}", session_id))
        .await;
    response.assert_status(axum::http::StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn test_session_stats() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "统计测试"}))
        .await;
    let body: Value = response.json();
    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server
        .get(&format!("/api/v1/sessions/{}/stats", session_id))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert_eq!(body["data"]["message_count"], 0);
}

#[tokio::test]
async fn test_send_message_and_list() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "对话测试"}))
        .await;
    let body: Value = response.json();
    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server
        .post(&format!("/api/v1/chat/{}", session_id))
        .json(&json!({"content": "你好", "stream": false}))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["success"].as_bool().unwrap());
    assert_eq!(body["data"]["role"], "assistant");

    let response = server
        .get(&format!("/api/v1/chat/{}/messages", session_id))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["data"].as_array().unwrap().len() >= 2);
}

#[tokio::test]
async fn test_send_empty_message_rejected() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({"title": "空消息测试"}))
        .await;
    let body: Value = response.json();
    let session_id = body["data"]["id"].as_str().unwrap().to_string();

    let response = server
        .post(&format!("/api/v1/chat/{}", session_id))
        .json(&json!({"content": "  ", "stream": false}))
        .await;
    response.assert_status(axum::http::StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_skills_not_enabled() {
    let server = setup_test_server().await;

    let response = server.get("/api/v1/skills").await;
    response.assert_status(axum::http::StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_memory_not_enabled() {
    let server = setup_test_server().await;

    let response = server.get("/api/v1/memories/stats").await;
    response.assert_status(axum::http::StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_files_not_enabled() {
    let server = setup_test_server().await;

    let response = server.get("/api/v1/files").await;
    response.assert_status(axum::http::StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_session_pagination() {
    let server = setup_test_server().await;

    for i in 0..5 {
        server
            .post("/api/v1/sessions")
            .json(&json!({"title": format!("会话 {}", i)}))
            .await;
    }

    let response = server
        .get("/api/v1/sessions")
        .add_query_param("limit", "2")
        .add_query_param("offset", "0")
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert_eq!(body["data"].as_array().unwrap().len(), 2);
    assert_eq!(body["total"], 5);
}

#[tokio::test]
async fn test_create_session_without_title() {
    let server = setup_test_server().await;

    let response = server
        .post("/api/v1/sessions")
        .json(&json!({}))
        .await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["success"].as_bool().unwrap());
}

#[tokio::test]
async fn test_get_nonexistent_session() {
    let server = setup_test_server().await;

    let response = server
        .get("/api/v1/sessions/00000000-0000-0000-0000-000000000000")
        .await;
    response.assert_status(axum::http::StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn test_openapi_endpoint() {
    let server = setup_test_server().await;

    let response = server.get("/api-docs/openapi.json").await;
    response.assert_status_ok();
    let body: Value = response.json();
    assert!(body["openapi"].is_string());
    assert!(body["paths"].is_object());
    assert!(body["info"]["title"].as_str().unwrap().contains("Agent"));
}

#[tokio::test]
async fn test_swagger_ui() {
    let server = setup_test_server().await;

    let response = server.get("/swagger-ui/").await;
    response.assert_status_ok();
}
