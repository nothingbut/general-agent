//! 任务执行器 - 执行单个任务
//!
//! 本模块实现了任务执行器，负责：
//! - 执行不同类型的任务
//! - 超时控制
//! - 重试机制（指数退避）
//! - 错误处理
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::*;
//!
//! # tokio_test::block_on(async {
//! let executor = TaskExecutor::new();
//! let task = Task::new("test", "Test Task", TaskType::Custom("demo".to_string()));
//!
//! let result = executor.execute_task(&task).await;
//! assert_eq!(result.status, TaskStatus::Completed);
//! # });
//! ```

use tokio::time::{timeout, Duration};
use anyhow::{Result, bail};

use super::models::*;

/// 任务执行器
#[derive(Clone)]
pub struct TaskExecutor;

impl TaskExecutor {
    /// 创建新的执行器
    pub fn new() -> Self {
        Self
    }

    /// 执行单个任务
    pub async fn execute_task(&self, task: &Task) -> TaskResult {
        let start = std::time::Instant::now();

        // 应用超时
        let result = timeout(
            Duration::from_secs(task.config.timeout_secs),
            self.execute_task_inner(task),
        )
        .await;

        let elapsed = start.elapsed().as_millis() as u64;

        match result {
            Ok(Ok(output)) => TaskResult::success(task.id.clone(), output, elapsed),
            Ok(Err(e)) => TaskResult::failure(task.id.clone(), e.to_string(), elapsed),
            Err(_) => TaskResult::failure(
                task.id.clone(),
                "Task execution timeout".to_string(),
                elapsed,
            ),
        }
    }

    /// 执行任务的内部逻辑（带重试）
    async fn execute_task_inner(&self, task: &Task) -> Result<String> {
        let mut last_error = None;

        for attempt in 0..=task.config.retry_count {
            if attempt > 0 {
                // 指数退避
                let delay = Duration::from_millis(100 * 2u64.pow(attempt - 1));
                tokio::time::sleep(delay).await;
            }

            match self.execute_task_once(task).await {
                Ok(output) => return Ok(output),
                Err(e) => {
                    last_error = Some(e);
                }
            }
        }

        Err(last_error.unwrap())
    }

    /// 执行任务一次（实际的任务逻辑）
    async fn execute_task_once(&self, task: &Task) -> Result<String> {
        match &task.task_type {
            TaskType::Custom(name) => {
                // 模拟任务执行
                tokio::time::sleep(Duration::from_millis(10)).await;
                Ok(format!("Executed custom task: {}", name))
            }
            TaskType::LLMCall => {
                bail!("LLMCall not implemented yet")
            }
            TaskType::SkillExecution => {
                bail!("SkillExecution not implemented yet")
            }
            TaskType::MCPToolCall => {
                bail!("MCPToolCall not implemented yet")
            }
            TaskType::Subworkflow => {
                bail!("Subworkflow not implemented yet")
            }
        }
    }
}

impl Default for TaskExecutor {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_execute_simple_task() {
        let executor = TaskExecutor::new();

        let task = Task::new("test", "Test Task", TaskType::Custom("hello".to_string()));

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
        assert!(result.output.is_some());
        assert!(result.error.is_none());
    }

    #[tokio::test]
    async fn test_execute_with_timeout() {
        let executor = TaskExecutor::new();

        let mut task = Task::new("test", "Test Task", TaskType::Custom("slow".to_string()));
        task.config.timeout_secs = 1; // 1 秒超时
        // 注意：我们的模拟任务只需要 10ms，所以不会超时

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
    }

    #[tokio::test]
    async fn test_execute_unimplemented_task() {
        let executor = TaskExecutor::new();

        let task = Task::new("test", "Test Task", TaskType::LLMCall);

        let result = executor.execute_task(&task).await;
        assert!(matches!(result.status, TaskStatus::Failed(_)));
        assert!(result.error.is_some());
        assert!(result.error.unwrap().contains("not implemented"));
    }
}
