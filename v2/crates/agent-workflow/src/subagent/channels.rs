//! Message channels and result types for subagent communication

use std::time::Duration;
use uuid::Uuid;

use super::config::SubagentTaskConfig;
use super::models::SessionStatus;

/// Task execution result
#[derive(Debug, Clone)]
pub struct TaskResult {
    pub session_id: Uuid,
    pub status: SessionStatus,
    pub output: String,
    pub metadata: ResultMetadata,
}

/// Result metadata
#[derive(Debug, Clone)]
pub struct ResultMetadata {
    pub execution_time: Duration,
    pub token_count: usize,
    pub model_used: String,
    pub error_count: usize,
    pub tool_calls: Vec<String>,
    pub memory_used: usize,
}

/// Commands for subagent control
#[derive(Debug)]
pub enum SubagentCommand {
    Start(SubagentTaskConfig),
    Cancel(Uuid),
    UpdateConfig(Uuid, super::config::SubagentConfig),
}
