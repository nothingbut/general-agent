//! 技能系统集成测试

use agent_core::{
    error::Result,
    models::MessageRole,
    traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    },
};
use agent_skills::{SkillLoader, SkillRegistry};
use agent_storage::{repository::*, Database};
use agent_workflow::{ConversationFlow, SessionManager};
use async_trait::async_trait;
use std::path::PathBuf;
use std::sync::Arc;

/// Mock LLM 客户端用于测试
struct MockLLMClient {
    response: String,
}

impl MockLLMClient {
    fn new(response: String) -> Self {
        Self { response }
    }
}

#[async_trait]
impl LLMClient for MockLLMClient {
    async fn complete(&self, request: CompletionRequest) -> Result<CompletionResponse> {
        // 返回包含输入消息内容的响应（用于验证技能执行）
        let content = if let Some(last_msg) = request.messages.last() {
            format!("Echo: {}", last_msg.content)
        } else {
            self.response.clone()
        };

        Ok(CompletionResponse {
            content,
            model: request.model,
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
    ) -> Result<Box<dyn CompletionStream>> {
        unimplemented!("Stream not needed for this test")
    }

    async fn list_models(&self) -> Result<Vec<ModelInfo>> {
        Ok(vec![])
    }

    fn provider_name(&self) -> &str {
        "mock"
    }
}

#[tokio::test]
async fn test_skill_invocation_in_conversation() {
    // 1. 设置测试环境
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient::new("Test response".to_string()));

    // 2. 加载测试技能
    let skills_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../agent-skills/examples/test_skills");
    let loader = SkillLoader::new(skills_dir).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    // 3. 创建启用技能的 ConversationFlow
    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    )
    .with_skills(Arc::new(registry));

    // 4. 创建会话
    let session = session_manager
        .create_session(Some("测试会话".to_string()))
        .await
        .unwrap();

    // 5. 发送技能调用
    let response = conversation_flow
        .send_message(session.id, "@greeting user_name='Alice'".to_string())
        .await
        .unwrap();

    // 6. 验证响应包含技能生成的内容
    assert!(response.contains("Hello Alice"), "Response should contain skill output: {}", response);

    // 7. 验证消息历史
    let messages = session_manager
        .get_messages(session.id, None)
        .await
        .unwrap();

    assert_eq!(messages.len(), 2); // 用户消息 + 助手响应
    assert_eq!(messages[0].role, MessageRole::User);
    assert!(messages[0].content.contains("Hello Alice")); // 用户消息应该是处理后的 prompt
    assert_eq!(messages[1].role, MessageRole::Assistant);
}

#[tokio::test]
async fn test_skill_not_enabled() {
    // 1. 设置测试环境（不启用技能）
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient::new("Test response".to_string()));

    // 2. 创建未启用技能的 ConversationFlow
    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    );
    // 注意：没有调用 .with_skills()

    // 3. 创建会话
    let session = session_manager
        .create_session(Some("测试会话".to_string()))
        .await
        .unwrap();

    // 4. 发送技能调用应该失败
    let result = conversation_flow
        .send_message(session.id, "@greeting user_name='Alice'".to_string())
        .await;

    assert!(result.is_err());
    let err = result.unwrap_err();
    assert!(format!("{}", err).contains("Skills not enabled"));
}

#[tokio::test]
async fn test_skill_with_namespace() {
    // 1. 设置测试环境
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient::new("Test response".to_string()));

    // 2. 加载测试技能
    let skills_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../agent-skills/examples/test_skills");
    let loader = SkillLoader::new(skills_dir).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    // 3. 创建启用技能的 ConversationFlow
    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    )
    .with_skills(Arc::new(registry));

    // 4. 创建会话
    let session = session_manager
        .create_session(Some("测试会话".to_string()))
        .await
        .unwrap();

    // 5. 通过完整名称调用（命名空间为空，所以就是短名称）
    let response = conversation_flow
        .send_message(session.id, "@greeting user_name='Bob'".to_string())
        .await
        .unwrap();

    assert!(response.contains("Hello Bob"), "Response should contain skill output: {}", response);
}

#[tokio::test]
async fn test_slash_syntax() {
    // 测试 /skill 语法
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient::new("Test response".to_string()));

    let skills_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../agent-skills/examples/test_skills");
    let loader = SkillLoader::new(skills_dir).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    )
    .with_skills(Arc::new(registry));

    let session = session_manager
        .create_session(Some("测试会话".to_string()))
        .await
        .unwrap();

    // 使用 / 语法
    let response = conversation_flow
        .send_message(session.id, "/greeting user_name='Charlie'".to_string())
        .await
        .unwrap();

    assert!(response.contains("Hello Charlie"), "Response should contain skill output: {}", response);
}

#[tokio::test]
async fn test_normal_message_without_skill() {
    // 测试普通消息（不触发技能）
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient::new("Normal response".to_string()));

    let skills_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../agent-skills/examples/test_skills");
    let loader = SkillLoader::new(skills_dir).unwrap();
    let skills = loader.load_all().unwrap();

    let mut registry = SkillRegistry::new();
    for skill in skills {
        registry.register(skill);
    }

    let conversation_flow = ConversationFlow::with_defaults(
        session_manager.clone(),
        llm_client,
    )
    .with_skills(Arc::new(registry));

    let session = session_manager
        .create_session(Some("测试会话".to_string()))
        .await
        .unwrap();

    // 发送普通消息
    let response = conversation_flow
        .send_message(session.id, "Hello, how are you?".to_string())
        .await
        .unwrap();

    // 应该收到 echo 响应
    assert!(response.contains("Echo:"));
    assert!(response.contains("Hello, how are you?"));
}
