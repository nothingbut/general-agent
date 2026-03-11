//! Subagent system implementation
//!
//! Provides subagent orchestration, stage management, and error handling

pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod state;

pub use channels::{ResultMetadata, SubagentCommand, TaskResult};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use state::SubagentState;
