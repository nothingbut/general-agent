use async_trait::async_trait;
use chrono::Utc;
use std::sync::Arc;
use std::time::Instant;

use crate::error::Result;
use crate::models::*;
use crate::registry::AgentRegistry;
use crate::traits::Coordinator;

pub struct PipelineCoordinator {
    registry: Arc<AgentRegistry>,
}

impl PipelineCoordinator {
    pub fn new(registry: Arc<AgentRegistry>) -> Self {
        Self { registry }
    }
}

#[async_trait]
impl Coordinator for PipelineCoordinator {
    async fn execute(&self, task: &mut CollaborationTask) -> Result<AggregatedResult> {
        let start = Instant::now();
        task.status = TaskStatus::Running;

        let mut agent_results = Vec::new();
        let mut pipeline_data = serde_json::json!({});

        for subtask in &mut task.subtasks {
            subtask.status = TaskStatus::Running;
            subtask.started_at = Some(Utc::now());

            let agent = self.registry.get(&subtask.assigned_agent)?;

            let mut context = subtask.context.clone();
            if let serde_json::Value::Object(ref mut map) = context {
                map.insert("pipeline_data".to_string(), pipeline_data.clone());
            }

            let task_start = Instant::now();
            match agent.execute_task(&subtask.task, context).await {
                Ok(output) => {
                    let execution_time = task_start.elapsed();
                    subtask.status = TaskStatus::Completed;
                    subtask.result = Some(output.clone());
                    subtask.completed_at = Some(Utc::now());
                    subtask.execution_time = Some(execution_time);

                    if let serde_json::Value::Object(ref mut map) = pipeline_data {
                        map.insert(
                            subtask.assigned_agent.clone(),
                            serde_json::Value::String(output.clone()),
                        );
                    }

                    agent_results.push(AgentResult {
                        agent_id: subtask.assigned_agent.clone(),
                        subtask_id: subtask.id,
                        result: output,
                        confidence: None,
                        execution_time,
                    });
                }
                Err(e) => {
                    let execution_time = task_start.elapsed();
                    subtask.status = TaskStatus::Failed;
                    subtask.error = Some(e.to_string());
                    subtask.completed_at = Some(Utc::now());
                    subtask.execution_time = Some(execution_time);

                    task.status = TaskStatus::Failed;

                    let aggregated = AggregatedResult {
                        summary: format!(
                            "Pipeline failed at stage '{}': {}",
                            subtask.assigned_agent, e
                        ),
                        agent_results,
                        metadata: serde_json::json!({
                            "strategy": "pipeline",
                            "failed_at": subtask.assigned_agent,
                            "completed_stages": task.completed_subtasks(),
                            "total_stages": task.subtasks.len(),
                        }),
                        total_execution_time: start.elapsed(),
                    };

                    task.result = Some(aggregated.clone());
                    return Ok(aggregated);
                }
            }
        }

        let total_execution_time = start.elapsed();
        task.status = TaskStatus::Completed;
        task.completed_at = Some(Utc::now());

        let summary = agent_results
            .last()
            .map(|r| r.result.clone())
            .unwrap_or_else(|| "Empty pipeline".to_string());

        let aggregated = AggregatedResult {
            summary,
            agent_results,
            metadata: serde_json::json!({
                "strategy": "pipeline",
                "pipeline_data": pipeline_data,
                "total_stages": task.subtasks.len(),
                "completed_stages": task.completed_subtasks(),
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

    struct TransformAgent {
        info: AgentInfo,
        prefix: String,
    }

    impl TransformAgent {
        fn new(id: &str, prefix: &str) -> Self {
            Self {
                info: AgentInfo::new(id, id),
                prefix: prefix.to_string(),
            }
        }
    }

    #[async_trait]
    impl Agent for TransformAgent {
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
                .get("pipeline_data")
                .and_then(|v| v.as_object())
                .map(|obj| {
                    obj.values()
                        .filter_map(|v| v.as_str())
                        .collect::<Vec<_>>()
                        .join(", ")
                })
                .unwrap_or_default();

            if prev.is_empty() {
                Ok(format!("{}:{}", self.prefix, task))
            } else {
                Ok(format!("{}:{}[{}]", self.prefix, task, prev))
            }
        }
    }

    #[tokio::test]
    async fn test_pipeline_data_flows_through() {
        let registry = Arc::new(AgentRegistry::new());
        registry
            .register(Arc::new(TransformAgent::new("extract", "E")))
            .unwrap();
        registry
            .register(Arc::new(TransformAgent::new("transform", "T")))
            .unwrap();
        registry
            .register(Arc::new(TransformAgent::new("load", "L")))
            .unwrap();

        let coordinator = PipelineCoordinator::new(registry);

        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new(
            "ETL Pipeline",
            "Extract-Transform-Load",
            CollaborationStrategy::Pipeline,
        )
        .with_subtasks(vec![
            SubTask::new(parent_id, "extract", "raw data", serde_json::json!({})),
            SubTask::new(parent_id, "transform", "clean data", serde_json::json!({})),
            SubTask::new(parent_id, "load", "store data", serde_json::json!({})),
        ]);

        let result = coordinator.execute(&mut task).await.unwrap();

        assert_eq!(task.status, TaskStatus::Completed);
        assert_eq!(task.completed_subtasks(), 3);
        assert_eq!(result.agent_results.len(), 3);
        // Final stage should have data from previous stages
        assert!(result.summary.contains("L:store data"));
    }
}
