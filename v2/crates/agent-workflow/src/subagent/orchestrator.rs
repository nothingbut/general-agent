use dashmap::DashMap;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use tokio::sync::{broadcast, mpsc};
use uuid::Uuid;

use super::channels::{SubagentCommand, TaskResult};
use super::error::{SubagentError, SubagentResult};
use super::state::SubagentState;

/// Orchestrator configuration
#[derive(Debug, Clone)]
pub struct OrchestratorConfig {
    pub max_concurrent_subagents: usize,
    pub max_stages: usize,
    pub default_timeout_secs: u64,
}

impl Default for OrchestratorConfig {
    fn default() -> Self {
        Self {
            max_concurrent_subagents: 10,
            max_stages: 5,
            default_timeout_secs: 300,
        }
    }
}

/// Subagent orchestrator
pub struct SubagentOrchestrator {
    config: OrchestratorConfig,
    state_map: Arc<DashMap<Uuid, SubagentState>>,
    active_count: Arc<AtomicUsize>,
    command_tx: mpsc::Sender<SubagentCommand>,
    command_rx: Option<mpsc::Receiver<SubagentCommand>>,
    result_tx: mpsc::Sender<TaskResult>,
    result_rx: Option<mpsc::Receiver<TaskResult>>,
    shutdown_tx: broadcast::Sender<()>,
}

impl SubagentOrchestrator {
    /// Create new orchestrator
    pub fn new(config: OrchestratorConfig) -> Self {
        let state_map = Arc::new(DashMap::new());
        let active_count = Arc::new(AtomicUsize::new(0));
        let (command_tx, command_rx) = mpsc::channel(100);
        let (result_tx, result_rx) = mpsc::channel(100);
        let (shutdown_tx, _) = broadcast::channel(16);

        Self {
            config,
            state_map,
            active_count,
            command_tx,
            command_rx: Some(command_rx),
            result_tx,
            result_rx: Some(result_rx),
            shutdown_tx,
        }
    }

    /// Get active subagent count
    pub fn active_count(&self) -> usize {
        self.active_count.load(Ordering::SeqCst)
    }

    /// Check concurrent limit
    pub async fn check_concurrent_limit(&self, new_tasks: usize) -> SubagentResult<()> {
        let current = self.active_count();
        if current + new_tasks > self.config.max_concurrent_subagents {
            return Err(SubagentError::TooManyConcurrentSubagents(
                self.config.max_concurrent_subagents,
            ));
        }
        Ok(())
    }

    /// Get state map reference
    pub fn state_map(&self) -> &Arc<DashMap<Uuid, SubagentState>> {
        &self.state_map
    }
}
