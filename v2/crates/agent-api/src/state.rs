use agent_file_storage::FileService;
use agent_memory::MemoryService;
use agent_skills::{SkillExecutor, SkillRegistry};
use agent_workflow::{AgentRuntime, ConversationFlow, MultiAgentService, SessionManager};
use std::sync::Arc;
use tokio::sync::Mutex;

#[derive(Clone)]
pub struct AppState {
    pub runtime: Arc<AgentRuntime>,
    pub conversation_flow: Arc<ConversationFlow>,
    pub memory_service: Option<Arc<Mutex<MemoryService>>>,
    pub file_service: Option<Arc<FileService>>,
    pub skill_executor: Arc<SkillExecutor>,
    pub multi_agent_service: Option<Arc<MultiAgentService>>,
}

impl AppState {
    pub fn new(runtime: Arc<AgentRuntime>, conversation_flow: Arc<ConversationFlow>) -> Self {
        Self {
            runtime,
            conversation_flow,
            memory_service: None,
            file_service: None,
            skill_executor: Arc::new(SkillExecutor::new()),
            multi_agent_service: None,
        }
    }

    pub fn with_memory(mut self, service: MemoryService) -> Self {
        self.memory_service = Some(Arc::new(Mutex::new(service)));
        self
    }

    pub fn with_files(mut self, service: FileService) -> Self {
        self.file_service = Some(Arc::new(service));
        self
    }

    pub fn with_multi_agent(mut self, service: MultiAgentService) -> Self {
        self.multi_agent_service = Some(Arc::new(service));
        self
    }

    pub fn session_manager(&self) -> &Arc<SessionManager> {
        self.runtime.session_manager()
    }

    pub fn skill_registry(&self) -> Option<&Arc<SkillRegistry>> {
        self.runtime.skill_registry()
    }
}
