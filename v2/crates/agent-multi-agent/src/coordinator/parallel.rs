use async_trait::async_trait;
use chrono::Utc;
use std::sync::Arc;
use std::time::Instant;

use crate::error::Result;
use crate::models::*;
use crate::registry::AgentRegistry;
use crate::traits::Coordinator;

pub struct ParallelCoordinator {
    registry: Arc<AgentRegistry>,
    timeout: std::time::Duration,
}

impl ParallelCoordinator {
    pub fn new(registry: Arc<AgentRegistry>) -> Self {
        Self {
            registry,
            timeout: std::time::Duration::from_secs(300),
        }
    }

    pub fn with_timeout(mut self, timeout: std::time::Duration) -> Self {
        self.timeout = timeout;
        self
    }
}

#[async_trait]
impl Coordinator for ParallelCoordinator {
    async fn execute(&self, task: &mut CollaborationTask) -> Result<AggregatedResult> {
        let start = Instant::now();
        task.status = TaskStatus::Running;

        let mut handles = Vec::new();

        for subtask in &mut task.subtasks {
            subtask.status = TaskStatus::Running;
            subtask.started_at = Some(Utc::now());

            let agent = self.registry.get(&subtask.assigned_agent)?;
            let task_desc = subtask.task.clone();
            let context = subtask.context.clone();
            let subtask_id = subtask.id;
            let agent_id = subtask.assigned_agent.clone();
            let timeout = self.timeout;

            let handle = tokio::spawn(async move {
                let task_start = Instant::now();
                let result = tokio::time::timeout(
                    timeout,
                    agent.execute_task(&task_desc, context),
                )
                .await;

                let execution_time = task_start.elapsed();

                match result {
                    Ok(Ok(output)) => AgentResult {
                        agent_id,
                        subtask_id,
                        result: output,
                        confidence: None,
                        execution_time,
                    },
                    Ok(Err(_)) => AgentResult {
                        agent_id,
                        subtask_id,
                        result: String::new(),
                        confidence: Some(0.0),
                        execution_time,
                    },
                    Err(_) => AgentResult {
                        agent_id,
                        subtask_id,
                        result: String::new(),
                        confidence: Some(0.0),
                        execution_time,
                    },
                }
            });

            handles.push((subtask.id, handle));
        }

        let mut agent_results = Vec::new();
        for (subtask_id, handle) in handles {
            match handle.await {
                Ok(result) => {
                    let succeeded = !result.result.is_empty();
                    if let Some(subtask) = task.subtasks.iter_mut().find(|s| s.id == subtask_id) {
                        if succeeded {
                            subtask.status = TaskStatus::Completed;
                            subtask.result = Some(result.result.clone());
                        } else {
                            subtask.status = TaskStatus::Failed;
                            subtask.error = Some("Execution failed or timed out".to_string());
                        }
                        subtask.completed_at = Some(Utc::now());
                        subtask.execution_time = Some(result.execution_time);
                    }
                    agent_results.push(result);
                }
                Err(e) => {
                    if let Some(subtask) = task.subtasks.iter_mut().find(|s| s.id == subtask_id) {
                        subtask.status = TaskStatus::Failed;
                        subtask.error = Some(format!("Join error: {}", e));
                        subtask.completed_at = Some(Utc::now());
                    }
                }
            }
        }

        let total_execution_time = start.elapsed();
        let successful_results: Vec<&str> = agent_results
            .iter()
            .filter(|r| !r.result.is_empty())
            .map(|r| r.result.as_str())
            .collect();

        let summary = if successful_results.is_empty() {
            task.status = TaskStatus::Failed;
            "All subtasks failed".to_string()
        } else {
            task.status = TaskStatus::Completed;
            task.completed_at = Some(Utc::now());
            successful_results.join("\n---\n")
        };

        let aggregated = AggregatedResult {
            summary,
            agent_results,
            metadata: serde_json::json!({
                "strategy": "parallel",
                "total_subtasks": task.subtasks.len(),
                "completed": task.completed_subtasks(),
                "failed": task.failed_subtasks(),
            }),
            total_execution_time,
        };

        task.result = Some(aggregated.clone());
        Ok(aggregated)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{AgentInfo, AgentMessage};
    use crate::traits::Agent;
    use async_trait::async_trait;
    use uuid::Uuid;

    struct EchoAgent {
        info: AgentInfo,
    }

    impl EchoAgent {
        fn new(id: &str) -> Self {
            Self {
                info: AgentInfo::new(id, id),
            }
        }
    }

    #[async_trait]
    impl Agent for EchoAgent {
        fn info(&self) -> &AgentInfo {
            &self.info
        }

        async fn handle_message(&self, msg: AgentMessage) -> crate::Result<AgentMessage> {
            Ok(AgentMessage::task_response(
                &self.info.id,
                &msg.from_agent,
                "echo",
                serde_json::json!({}),
                msg.correlation_id,
            ))
        }

        async fn execute_task(
            &self,
            task: &str,
            _context: serde_json::Value,
        ) -> crate::Result<String> {
            Ok(format!("[{}] {}", self.info.id, task))
        }
    }

    #[tokio::test]
    async fn test_parallel_execution() {
        let registry = Arc::new(AgentRegistry::new());
        registry
            .register(Arc::new(EchoAgent::new("a1")))
            .unwrap();
        registry
            .register(Arc::new(EchoAgent::new("a2")))
            .unwrap();

        let coordinator = ParallelCoordinator::new(registry);

        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new(
            "Test parallel",
            "Test desc",
            CollaborationStrategy::Parallel,
        )
        .with_subtasks(vec![
            SubTask::new(parent_id, "a1", "search web", serde_json::json!({})),
            SubTask::new(parent_id, "a2", "analyze data", serde_json::json!({})),
        ]);

        let result = coordinator.execute(&mut task).await.unwrap();

        assert_eq!(task.status, TaskStatus::Completed);
        assert_eq!(task.completed_subtasks(), 2);
        assert_eq!(result.agent_results.len(), 2);
        assert!(result.summary.contains("[a1] search web"));
        assert!(result.summary.contains("[a2] analyze data"));
    }
}
