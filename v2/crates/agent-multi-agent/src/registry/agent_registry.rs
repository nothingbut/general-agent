use dashmap::DashMap;
use std::sync::Arc;

use crate::error::{MultiAgentError, Result};
use crate::models::{AgentCapability, AgentInfo, AgentStatus};
use crate::traits::Agent;

pub struct AgentRegistry {
    agents: DashMap<String, Arc<dyn Agent>>,
}

impl AgentRegistry {
    pub fn new() -> Self {
        Self {
            agents: DashMap::new(),
        }
    }

    pub fn register(&self, agent: Arc<dyn Agent>) -> Result<()> {
        let id = agent.info().id.clone();
        if self.agents.contains_key(&id) {
            return Err(MultiAgentError::AgentAlreadyRegistered(id));
        }
        self.agents.insert(id, agent);
        Ok(())
    }

    pub fn unregister(&self, agent_id: &str) -> Result<()> {
        self.agents
            .remove(agent_id)
            .ok_or_else(|| MultiAgentError::AgentNotFound(agent_id.to_string()))?;
        Ok(())
    }

    pub fn get(&self, agent_id: &str) -> Result<Arc<dyn Agent>> {
        self.agents
            .get(agent_id)
            .map(|entry| entry.value().clone())
            .ok_or_else(|| MultiAgentError::AgentNotFound(agent_id.to_string()))
    }

    pub fn find_by_capability(&self, capability: &AgentCapability) -> Vec<Arc<dyn Agent>> {
        self.agents
            .iter()
            .filter(|entry| {
                let info = entry.value().info();
                info.has_capability(capability) && info.is_available()
            })
            .map(|entry| entry.value().clone())
            .collect()
    }

    pub fn list_agents(&self) -> Vec<AgentInfo> {
        self.agents
            .iter()
            .map(|entry| entry.value().info().clone())
            .collect()
    }

    pub fn available_agents(&self) -> Vec<AgentInfo> {
        self.agents
            .iter()
            .filter(|entry| entry.value().info().is_available())
            .map(|entry| entry.value().info().clone())
            .collect()
    }

    pub fn agent_count(&self) -> usize {
        self.agents.len()
    }

    pub fn update_status(&self, agent_id: &str, status: AgentStatus) -> Result<()> {
        let _agent = self.get(agent_id)?;
        // AgentInfo status is managed by the agent itself; this is a no-op check
        // that the agent exists. Individual agents handle their own status.
        let _ = status;
        Ok(())
    }
}

impl Default for AgentRegistry {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{AgentCapability, AgentInfo, AgentMessage};
    use crate::traits::Agent;
    use async_trait::async_trait;

    struct MockAgent {
        info: AgentInfo,
    }

    impl MockAgent {
        fn new(id: &str, caps: Vec<AgentCapability>) -> Self {
            Self {
                info: AgentInfo::new(id, id).with_capabilities(caps),
            }
        }
    }

    #[async_trait]
    impl Agent for MockAgent {
        fn info(&self) -> &AgentInfo {
            &self.info
        }

        async fn handle_message(&self, msg: AgentMessage) -> crate::Result<AgentMessage> {
            Ok(AgentMessage::task_response(
                &self.info.id,
                &msg.from_agent,
                "ok",
                serde_json::json!({}),
                msg.correlation_id,
            ))
        }

        async fn execute_task(
            &self,
            _task: &str,
            _context: serde_json::Value,
        ) -> crate::Result<String> {
            Ok("done".to_string())
        }
    }

    #[test]
    fn test_register_and_get() {
        let registry = AgentRegistry::new();
        let agent = Arc::new(MockAgent::new("a1", vec![AgentCapability::Search]));

        registry.register(agent).unwrap();
        assert_eq!(registry.agent_count(), 1);

        let found = registry.get("a1").unwrap();
        assert_eq!(found.info().id, "a1");
    }

    #[test]
    fn test_duplicate_registration() {
        let registry = AgentRegistry::new();
        let a1 = Arc::new(MockAgent::new("a1", vec![]));
        let a1_dup = Arc::new(MockAgent::new("a1", vec![]));

        registry.register(a1).unwrap();
        let err = registry.register(a1_dup).unwrap_err();
        assert!(matches!(err, MultiAgentError::AgentAlreadyRegistered(_)));
    }

    #[test]
    fn test_unregister() {
        let registry = AgentRegistry::new();
        let agent = Arc::new(MockAgent::new("a1", vec![]));

        registry.register(agent).unwrap();
        assert_eq!(registry.agent_count(), 1);

        registry.unregister("a1").unwrap();
        assert_eq!(registry.agent_count(), 0);

        let err = registry.unregister("a1").unwrap_err();
        assert!(matches!(err, MultiAgentError::AgentNotFound(_)));
    }

    #[test]
    fn test_find_by_capability() {
        let registry = AgentRegistry::new();
        registry
            .register(Arc::new(MockAgent::new(
                "search1",
                vec![AgentCapability::Search],
            )))
            .unwrap();
        registry
            .register(Arc::new(MockAgent::new(
                "search2",
                vec![AgentCapability::Search, AgentCapability::Analysis],
            )))
            .unwrap();
        registry
            .register(Arc::new(MockAgent::new(
                "summary1",
                vec![AgentCapability::Summary],
            )))
            .unwrap();

        let search_agents = registry.find_by_capability(&AgentCapability::Search);
        assert_eq!(search_agents.len(), 2);

        let analysis_agents = registry.find_by_capability(&AgentCapability::Analysis);
        assert_eq!(analysis_agents.len(), 1);

        let summary_agents = registry.find_by_capability(&AgentCapability::Summary);
        assert_eq!(summary_agents.len(), 1);
    }

    #[test]
    fn test_list_and_available() {
        let registry = AgentRegistry::new();
        registry
            .register(Arc::new(MockAgent::new("a1", vec![])))
            .unwrap();
        registry
            .register(Arc::new(MockAgent::new("a2", vec![])))
            .unwrap();

        assert_eq!(registry.list_agents().len(), 2);
        assert_eq!(registry.available_agents().len(), 2);
    }
}
