//! Subagent system implementation
//!
//! Provides subagent orchestration, stage management, and error handling

pub mod channels;
pub mod config;
pub mod error;
pub mod models;
pub mod orchestrator;
pub mod progress;
pub mod state;
pub mod task;

pub use channels::{ResultMetadata, SubagentCommand, TaskResult};
pub use config::*;
pub use error::{SubagentError, SubagentResult};
pub use models::{SessionStatus, SessionType};
pub use orchestrator::{OrchestratorConfig, SubagentOrchestrator};
pub use progress::ProgressEstimator;
pub use state::SubagentState;
pub use task::SubagentTask;
