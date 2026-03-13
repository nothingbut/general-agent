//! Workflow 核心数据模型

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// 工作流定义
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Workflow {
    pub id: String,
    pub name: String,
    pub tasks: Vec<Task>,
    pub created_at: DateTime<Utc>,
}

impl Workflow {
    /// 创建新的工作流
    pub fn new(id: impl Into<String>, name: impl Into<String>) -> Self {
        Self {
            id: id.into(),
            name: name.into(),
            tasks: Vec::new(),
            created_at: Utc::now(),
        }
    }

    /// 添加任务
    pub fn add_task(&mut self, task: Task) {
        self.tasks.push(task);
    }
}

/// 任务定义
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Task {
    pub id: String,
    pub name: String,
    pub task_type: TaskType,
    pub dependencies: Vec<String>, // Task IDs
    pub config: TaskConfig,
    pub metadata: HashMap<String, String>,
}

impl Task {
    /// 创建新任务
    pub fn new(id: impl Into<String>, name: impl Into<String>, task_type: TaskType) -> Self {
        Self {
            id: id.into(),
            name: name.into(),
            task_type,
            dependencies: Vec::new(),
            config: TaskConfig::default(),
            metadata: HashMap::new(),
        }
    }

    /// 添加依赖
    pub fn with_dependency(mut self, dep_id: impl Into<String>) -> Self {
        self.dependencies.push(dep_id.into());
        self
    }

    /// 设置配置
    pub fn with_config(mut self, config: TaskConfig) -> Self {
        self.config = config;
        self
    }
}

/// 任务类型
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum TaskType {
    /// LLM 调用
    LLMCall,
    /// Skills 技能执行
    SkillExecution,
    /// MCP 工具调用
    MCPToolCall,
    /// 子工作流
    Subworkflow,
    /// 自定义任务（用于测试）
    Custom(String),
}

/// 任务配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskConfig {
    /// 重试次数
    pub retry_count: u32,
    /// 超时时间（秒）
    pub timeout_secs: u64,
    /// 优先级
    pub priority: i32,
}

impl Default for TaskConfig {
    fn default() -> Self {
        Self {
            retry_count: 3,
            timeout_secs: 60,
            priority: 0,
        }
    }
}

/// 任务状态
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub enum TaskStatus {
    /// 等待执行
    Pending,
    /// 执行中
    Running,
    /// 执行成功
    Completed,
    /// 执行失败
    Failed(String),
    /// 已取消
    Cancelled,
}

/// 工作流执行结果
#[derive(Debug)]
pub struct WorkflowResult {
    pub workflow_id: String,
    pub task_results: HashMap<String, TaskResult>,
    pub execution_time_ms: u64,
}

/// 任务执行结果
#[derive(Debug, Clone)]
pub struct TaskResult {
    pub task_id: String,
    pub status: TaskStatus,
    pub output: Option<String>,
    pub error: Option<String>,
    pub execution_time_ms: u64,
}

impl TaskResult {
    /// 创建成功结果
    pub fn success(task_id: String, output: String, execution_time_ms: u64) -> Self {
        Self {
            task_id,
            status: TaskStatus::Completed,
            output: Some(output),
            error: None,
            execution_time_ms,
        }
    }

    /// 创建失败结果
    pub fn failure(task_id: String, error: String, execution_time_ms: u64) -> Self {
        Self {
            task_id,
            status: TaskStatus::Failed(error.clone()),
            output: None,
            error: Some(error),
            execution_time_ms,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_workflow_creation() {
        let workflow = Workflow::new("test-wf", "Test Workflow");
        assert_eq!(workflow.id, "test-wf");
        assert_eq!(workflow.name, "Test Workflow");
        assert!(workflow.tasks.is_empty());
    }

    #[test]
    fn test_task_creation() {
        let task = Task::new("task-1", "Task 1", TaskType::Custom("test".to_string()));
        assert_eq!(task.id, "task-1");
        assert_eq!(task.name, "Task 1");
        assert_eq!(task.task_type, TaskType::Custom("test".to_string()));
        assert!(task.dependencies.is_empty());
    }

    #[test]
    fn test_task_with_dependency() {
        let task = Task::new("task-2", "Task 2", TaskType::LLMCall)
            .with_dependency("task-1");
        assert_eq!(task.dependencies.len(), 1);
        assert_eq!(task.dependencies[0], "task-1");
    }

    #[test]
    fn test_task_config_default() {
        let config = TaskConfig::default();
        assert_eq!(config.retry_count, 3);
        assert_eq!(config.timeout_secs, 60);
        assert_eq!(config.priority, 0);
    }

    #[test]
    fn test_task_result_success() {
        let result = TaskResult::success("task-1".to_string(), "done".to_string(), 100);
        assert_eq!(result.status, TaskStatus::Completed);
        assert_eq!(result.output, Some("done".to_string()));
        assert!(result.error.is_none());
    }

    #[test]
    fn test_task_result_failure() {
        let result = TaskResult::failure("task-1".to_string(), "error occurred".to_string(), 50);
        assert!(matches!(result.status, TaskStatus::Failed(_)));
        assert_eq!(result.error, Some("error occurred".to_string()));
        assert!(result.output.is_none());
    }
}
