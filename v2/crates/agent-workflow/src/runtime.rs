//! AgentRuntime - 统一的运行时环境

use agent_storage::Database;
use agent_skills::SkillRegistry;
use crate::{SessionManager, subagent::SubagentOrchestrator};
use agent_core::traits::LLMClient;
use std::sync::Arc;
use anyhow::Result;

/// 统一的运行时环境，持有所有共享资源
pub struct AgentRuntime {
    db: Arc<Database>,
    session_manager: Arc<SessionManager>,
    orchestrator: Arc<SubagentOrchestrator>,
    llm_client: Arc<dyn LLMClient>,
    skill_registry: Option<Arc<SkillRegistry>>,
}

impl AgentRuntime {
    /// 创建新的运行时
    pub async fn new(
        db: Database,
        llm_client: Arc<dyn LLMClient>,
        skill_registry: Option<Arc<SkillRegistry>>,
    ) -> Result<Self> {
        let db = Arc::new(db);

        // 创建 repositories
        let session_repo = Arc::new(
            agent_storage::SqliteSessionRepository::new(db.pool().clone())
        );
        let message_repo = Arc::new(
            agent_storage::SqliteMessageRepository::new(db.pool().clone())
        );

        let session_manager = Arc::new(
            SessionManager::new(session_repo, message_repo)
        );

        // 创建 orchestrator
        let orchestrator = SubagentOrchestrator::new(
            crate::subagent::OrchestratorConfig::default()
        );

        let orchestrator = Arc::new(orchestrator);

        Ok(Self {
            db,
            session_manager,
            orchestrator,
            llm_client,
            skill_registry,
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
}
