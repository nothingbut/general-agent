//! Data models for subagent system

use serde::{Deserialize, Serialize};

/// Type of session (main or subagent)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionType {
    /// Main agent session
    Main,
    /// Subagent session
    Subagent,
}

/// Status of a session
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionStatus {
    /// Session is idle, waiting to start
    Idle,
    /// Session is currently running
    Running,
    /// Session completed successfully
    Completed,
    /// Session failed with an error
    Failed,
    /// Session was cancelled
    Cancelled,
}

impl Default for SessionStatus {
    fn default() -> Self {
        Self::Idle
    }
}
