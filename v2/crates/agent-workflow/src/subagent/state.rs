//! Subagent state management

use chrono::{DateTime, Utc};
use std::time::Duration;
use uuid::Uuid;

use super::models::SessionStatus;

/// Represents the runtime state of a subagent session
#[derive(Debug, Clone)]
pub struct SubagentState {
    /// Unique identifier for this subagent session
    pub session_id: Uuid,

    /// ID of the parent session that spawned this subagent
    pub parent_id: Uuid,

    /// Identifier for the stage this subagent is executing
    pub stage_id: String,

    /// Current status of the subagent session
    pub status: SessionStatus,

    /// Progress percentage (0.0 to 1.0)
    pub progress: f32,

    /// Timestamp when the subagent session started
    pub started_at: DateTime<Utc>,

    /// Timestamp of last state update
    pub updated_at: DateTime<Utc>,

    /// Estimated time remaining (if available)
    pub estimated_remaining: Option<Duration>,

    /// Number of messages exchanged in this session
    pub message_count: usize,

    /// Error message if status is Failed
    pub error: Option<String>,
}

impl SubagentState {
    /// Creates a new SubagentState with initial values
    pub fn new(session_id: Uuid, parent_id: Uuid, stage_id: String) -> Self {
        let now = Utc::now();

        Self {
            session_id,
            parent_id,
            stage_id,
            status: SessionStatus::default(),
            progress: 0.0,
            started_at: now,
            updated_at: now,
            estimated_remaining: None,
            message_count: 0,
            error: None,
        }
    }
}
