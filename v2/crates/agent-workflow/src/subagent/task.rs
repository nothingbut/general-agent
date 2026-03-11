//! SubagentTask execution unit

use dashmap::DashMap;
use std::sync::Arc;
use std::time::Instant;
use tokio::sync::mpsc;
use uuid::Uuid;

use super::channels::{ResultMetadata, TaskResult};
use super::config::SubagentTaskConfig;
use super::error::SubagentResult;
use super::models::SessionStatus;
use super::progress::ProgressEstimator;
use super::state::SubagentState;

/// SubagentTask execution unit
pub struct SubagentTask {
    session_id: Uuid,
    config: SubagentTaskConfig,
    state: Arc<DashMap<Uuid, SubagentState>>,
    progress_estimator: ProgressEstimator,
}

impl SubagentTask {
    /// Creates a new SubagentTask
    pub fn new(
        session_id: Uuid,
        config: SubagentTaskConfig,
        state: Arc<DashMap<Uuid, SubagentState>>,
        progress_estimator: ProgressEstimator,
    ) -> Self {
        Self {
            session_id,
            config,
            state,
            progress_estimator,
        }
    }

    /// Runs the subagent task asynchronously
    pub async fn run(
        self,
        result_tx: mpsc::Sender<TaskResult>,
    ) -> SubagentResult<()> {
        let start_time = Instant::now();

        // Initialize state
        let initial_state = SubagentState::new(
            self.session_id,
            self.config.parent_id,
            self.config.stage_id.clone(),
        )?;
        self.state.insert(self.session_id, initial_state);

        // Simulate task execution
        let final_state = self.state.get(&self.session_id)
            .unwrap()
            .clone()
            .with_status(SessionStatus::Completed);
        self.state.insert(self.session_id, final_state);

        // Send result
        let result = TaskResult {
            session_id: self.session_id,
            status: SessionStatus::Completed,
            output: "Task completed".to_string(),
            metadata: ResultMetadata {
                execution_time: start_time.elapsed(),
                token_count: 0,
                model_used: "test".to_string(),
                error_count: 0,
                tool_calls: vec![],
                memory_used: 0,
            },
        };

        result_tx.send(result).await.map_err(|_| {
            super::error::SubagentError::ChannelClosed
        })?;

        Ok(())
    }

    /// Sends status updates to parent
    async fn send_status_update(&self) -> SubagentResult<()> {
        // Status updates handled via shared state
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::subagent::{LLMConfig, SharedContext, SubagentConfig, TaskType};

    fn create_test_config(session_id: Uuid, parent_id: Uuid) -> SubagentTaskConfig {
        SubagentTaskConfig {
            id: session_id,
            parent_id,
            stage_id: "test-stage".to_string(),
            config: SubagentConfig {
                title: "Test Task".to_string(),
                initial_prompt: "Test prompt".to_string(),
                shared_context: SharedContext::default(),
                llm_config: LLMConfig::default(),
                keep_alive: false,
                timeout: None,
            },
            priority: 0,
            task_type: TaskType::Testing,
        }
    }

    #[tokio::test]
    async fn test_subagent_task_creation() {
        let session_id = Uuid::new_v4();
        let parent_id = Uuid::new_v4();
        let config = create_test_config(session_id, parent_id);
        let state = Arc::new(DashMap::new());
        let progress_estimator = ProgressEstimator::new(TaskType::Testing);

        let task = SubagentTask::new(session_id, config, state, progress_estimator);
        assert_eq!(task.session_id, session_id);
    }

    #[tokio::test]
    async fn test_run_execution() {
        let session_id = Uuid::new_v4();
        let parent_id = Uuid::new_v4();
        let config = create_test_config(session_id, parent_id);
        let state = Arc::new(DashMap::new());
        let progress_estimator = ProgressEstimator::new(TaskType::Testing);
        let (result_tx, mut result_rx) = mpsc::channel(10);

        let task = SubagentTask::new(session_id, config, state.clone(), progress_estimator);

        tokio::spawn(async move {
            task.run(result_tx).await
        });

        let result = tokio::time::timeout(
            std::time::Duration::from_secs(5),
            result_rx.recv()
        ).await;

        assert!(result.is_ok());
        let task_result = result.unwrap().unwrap();
        assert_eq!(task_result.status, SessionStatus::Completed);
    }
}
