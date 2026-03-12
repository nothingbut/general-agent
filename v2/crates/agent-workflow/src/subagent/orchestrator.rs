//! SubagentOrchestrator - Central coordinator for subagent execution

use chrono::Utc;
use dashmap::DashMap;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use tokio::sync::{broadcast, mpsc};
use uuid::Uuid;

use super::channels::{SubagentCommand, TaskResult};
use super::config::{SubagentConfig, SubagentTaskConfig, TaskType};
use super::error::{SubagentError, SubagentResult};
use super::progress::ProgressEstimator;
use super::state::SubagentState;
use super::task::SubagentTask;

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
    pool: Option<agent_storage::Database>,
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
            pool: None,
        }
    }

    /// Set database pool for persistence
    pub fn set_database_pool(&mut self, pool: agent_storage::Database) {
        self.pool = Some(pool);
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

    /// Creates and executes a stage with multiple subagent tasks
    ///
    /// # Arguments
    ///
    /// * `parent_session_id` - ID of the parent session spawning these subagents
    /// * `tasks` - List of task descriptions (one per subagent)
    /// * `config` - Optional configuration (will use defaults if None)
    ///
    /// # Returns
    ///
    /// The stage UUID on success
    ///
    /// # Errors
    ///
    /// Returns error if:
    /// - Too many concurrent subagents would be created
    /// - Task creation fails
    pub async fn create_and_execute_stage(
        &self,
        parent_session_id: Uuid,
        tasks: Vec<String>,
        config: Option<SubagentConfig>,
    ) -> SubagentResult<Uuid> {
        // Check concurrent limit
        self.check_concurrent_limit(tasks.len())?;

        // Reserve slots atomically
        self.try_reserve_slots(tasks.len())?;

        // Generate stage ID
        let stage_id = Uuid::new_v4();
        let stage_id_str = stage_id.to_string();

        // Generate stage name with timestamp
        let stage_name = format!("Stage - {}", Utc::now().format("%H:%M:%S"));

        // Save stage to database if pool is set
        if self.pool.is_some() {
            self.save_stage(&stage_id_str, parent_session_id, &stage_name, tasks.len()).await?;
        }

        // Use provided config or default
        let base_config = config.unwrap_or_else(|| SubagentConfig {
            title: "Subagent Task".to_string(),
            initial_prompt: String::new(),
            shared_context: super::config::SharedContext::default(),
            llm_config: super::config::LLMConfig::default(),
            keep_alive: false,
            timeout: None,
        });

        // Get result sender for tasks
        let result_tx = self.result_tx.clone();

        // Clone active_count for slot release on task completion
        let active_count = self.active_count.clone();

        // Create and spawn tasks
        for (idx, task_desc) in tasks.into_iter().enumerate() {
            let session_id = Uuid::new_v4();
            let title = format!("Task {}: {}", idx + 1, task_desc);

            // Save subagent session to database if pool is set
            if self.pool.is_some() {
                self.create_subagent_session(
                    session_id,
                    parent_session_id,
                    &stage_id_str,
                    &title,
                ).await?;
            }

            // Create task config
            let mut task_config = base_config.clone();
            task_config.title = format!("{} #{}", task_config.title, idx + 1);
            task_config.initial_prompt = task_desc;

            let subagent_task_config = SubagentTaskConfig {
                id: session_id,
                config: task_config,
                parent_id: parent_session_id,
                stage_id: stage_id_str.clone(),
                priority: 0,
                task_type: TaskType::Custom,
            };

            // Create progress estimator
            let progress_estimator = ProgressEstimator::new(TaskType::Custom);

            // Create and run task
            let task = SubagentTask::new(
                session_id,
                subagent_task_config,
                self.state_map.clone(),
                progress_estimator,
            );

            // NOTE: There is a race condition window here:
            // SubagentState is created inside SubagentTask::run() (task.rs:48-53),
            // not before spawning. This means between tokio::spawn() and the state
            // insertion in run(), get_subagent_states() may return incomplete results.
            // TODO: In a future task, refactor to create state here before spawning
            // to eliminate this race condition window.

            let result_tx_clone = result_tx.clone();
            let active_count_clone = active_count.clone();
            tokio::spawn(async move {
                let result = task.run(result_tx_clone).await;

                // Always release slot when task completes (success or failure)
                active_count_clone.fetch_sub(1, Ordering::Release);

                // Log errors without swallowing them
                if let Err(e) = result {
                    tracing::error!(
                        session_id = %session_id,
                        error = %e,
                        "SubagentTask execution failed"
                    );
                }
            });
        }

        Ok(stage_id)
    }

    /// Get all subagent states for a given parent session
    pub fn get_subagent_states(&self, parent_session_id: Uuid) -> Vec<SubagentState> {
        self.state_map
            .iter()
            .filter(|entry| entry.value().parent_id() == parent_session_id)
            .map(|entry| entry.value().clone())
            .collect()
    }

    /// Get all states in the orchestrator
    pub fn get_all_states(&self) -> Vec<SubagentState> {
        self.state_map
            .iter()
            .map(|entry| entry.value().clone())
            .collect()
    }

    /// Get statistics for a specific stage
    pub fn get_stage_stats(&self, stage_id: &str) -> StageStats {
        let states: Vec<_> = self
            .state_map
            .iter()
            .filter(|entry| entry.value().stage_id() == stage_id)
            .map(|entry| entry.value().clone())
            .collect();

        let total = states.len();
        let completed = states
            .iter()
            .filter(|s| s.status() == super::models::SessionStatus::Completed)
            .count();
        let failed = states
            .iter()
            .filter(|s| s.status() == super::models::SessionStatus::Failed)
            .count();
        let running = states
            .iter()
            .filter(|s| s.status() == super::models::SessionStatus::Running)
            .count();

        StageStats {
            total,
            completed,
            failed,
            running,
        }
    }

    /// Save stage to database
    ///
    /// # Arguments
    ///
    /// * `stage_id` - Stage identifier
    /// * `parent_session_id` - Parent session UUID
    /// * `stage_name` - Human-readable stage name
    /// * `total_tasks` - Number of tasks in this stage
    ///
    /// # Errors
    ///
    /// Returns error if database pool not set or insertion fails
    async fn save_stage(
        &self,
        stage_id: &str,
        parent_session_id: Uuid,
        stage_name: &str,
        total_tasks: usize,
    ) -> SubagentResult<()> {
        let pool = self
            .pool
            .as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        sqlx::query(
            "INSERT INTO stages (id, parent_session_id, name, status, created_at, total_tasks, completed_tasks)
             VALUES (?, ?, ?, 'Running', datetime('now'), ?, 0)"
        )
        .bind(stage_id)
        .bind(parent_session_id.to_string())
        .bind(stage_name)
        .bind(total_tasks as i64)
        .execute(pool.pool())
        .await?;

        Ok(())
    }

    /// Create subagent session with dual-table insertion
    ///
    /// Inserts into both `sessions` (base session) and `subagent_sessions` (metadata) tables
    /// using a transaction to ensure atomicity.
    ///
    /// # Arguments
    ///
    /// * `session_id` - Subagent session UUID
    /// * `parent_id` - Parent session UUID
    /// * `stage_id` - Stage identifier
    /// * `title` - Session title
    ///
    /// # Errors
    ///
    /// Returns error if database pool not set or insertion fails.
    /// If either insertion fails, the transaction is rolled back automatically.
    async fn create_subagent_session(
        &self,
        session_id: Uuid,
        parent_id: Uuid,
        stage_id: &str,
        title: &str,
    ) -> SubagentResult<()> {
        let pool = self
            .pool
            .as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        // Begin transaction for atomic dual-table insertion
        let mut tx = pool.pool().begin().await?;

        // Step 1: Insert into sessions table (base session record)
        sqlx::query(
            "INSERT INTO sessions (id, title, created_at, updated_at, context)
             VALUES (?, ?, datetime('now'), datetime('now'), '{}')"
        )
        .bind(session_id.to_string())
        .bind(title)
        .execute(&mut *tx)
        .await?;

        // Step 2: Insert into subagent_sessions table (subagent metadata)
        sqlx::query(
            "INSERT INTO subagent_sessions (session_id, parent_id, session_type, status, stage_id, created_at, updated_at)
             VALUES (?, ?, 'Subagent', 'Idle', ?, datetime('now'), datetime('now'))"
        )
        .bind(session_id.to_string())
        .bind(parent_id.to_string())
        .bind(stage_id)
        .execute(&mut *tx)
        .await?;

        // Commit transaction (both inserts succeed or both fail)
        tx.commit().await?;

        Ok(())
    }

    /// Update subagent session status
    ///
    /// # Arguments
    ///
    /// * `session_id` - Subagent session UUID
    /// * `status` - New status value
    ///
    /// # Errors
    ///
    /// Returns error if database pool not set or update fails
    pub async fn update_subagent_status(
        &self,
        session_id: Uuid,
        status: &str,
    ) -> SubagentResult<()> {
        let pool = self
            .pool
            .as_ref()
            .ok_or_else(|| SubagentError::ConfigError("Database pool not set".to_string()))?;

        sqlx::query(
            "UPDATE subagent_sessions SET status = ?, updated_at = datetime('now') WHERE session_id = ?"
        )
        .bind(status)
        .bind(session_id.to_string())
        .execute(pool.pool())
        .await?;

        Ok(())
    }
}

/// Statistics for a stage
#[derive(Debug, Clone)]
pub struct StageStats {
    pub total: usize,
    pub completed: usize,
    pub failed: usize,
    pub running: usize,
}
