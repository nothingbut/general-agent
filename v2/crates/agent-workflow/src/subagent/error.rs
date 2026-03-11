//! Error types for subagent system

use thiserror::Error;

/// Result type for subagent operations
pub type SubagentResult<T> = Result<T, SubagentError>;

/// Errors that can occur in the subagent system
#[derive(Error, Debug)]
pub enum SubagentError {
    /// A specific stage in the subagent pipeline failed
    #[error("Stage {stage} failed: {reason}")]
    StageFailed { stage: usize, reason: String },

    /// Stage execution timed out
    #[error("Stage {stage} timed out after {duration_ms}ms")]
    Timeout { stage: usize, duration_ms: u64 },

    /// Too many concurrent subagents running
    #[error("Too many concurrent subagents (limit: {limit})")]
    TooManyConcurrentSubagents { limit: usize },

    /// Too many stages in the pipeline
    #[error("Too many stages (limit: {limit})")]
    TooManyStages { limit: usize },

    /// Failed to create task in task system
    #[error("Failed to create task: {reason}")]
    TaskCreationFailed { reason: String },

    /// Channel closed unexpectedly
    #[error("Channel closed unexpectedly")]
    ChannelClosed,

    /// Database operation failed
    #[error("Database error: {0}")]
    DatabaseError(#[from] sqlx::Error),

    /// LLM operation failed
    #[error("LLM error: {0}")]
    LLMError(String),

    /// Serialization/deserialization failed
    #[error("Serialization error: {0}")]
    SerializationError(#[from] serde_json::Error),

    /// Subagent stage panicked
    #[error("Stage {stage} panicked: {message}")]
    PanicError { stage: usize, message: String },

    /// Permission denied for operation
    #[error("Permission denied: {reason}")]
    PermissionDenied { reason: String },

    /// Path not allowed by security policy
    #[error("Path not allowed: {path}")]
    PathNotAllowed { path: String },

    /// Configuration error
    #[error("Configuration error: {0}")]
    ConfigError(String),

    /// Shutdown requested
    #[error("Shutdown requested")]
    ShutdownRequested,
}
