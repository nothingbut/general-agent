//! Message channels and result types for subagent communication

use serde::{Deserialize, Serialize};
use std::time::Duration;
use uuid::Uuid;

use super::config::SubagentTaskConfig;
use super::models::SessionStatus;

/// Task execution result
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskResult {
    pub session_id: Uuid,
    pub status: SessionStatus,
    pub output: String,
    pub metadata: ResultMetadata,
}

/// Result metadata
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ResultMetadata {
    #[serde(with = "serde_millis")]
    pub execution_time: Duration,
    pub token_count: usize,
    pub model_used: String,
    pub error_count: usize,
    pub tool_calls: Vec<String>,
    pub memory_used: usize,
}

/// Commands for subagent control
#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum SubagentCommand {
    Start(SubagentTaskConfig),
    Cancel(Uuid),
    UpdateConfig(Uuid, super::config::SubagentConfig),
}

/// Helper module for serializing Duration as milliseconds
mod serde_millis {
    use serde::{Deserialize, Deserializer, Serialize, Serializer};
    use std::time::Duration;

    pub fn serialize<S>(duration: &Duration, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        duration.as_millis().serialize(serializer)
    }

    pub fn deserialize<'de, D>(deserializer: D) -> Result<Duration, D::Error>
    where
        D: Deserializer<'de>,
    {
        let millis = u64::deserialize(deserializer)?;
        Ok(Duration::from_millis(millis))
    }
}
