//! AgentRuntime - 统一的运行时环境

use agent_storage::Database;
use agent_skills::SkillRegistry;
#[cfg(feature = "compression")]
use agent_context_compression::{CompressionConfig, CompressionService};
use crate::{SessionManager, subagent::SubagentOrchestrator};
use agent_core::traits::LLMClient;
use std::sync::Arc;
use anyhow::{Context, Result};

/// 统一的运行时环境，持有所有共享资源
pub struct AgentRuntime {
    db: Arc<Database>,
    session_manager: Arc<SessionManager>,
    orchestrator: Arc<SubagentOrchestrator>,
    llm_client: Arc<dyn LLMClient>,
    skill_registry: Option<Arc<SkillRegistry>>,
    #[cfg(feature = "compression")]
    compression_service: Option<CompressionService>,
    #[cfg(feature = "multi-agent")]
    multi_agent_service: Option<crate::MultiAgentService>,
}

impl AgentRuntime {
    /// 创建新的运行时
    pub async fn new(
        db: Database,
        llm_client: Arc<dyn LLMClient>,
        skill_registry: Option<Arc<SkillRegistry>>,
    ) -> Result<Self> {
        let db = Arc::new(db);

        // Create session repository
        let session_repo = (|| {
            let repo = agent_storage::SqliteSessionRepository::new(db.pool().clone());
            Ok::<_, anyhow::Error>(Arc::new(repo))
        })()
        .context("Failed to create session repository")?;

        // Create message repository
        let message_repo = (|| {
            let repo = agent_storage::SqliteMessageRepository::new(db.pool().clone());
            Ok::<_, anyhow::Error>(Arc::new(repo))
        })()
        .context("Failed to create message repository")?;

        // Create session manager
        let session_manager = (|| {
            let manager = SessionManager::new(session_repo, message_repo);
            Ok::<_, anyhow::Error>(Arc::new(manager))
        })()
        .context("Failed to create session manager")?;

        // Create orchestrator
        let orchestrator = (|| {
            let config = crate::subagent::OrchestratorConfig::default();
            let orch = SubagentOrchestrator::new(config);
            Ok::<_, anyhow::Error>(Arc::new(orch))
        })()
        .context("Failed to create subagent orchestrator")?;

        // 创建压缩服务
        #[cfg(feature = "compression")]
        let compression_service = {
            let config = CompressionConfig::default();
            match CompressionService::new(llm_client.clone(), config) {
                Ok(service) => {
                    tracing::info!("Compression service initialized");
                    Some(service)
                }
                Err(e) => {
                    tracing::warn!("Failed to initialize compression service: {}", e);
                    None
                }
            }
        };

        #[cfg(feature = "multi-agent")]
        let multi_agent_service = {
            let model = "qwen2.5:7b".to_string();
            let service = crate::MultiAgentService::new(llm_client.clone(), model);
            match service.register_default_agents() {
                Ok(()) => {
                    tracing::info!("Multi-agent service initialized with default agents");
                    Some(service)
                }
                Err(e) => {
                    tracing::warn!("Failed to register default agents: {}", e);
                    Some(service)
                }
            }
        };

        Ok(Self {
            db,
            session_manager,
            orchestrator,
            llm_client,
            skill_registry,
            #[cfg(feature = "compression")]
            compression_service,
            #[cfg(feature = "multi-agent")]
            multi_agent_service,
        })
    }

    /// 获取会话管理器
    pub fn session_manager(&self) -> &Arc<SessionManager> {
        &self.session_manager
    }

    /// 获取子代理编排器
    pub fn orchestrator(&self) -> &Arc<SubagentOrchestrator> {
        &self.orchestrator
    }

    /// 获取 LLM 客户端
    pub fn llm_client(&self) -> &Arc<dyn LLMClient> {
        &self.llm_client
    }

    /// 获取技能注册表
    pub fn skill_registry(&self) -> Option<&Arc<SkillRegistry>> {
        self.skill_registry.as_ref()
    }

    /// 获取数据库引用
    pub fn database(&self) -> &Arc<Database> {
        &self.db
    }

    /// 获取压缩服务（转移所有权，用于注入到 ConversationFlow）
    #[cfg(feature = "compression")]
    pub fn take_compression_service(&mut self) -> Option<CompressionService> {
        self.compression_service.take()
    }

    /// 获取多 Agent 协作服务
    #[cfg(feature = "multi-agent")]
    pub fn multi_agent_service(&self) -> Option<&crate::MultiAgentService> {
        self.multi_agent_service.as_ref()
    }

    /// 获取多 Agent 协作服务（转移所有权）
    #[cfg(feature = "multi-agent")]
    pub fn take_multi_agent_service(&mut self) -> Option<crate::MultiAgentService> {
        self.multi_agent_service.take()
    }
}
