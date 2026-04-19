use async_trait::async_trait;
use chrono::Utc;
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Instant;

use crate::error::{MultiAgentError, Result};
use crate::models::*;
use crate::registry::AgentRegistry;
use crate::traits::Coordinator;

pub struct VotingCoordinator {
    registry: Arc<AgentRegistry>,
    min_votes: usize,
}

impl VotingCoordinator {
    pub fn new(registry: Arc<AgentRegistry>, min_votes: usize) -> Self {
        Self {
            registry,
            min_votes,
        }
    }
}

#[async_trait]
impl Coordinator for VotingCoordinator {
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

            let handle = tokio::spawn(async move {
                let task_start = Instant::now();
                let result = agent.execute_task(&task_desc, context).await;
                let execution_time = task_start.elapsed();

                match result {
                    Ok(output) => AgentResult {
                        agent_id,
                        subtask_id,
                        result: output,
                        confidence: None,
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
            if let Ok(result) = handle.await {
                let succeeded = !result.result.is_empty();
                if let Some(subtask) = task.subtasks.iter_mut().find(|s| s.id == subtask_id) {
                    if succeeded {
                        subtask.status = TaskStatus::Completed;
                        subtask.result = Some(result.result.clone());
                    } else {
                        subtask.status = TaskStatus::Failed;
                    }
                    subtask.completed_at = Some(Utc::now());
                    subtask.execution_time = Some(result.execution_time);
                }
                agent_results.push(result);
            }
        }

        let successful_count = agent_results.iter().filter(|r| !r.result.is_empty()).count();

        if successful_count < self.min_votes {
            task.status = TaskStatus::Failed;
            return Err(MultiAgentError::StrategyError(format!(
                "Not enough votes: got {}, need {}",
                successful_count,
                self.min_votes
            )));
        }

        let mut vote_counts: HashMap<String, usize> = HashMap::new();
        for r in &agent_results {
            if !r.result.is_empty() {
                let normalized = r.result.trim().to_string();
                *vote_counts.entry(normalized).or_insert(0) += 1;
            }
        }

        let winner = vote_counts
            .into_iter()
            .max_by_key(|(_, count)| *count)
            .map(|(result, _)| result)
            .unwrap_or_default();

        let total_execution_time = start.elapsed();

        task.status = TaskStatus::Completed;
        task.completed_at = Some(Utc::now());

        let aggregated = AggregatedResult {
            summary: winner,
            agent_results,
            metadata: serde_json::json!({
                "strategy": "voting",
                "min_votes": self.min_votes,
                "total_votes": successful_count,
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

    struct FixedAnswerAgent {
        info: AgentInfo,
        answer: String,
    }

    impl FixedAnswerAgent {
        fn new(id: &str, answer: &str) -> Self {
            Self {
                info: AgentInfo::new(id, id),
                answer: answer.to_string(),
            }
        }
    }

    #[async_trait]
    impl Agent for FixedAnswerAgent {
        fn info(&self) -> &AgentInfo {
            &self.info
        }

        async fn handle_message(&self, msg: AgentMessage) -> crate::Result<AgentMessage> {
            Ok(AgentMessage::task_response(
                &self.info.id,
                &msg.from_agent,
                &self.answer,
                serde_json::json!({}),
                msg.correlation_id,
            ))
        }

        async fn execute_task(
            &self,
            _task: &str,
            _context: serde_json::Value,
        ) -> crate::Result<String> {
            Ok(self.answer.clone())
        }
    }

    #[tokio::test]
    async fn test_voting_majority_wins() {
        let registry = Arc::new(AgentRegistry::new());
        registry
            .register(Arc::new(FixedAnswerAgent::new("v1", "yes")))
            .unwrap();
        registry
            .register(Arc::new(FixedAnswerAgent::new("v2", "yes")))
            .unwrap();
        registry
            .register(Arc::new(FixedAnswerAgent::new("v3", "no")))
            .unwrap();

        let coordinator = VotingCoordinator::new(registry, 2);

        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new(
            "Vote test",
            "Voting desc",
            CollaborationStrategy::Voting { min_votes: 2 },
        )
        .with_subtasks(vec![
            SubTask::new(parent_id, "v1", "decide", serde_json::json!({})),
            SubTask::new(parent_id, "v2", "decide", serde_json::json!({})),
            SubTask::new(parent_id, "v3", "decide", serde_json::json!({})),
        ]);

        let result = coordinator.execute(&mut task).await.unwrap();

        assert_eq!(task.status, TaskStatus::Completed);
        assert_eq!(result.summary, "yes");
    }

    #[tokio::test]
    async fn test_voting_insufficient_votes() {
        let registry = Arc::new(AgentRegistry::new());
        registry
            .register(Arc::new(FixedAnswerAgent::new("v1", "yes")))
            .unwrap();

        let coordinator = VotingCoordinator::new(registry, 3);

        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new(
            "Vote test",
            "Voting desc",
            CollaborationStrategy::Voting { min_votes: 3 },
        )
        .with_subtasks(vec![SubTask::new(
            parent_id,
            "v1",
            "decide",
            serde_json::json!({}),
        )]);

        let err = coordinator.execute(&mut task).await.unwrap_err();
        assert!(matches!(err, MultiAgentError::StrategyError(_)));
    }
}
