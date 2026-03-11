//! Subagent state management

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::time::Duration;
use uuid::Uuid;

use super::error::{SubagentError, SubagentResult};
use super::models::SessionStatus;

/// Represents the runtime state of a subagent session
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubagentState {
    /// Unique identifier for this subagent session
    session_id: Uuid,

    /// ID of the parent session that spawned this subagent
    parent_id: Uuid,

    /// Identifier for the stage this subagent is executing
    stage_id: String,

    /// Current status of the subagent session
    status: SessionStatus,

    /// Progress percentage (0.0 to 1.0)
    progress: f32,

    /// Timestamp when the subagent session started
    started_at: DateTime<Utc>,

    /// Timestamp of last state update
    updated_at: DateTime<Utc>,

    /// Estimated time remaining (if available)
    estimated_remaining: Option<Duration>,

    /// Number of messages exchanged in this session
    message_count: usize,

    /// Error message if status is Failed
    error: Option<String>,
}

impl SubagentState {
    /// Creates a new SubagentState with initial values
    ///
    /// # Errors
    ///
    /// Returns `SubagentError::ConfigError` if `stage_id` is empty or whitespace-only.
    pub fn new(session_id: Uuid, parent_id: Uuid, stage_id: String) -> SubagentResult<Self> {
        if stage_id.trim().is_empty() {
            return Err(SubagentError::ConfigError(
                "stage_id cannot be empty".to_string(),
            ));
        }

        let now = Utc::now();

        Ok(Self {
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
        })
    }

    // Getters

    /// Returns the session ID
    pub fn session_id(&self) -> Uuid {
        self.session_id
    }

    /// Returns the parent session ID
    pub fn parent_id(&self) -> Uuid {
        self.parent_id
    }

    /// Returns the stage ID
    pub fn stage_id(&self) -> &str {
        &self.stage_id
    }

    /// Returns the current status
    pub fn status(&self) -> SessionStatus {
        self.status
    }

    /// Returns the current progress (0.0 to 1.0)
    pub fn progress(&self) -> f32 {
        self.progress
    }

    /// Returns the start timestamp
    pub fn started_at(&self) -> DateTime<Utc> {
        self.started_at
    }

    /// Returns the last update timestamp
    pub fn updated_at(&self) -> DateTime<Utc> {
        self.updated_at
    }

    /// Returns the estimated remaining time, if available
    pub fn estimated_remaining(&self) -> Option<Duration> {
        self.estimated_remaining
    }

    /// Returns the message count
    pub fn message_count(&self) -> usize {
        self.message_count
    }

    /// Returns the error message, if any
    pub fn error(&self) -> Option<&str> {
        self.error.as_deref()
    }

    // Immutable update methods

    /// Returns a new SubagentState with updated progress
    ///
    /// # Errors
    ///
    /// Returns `SubagentError::ConfigError` if progress is not in range 0.0-1.0 or is NaN.
    pub fn with_progress(self, progress: f32) -> SubagentResult<Self> {
        if progress.is_nan() || !(0.0..=1.0).contains(&progress) {
            return Err(SubagentError::ConfigError(format!(
                "progress must be 0.0-1.0, got {}",
                progress
            )));
        }

        Ok(Self {
            progress,
            updated_at: Utc::now(),
            ..self
        })
    }

    /// Returns a new SubagentState with updated status
    pub fn with_status(self, status: SessionStatus) -> Self {
        Self {
            status,
            updated_at: Utc::now(),
            ..self
        }
    }

    /// Returns a new SubagentState with an error message
    pub fn with_error(self, error: String) -> Self {
        Self {
            error: Some(error),
            updated_at: Utc::now(),
            ..self
        }
    }

    /// Returns a new SubagentState with estimated remaining time
    pub fn with_estimated_remaining(self, duration: Duration) -> Self {
        Self {
            estimated_remaining: Some(duration),
            updated_at: Utc::now(),
            ..self
        }
    }

    /// Returns a new SubagentState with incremented message count
    pub fn increment_message_count(self) -> Self {
        Self {
            message_count: self.message_count + 1,
            updated_at: Utc::now(),
            ..self
        }
    }
}
