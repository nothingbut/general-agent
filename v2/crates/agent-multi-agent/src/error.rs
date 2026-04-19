use thiserror::Error;

pub type Result<T> = std::result::Result<T, MultiAgentError>;

#[derive(Error, Debug)]
pub enum MultiAgentError {
    #[error("Agent not found: {0}")]
    AgentNotFound(String),

    #[error("Agent already registered: {0}")]
    AgentAlreadyRegistered(String),

    #[error("No agents available for capability: {0}")]
    NoAgentForCapability(String),

    #[error("Task decomposition failed: {0}")]
    DecompositionFailed(String),

    #[error("Aggregation failed: {0}")]
    AggregationFailed(String),

    #[error("Coordination failed: {0}")]
    CoordinationFailed(String),

    #[error("Agent execution failed: {agent_id}: {reason}")]
    ExecutionFailed { agent_id: String, reason: String },

    #[error("Strategy error: {0}")]
    StrategyError(String),

    #[error("Channel closed")]
    ChannelClosed,

    #[error("Timeout after {0:?}")]
    Timeout(std::time::Duration),

    #[error("LLM error: {0}")]
    LLM(String),

    #[error("Configuration error: {0}")]
    Config(String),

    #[error("Serialization error: {0}")]
    Serde(#[from] serde_json::Error),

    #[error("Core error: {0}")]
    Core(#[from] agent_core::Error),
}
