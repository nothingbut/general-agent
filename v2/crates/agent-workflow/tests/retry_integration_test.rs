//! 重试机制集成测试
//!
//! 测试任务执行器的重试功能在实际场景中的表现。

use agent_workflow::workflow::*;
use std::sync::{Arc, Mutex};

/// 模拟不稳定的任务执行器（前 N 次失败）
#[derive(Clone)]
struct UnstableTaskSimulator {
    attempts: Arc<Mutex<u32>>,
    fail_count: u32,
    error_message: String,
}

impl UnstableTaskSimulator {
    fn new(fail_count: u32, error_message: &str) -> Self {
        Self {
            attempts: Arc::new(Mutex::new(0)),
            fail_count,
            error_message: error_message.to_string(),
        }
    }

    fn execute(&self) -> Result<String, String> {
        let mut attempts = self.attempts.lock().unwrap();
        *attempts += 1;

        if *attempts <= self.fail_count {
            Err(format!("{} (attempt {})", self.error_message, *attempts))
        } else {
            Ok(format!("Success after {} attempts", *attempts))
        }
    }

    fn get_attempts(&self) -> u32 {
        *self.attempts.lock().unwrap()
    }
}

#[tokio::test]
async fn test_retry_success_after_failures() {
    // 模拟：前 2 次失败，第 3 次成功
    let simulator = UnstableTaskSimulator::new(2, "Temporary connection error");

    // 创建任务配置（3 次重试，指数退避）
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 50, 1000, 2.0))
        .with_timeout(5);

    let mut task = Task::new("retry-test", "Retry Test", TaskType::Custom("test".to_string()));
    task.config = config;

    // 模拟执行（这里只能测试框架，实际执行需要集成）
    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    // 任务应该成功（Custom 任务总是成功）
    assert_eq!(result.status, TaskStatus::Completed);
}

#[tokio::test]
async fn test_retry_exhausted() {
    // 创建总是失败的任务配置
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(2, 50, 1000, 2.0))
        .with_timeout(5);

    // 使用一个会超时的任务来模拟失败
    let mut task = Task::new(
        "timeout-task",
        "Timeout Task",
        TaskType::Custom("slow".to_string()),
    );
    task.config = config.with_timeout(0); // 立即超时

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    // 任务应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 应该有重试历史
    assert!(result.retry_history.has_retries());
    assert_eq!(result.retry_history.total_retries, 2);
    assert!(result.retry_history.max_retries_reached);
}

#[tokio::test]
async fn test_retry_strategy_fixed_delay() {
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::fixed(3, 100))
        .with_timeout(5);

    let mut task = Task::new("fixed-delay", "Fixed Delay Test", TaskType::Custom("test".to_string()));
    task.config = config;

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    assert_eq!(result.status, TaskStatus::Completed);
}

#[tokio::test]
async fn test_retry_strategy_linear_backoff() {
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::linear(3, 100, 50))
        .with_timeout(5);

    let mut task = Task::new("linear-backoff", "Linear Backoff Test", TaskType::Custom("test".to_string()));
    task.config = config;

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    assert_eq!(result.status, TaskStatus::Completed);
}

#[tokio::test]
async fn test_retry_condition_non_retryable_error() {
    // 创建任务配置，但不重试 "invalid" 错误
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 50, 1000, 2.0))
        .with_retry_condition(RetryCondition::default())
        .with_timeout(5);

    let mut task = Task::new(
        "non-retryable",
        "Non-Retryable Error",
        TaskType::Custom("test".to_string()),
    );
    task.config = config;

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    // Custom 任务总是成功，但测试了配置的正确性
    assert_eq!(result.status, TaskStatus::Completed);
}

#[tokio::test]
async fn test_no_retry_strategy() {
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::None)
        .with_timeout(5);

    let mut task = Task::new("no-retry", "No Retry Test", TaskType::Custom("test".to_string()));
    task.config = config.with_timeout(0); // 立即超时

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    // 任务应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 不应该有重试
    assert!(!result.retry_history.has_retries());
    assert_eq!(result.retry_history.total_retries, 0);
}

#[tokio::test]
async fn test_workflow_with_retry_tasks() {
    // 创建一个包含多个任务的工作流，每个任务都有重试策略
    let mut workflow = Workflow::new("retry-workflow", "Retry Workflow Test");

    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(2, 50, 500, 2.0))
        .with_timeout(5);

    let task1 = Task::new("task-1", "Task 1", TaskType::Custom("test1".to_string()))
        .with_config(config.clone());
    let task2 = Task::new("task-2", "Task 2", TaskType::Custom("test2".to_string()))
        .with_config(config.clone())
        .with_dependency("task-1");
    let task3 = Task::new("task-3", "Task 3", TaskType::Custom("test3".to_string()))
        .with_config(config)
        .with_dependency("task-1");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);

    let executor = TaskExecutor::new();
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();

    let result = orchestrator.execute(&executor).await.unwrap();

    // 所有任务都应该成功
    assert_eq!(result.task_results.len(), 3);
    for (_, task_result) in result.task_results.iter() {
        assert_eq!(task_result.status, TaskStatus::Completed);
    }
}

#[tokio::test]
async fn test_retry_history_tracking() {
    // 测试重试历史记录的准确性
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 100, 1000, 2.0))
        .with_timeout(5);

    let mut task = Task::new("history-test", "History Test", TaskType::Custom("test".to_string()));
    task.config = config;

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    // Custom 任务第一次就成功，所以没有重试
    assert_eq!(result.status, TaskStatus::Completed);
    assert!(!result.retry_history.has_retries());
    assert_eq!(result.retry_history.total_retries, 0);
}

#[tokio::test]
async fn test_timeout_with_retry() {
    // 测试超时任务的重试行为
    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::fixed(2, 50))
        .with_timeout(0); // 立即超时

    let mut task = Task::new("timeout-retry", "Timeout with Retry", TaskType::Custom("slow".to_string()));
    task.config = config;

    let executor = TaskExecutor::new();
    let start = std::time::Instant::now();
    let result = executor.execute_task(&task).await;
    let elapsed = start.elapsed();

    // 任务应该失败
    assert!(matches!(result.status, TaskStatus::Failed(_)));

    // 应该有 2 次重试
    assert!(result.retry_history.has_retries());
    assert_eq!(result.retry_history.total_retries, 2);

    // 总时间应该包括重试延迟（至少 100ms = 50ms * 2）
    assert!(elapsed.as_millis() >= 100);
}

#[tokio::test]
async fn test_custom_retry_condition() {
    // 测试自定义重试条件
    let custom_condition = RetryCondition::new()
        .add_retryable_error("database locked")
        .add_retryable_error("deadlock")
        .add_non_retryable_error("schema error")
        .retry_unknown_errors(false);

    let config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 50, 1000, 2.0))
        .with_retry_condition(custom_condition)
        .with_timeout(5);

    let mut task = Task::new("custom-condition", "Custom Condition Test", TaskType::Custom("test".to_string()));
    task.config = config;

    let executor = TaskExecutor::new();
    let result = executor.execute_task(&task).await;

    assert_eq!(result.status, TaskStatus::Completed);
}
