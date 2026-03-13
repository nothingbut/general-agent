//! Workflow 核心数据模型
//!
//! 本模块定义了工作流系统的核心数据结构：
//! - [`Workflow`] - 工作流定义
//! - [`Task`] - 任务定义
//! - [`TaskType`] - 任务类型枚举
//! - [`TaskConfig`] - 任务配置
//! - [`TaskStatus`] - 任务状态
//! - [`WorkflowResult`] - 工作流执行结果
//! - [`TaskResult`] - 任务执行结果
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::*;
//!
//! // 创建工作流
//! let mut workflow = Workflow::new("my-workflow", "My Workflow");
//!
//! // 添加任务
//! let task_a = Task::new("task-a", "Task A", TaskType::Custom("test".to_string()));
//! let task_b = Task::new("task-b", "Task B", TaskType::Custom("test".to_string()))
//!     .with_dependency("task-a");
//!
//! workflow.add_task(task_a);
//! workflow.add_task(task_b);
//! ```

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// 工作流定义
///
/// 包含一组有依赖关系的任务，形成 DAG（有向无环图）。
///
/// # 字段
///
/// - `id` - 工作流唯一标识符
/// - `name` - 工作流名称
/// - `tasks` - 任务列表
/// - `created_at` - 创建时间
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
///
/// 工作流中的单个任务单元，可以指定依赖关系。
///
/// # 字段
///
/// - `id` - 任务唯一标识符
/// - `name` - 任务名称
/// - `task_type` - 任务类型（LLM调用、技能执行等）
/// - `dependencies` - 依赖的任务 ID 列表
/// - `config` - 任务配置（重试、超时等）
/// - `metadata` - 额外的元数据
///
/// # 示例
///
/// ```rust
/// use agent_workflow::workflow::*;
///
/// let task = Task::new("task-1", "Task 1", TaskType::Custom("test".to_string()))
///     .with_dependency("task-0")
///     .with_config(TaskConfig {
///         retry_count: 3,
///         timeout_secs: 30,
///         priority: 1,
///     });
/// ```
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
///
/// 定义任务的执行类型，决定使用哪个执行器。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum TaskType {
    /// LLM 调用 - 调用大语言模型
    LLMCall {
        /// 提示词内容
        prompt: String,
        /// 模型名称（如 claude-3-5-sonnet-20241022）
        #[serde(skip_serializing_if = "Option::is_none")]
        model: Option<String>,
        /// 温度参数 (0.0 - 2.0)
        #[serde(skip_serializing_if = "Option::is_none")]
        temperature: Option<f32>,
        /// 最大 token 数
        #[serde(skip_serializing_if = "Option::is_none")]
        max_tokens: Option<u32>,
    },
    /// Skills 技能执行 - 执行预定义的技能
    SkillExecution {
        /// 技能名称
        skill_name: String,
        /// 技能参数（JSON 格式）
        #[serde(skip_serializing_if = "Option::is_none")]
        params: Option<serde_json::Value>,
    },
    /// MCP 工具调用 - 调用 MCP 服务器的工具
    MCPToolCall {
        /// MCP 服务器名称
        server_name: String,
        /// 工具名称
        tool_name: String,
        /// 工具参数（JSON 格式）
        #[serde(skip_serializing_if = "Option::is_none")]
        params: Option<serde_json::Value>,
    },
    /// 子工作流 - 嵌套执行另一个工作流
    Subworkflow {
        /// 子工作流 ID
        workflow_id: String,
    },
    /// 自定义任务 - 自定义逻辑（主要用于测试）
    Custom(String),
}

/// 任务配置
///
/// 控制任务执行的行为参数。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskConfig {
    /// 重试次数 - 任务失败后的最大重试次数
    pub retry_count: u32,
    /// 超时时间（秒）- 单次执行的超时限制
    pub timeout_secs: u64,
    /// 优先级 - 数值越大优先级越高（暂未实现）
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
///
/// 表示任务的执行状态，遵循状态机转换：
/// `Pending` → `Running` → `Completed` | `Failed` | `Cancelled`
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub enum TaskStatus {
    /// 等待执行 - 初始状态或依赖未满足
    Pending,
    /// 执行中 - 正在执行
    Running,
    /// 执行成功 - 任务完成
    Completed,
    /// 执行失败 - 包含错误信息
    Failed(String),
    /// 已取消 - 被用户或系统取消
    Cancelled,
}

/// 工作流执行结果
///
/// 包含工作流的完整执行结果和统计信息。
#[derive(Debug)]
pub struct WorkflowResult {
    /// 工作流 ID
    pub workflow_id: String,
    /// 所有任务的执行结果（TaskID -> TaskResult）
    pub task_results: HashMap<String, TaskResult>,
    /// 总执行时间（毫秒）
    pub execution_time_ms: u64,
}

/// 任务执行结果
///
/// 单个任务的执行结果，包含状态、输出和错误信息。
#[derive(Debug, Clone)]
pub struct TaskResult {
    /// 任务 ID
    pub task_id: String,
    /// 任务状态
    pub status: TaskStatus,
    /// 任务输出（成功时）
    pub output: Option<String>,
    /// 错误信息（失败时）
    pub error: Option<String>,
    /// 执行时间（毫秒）
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
        let task = Task::new(
            "task-2",
            "Task 2",
            TaskType::LLMCall {
                prompt: "test prompt".to_string(),
                model: None,
                temperature: None,
                max_tokens: None,
            },
        )
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
