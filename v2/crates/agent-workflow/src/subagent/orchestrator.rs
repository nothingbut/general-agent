//! SubagentOrchestrator - Central coordinator for subagent execution

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

    /// Check concurrent limit (read-only validation, doesn't reserve slots)
    pub fn check_concurrent_limit(&self, new_tasks: usize) -> SubagentResult<()> {
        let current = self.active_count();
        let total = current
            .checked_add(new_tasks)
            .ok_or_else(|| SubagentError::ConfigError("Task count overflow".to_string()))?;

        if total > self.config.max_concurrent_subagents {
            return Err(SubagentError::TooManyConcurrentSubagents {
                limit: self.config.max_concurrent_subagents,
            });
        }
        Ok(())
    }

    /// Atomically reserve slots for new tasks
    /// Returns Ok if reservation succeeded, Err if would exceed limit
    pub fn try_reserve_slots(&self, count: usize) -> SubagentResult<()> {
        let mut current = self.active_count.load(Ordering::Acquire);
        loop {
            let new_total = current
                .checked_add(count)
                .ok_or_else(|| SubagentError::ConfigError("Task count overflow".to_string()))?;

            if new_total > self.config.max_concurrent_subagents {
                return Err(SubagentError::TooManyConcurrentSubagents {
                    limit: self.config.max_concurrent_subagents,
                });
            }

            match self.active_count.compare_exchange_weak(
                current,
                new_total,
                Ordering::AcqRel,
                Ordering::Acquire,
            ) {
                Ok(_) => return Ok(()),
                Err(actual) => current = actual, // Retry with updated value
            }
        }
    }

    /// Release reserved slots when tasks complete
    pub fn release_slots(&self, count: usize) {
        self.active_count.fetch_sub(count, Ordering::Release);
    }

    /// Take the command receiver (can only be called once)
    pub fn take_command_rx(&mut self) -> Option<mpsc::Receiver<SubagentCommand>> {
        self.command_rx.take()
    }

    /// Take the result receiver (can only be called once)
    pub fn take_result_rx(&mut self) -> Option<mpsc::Receiver<TaskResult>> {
        self.result_rx.take()
    }

    /// Get command sender clone
    pub fn command_tx(&self) -> mpsc::Sender<SubagentCommand> {
        self.command_tx.clone()
    }

    /// Get result sender clone
    pub fn result_tx(&self) -> mpsc::Sender<TaskResult> {
        self.result_tx.clone()
    }

    /// Subscribe to shutdown notifications
    pub fn subscribe_shutdown(&self) -> broadcast::Receiver<()> {
        self.shutdown_tx.subscribe()
    }

    /// Get state map reference
    pub fn state_map(&self) -> &Arc<DashMap<Uuid, SubagentState>> {
        &self.state_map
    }
}
