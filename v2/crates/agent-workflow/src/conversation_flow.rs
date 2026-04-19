//! 对话流程管理
//!
//! 负责管理与 LLM 的对话交互

use agent_core::{
    error::Result,
    models::{Message, MessageRole},
    traits::llm::{CompletionRequest, CompletionStream, LLMClient},
};
#[cfg(feature = "compression")]
use agent_context_compression::{CompressionService, StrategyType};
#[cfg(feature = "file-storage")]
use agent_file_storage::{parse_file_references, FileService, FileTarget};
#[cfg(feature = "memory")]
use agent_memory::MemoryService;
use agent_skills::{SkillExecutor, SkillRegistry};
use std::sync::Arc;
#[cfg(any(feature = "compression", feature = "memory"))]
use tokio::sync::Mutex;
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

    // 上下文压缩服务（可选，feature gated）
    #[cfg(feature = "compression")]
    compression_service: Option<Arc<Mutex<CompressionService>>>,

    // 长期记忆服务（可选，feature gated）
    #[cfg(feature = "memory")]
    memory_service: Option<Arc<Mutex<MemoryService>>>,

    // MCP 客户端（可选，feature gated）
    #[cfg(feature = "mcp")]
    mcp_clients: Option<Arc<Vec<Arc<dyn agent_core::traits::MCPClient>>>>,

    // 文件存储服务（可选，feature gated）
    #[cfg(feature = "file-storage")]
    file_service: Option<Arc<FileService>>,

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
            #[cfg(feature = "compression")]
            compression_service: None,
            #[cfg(feature = "memory")]
            memory_service: None,
            #[cfg(feature = "file-storage")]
            file_service: None,
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

    /// 启用上下文压缩
    #[cfg(feature = "compression")]
    pub fn with_compression(mut self, service: CompressionService) -> Self {
        self.compression_service = Some(Arc::new(Mutex::new(service)));
        self
    }

    /// 启用长期记忆
    #[cfg(feature = "memory")]
    pub fn with_memory(mut self, service: MemoryService) -> Self {
        self.memory_service = Some(Arc::new(Mutex::new(service)));
        self
    }

    /// 启用文件存储
    #[cfg(feature = "file-storage")]
    pub fn with_file_storage(mut self, service: FileService) -> Self {
        self.file_service = Some(Arc::new(service));
        self
    }

    /// 获取文件服务引用
    #[cfg(feature = "file-storage")]
    pub fn file_service(&self) -> Option<&Arc<FileService>> {
        self.file_service.as_ref()
    }

    /// 解析消息中的 @file: 引用，读取文件内容并替换
    #[cfg(feature = "file-storage")]
    async fn resolve_file_references(&self, content: &str, owner_id: &str) -> String {
        let file_service = match &self.file_service {
            Some(svc) => svc,
            None => return content.to_string(),
        };

        let refs = parse_file_references(content);
        if refs.is_empty() {
            return content.to_string();
        }

        let mut replacements = Vec::new();
        for file_ref in &refs {
            let result = match &file_ref.target {
                FileTarget::ById(id) => file_service.read_file_as_text(*id, owner_id).await,
                FileTarget::ByName(name) => {
                    match file_service.get_file_by_name(name, owner_id).await {
                        Ok(f) => file_service.read_file_as_text(f.id, owner_id).await,
                        Err(e) => Err(e),
                    }
                }
            };

            let replacement = match result {
                Ok(text) => {
                    let name = match &file_ref.target {
                        FileTarget::ById(id) => id.to_string(),
                        FileTarget::ByName(n) => n.clone(),
                    };
                    info!("Resolved file reference: {}", name);
                    format!("[文件: {}]\n{}\n[/文件]", name, text)
                }
                Err(e) => {
                    tracing::warn!("Failed to resolve file reference {}: {}", file_ref.raw, e);
                    format!("[文件引用错误: {}]", e)
                }
            };

            replacements.push((file_ref.raw.clone(), replacement));
        }

        agent_file_storage::replace_file_references(content, &replacements)
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

        // 解析 @file: 引用
        #[cfg(feature = "file-storage")]
        let processed_content = self.resolve_file_references(&processed_content, "default").await;

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

        // 解析 @file: 引用
        #[cfg(feature = "file-storage")]
        let processed_content = self.resolve_file_references(&processed_content, "default").await;

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

        // 如果启用了记忆，注入相关记忆作为系统上下文
        #[cfg(feature = "memory")]
        let messages = {
            if let Some(ref memory) = self.memory_service {
                let last_user_msg = messages
                    .iter()
                    .rev()
                    .find(|m| m.role == MessageRole::User);

                if let Some(user_msg) = last_user_msg {
                    let service = memory.lock().await;
                    match service.find_relevant(&user_msg.content, 5).await {
                        Ok(memories) if !memories.is_empty() => {
                            let mut memory_text = String::from("以下是与当前对话相关的长期记忆：\n");
                            for m in &memories {
                                memory_text.push_str(&format!(
                                    "- [{}] {}\n",
                                    m.memory_type, m.content
                                ));
                            }
                            info!("Injected {} relevant memories into context", memories.len());

                            let mut enriched = vec![Message::new(
                                session_id,
                                MessageRole::System,
                                memory_text,
                            )];
                            enriched.extend(messages);
                            enriched
                        }
                        Ok(_) => messages,
                        Err(e) => {
                            tracing::warn!("Memory retrieval failed, skipping: {}", e);
                            messages
                        }
                    }
                } else {
                    messages
                }
            } else {
                messages
            }
        };

        // 如果启用了压缩，自动压缩上下文
        #[cfg(feature = "compression")]
        let messages = {
            if let Some(ref compression) = self.compression_service {
                let mut service = compression.lock().await;
                if service.should_compress(&messages) {
                    info!(
                        "Auto-compressing context: {} messages",
                        messages.len()
                    );
                    match service.auto_compress(&messages).await {
                        Ok(result) => {
                            info!(
                                "Compressed {} -> {} messages (ratio: {:.2}%)",
                                result.original_count,
                                result.compressed_count,
                                result.compression_ratio * 100.0
                            );
                            result.compressed_messages
                        }
                        Err(e) => {
                            tracing::warn!("Compression failed, using original messages: {}", e);
                            messages
                        }
                    }
                } else {
                    messages
                }
            } else {
                messages
            }
        };

        Ok(messages)
    }

    /// 手动压缩会话上下文
    #[cfg(feature = "compression")]
    pub async fn compress_session(
        &self,
        session_id: Uuid,
        strategy: Option<StrategyType>,
    ) -> Result<agent_context_compression::CompressionResult> {
        let compression = self
            .compression_service
            .as_ref()
            .ok_or_else(|| agent_core::error::Error::Config("Compression not enabled".into()))?;

        let messages = self
            .session_manager
            .get_messages(session_id, None)
            .await?;

        let mut service = compression.lock().await;
        let result = match strategy {
            Some(s) => service.compress_with_strategy(&messages, s).await,
            None => service.auto_compress(&messages).await,
        };

        result.map_err(|e| agent_core::error::Error::External(format!("Compression failed: {}", e)))
    }

    /// 获取压缩历史
    #[cfg(feature = "compression")]
    pub async fn compression_history(&self) -> Vec<agent_context_compression::CompressionRecord> {
        if let Some(ref compression) = self.compression_service {
            let service = compression.lock().await;
            service.history().to_vec()
        } else {
            Vec::new()
        }
    }

    /// 估算会话 token 数
    #[cfg(feature = "compression")]
    pub async fn estimate_session_tokens(&self, session_id: Uuid) -> Result<usize> {
        let compression = self
            .compression_service
            .as_ref()
            .ok_or_else(|| agent_core::error::Error::Config("Compression not enabled".into()))?;

        let messages = self
            .session_manager
            .get_messages(session_id, None)
            .await?;

        let service = compression.lock().await;
        Ok(service.estimate_tokens(&messages))
    }

    /// 从会话消息中提取记忆
    #[cfg(feature = "memory")]
    pub async fn extract_memories(
        &self,
        session_id: Uuid,
    ) -> Result<Vec<agent_memory::Memory>> {
        let memory = self
            .memory_service
            .as_ref()
            .ok_or_else(|| agent_core::error::Error::Config("Memory not enabled".into()))?;

        let messages = self
            .session_manager
            .get_messages(session_id, None)
            .await?;

        let service = memory.lock().await;
        service
            .extract_from_messages(&messages, Some(session_id))
            .await
            .map_err(|e| agent_core::error::Error::External(format!("Memory extraction failed: {}", e)))
    }

    /// 获取记忆统计
    #[cfg(feature = "memory")]
    pub async fn memory_stats(&self) -> Result<Option<agent_memory::MemoryStats>> {
        if let Some(ref memory) = self.memory_service {
            let service = memory.lock().await;
            let stats = service
                .stats()
                .await
                .map_err(|e| agent_core::error::Error::External(format!("Memory stats failed: {}", e)))?;
            Ok(Some(stats))
        } else {
            Ok(None)
        }
    }

    /// 获取记忆服务引用（用于 CLI 直接操作）
    #[cfg(feature = "memory")]
    pub fn memory_service(&self) -> Option<&Arc<Mutex<MemoryService>>> {
        self.memory_service.as_ref()
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

#[cfg(test)]
#[cfg(feature = "compression")]
mod compression_tests {
    use super::*;
    use agent_context_compression::{CompressionConfig as CConfig, CompressionService};
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    };
    use agent_storage::{repository::*, Database};
    use async_trait::async_trait;

    struct MockLLMClient;

    #[async_trait]
    impl LLMClient for MockLLMClient {
        async fn complete(
            &self,
            _request: CompletionRequest,
        ) -> agent_core::Result<CompletionResponse> {
            Ok(CompletionResponse {
                content: "对话摘要".to_string(),
                model: "mock".to_string(),
                usage: TokenUsage::new(10, 5),
                finish_reason: Some("stop".to_string()),
            })
        }

        async fn stream(
            &self,
            _request: CompletionRequest,
        ) -> agent_core::Result<Box<dyn CompletionStream>> {
            unimplemented!()
        }

        async fn list_models(&self) -> agent_core::Result<Vec<ModelInfo>> {
            Ok(vec![])
        }

        fn provider_name(&self) -> &str {
            "mock"
        }
    }

    async fn setup_with_compression() -> (Database, ConversationFlow) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient);

        let compression_config = CConfig {
            auto_trigger_threshold: 10,
            sliding_window_size: 5,
            ..Default::default()
        };
        let compression = CompressionService::new(llm_client.clone(), compression_config).unwrap();

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config)
            .with_compression(compression);

        (db, flow)
    }

    #[tokio::test]
    async fn test_flow_with_compression_enabled() {
        let (_db, flow) = setup_with_compression().await;
        assert!(flow.compression_service.is_some());
    }

    #[tokio::test]
    async fn test_build_context_no_compression_needed() {
        let (_db, flow) = setup_with_compression().await;

        let session = flow
            .session_manager
            .create_session(Some("Test".to_string()))
            .await
            .unwrap();

        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Msg {}", i));
            flow.session_manager.add_message(session.id, msg).await.unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(5)).await;
        }

        let context = flow.build_context(session.id).await.unwrap();
        // 5 条消息低于阈值 10，不压缩
        assert_eq!(context.len(), 5);
    }

    #[tokio::test]
    async fn test_build_context_with_compression_triggered() {
        let (_db, flow) = setup_with_compression().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        // 添加 15 条消息，超过阈值 10
        for i in 1..=15 {
            let role = if i % 2 == 0 {
                MessageRole::Assistant
            } else {
                MessageRole::User
            };
            let msg = Message::new(session.id, role, format!("Message {}", i));
            flow.session_manager.add_message(session.id, msg).await.unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(5)).await;
        }

        let context = flow.build_context(session.id).await.unwrap();
        // 应该被压缩，消息数少于原始 15 条
        assert!(context.len() < 15);
    }

    #[tokio::test]
    async fn test_compress_session_manual() {
        let (_db, flow) = setup_with_compression().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        for i in 1..=20 {
            let role = if i % 2 == 0 {
                MessageRole::Assistant
            } else {
                MessageRole::User
            };
            let msg = Message::new(session.id, role, format!("Message {}", i));
            flow.session_manager.add_message(session.id, msg).await.unwrap();
        }

        let result = flow
            .compress_session(session.id, Some(StrategyType::SlidingWindow))
            .await
            .unwrap();

        assert_eq!(result.original_count, 20);
        assert_eq!(result.compressed_count, 5); // sliding_window_size = 5
        assert!(result.compression_ratio < 1.0);
    }

    #[tokio::test]
    async fn test_estimate_session_tokens() {
        let (_db, flow) = setup_with_compression().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        let msg = Message::new(session.id, MessageRole::User, "Hello world, this is a test message".to_string());
        flow.session_manager.add_message(session.id, msg).await.unwrap();

        let tokens = flow.estimate_session_tokens(session.id).await.unwrap();
        assert!(tokens > 0);
    }

    #[tokio::test]
    async fn test_compression_history_tracking() {
        let (_db, flow) = setup_with_compression().await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        for i in 1..=20 {
            let msg = Message::new(session.id, MessageRole::User, format!("Msg {}", i));
            flow.session_manager.add_message(session.id, msg).await.unwrap();
        }

        // 初始历史为空
        assert!(flow.compression_history().await.is_empty());

        // 执行压缩
        flow.compress_session(session.id, Some(StrategyType::SlidingWindow))
            .await
            .unwrap();

        // 历史应有 1 条记录
        let history = flow.compression_history().await;
        assert_eq!(history.len(), 1);
        assert_eq!(history[0].strategy_used, "sliding_window");
    }

    #[tokio::test]
    async fn test_flow_without_compression() {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));
        let llm_client: Arc<dyn LLMClient> = Arc::new(MockLLMClient);

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config);

        // 无压缩服务
        assert!(flow.compression_service.is_none());

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        for i in 1..=25 {
            let msg = Message::new(session.id, MessageRole::User, format!("Msg {}", i));
            flow.session_manager.add_message(session.id, msg).await.unwrap();
        }

        // 不会被压缩，只受 max_context_messages 限制
        let context = flow.build_context(session.id).await.unwrap();
        assert_eq!(context.len(), 20); // 默认 max_context_messages = 20
    }
}

#[cfg(test)]
#[cfg(feature = "memory")]
mod memory_tests {
    use super::*;
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    };
    use agent_memory::{
        MemoryExtractor, MemoryService, SqliteMemoryRepository, VectorMemoryStore,
    };
    use agent_storage::{repository::*, Database};
    use async_trait::async_trait;

    struct MockMemoryLLM {
        response: String,
    }

    #[async_trait]
    impl LLMClient for MockMemoryLLM {
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

    async fn setup_with_memory(
        llm_response: &str,
    ) -> (Database, sqlx::SqlitePool, ConversationFlow) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        // 创建 memory 专用的 SQLite 数据库
        let memory_pool = sqlx::SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("../agent-memory/migrations")
            .run(&memory_pool)
            .await
            .unwrap();

        let memory_repo: Arc<dyn agent_memory::MemoryRepository> =
            Arc::new(SqliteMemoryRepository::new(memory_pool.clone()));
        let vector_store = VectorMemoryStore::new(memory_repo.clone());

        let llm_client: Arc<dyn LLMClient> = Arc::new(MockMemoryLLM {
            response: llm_response.to_string(),
        });
        let extractor = MemoryExtractor::new(llm_client.clone(), "mock".to_string());

        let mut memory_service = MemoryService::new(memory_repo, vector_store, extractor);
        memory_service.initialize().await.unwrap();

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config)
            .with_memory(memory_service);

        (db, memory_pool, flow)
    }

    #[tokio::test]
    async fn test_flow_with_memory_enabled() {
        let (_db, _mpool, flow) = setup_with_memory("[]").await;
        assert!(flow.memory_service.is_some());
    }

    #[tokio::test]
    async fn test_build_context_injects_relevant_memories() {
        let (_db, _mpool, flow) = setup_with_memory("[]").await;

        // 先手动创建一条记忆
        {
            let service = flow.memory_service.as_ref().unwrap().lock().await;
            let memory =
                agent_memory::Memory::new(agent_memory::MemoryType::User, "用户熟悉 Rust 编程".to_string());
            service.create(memory).await.unwrap();
        }

        let session = flow
            .session_manager
            .create_session(Some("Test Memory".to_string()))
            .await
            .unwrap();

        let msg = Message::new(session.id, MessageRole::User, "Rust".to_string());
        flow.session_manager
            .add_message(session.id, msg)
            .await
            .unwrap();

        let context = flow.build_context(session.id).await.unwrap();

        // 应该有 2 条消息：1 条 system（记忆注入） + 1 条 user
        assert_eq!(context.len(), 2);
        assert_eq!(context[0].role, MessageRole::System);
        assert!(context[0].content.contains("长期记忆"));
        assert!(context[0].content.contains("Rust"));
    }

    #[tokio::test]
    async fn test_build_context_no_memories_found() {
        let (_db, _mpool, flow) = setup_with_memory("[]").await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        let msg = Message::new(
            session.id,
            MessageRole::User,
            "xyzzy_not_a_real_word".to_string(),
        );
        flow.session_manager
            .add_message(session.id, msg)
            .await
            .unwrap();

        let context = flow.build_context(session.id).await.unwrap();

        // 无相关记忆，只有 1 条 user 消息
        assert_eq!(context.len(), 1);
        assert_eq!(context[0].role, MessageRole::User);
    }

    #[tokio::test]
    async fn test_extract_memories_from_session() {
        let response = r#"[{"type": "user", "content": "用户喜欢 Rust", "source": "对话"}]"#;
        let (_db, _mpool, flow) = setup_with_memory(response).await;

        let session = flow
            .session_manager
            .create_session(None)
            .await
            .unwrap();

        let msg = Message::new(
            session.id,
            MessageRole::User,
            "我很喜欢用 Rust 开发".to_string(),
        );
        flow.session_manager
            .add_message(session.id, msg)
            .await
            .unwrap();

        let saved = flow.extract_memories(session.id).await.unwrap();
        assert_eq!(saved.len(), 1);
        assert_eq!(saved[0].memory_type, agent_memory::MemoryType::User);
    }

    #[tokio::test]
    async fn test_memory_stats() {
        let (_db, _mpool, flow) = setup_with_memory("[]").await;

        {
            let service = flow.memory_service.as_ref().unwrap().lock().await;
            service
                .create(agent_memory::Memory::new(
                    agent_memory::MemoryType::User,
                    "u1".to_string(),
                ))
                .await
                .unwrap();
            service
                .create(agent_memory::Memory::new(
                    agent_memory::MemoryType::Feedback,
                    "f1".to_string(),
                ))
                .await
                .unwrap();
        }

        let stats = flow.memory_stats().await.unwrap().unwrap();
        assert_eq!(stats.total_memories, 2);
    }

    #[tokio::test]
    async fn test_flow_without_memory() {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));
        let llm_client: Arc<dyn LLMClient> = Arc::new(MockMemoryLLM {
            response: "[]".to_string(),
        });

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config);

        assert!(flow.memory_service.is_none());

        let session = flow.session_manager.create_session(None).await.unwrap();
        let msg = Message::new(session.id, MessageRole::User, "Hello".to_string());
        flow.session_manager
            .add_message(session.id, msg)
            .await
            .unwrap();

        let context = flow.build_context(session.id).await.unwrap();
        assert_eq!(context.len(), 1);
    }
}

#[cfg(test)]
#[cfg(feature = "file-storage")]
mod file_storage_tests {
    use super::*;
    use agent_core::models::MessageRole;
    use agent_core::traits::llm::{
        CompletionRequest, CompletionResponse, CompletionStream, LLMClient, ModelInfo, TokenUsage,
    };
    use agent_file_storage::{AccessLevel, FileRepository, FileService, FileStorage};
    use agent_storage::{repository::*, Database};
    use async_trait::async_trait;

    struct MockFSLLM;

    #[async_trait]
    impl LLMClient for MockFSLLM {
        async fn complete(&self, _req: CompletionRequest) -> agent_core::Result<CompletionResponse> {
            Ok(CompletionResponse {
                content: "文件内容已收到".to_string(),
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

    async fn setup_with_file_storage() -> (Database, tempfile::TempDir, ConversationFlow) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

        let tmp = tempfile::TempDir::new().unwrap();
        let upload_dir = tmp.path().join("uploads");

        let file_pool = sqlx::SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("../agent-file-storage/migrations")
            .run(&file_pool)
            .await
            .unwrap();

        let file_repo = FileRepository::new(file_pool);
        let file_storage = FileStorage::new(&upload_dir, 10 * 1024 * 1024).await.unwrap();
        let file_service = FileService::new(file_repo, file_storage);

        let llm_client: Arc<dyn LLMClient> = Arc::new(MockFSLLM);
        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config)
            .with_file_storage(file_service);

        (db, tmp, flow)
    }

    #[tokio::test]
    async fn test_flow_with_file_storage_enabled() {
        let (_db, _tmp, flow) = setup_with_file_storage().await;
        assert!(flow.file_service.is_some());
    }

    #[tokio::test]
    async fn test_resolve_file_references_by_name() {
        let (_db, tmp, flow) = setup_with_file_storage().await;

        let source = tmp.path().join("hello.rs");
        tokio::fs::write(&source, b"fn main() { println!(\"hello\"); }").await.unwrap();

        let file_svc = flow.file_service.as_ref().unwrap();
        file_svc
            .upload_file(&source, "default", AccessLevel::Private, None)
            .await
            .unwrap();

        let result = flow
            .resolve_file_references("请分析 @file:hello.rs 这个文件", "default")
            .await;

        assert!(result.contains("[文件: hello.rs]"));
        assert!(result.contains("fn main()"));
        assert!(result.contains("[/文件]"));
        assert!(!result.contains("@file:"));
    }

    #[tokio::test]
    async fn test_resolve_file_references_by_id() {
        let (_db, tmp, flow) = setup_with_file_storage().await;

        let source = tmp.path().join("data.json");
        tokio::fs::write(&source, b"{\"key\": \"value\"}").await.unwrap();

        let file_svc = flow.file_service.as_ref().unwrap();
        let file = file_svc
            .upload_file(&source, "default", AccessLevel::Private, None)
            .await
            .unwrap();

        let input = format!("查看文件 @file:{}", file.id);
        let result = flow.resolve_file_references(&input, "default").await;

        assert!(result.contains("\"key\": \"value\""));
    }

    #[tokio::test]
    async fn test_resolve_file_references_not_found() {
        let (_db, _tmp, flow) = setup_with_file_storage().await;

        let result = flow
            .resolve_file_references("看看 @file:nonexistent.txt", "default")
            .await;

        assert!(result.contains("[文件引用错误:"));
    }

    #[tokio::test]
    async fn test_resolve_multiple_file_references() {
        let (_db, tmp, flow) = setup_with_file_storage().await;

        let s1 = tmp.path().join("a.rs");
        let s2 = tmp.path().join("b.rs");
        tokio::fs::write(&s1, b"// file a").await.unwrap();
        tokio::fs::write(&s2, b"// file b").await.unwrap();

        let file_svc = flow.file_service.as_ref().unwrap();
        file_svc.upload_file(&s1, "default", AccessLevel::Private, None).await.unwrap();
        file_svc.upload_file(&s2, "default", AccessLevel::Private, None).await.unwrap();

        let result = flow
            .resolve_file_references("比较 @file:a.rs 和 @file:b.rs", "default")
            .await;

        assert!(result.contains("[文件: a.rs]"));
        assert!(result.contains("// file a"));
        assert!(result.contains("[文件: b.rs]"));
        assert!(result.contains("// file b"));
    }

    #[tokio::test]
    async fn test_resolve_no_references() {
        let (_db, _tmp, flow) = setup_with_file_storage().await;

        let result = flow
            .resolve_file_references("普通消息没有文件引用", "default")
            .await;

        assert_eq!(result, "普通消息没有文件引用");
    }

    #[tokio::test]
    async fn test_flow_without_file_storage() {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
        let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));
        let llm_client: Arc<dyn LLMClient> = Arc::new(MockFSLLM);

        let config = ConversationConfig::default();
        let flow = ConversationFlow::new(session_manager, llm_client, config);

        assert!(flow.file_service.is_none());
    }
}
