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
            TaskType::LLMCall { prompt, model, temperature, max_tokens } => {
                self.execute_llm_call(prompt, model.as_deref(), *temperature, *max_tokens).await
            }
            TaskType::SkillExecution { skill_name, params } => {
                bail!("SkillExecution not implemented yet: {} {:?}", skill_name, params)
            }
            TaskType::MCPToolCall { server_name, tool_name, params } => {
                bail!("MCPToolCall not implemented yet: {}:{} {:?}", server_name, tool_name, params)
            }
            TaskType::Subworkflow { workflow_id } => {
                bail!("Subworkflow not implemented yet: {}", workflow_id)
            }
        }
    }

    /// 执行 LLM 调用
    async fn execute_llm_call(
        &self,
        prompt: &str,
        model: Option<&str>,
        temperature: Option<f32>,
        max_tokens: Option<u32>,
    ) -> Result<String> {
        use agent_core::{
            models::{Message, MessageRole},
            traits::llm::{CompletionRequest, LLMClient},
        };
        use agent_llm::AnthropicClient;
        use uuid::Uuid;

        // 创建 LLM 客户端
        let client = AnthropicClient::from_env()
            .map_err(|e| anyhow::anyhow!("Failed to create LLM client: {}", e))?;

        // 构建消息
        let session_id = Uuid::new_v4(); // 临时会话 ID
        let message = Message::new(session_id, MessageRole::User, prompt.to_string());

        // 构建请求
        let mut request = CompletionRequest::new(
            vec![message],
            model.unwrap_or("claude-3-5-sonnet-20241022").to_string(),
        );

        if let Some(temp) = temperature {
            request = request.with_temperature(temp);
        }
        if let Some(tokens) = max_tokens {
            request = request.with_max_tokens(tokens);
        }

        // 调用 LLM
        let response = client.complete(request).await
            .map_err(|e| anyhow::anyhow!("LLM call failed: {}", e))?;

        Ok(response.content)
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
    async fn test_execute_unimplemented_skill() {
        let executor = TaskExecutor::new();

        let task = Task::new(
            "test",
            "Test Task",
            TaskType::SkillExecution {
                skill_name: "test_skill".to_string(),
                params: None,
            },
        );

        let result = executor.execute_task(&task).await;
        assert!(matches!(result.status, TaskStatus::Failed(_)));
        assert!(result.error.is_some());
        assert!(result.error.unwrap().contains("not implemented"));
    }

    #[tokio::test]
    #[ignore] // 需要真实的 API Key，在 CI 中跳过
    async fn test_execute_llm_call() {
        // 只有在设置了 ANTHROPIC_API_KEY 时才运行
        if std::env::var("ANTHROPIC_API_KEY").is_err() {
            return;
        }

        let executor = TaskExecutor::new();

        let task = Task::new(
            "test",
            "Test LLM Call",
            TaskType::LLMCall {
                prompt: "What is 2+2? Answer with just the number.".to_string(),
                model: Some("claude-3-5-sonnet-20241022".to_string()),
                temperature: Some(0.0),
                max_tokens: Some(10),
            },
        );

        let result = executor.execute_task(&task).await;
        assert_eq!(result.status, TaskStatus::Completed);
        assert!(result.output.is_some());
        let output = result.output.unwrap();
        assert!(output.contains("4"), "Expected '4' in output, got: {}", output);
    }
}
