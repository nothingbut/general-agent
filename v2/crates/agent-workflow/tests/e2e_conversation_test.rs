//! 端到端对话流程集成测试

use agent_core::{
    error::Result,
    models::MessageRole,
    traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo,
        StreamChunk, TokenUsage,
    },
};
use agent_storage::{repository::*, Database};
use agent_workflow::{ConversationConfig, ConversationFlow, SessionManager};
use async_trait::async_trait;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;

/// 可配置的 Mock LLM 客户端
struct ConfigurableMockLLM {
    responses: Vec<String>,
    current: AtomicUsize,
}

impl ConfigurableMockLLM {
    fn new(responses: Vec<String>) -> Self {
        Self {
            responses,
            current: AtomicUsize::new(0),
        }
    }

    fn next_response(&self) -> String {
        let idx = self.current.fetch_add(1, Ordering::SeqCst);
        self.responses
            .get(idx % self.responses.len())
            .cloned()
            .unwrap_or_else(|| "Default response".to_string())
    }
}

#[async_trait]
impl LLMClient for ConfigurableMockLLM {
    async fn complete(&self, _request: CompletionRequest) -> Result<CompletionResponse> {
        Ok(CompletionResponse {
            content: self.next_response(),
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
        // 简单的 mock stream
        struct MockStream {
            chunks: Vec<String>,
            current: usize,
        }

        #[async_trait]
        impl CompletionStream for MockStream {
            async fn next(&mut self) -> Result<Option<StreamChunk>> {
                if self.current >= self.chunks.len() {
                    return Ok(None);
                }

                let chunk = StreamChunk {
                    delta: self.chunks[self.current].clone(),
                    is_final: self.current == self.chunks.len() - 1,
                    finish_reason: if self.current == self.chunks.len() - 1 {
                        Some("stop".to_string())
                    } else {
                        None
                    },
                };

                self.current += 1;
                Ok(Some(chunk))
            }
        }

        let response = self.next_response();
        let chunks: Vec<String> = response.chars().map(|c| c.to_string()).collect();

        Ok(Box::new(MockStream { chunks, current: 0 }))
    }

    async fn list_models(&self) -> Result<Vec<ModelInfo>> {
        Ok(vec![])
    }

    fn provider_name(&self) -> &str {
        "mock"
    }
}

/// 测试辅助函数：创建测试环境
async fn setup() -> (Database, Arc<SessionManager>, Arc<dyn LLMClient>) {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(ConfigurableMockLLM::new(vec![
        "Hello! How can I help you?".to_string(),
        "That's a great question!".to_string(),
        "I understand your concern.".to_string(),
    ]));

    (db, session_manager, llm_client)
}

#[tokio::test]
async fn test_full_conversation_flow() {
    let (_db, session_manager, llm_client) = setup().await;

    // 创建会话
    let session = session_manager
        .create_session(Some("E2E Test".to_string()))
        .await
        .unwrap();

    // 创建对话流程
    let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 发送消息并获取响应
    let response = flow
        .send_message(session.id, "Hello".to_string())
        .await
        .unwrap();

    // 验证响应
    assert!(!response.is_empty());
    assert!(response.contains("help"));

    // 验证消息已保存
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages.len(), 2); // 用户 + 助手
    assert_eq!(messages[0].role, MessageRole::User);
    assert_eq!(messages[0].content, "Hello");
    assert_eq!(messages[1].role, MessageRole::Assistant);
}

#[tokio::test]
async fn test_multi_turn_conversation() {
    let (_db, session_manager, llm_client) = setup().await;

    let session = session_manager.create_session(None).await.unwrap();
    let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 第一轮对话
    let response1 = flow
        .send_message(session.id, "What is Rust?".to_string())
        .await
        .unwrap();
    assert!(!response1.is_empty());

    // 第二轮对话
    let response2 = flow
        .send_message(session.id, "Tell me more".to_string())
        .await
        .unwrap();
    assert!(!response2.is_empty());

    // 第三轮对话
    let response3 = flow
        .send_message(session.id, "Thanks!".to_string())
        .await
        .unwrap();
    assert!(!response3.is_empty());

    // 验证消息历史
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages.len(), 6); // 3 轮 * 2 消息

    // 验证消息顺序
    for i in (0..6).step_by(2) {
        assert_eq!(messages[i].role, MessageRole::User);
        assert_eq!(messages[i + 1].role, MessageRole::Assistant);
    }
}

#[tokio::test]
async fn test_context_management() {
    let (_db, session_manager, llm_client) = setup().await;

    let session = session_manager.create_session(None).await.unwrap();

    // 创建带上下文限制的配置
    let config = ConversationConfig::default().with_max_context_messages(4);
    let flow = ConversationFlow::new(session_manager.clone(), llm_client, config);

    // 发送 5 轮对话（10 条消息）
    for i in 1..=5 {
        flow.send_message(session.id, format!("Message {}", i))
            .await
            .unwrap();
    }

    // 获取上下文（应该只有最近 4 条消息）
    let context = flow.build_context(session.id).await.unwrap();
    assert_eq!(context.len(), 4);

    // 验证是最近的消息
    assert!(context[0].content.contains("Message 3") || context[0].content.contains("Message 4"));
}

#[tokio::test]
async fn test_conversation_persistence() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> =
        Arc::new(ConfigurableMockLLM::new(vec!["Response".to_string()]));

    // 第一个对话流程
    let flow1 = ConversationFlow::with_defaults(session_manager.clone(), llm_client.clone());
    let session = session_manager
        .create_session(Some("Persistent Session".to_string()))
        .await
        .unwrap();

    flow1
        .send_message(session.id, "First message".to_string())
        .await
        .unwrap();

    // 模拟重启：创建新的对话流程
    let flow2 = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 继续对话
    flow2
        .send_message(session.id, "Second message".to_string())
        .await
        .unwrap();

    // 验证历史完整
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages.len(), 4); // 2 轮对话
    assert_eq!(messages[0].content, "First message");
    assert_eq!(messages[2].content, "Second message");
}

#[tokio::test]
async fn test_stream_conversation() {
    let (_db, session_manager, llm_client) = setup().await;

    let session = session_manager.create_session(None).await.unwrap();
    let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 发送流式消息
    let (mut stream, context) = flow
        .send_message_stream(session.id, "Hello streaming".to_string())
        .await
        .unwrap();

    // 收集所有块
    let mut full_response = String::new();
    while let Some(chunk) = stream.next().await.unwrap() {
        full_response.push_str(&chunk.delta);
        if chunk.is_final {
            break;
        }
    }

    // 保存响应
    context.save_response(full_response.clone()).await.unwrap();

    // 验证消息已保存
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages.len(), 2);
    assert_eq!(messages[0].content, "Hello streaming");
    assert_eq!(messages[1].content, full_response);
}

#[tokio::test]
async fn test_conversation_error_recovery() {
    // 创建会失败的 LLM 客户端
    struct FailingLLM {
        fail_count: AtomicUsize,
    }

    #[async_trait]
    impl LLMClient for FailingLLM {
        async fn complete(&self, _request: CompletionRequest) -> Result<CompletionResponse> {
            let count = self.fail_count.fetch_add(1, Ordering::SeqCst);
            if count < 2 {
                Err(agent_core::error::Error::LLM("Temporary failure".into()))
            } else {
                Ok(CompletionResponse {
                    content: "Success!".to_string(),
                    model: "mock".to_string(),
                    usage: TokenUsage {
                        prompt_tokens: 10,
                        completion_tokens: 10,
                        total_tokens: 20,
                    },
                    finish_reason: Some("stop".to_string()),
                })
            }
        }

        async fn stream(&self, _request: CompletionRequest) -> Result<Box<dyn CompletionStream>> {
            unimplemented!()
        }

        async fn list_models(&self) -> Result<Vec<ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "failing-mock"
        }
    }

    let (_db, session_manager, _) = setup().await;
    let failing_llm: Arc<dyn LLMClient> = Arc::new(FailingLLM {
        fail_count: AtomicUsize::new(0),
    });

    let session = session_manager.create_session(None).await.unwrap();
    let flow = ConversationFlow::with_defaults(session_manager.clone(), failing_llm);

    // 前两次调用应该失败
    let result1 = flow.send_message(session.id, "Test 1".to_string()).await;
    assert!(result1.is_err());

    let result2 = flow.send_message(session.id, "Test 2".to_string()).await;
    assert!(result2.is_err());

    // 第三次应该成功
    let result3 = flow.send_message(session.id, "Test 3".to_string()).await;
    assert!(result3.is_ok());

    // 验证消息历史：前两次失败但用户消息被保存，第三次成功
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    // 应该有 4 条消息：
    // - Test 1 (user) - LLM 失败，无 assistant 消息
    // - Test 2 (user) - LLM 失败，无 assistant 消息
    // - Test 3 (user) + Success! (assistant) - LLM 成功
    assert_eq!(messages.len(), 4);
    assert_eq!(messages[0].content, "Test 1");
    assert_eq!(messages[0].role, MessageRole::User);
    assert_eq!(messages[1].content, "Test 2");
    assert_eq!(messages[1].role, MessageRole::User);
    assert_eq!(messages[2].content, "Test 3");
    assert_eq!(messages[2].role, MessageRole::User);
    assert_eq!(messages[3].content, "Success!");
    assert_eq!(messages[3].role, MessageRole::Assistant);
}

#[tokio::test]
async fn test_empty_message_handling() {
    let (_db, session_manager, llm_client) = setup().await;

    let session = session_manager.create_session(None).await.unwrap();
    let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 发送空消息（应该被处理）
    let result = flow.send_message(session.id, "".to_string()).await;

    // 验证空消息也能正常处理
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_large_message_handling() {
    let (_db, session_manager, llm_client) = setup().await;

    let session = session_manager.create_session(None).await.unwrap();
    let flow = ConversationFlow::with_defaults(session_manager.clone(), llm_client);

    // 创建大消息（10KB）
    let large_message = "x".repeat(10_000);

    let result = flow
        .send_message(session.id, large_message.clone())
        .await;

    assert!(result.is_ok());

    // 验证消息完整保存
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages[0].content.len(), 10_000);
}
