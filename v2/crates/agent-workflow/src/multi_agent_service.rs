use agent_core::traits::LLMClient;
use agent_multi_agent::{
    agents::LlmAgent,
    coordinator::{ParallelCoordinator, PipelineCoordinator, SequentialCoordinator, VotingCoordinator},
    models::{CollaborationStrategy, CollaborationTask, SubTask},
    registry::AgentRegistry,
    router::MessageRouter,
    traits::{Agent, Coordinator},
    AggregatedResult, MultiAgentError,
};
use std::sync::Arc;
use uuid::Uuid;

pub struct MultiAgentService {
    registry: Arc<AgentRegistry>,
    router: Arc<MessageRouter>,
    llm_client: Arc<dyn LLMClient>,
    model: String,
}

impl MultiAgentService {
    pub fn new(llm_client: Arc<dyn LLMClient>, model: impl Into<String>) -> Self {
        let registry = Arc::new(AgentRegistry::new());
        let router = Arc::new(MessageRouter::new(registry.clone(), 256));

        Self {
            registry,
            router,
            llm_client,
            model: model.into(),
        }
    }

    pub fn register_default_agents(&self) -> agent_multi_agent::Result<()> {
        let search = LlmAgent::search_agent(self.llm_client.clone(), &self.model);
        let analysis = LlmAgent::analysis_agent(self.llm_client.clone(), &self.model);
        let summary = LlmAgent::summary_agent(self.llm_client.clone(), &self.model);

        self.registry.register(Arc::new(search))?;
        self.registry.register(Arc::new(analysis))?;
        self.registry.register(Arc::new(summary))?;

        Ok(())
    }

    pub fn register_agent(&self, agent: Arc<dyn Agent>) -> agent_multi_agent::Result<()> {
        self.registry.register(agent)
    }

    pub fn registry(&self) -> &Arc<AgentRegistry> {
        &self.registry
    }

    pub fn router(&self) -> &Arc<MessageRouter> {
        &self.router
    }

    pub async fn execute_collaboration(
        &self,
        title: impl Into<String>,
        description: impl Into<String>,
        strategy: CollaborationStrategy,
        subtask_specs: Vec<(String, String, serde_json::Value)>,
    ) -> agent_multi_agent::Result<AggregatedResult> {
        let task_id = Uuid::new_v4();
        let subtasks: Vec<SubTask> = subtask_specs
            .into_iter()
            .map(|(agent_id, task, context)| SubTask::new(task_id, agent_id, task, context))
            .collect();

        let mut task = CollaborationTask::new(title, description, strategy.clone())
            .with_subtasks(subtasks);

        let coordinator: Box<dyn Coordinator> = match &strategy {
            CollaborationStrategy::Parallel => {
                Box::new(ParallelCoordinator::new(self.registry.clone()))
            }
            CollaborationStrategy::Sequential => {
                Box::new(SequentialCoordinator::new(self.registry.clone()))
            }
            CollaborationStrategy::Voting { min_votes } => {
                Box::new(VotingCoordinator::new(self.registry.clone(), *min_votes))
            }
            CollaborationStrategy::Pipeline => {
                Box::new(PipelineCoordinator::new(self.registry.clone()))
            }
        };

        coordinator.execute(&mut task).await
    }

    pub fn list_agents(&self) -> Vec<agent_multi_agent::AgentInfo> {
        self.registry.list_agents()
    }

    pub fn agent_count(&self) -> usize {
        self.registry.agent_count()
    }
}
