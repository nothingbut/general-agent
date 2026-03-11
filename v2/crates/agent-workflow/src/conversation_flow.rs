//! 对话流程管理
//!
//! 负责管理与 LLM 的对话交互

use agent_core::{
    error::Result,
    models::{Message, MessageRole},
    traits::llm::{CompletionRequest, CompletionStream, LLMClient},
};
use agent_skills::{SkillExecutor, SkillRegistry};
use std::sync::Arc;
use tracing::{debug, info};
use uuid::Uuid;

use crate::SessionManager;

/// 对话流程配置
#[derive(Debug, Clone)]
pub struct ConversationConfig {
    /// LLM 模型名称
    pub model: String,
    /// 最大上下文消息数（0 表示无限制）
    pub max_context_messages: usize,
    /// 温度参数
    pub temperature: Option<f32>,
    /// 最大生成 token 数
    pub max_tokens: Option<u32>,
    /// 系统提示词
    pub system_prompt: Option<String>,
}

impl Default for ConversationConfig {
    fn default() -> Self {
        Self {
            model: "claude-3-5-sonnet-20241022".to_string(),
            max_context_messages: 20,
            temperature: None,
            max_tokens: Some(4096),
            system_prompt: None,
        }
    }
}

impl ConversationConfig {
    /// 创建新的配置
    pub fn new(model: String) -> Self {
        Self {
            model,
            ..Default::default()
        }
    }

    /// 设置最大上下文消息数
    pub fn with_max_context_messages(mut self, max: usize) -> Self {
        self.max_context_messages = max;
        self
    }

    /// 设置温度参数
    pub fn with_temperature(mut self, temperature: f32) -> Self {
        self.temperature = Some(temperature);
        self
    }

    /// 设置最大 token 数
    pub fn with_max_tokens(mut self, max_tokens: u32) -> Self {
        self.max_tokens = Some(max_tokens);
        self
    }

    /// 设置系统提示词
    pub fn with_system_prompt(mut self, prompt: String) -> Self {
        self.system_prompt = Some(prompt);
        self
    }
}

/// 对话流程管理器
///
/// 集成 SessionManager 和 LLMClient，
/// 提供完整的对话交互功能
pub struct ConversationFlow {
    session_manager: Arc<SessionManager>,
    llm_client: Arc<dyn LLMClient>,
    config: ConversationConfig,
    // 技能系统组件（可选）
    skill_registry: Option<Arc<SkillRegistry>>,
    skill_executor: SkillExecutor,

    // MCP 客户端（可选，feature gated）
    #[cfg(feature = "mcp")]
    mcp_clients: Option<Arc<Vec<Arc<dyn agent_core::traits::MCPClient>>>>,

    // RAG 检索器（可选，feature gated）
    #[cfg(feature = "rag")]
    rag_retriever: Option<Arc<dyn agent_core::traits::RAGRetriever>>,
}

impl ConversationFlow {
    /// 创建新的对话流程管理器
    ///
    /// # Arguments
    ///
    /// * `session_manager` - 会话管理器
    /// * `llm_client` - LLM 客户端
    /// * `config` - 对话配置
    pub fn new(
        session_manager: Arc<SessionManager>,
        llm_client: Arc<dyn LLMClient>,
        config: ConversationConfig,
    ) -> Self {
        Self {
            session_manager,
            llm_client,
            config,
            skill_registry: None,
            skill_executor: SkillExecutor::new(),
            #[cfg(feature = "mcp")]
            mcp_clients: None,
            #[cfg(feature = "rag")]
            rag_retriever: None,
        }
    }

    /// 使用默认配置创建
    pub fn with_defaults(
        session_manager: Arc<SessionManager>,
        llm_client: Arc<dyn LLMClient>,
    ) -> Self {
        Self::new(session_manager, llm_client, ConversationConfig::default())
    }

    /// 启用技能系统
    ///
    /// # Arguments
    ///
    /// * `registry` - 技能注册表
    pub fn with_skills(mut self, registry: Arc<SkillRegistry>) -> Self {
        self.skill_registry = Some(registry);
        self
    }

    /// 启用 MCP 工具调用
    ///
    /// # Arguments
    ///
    /// * `clients` - MCP 客户端列表
    #[cfg(feature = "mcp")]
    pub fn with_mcp(mut self, clients: Vec<Arc<dyn agent_core::traits::MCPClient>>) -> Self {
        self.mcp_clients = Some(Arc::new(clients));
        self
    }

    /// 启用 RAG 上下文增强
    ///
    /// # Arguments
    ///
    /// * `retriever` - RAG 检索器
    #[cfg(feature = "rag")]
    pub fn with_rag(mut self, retriever: Arc<dyn agent_core::traits::RAGRetriever>) -> Self {
        self.rag_retriever = Some(retriever);
        self
    }

    /// 检测是否是技能调用
    fn is_skill_invocation(&self, content: &str) -> bool {
        let trimmed = content.trim_start();
        trimmed.starts_with('@') || trimmed.starts_with('/')
    }

    /// 处理技能调用
    async fn handle_skill_invocation(&self, content: &str) -> Result<String> {
        let registry = self
            .skill_registry
            .as_ref()
            .ok_or_else(|| agent_core::error::Error::Config("Skills not enabled".into()))?;

        // 解析调用
        let (skill_name, params) = self
            .skill_executor
            .parse_invocation(content)
            .map_err(|e| agent_core::error::Error::InvalidInput(format!("Failed to parse skill: {}", e)))?;

        // 获取技能定义
        let skill = registry
            .get(&skill_name)
            .map_err(|e| agent_core::error::Error::SkillNotFound(format!("{}", e)))?;

        // 执行技能
        let prompt = self
            .skill_executor
            .execute(skill, params)
            .map_err(|e| agent_core::error::Error::InvalidInput(format!("Failed to execute skill: {}", e)))?;

        Ok(prompt)
    }

    /// 发送消息并获取响应
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `content` - 用户消息内容
    ///
    /// # Returns
    ///
    /// LLM 的响应内容
    pub async fn send_message(&self, session_id: Uuid, content: String) -> Result<String> {
        info!("Sending message to session: {}", session_id);

        // 检测并处理技能调用
        let processed_content = if self.is_skill_invocation(&content) {
            info!("Detected skill invocation: {}", content);
            self.handle_skill_invocation(&content).await?
        } else {
            content
        };

        // 1. 创建用户消息（使用处理后的内容）
        let user_message = Message::new(session_id, MessageRole::User, processed_content);
        self.session_manager
            .add_message(session_id, user_message)
            .await?;

        // 2. 构建上下文
        let context = self.build_context(session_id).await?;

        debug!("Built context with {} messages", context.len());

        // 3. 调用 LLM
        let request = self.build_request(context)?;
        let response = self.llm_client.complete(request).await?;

        info!(
            "Received LLM response: {} tokens",
            response.usage.total_tokens
        );

        // 4. 保存助手响应
        let assistant_message = Message::new(session_id, MessageRole::Assistant, response.content.clone());
        self.session_manager
            .add_message(session_id, assistant_message)
            .await?;

        Ok(response.content)
    }

    /// 发送消息并获取流式响应
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `content` - 用户消息内容
    ///
    /// # Returns
    ///
    /// 流式响应对象和用于保存完整响应的闭包
    pub async fn send_message_stream(
        &self,
        session_id: Uuid,
        content: String,
    ) -> Result<(Box<dyn CompletionStream>, StreamContext)> {
        info!("Sending streaming message to session: {}", session_id);

        // 检测并处理技能调用
        let processed_content = if self.is_skill_invocation(&content) {
            info!("Detected skill invocation: {}", content);
            self.handle_skill_invocation(&content).await?
        } else {
            content
        };

        // 1. 创建用户消息（使用处理后的内容）
        let user_message = Message::new(session_id, MessageRole::User, processed_content);
        self.session_manager
            .add_message(session_id, user_message)
            .await?;

        // 2. 构建上下文
        let context = self.build_context(session_id).await?;

        debug!("Built context with {} messages", context.len());

        // 3. 调用 LLM 获取流
        let request = self.build_request(context)?;
        let stream = self.llm_client.stream(request).await?;

        // 4. 返回流和保存上下文
        let save_context = StreamContext {
            session_id,
            session_manager: self.session_manager.clone(),
        };

        Ok((stream, save_context))
    }

    /// 构建 LLM 请求
    fn build_request(&self, messages: Vec<Message>) -> Result<CompletionRequest> {
        let mut request = CompletionRequest::new(messages, self.config.model.clone());

        if let Some(temp) = self.config.temperature {
            request = request.with_temperature(temp);
        }

        if let Some(max_tokens) = self.config.max_tokens {
            request = request.with_max_tokens(max_tokens);
        }

        if let Some(ref system_prompt) = self.config.system_prompt {
            request = request.with_system_prompt(system_prompt.clone());
        }

        Ok(request)
    }

    /// 构建上下文消息列表
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    ///
    /// # Returns
    ///
    /// 上下文消息列表（限制在配置的最大数量内）
    pub async fn build_context(&self, session_id: Uuid) -> Result<Vec<Message>> {
        debug!("Building context for session: {}", session_id);

        let messages = if self.config.max_context_messages > 0 {
            self.session_manager
                .get_recent_messages(session_id, self.config.max_context_messages as u32)
                .await?
        } else {
            self.session_manager
                .get_messages(session_id, None)
                .await?
        };

        debug!("Context contains {} messages", messages.len());

        Ok(messages)
    }

    /// 获取配置
    pub fn config(&self) -> &ConversationConfig {
        &self.config
    }

    /// 更新配置
    pub fn set_config(&mut self, config: ConversationConfig) {
        self.config = config;
    }
}

/// 流式响应上下文
///
/// 用于在流式响应完成后保存完整的响应内容
pub struct StreamContext {
    session_id: Uuid,
    session_manager: Arc<SessionManager>,
}

impl StreamContext {
    /// 保存流式响应的完整内容
    ///
    /// # Arguments
    ///
    /// * `content` - 完整的响应内容
    pub async fn save_response(&self, content: String) -> Result<()> {
        info!("Saving stream response for session: {}", self.session_id);

        let assistant_message = Message::new(self.session_id, MessageRole::Assistant, content);
        self.session_manager
            .add_message(self.session_id, assistant_message)
            .await?;

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_llm::AnthropicClient;
    use agent_storage::{repository::*, Database};

    async fn setup() -> (Database, ConversationFlow) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        // 使用测试 API key（实际测试时需要真实的 key）
        let llm_client: Arc<dyn LLMClient> = Arc::new(
            AnthropicClient::from_api_key("test-key".to_string()).unwrap(),
        );

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config);

        (db, flow)
    }

    #[tokio::test]
    async fn test_create_conversation_flow() {
        let (_db, flow) = setup().await;

        assert_eq!(flow.config().model, "claude-3-5-sonnet-20241022");
        assert_eq!(flow.config().max_context_messages, 20);
    }

    #[tokio::test]
    async fn test_config_builder() {
        let config = ConversationConfig::new("gpt-4".to_string())
            .with_max_context_messages(10)
            .with_temperature(0.7)
            .with_max_tokens(2000)
            .with_system_prompt("You are helpful".to_string());

        assert_eq!(config.model, "gpt-4");
        assert_eq!(config.max_context_messages, 10);
        assert_eq!(config.temperature, Some(0.7));
        assert_eq!(config.max_tokens, Some(2000));
        assert_eq!(config.system_prompt, Some("You are helpful".to_string()));
    }

    #[tokio::test]
    async fn test_build_context_empty() {
        let (_db, flow) = setup().await;

        let session = flow
            .session_manager
            .create_session(Some("Test".to_string()))
            .await
            .unwrap();

        let context = flow.build_context(session.id).await.unwrap();

        assert_eq!(context.len(), 0);
    }

    #[tokio::test]
    async fn test_build_context_with_messages() {
        let (_db, flow) = setup().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        // 添加消息
        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            flow.session_manager
                .add_message(session.id, msg)
                .await
                .unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;
        }

        let context = flow.build_context(session.id).await.unwrap();

        assert_eq!(context.len(), 5);
    }

    #[tokio::test]
    async fn test_build_context_with_limit() {
        let (_db, flow) = setup().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        // 添加 25 条消息
        for i in 1..=25 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            flow.session_manager
                .add_message(session.id, msg)
                .await
                .unwrap();
        }

        let context = flow.build_context(session.id).await.unwrap();

        // 默认配置限制为 20 条
        assert_eq!(context.len(), 20);
    }

    #[tokio::test]
    async fn test_build_request() {
        let (_db, flow) = setup().await;

        let session = flow.session_manager.create_session(None).await.unwrap();

        let msg = Message::new(session.id, MessageRole::User, "Test".to_string());
        let messages = vec![msg];

        let request = flow.build_request(messages).unwrap();

        assert_eq!(request.model, "claude-3-5-sonnet-20241022");
        assert_eq!(request.max_tokens, Some(4096));
    }

    #[tokio::test]
    async fn test_stream_context_save() {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        let session = session_manager.create_session(None).await.unwrap();

        let context = StreamContext {
            session_id: session.id,
            session_manager: session_manager.clone(),
        };

        context
            .save_response("Test response".to_string())
            .await
            .unwrap();

        let messages = session_manager
            .get_messages(session.id, None)
            .await
            .unwrap();

        assert_eq!(messages.len(), 1);
        assert_eq!(messages[0].content, "Test response");
        assert_eq!(messages[0].role, MessageRole::Assistant);
    }
}
