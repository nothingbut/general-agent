pub mod agents;
pub mod coordinator;
pub mod error;
pub mod models;
pub mod registry;
pub mod router;
pub mod traits;

pub use error::{MultiAgentError, Result};

pub use models::{
    AgentCapability, AgentInfo, AgentMessage, AgentResult, AgentStatus, AggregatedResult,
    CollaborationStrategy, CollaborationTask, MessageContent, SubTask, TaskStatus,
};
pub use traits::{Agent, Coordinator, ResultAggregator, TaskDecomposer};
