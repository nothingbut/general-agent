use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::fmt;
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
pub enum ScheduleType {
    Cron = 0,
    Natural = 1,
}

impl ScheduleType {
    pub fn from_i32(v: i32) -> Option<Self> {
        match v {
            0 => Some(Self::Cron),
            1 => Some(Self::Natural),
            _ => None,
        }
    }
}

impl fmt::Display for ScheduleType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Cron => write!(f, "cron"),
            Self::Natural => write!(f, "natural"),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
pub enum TaskType {
    SkillInvocation = 0,
    MemoryReminder = 1,
    CustomCommand = 2,
}

impl TaskType {
    pub fn from_i32(v: i32) -> Option<Self> {
        match v {
            0 => Some(Self::SkillInvocation),
            1 => Some(Self::MemoryReminder),
            2 => Some(Self::CustomCommand),
            _ => None,
        }
    }
}

impl fmt::Display for TaskType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::SkillInvocation => write!(f, "skill"),
            Self::MemoryReminder => write!(f, "reminder"),
            Self::CustomCommand => write!(f, "command"),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
pub enum TaskStatus {
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Paused = 4,
}

impl TaskStatus {
    pub fn from_i32(v: i32) -> Option<Self> {
        match v {
            0 => Some(Self::Pending),
            1 => Some(Self::Running),
            2 => Some(Self::Completed),
            3 => Some(Self::Failed),
            4 => Some(Self::Paused),
            _ => None,
        }
    }

    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Pending => "pending",
            Self::Running => "running",
            Self::Completed => "completed",
            Self::Failed => "failed",
            Self::Paused => "paused",
        }
    }

    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_lowercase().as_str() {
            "pending" => Some(Self::Pending),
            "running" => Some(Self::Running),
            "completed" => Some(Self::Completed),
            "failed" => Some(Self::Failed),
            "paused" => Some(Self::Paused),
            _ => None,
        }
    }
}

impl fmt::Display for TaskStatus {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.as_str())
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[repr(i32)]
pub enum ExecutionStatus {
    Success = 0,
    Failed = 1,
    Timeout = 2,
}

impl ExecutionStatus {
    pub fn from_i32(v: i32) -> Option<Self> {
        match v {
            0 => Some(Self::Success),
            1 => Some(Self::Failed),
            2 => Some(Self::Timeout),
            _ => None,
        }
    }
}

impl fmt::Display for ExecutionStatus {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Success => write!(f, "success"),
            Self::Failed => write!(f, "failed"),
            Self::Timeout => write!(f, "timeout"),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskPayload {
    pub skill_name: Option<String>,
    pub parameters: Option<serde_json::Value>,
    pub reminder_content: Option<String>,
    pub command: Option<String>,
}

impl Default for TaskPayload {
    fn default() -> Self {
        Self {
            skill_name: None,
            parameters: None,
            reminder_content: None,
            command: None,
        }
    }
}

impl TaskPayload {
    pub fn skill(name: String, params: Option<serde_json::Value>) -> Self {
        Self {
            skill_name: Some(name),
            parameters: params,
            ..Default::default()
        }
    }

    pub fn reminder(content: String) -> Self {
        Self {
            reminder_content: Some(content),
            ..Default::default()
        }
    }

    pub fn command(cmd: String) -> Self {
        Self {
            command: Some(cmd),
            ..Default::default()
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScheduledTask {
    pub id: Uuid,
    pub name: String,
    pub description: Option<String>,
    pub owner_id: String,
    pub schedule: String,
    pub schedule_type: ScheduleType,
    pub task_type: TaskType,
    pub task_payload: TaskPayload,
    pub status: TaskStatus,
    pub max_retries: i32,
    pub timeout_seconds: i32,
    pub created_at: DateTime<Utc>,
    pub updated_at: Option<DateTime<Utc>>,
    pub last_execution_at: Option<DateTime<Utc>>,
    pub next_execution_at: Option<DateTime<Utc>>,
    pub execution_count: i32,
}

impl ScheduledTask {
    pub fn new(
        name: String,
        owner_id: String,
        schedule: String,
        schedule_type: ScheduleType,
        task_type: TaskType,
        payload: TaskPayload,
    ) -> Self {
        Self {
            id: Uuid::new_v4(),
            name,
            description: None,
            owner_id,
            schedule,
            schedule_type,
            task_type,
            task_payload: payload,
            status: TaskStatus::Pending,
            max_retries: 3,
            timeout_seconds: 300,
            created_at: Utc::now(),
            updated_at: None,
            last_execution_at: None,
            next_execution_at: None,
            execution_count: 0,
        }
    }

    pub fn with_description(mut self, desc: String) -> Self {
        self.description = Some(desc);
        self
    }

    pub fn with_max_retries(mut self, retries: i32) -> Self {
        self.max_retries = retries;
        self
    }

    pub fn with_timeout(mut self, seconds: i32) -> Self {
        self.timeout_seconds = seconds;
        self
    }

    pub fn is_active(&self) -> bool {
        matches!(self.status, TaskStatus::Pending | TaskStatus::Running)
    }

    pub fn is_due(&self) -> bool {
        match self.next_execution_at {
            Some(next) => next <= Utc::now() && self.is_active(),
            None => false,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskExecution {
    pub id: Uuid,
    pub task_id: Uuid,
    pub started_at: DateTime<Utc>,
    pub completed_at: Option<DateTime<Utc>>,
    pub status: ExecutionStatus,
    pub result: Option<String>,
    pub error_message: Option<String>,
    pub retry_count: i32,
}

impl TaskExecution {
    pub fn new(task_id: Uuid) -> Self {
        Self {
            id: Uuid::new_v4(),
            task_id,
            started_at: Utc::now(),
            completed_at: None,
            status: ExecutionStatus::Success,
            result: None,
            error_message: None,
            retry_count: 0,
        }
    }

    pub fn mark_success(mut self, result: Option<String>) -> Self {
        self.completed_at = Some(Utc::now());
        self.status = ExecutionStatus::Success;
        self.result = result;
        self
    }

    pub fn mark_failed(mut self, error: String, retry_count: i32) -> Self {
        self.completed_at = Some(Utc::now());
        self.status = ExecutionStatus::Failed;
        self.error_message = Some(error);
        self.retry_count = retry_count;
        self
    }

    pub fn mark_timeout(mut self) -> Self {
        self.completed_at = Some(Utc::now());
        self.status = ExecutionStatus::Timeout;
        self
    }
}

#[derive(Debug, Clone, Default)]
pub struct TaskStats {
    pub total_tasks: i64,
    pub active_tasks: i64,
    pub paused_tasks: i64,
    pub completed_tasks: i64,
    pub failed_tasks: i64,
    pub total_executions: i64,
    pub successful_executions: i64,
    pub failed_executions: i64,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_schedule_type() {
        assert_eq!(ScheduleType::from_i32(0), Some(ScheduleType::Cron));
        assert_eq!(ScheduleType::from_i32(1), Some(ScheduleType::Natural));
        assert_eq!(ScheduleType::from_i32(99), None);
        assert_eq!(ScheduleType::Cron.to_string(), "cron");
    }

    #[test]
    fn test_task_type() {
        assert_eq!(TaskType::from_i32(0), Some(TaskType::SkillInvocation));
        assert_eq!(TaskType::from_i32(1), Some(TaskType::MemoryReminder));
        assert_eq!(TaskType::from_i32(2), Some(TaskType::CustomCommand));
        assert_eq!(TaskType::from_i32(99), None);
    }

    #[test]
    fn test_task_status() {
        assert_eq!(TaskStatus::from_str("pending"), Some(TaskStatus::Pending));
        assert_eq!(TaskStatus::from_str("PAUSED"), Some(TaskStatus::Paused));
        assert_eq!(TaskStatus::from_str("xyz"), None);
        assert_eq!(TaskStatus::Running.as_str(), "running");
    }

    #[test]
    fn test_task_payload_constructors() {
        let skill = TaskPayload::skill("greet".into(), None);
        assert_eq!(skill.skill_name, Some("greet".to_string()));
        assert!(skill.command.is_none());

        let reminder = TaskPayload::reminder("买牛奶".into());
        assert_eq!(reminder.reminder_content, Some("买牛奶".to_string()));

        let cmd = TaskPayload::command("echo hello".into());
        assert_eq!(cmd.command, Some("echo hello".to_string()));
    }

    #[test]
    fn test_scheduled_task_lifecycle() {
        let task = ScheduledTask::new(
            "daily_greet".into(),
            "user1".into(),
            "0 9 * * *".into(),
            ScheduleType::Cron,
            TaskType::SkillInvocation,
            TaskPayload::skill("greet".into(), None),
        )
        .with_description("每日问候".into())
        .with_max_retries(5)
        .with_timeout(60);

        assert_eq!(task.name, "daily_greet");
        assert_eq!(task.status, TaskStatus::Pending);
        assert!(task.is_active());
        assert!(!task.is_due());
        assert_eq!(task.max_retries, 5);
        assert_eq!(task.timeout_seconds, 60);
    }

    #[test]
    fn test_task_execution_lifecycle() {
        let task_id = Uuid::new_v4();
        let exec = TaskExecution::new(task_id);
        assert_eq!(exec.task_id, task_id);
        assert!(exec.completed_at.is_none());

        let success = exec.clone().mark_success(Some("OK".into()));
        assert_eq!(success.status, ExecutionStatus::Success);
        assert!(success.completed_at.is_some());

        let failed = exec.clone().mark_failed("error".into(), 2);
        assert_eq!(failed.status, ExecutionStatus::Failed);
        assert_eq!(failed.retry_count, 2);

        let timeout = exec.mark_timeout();
        assert_eq!(timeout.status, ExecutionStatus::Timeout);
    }

    #[test]
    fn test_execution_status() {
        assert_eq!(ExecutionStatus::from_i32(0), Some(ExecutionStatus::Success));
        assert_eq!(ExecutionStatus::from_i32(1), Some(ExecutionStatus::Failed));
        assert_eq!(ExecutionStatus::from_i32(2), Some(ExecutionStatus::Timeout));
        assert_eq!(ExecutionStatus::from_i32(99), None);
    }
}
