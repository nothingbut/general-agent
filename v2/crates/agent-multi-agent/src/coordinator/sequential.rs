use async_trait::async_trait;
use chrono::Utc;
use std::sync::Arc;
use std::time::Instant;

use crate::error::Result;
use crate::models::*;
use crate::registry::AgentRegistry;
use crate::traits::Coordinator;

pub struct SequentialCoordinator {
    registry: Arc<AgentRegistry>,
    stop_on_failure: bool,
}

impl SequentialCoordinator {
    pub fn new(registry: Arc<AgentRegistry>) -> Self {
        Self {
            registry,
            stop_on_failure: true,
        }
    }

    pub fn with_stop_on_failure(mut self, stop: bool) -> Self {
        self.stop_on_failure = stop;
        self
    }
}

#[async_trait]
impl Coordinator for SequentialCoordinator {
    async fn execute(&self, task: &mut CollaborationTask) -> Result<AggregatedResult> {
        let start = Instant::now();
        task.status = TaskStatus::Running;

        let mut agent_results = Vec::new();
        let mut previous_output: Option<String> = None;

        for subtask in &mut task.subtasks {
            subtask.status = TaskStatus::Running;
            subtask.started_at = Some(Utc::now());

            let agent = self.registry.get(&subtask.assigned_agent)?;

            let mut context = subtask.context.clone();
            if let Some(ref prev) = previous_output {
                if let serde_json::Value::Object(ref mut map) = context {
                    map.insert(
                        "previous_result".to_string(),
                        serde_json::Value::String(prev.clone()),
                    );
                }
            }

            let task_start = Instant::now();
            match agent.execute_task(&subtask.task, context).await {
                Ok(output) => {
                    let execution_time = task_start.elapsed();
                    subtask.status = TaskStatus::Completed;
                    subtask.result = Some(output.clone());
                    subtask.completed_at = Some(Utc::now());
                    subtask.execution_time = Some(execution_time);

                    agent_results.push(AgentResult {
                        agent_id: subtask.assigned_agent.clone(),
                        subtask_id: subtask.id,
                        result: output.clone(),
                        confidence: None,
                        execution_time,
                    });

                    previous_output = Some(output);
                }
                Err(e) => {
                    let execution_time = task_start.elapsed();
                    subtask.status = TaskStatus::Failed;
                    subtask.error = Some(e.to_string());
                    subtask.completed_at = Some(Utc::now());
                    subtask.execution_time = Some(execution_time);

                    agent_results.push(AgentResult {
                        agent_id: subtask.assigned_agent.clone(),
                        subtask_id: subtask.id,
                        result: String::new(),
                        confidence: Some(0.0),
                        execution_time,
                    });

                    if self.stop_on_failure {
                        break;
                    }
                }
            }
        }

        let total_execution_time = start.elapsed();

        let summary = if task.failed_subtasks() > 0 && self.stop_on_failure {
            task.status = TaskStatus::Failed;
            format!(
                "Sequential execution stopped: {}/{} completed",
                task.completed_subtasks(),
                task.subtasks.len()
            )
        } else {
            task.status = TaskStatus::Completed;
            task.completed_at = Some(Utc::now());
            previous_output.unwrap_or_else(|| "No results".to_string())
        };

        let aggregated = AggregatedResult {
            summary,
            agent_results,
            metadata: serde_json::json!({
                "strategy": "sequential",
                "total_subtasks": task.subtasks.len(),
                "completed": task.completed_subtasks(),
                "failed": task.failed_subtasks(),
                "stop_on_failure": self.stop_on_failure,
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

    struct PipeAgent {
        info: AgentInfo,
    }

    impl PipeAgent {
        fn new(id: &str) -> Self {
            Self {
                info: AgentInfo::new(id, id),
            }
        }
    }

    #[async_trait]
    impl Agent for PipeAgent {
        fn info(&self) -> &AgentInfo {
            &self.info
        }

        async fn handle_message(&self, msg: AgentMessage) -> crate::Result<AgentMessage> {
            Ok(AgentMessage::task_response(
                &self.info.id,
                &msg.from_agent,
                "ok",
                serde_json::json!({}),
                msg.correlation_id,
            ))
        }

        async fn execute_task(
            &self,
            task: &str,
            context: serde_json::Value,
        ) -> crate::Result<String> {
            let prev = context
                .get("previous_result")
                .and_then(|v| v.as_str())
                .unwrap_or("");

            if prev.is_empty() {
                Ok(format!("[{}] {}", self.info.id, task))
            } else {
                Ok(format!("[{}] {} (prev: {})", self.info.id, task, prev))
            }
        }
    }

    #[tokio::test]
    async fn test_sequential_with_chaining() {
        let registry = Arc::new(AgentRegistry::new());
        registry
            .register(Arc::new(PipeAgent::new("step1")))
            .unwrap();
        registry
            .register(Arc::new(PipeAgent::new("step2")))
            .unwrap();

        let coordinator = SequentialCoordinator::new(registry);

        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new(
            "Sequential test",
            "Chained execution",
            CollaborationStrategy::Sequential,
        )
        .with_subtasks(vec![
            SubTask::new(parent_id, "step1", "gather data", serde_json::json!({})),
            SubTask::new(parent_id, "step2", "process data", serde_json::json!({})),
        ]);

        let result = coordinator.execute(&mut task).await.unwrap();

        assert_eq!(task.status, TaskStatus::Completed);
        assert_eq!(task.completed_subtasks(), 2);
        assert!(result.summary.contains("[step2] process data"));
        assert!(result.summary.contains("prev: [step1] gather data"));
    }
}
