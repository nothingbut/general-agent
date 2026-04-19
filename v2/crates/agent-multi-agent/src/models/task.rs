use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::time::Duration;
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum TaskStatus {
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CollaborationTask {
    pub id: Uuid,
    pub title: String,
    pub description: String,
    pub subtasks: Vec<SubTask>,
    pub strategy: CollaborationStrategy,
    pub status: TaskStatus,
    pub created_at: DateTime<Utc>,
    pub completed_at: Option<DateTime<Utc>>,
    pub result: Option<AggregatedResult>,
}

impl CollaborationTask {
    pub fn new(
        title: impl Into<String>,
        description: impl Into<String>,
        strategy: CollaborationStrategy,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            title: title.into(),
            description: description.into(),
            subtasks: Vec::new(),
            strategy,
            status: TaskStatus::Pending,
            created_at: Utc::now(),
            completed_at: None,
            result: None,
        }
    }

    pub fn with_subtasks(mut self, subtasks: Vec<SubTask>) -> Self {
        self.subtasks = subtasks;
        self
    }

    pub fn completed_subtasks(&self) -> usize {
        self.subtasks
            .iter()
            .filter(|s| s.status == TaskStatus::Completed)
            .count()
    }

    pub fn failed_subtasks(&self) -> usize {
        self.subtasks
            .iter()
            .filter(|s| s.status == TaskStatus::Failed)
            .count()
    }

    pub fn is_done(&self) -> bool {
        matches!(
            self.status,
            TaskStatus::Completed | TaskStatus::Failed | TaskStatus::Cancelled
        )
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubTask {
    pub id: Uuid,
    pub parent_id: Uuid,
    pub assigned_agent: String,
    pub task: String,
    pub context: serde_json::Value,
    pub status: TaskStatus,
    pub result: Option<String>,
    pub error: Option<String>,
    pub started_at: Option<DateTime<Utc>>,
    pub completed_at: Option<DateTime<Utc>>,
    pub execution_time: Option<Duration>,
}

impl SubTask {
    pub fn new(
        parent_id: Uuid,
        assigned_agent: impl Into<String>,
        task: impl Into<String>,
        context: serde_json::Value,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            parent_id,
            assigned_agent: assigned_agent.into(),
            task: task.into(),
            context,
            status: TaskStatus::Pending,
            result: None,
            error: None,
            started_at: None,
            completed_at: None,
            execution_time: None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum CollaborationStrategy {
    Parallel,
    Sequential,
    Voting { min_votes: usize },
    Pipeline,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AggregatedResult {
    pub summary: String,
    pub agent_results: Vec<AgentResult>,
    pub metadata: serde_json::Value,
    pub total_execution_time: Duration,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AgentResult {
    pub agent_id: String,
    pub subtask_id: Uuid,
    pub result: String,
    pub confidence: Option<f64>,
    pub execution_time: Duration,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_collaboration_task_builder() {
        let task = CollaborationTask::new(
            "Analyze codebase",
            "Perform multi-perspective analysis",
            CollaborationStrategy::Parallel,
        );

        assert_eq!(task.title, "Analyze codebase");
        assert_eq!(task.status, TaskStatus::Pending);
        assert!(task.subtasks.is_empty());
        assert!(!task.is_done());
    }

    #[test]
    fn test_subtask_tracking() {
        let parent_id = Uuid::new_v4();
        let mut task = CollaborationTask::new("Test", "Test", CollaborationStrategy::Parallel);
        task.subtasks = vec![
            {
                let mut s = SubTask::new(parent_id, "agent-a", "search", serde_json::json!({}));
                s.status = TaskStatus::Completed;
                s
            },
            {
                let mut s = SubTask::new(parent_id, "agent-b", "analyze", serde_json::json!({}));
                s.status = TaskStatus::Failed;
                s
            },
            SubTask::new(parent_id, "agent-c", "summarize", serde_json::json!({})),
        ];

        assert_eq!(task.completed_subtasks(), 1);
        assert_eq!(task.failed_subtasks(), 1);
    }

    #[test]
    fn test_task_done_states() {
        let mut task =
            CollaborationTask::new("T", "D", CollaborationStrategy::Sequential);

        assert!(!task.is_done());

        task.status = TaskStatus::Completed;
        assert!(task.is_done());

        task.status = TaskStatus::Failed;
        assert!(task.is_done());

        task.status = TaskStatus::Cancelled;
        assert!(task.is_done());

        task.status = TaskStatus::Running;
        assert!(!task.is_done());
    }

    #[test]
    fn test_collaboration_strategy_serialization() {
        let strategies = vec![
            CollaborationStrategy::Parallel,
            CollaborationStrategy::Sequential,
            CollaborationStrategy::Voting { min_votes: 3 },
            CollaborationStrategy::Pipeline,
        ];

        for strategy in strategies {
            let json = serde_json::to_string(&strategy).unwrap();
            let restored: CollaborationStrategy = serde_json::from_str(&json).unwrap();
            assert_eq!(strategy, restored);
        }
    }
}
