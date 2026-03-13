//! 重试机制演示
//!
//! 展示如何使用重试策略来处理不稳定的任务执行。
//!
//! 运行: cargo run -p agent-workflow --example retry_demo

use agent_workflow::workflow::*;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("\n=== 重试机制演示 ===\n");

    let executor = TaskExecutor::new();

    // 场景 1: 指数退避策略
    println!("【场景 1】指数退避策略");
    println!("配置: 最大 3 次重试，初始延迟 100ms，指数因子 2.0");
    println!("延迟序列: 100ms → 200ms → 400ms\n");

    let config1 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 100, 5000, 2.0))
        .with_timeout(10);

    let mut task1 = Task::new(
        "exp-backoff",
        "指数退避任务",
        TaskType::Custom("stable".to_string()),
    );
    task1.config = config1;

    let result1 = executor.execute_task(&task1).await;
    print_result(&result1);

    // 场景 2: 固定延迟策略
    println!("\n【场景 2】固定延迟策略");
    println!("配置: 最大 3 次重试，固定延迟 200ms");
    println!("延迟序列: 200ms → 200ms → 200ms\n");

    let config2 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::fixed(3, 200))
        .with_timeout(10);

    let mut task2 = Task::new(
        "fixed-delay",
        "固定延迟任务",
        TaskType::Custom("stable".to_string()),
    );
    task2.config = config2;

    let result2 = executor.execute_task(&task2).await;
    print_result(&result2);

    // 场景 3: 线性增长策略
    println!("\n【场景 3】线性增长策略");
    println!("配置: 最大 3 次重试，初始延迟 100ms，每次增加 150ms");
    println!("延迟序列: 100ms → 250ms → 400ms\n");

    let config3 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::linear(3, 100, 150))
        .with_timeout(10);

    let mut task3 = Task::new(
        "linear-backoff",
        "线性增长任务",
        TaskType::Custom("stable".to_string()),
    );
    task3.config = config3;

    let result3 = executor.execute_task(&task3).await;
    print_result(&result3);

    // 场景 4: 超时触发重试
    println!("\n【场景 4】超时触发重试");
    println!("配置: 最大 2 次重试，超时 0 秒（立即超时）");
    println!("预期: 任务超时后重试 2 次，最终失败\n");

    let config4 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(2, 50, 1000, 2.0))
        .with_timeout(0); // 立即超时

    let mut task4 = Task::new(
        "timeout-task",
        "超时任务",
        TaskType::Custom("slow".to_string()),
    );
    task4.config = config4;

    let result4 = executor.execute_task(&task4).await;
    print_result(&result4);

    // 场景 5: 自定义重试条件
    println!("\n【场景 5】自定义重试条件");
    println!("配置: 只重试包含 'timeout' 或 'connection' 的错误");
    println!("不重试包含 'invalid' 或 'forbidden' 的错误\n");

    let custom_condition = RetryCondition::new()
        .add_retryable_error("timeout")
        .add_retryable_error("connection")
        .add_non_retryable_error("invalid")
        .add_non_retryable_error("forbidden")
        .retry_unknown_errors(false);

    let config5 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(3, 100, 1000, 2.0))
        .with_retry_condition(custom_condition)
        .with_timeout(10);

    let mut task5 = Task::new(
        "conditional-retry",
        "条件重试任务",
        TaskType::Custom("stable".to_string()),
    );
    task5.config = config5;

    let result5 = executor.execute_task(&task5).await;
    print_result(&result5);

    // 场景 6: 无重试策略
    println!("\n【场景 6】无重试策略");
    println!("配置: RetryStrategy::None");
    println!("预期: 失败后不重试\n");

    let config6 = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::None)
        .with_timeout(0); // 立即超时

    let mut task6 = Task::new(
        "no-retry",
        "无重试任务",
        TaskType::Custom("slow".to_string()),
    );
    task6.config = config6;

    let result6 = executor.execute_task(&task6).await;
    print_result(&result6);

    // 场景 7: 工作流中的重试任务
    println!("\n【场景 7】工作流中的重试任务");
    println!("创建包含 3 个任务的工作流，每个任务都有重试策略\n");

    let mut workflow = Workflow::new("retry-workflow", "重试工作流演示");

    let retry_config = TaskConfig::new()
        .with_retry_strategy(RetryStrategy::exponential(2, 100, 1000, 2.0))
        .with_timeout(10);

    let wf_task1 = Task::new("wf-task-1", "工作流任务 1", TaskType::Custom("wf1".to_string()))
        .with_config(retry_config.clone());
    let wf_task2 = Task::new("wf-task-2", "工作流任务 2", TaskType::Custom("wf2".to_string()))
        .with_config(retry_config.clone())
        .with_dependency("wf-task-1");
    let wf_task3 = Task::new("wf-task-3", "工作流任务 3", TaskType::Custom("wf3".to_string()))
        .with_config(retry_config)
        .with_dependency("wf-task-1");

    workflow.add_task(wf_task1);
    workflow.add_task(wf_task2);
    workflow.add_task(wf_task3);

    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let workflow_result = orchestrator.execute(&executor).await?;

    println!("✓ 工作流执行完成");
    println!("  任务数量: {}", workflow_result.task_results.len());
    println!("  执行时间: {}ms", workflow_result.execution_time_ms);

    for (task_id, task_result) in workflow_result.task_results.iter() {
        println!("\n  任务 [{}]:", task_id);
        println!("    状态: {:?}", task_result.status);
        if task_result.retry_history.has_retries() {
            println!("    重试次数: {}", task_result.retry_history.total_retries);
        } else {
            println!("    重试次数: 0 (首次成功)");
        }
    }

    println!("\n=== 演示结束 ===\n");
    Ok(())
}

/// 打印任务结果
fn print_result(result: &TaskResult) {
    println!("任务 [{}] 结果:", result.task_id);
    println!("  状态: {:?}", result.status);
    println!("  执行时间: {}ms", result.execution_time_ms);

    if result.retry_history.has_retries() {
        println!("  重试信息:");
        println!("    总重试次数: {}", result.retry_history.total_retries);
        println!("    达到最大重试: {}", result.retry_history.max_retries_reached);

        for attempt in &result.retry_history.attempts {
            println!(
                "    - 第 {} 次重试: 延迟 {}ms, 错误: {}",
                attempt.attempt,
                attempt.delay_ms,
                attempt.error
            );
        }
    } else {
        println!("  重试信息: 无重试 (首次成功)");
    }

    if let Some(output) = &result.output {
        println!("  输出: {}", output);
    }
    if let Some(error) = &result.error {
        println!("  错误: {}", error);
    }
}
