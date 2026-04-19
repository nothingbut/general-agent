use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use uuid::Uuid;

#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum AgentCapability {
    Search,
    Analysis,
    Summary,
    CodeReview,
    Translation,
    DataExtraction,
    Reasoning,
    Custom(String),
}

impl AgentCapability {
    pub fn as_str(&self) -> &str {
        match self {
            Self::Search => "search",
            Self::Analysis => "analysis",
            Self::Summary => "summary",
            Self::CodeReview => "code_review",
            Self::Translation => "translation",
            Self::DataExtraction => "data_extraction",
            Self::Reasoning => "reasoning",
            Self::Custom(s) => s.as_str(),
        }
    }

    pub fn from_str(s: &str) -> Self {
        match s {
            "search" => Self::Search,
            "analysis" => Self::Analysis,
            "summary" => Self::Summary,
            "code_review" => Self::CodeReview,
            "translation" => Self::Translation,
            "data_extraction" => Self::DataExtraction,
            "reasoning" => Self::Reasoning,
            other => Self::Custom(other.to_string()),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum AgentStatus {
    Idle,
    Busy,
    Offline,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AgentInfo {
    pub id: String,
    pub name: String,
    pub description: String,
    pub capabilities: HashSet<AgentCapability>,
    pub status: AgentStatus,
    pub max_concurrent_tasks: usize,
    pub active_tasks: usize,
    pub system_prompt: Option<String>,
    pub registered_at: DateTime<Utc>,
}

impl AgentInfo {
    pub fn new(id: impl Into<String>, name: impl Into<String>) -> Self {
        Self {
            id: id.into(),
            name: name.into(),
            description: String::new(),
            capabilities: HashSet::new(),
            status: AgentStatus::Idle,
            max_concurrent_tasks: 1,
            active_tasks: 0,
            system_prompt: None,
            registered_at: Utc::now(),
        }
    }

    pub fn with_description(mut self, desc: impl Into<String>) -> Self {
        self.description = desc.into();
        self
    }

    pub fn with_capability(mut self, cap: AgentCapability) -> Self {
        self.capabilities.insert(cap);
        self
    }

    pub fn with_capabilities(mut self, caps: impl IntoIterator<Item = AgentCapability>) -> Self {
        self.capabilities.extend(caps);
        self
    }

    pub fn with_system_prompt(mut self, prompt: impl Into<String>) -> Self {
        self.system_prompt = Some(prompt.into());
        self
    }

    pub fn with_max_concurrent(mut self, max: usize) -> Self {
        self.max_concurrent_tasks = max;
        self
    }

    pub fn has_capability(&self, cap: &AgentCapability) -> bool {
        self.capabilities.contains(cap)
    }

    pub fn is_available(&self) -> bool {
        self.status != AgentStatus::Offline && self.active_tasks < self.max_concurrent_tasks
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AgentMessage {
    pub id: Uuid,
    pub from_agent: String,
    pub to_agent: String,
    pub content: MessageContent,
    pub correlation_id: Uuid,
    pub created_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum MessageContent {
    TaskRequest {
        task: String,
        context: serde_json::Value,
    },
    TaskResponse {
        result: String,
        metadata: serde_json::Value,
    },
    Error {
        code: String,
        message: String,
    },
    Ping,
    Pong,
}

impl AgentMessage {
    pub fn task_request(
        from: impl Into<String>,
        to: impl Into<String>,
        task: impl Into<String>,
        context: serde_json::Value,
        correlation_id: Uuid,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            from_agent: from.into(),
            to_agent: to.into(),
            content: MessageContent::TaskRequest {
                task: task.into(),
                context,
            },
            correlation_id,
            created_at: Utc::now(),
        }
    }

    pub fn task_response(
        from: impl Into<String>,
        to: impl Into<String>,
        result: impl Into<String>,
        metadata: serde_json::Value,
        correlation_id: Uuid,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            from_agent: from.into(),
            to_agent: to.into(),
            content: MessageContent::TaskResponse {
                result: result.into(),
                metadata,
            },
            correlation_id,
            created_at: Utc::now(),
        }
    }

    pub fn error(
        from: impl Into<String>,
        to: impl Into<String>,
        code: impl Into<String>,
        message: impl Into<String>,
        correlation_id: Uuid,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            from_agent: from.into(),
            to_agent: to.into(),
            content: MessageContent::Error {
                code: code.into(),
                message: message.into(),
            },
            correlation_id,
            created_at: Utc::now(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_agent_info_builder() {
        let agent = AgentInfo::new("search-agent", "Search Agent")
            .with_description("Performs web and document searches")
            .with_capability(AgentCapability::Search)
            .with_capability(AgentCapability::DataExtraction)
            .with_system_prompt("You are a search specialist.")
            .with_max_concurrent(3);

        assert_eq!(agent.id, "search-agent");
        assert_eq!(agent.name, "Search Agent");
        assert!(agent.has_capability(&AgentCapability::Search));
        assert!(agent.has_capability(&AgentCapability::DataExtraction));
        assert!(!agent.has_capability(&AgentCapability::Summary));
        assert_eq!(agent.max_concurrent_tasks, 3);
        assert!(agent.is_available());
    }

    #[test]
    fn test_agent_availability() {
        let mut agent = AgentInfo::new("a1", "Agent 1").with_max_concurrent(2);
        assert!(agent.is_available());

        agent.active_tasks = 2;
        assert!(!agent.is_available());

        agent.active_tasks = 1;
        agent.status = AgentStatus::Offline;
        assert!(!agent.is_available());
    }

    #[test]
    fn test_capability_roundtrip() {
        let cap = AgentCapability::Search;
        let s = cap.as_str();
        let restored = AgentCapability::from_str(s);
        assert_eq!(cap, restored);

        let custom = AgentCapability::Custom("my_skill".to_string());
        let s2 = custom.as_str();
        let restored2 = AgentCapability::from_str(s2);
        assert_eq!(custom, restored2);
    }

    #[test]
    fn test_agent_message_creation() {
        let corr_id = Uuid::new_v4();
        let msg = AgentMessage::task_request(
            "agent-a",
            "agent-b",
            "Analyze this text",
            serde_json::json!({"text": "hello"}),
            corr_id,
        );

        assert_eq!(msg.from_agent, "agent-a");
        assert_eq!(msg.to_agent, "agent-b");
        assert_eq!(msg.correlation_id, corr_id);
        assert!(matches!(msg.content, MessageContent::TaskRequest { .. }));
    }

    #[test]
    fn test_agent_message_serialization() {
        let corr_id = Uuid::new_v4();
        let msg = AgentMessage::task_response(
            "agent-b",
            "agent-a",
            "Analysis complete",
            serde_json::json!({"score": 0.95}),
            corr_id,
        );

        let json = serde_json::to_string(&msg).unwrap();
        let deserialized: AgentMessage = serde_json::from_str(&json).unwrap();
        assert_eq!(deserialized.from_agent, "agent-b");
        assert_eq!(deserialized.correlation_id, corr_id);
    }
}
