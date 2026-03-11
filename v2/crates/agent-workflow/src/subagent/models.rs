use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionType {
    Main,
    Subagent,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SessionStatus {
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled,
}

impl Default for SessionStatus {
    fn default() -> Self {
        Self::Idle
    }
}
