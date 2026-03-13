//! 工作流控制测试 - 取消、暂停、恢复

use agent_workflow::workflow::*;
use std::sync::Arc;

/// 创建测试任务
fn create_test_task(id: &str, deps: Vec<&str>) -> Task {
    use std::collections::HashMap;
    Task {
        id: id.to_string(),
        name: format!("Task {}", id),
        task_type: TaskType::Custom("test".to_string()),
        dependencies: deps.iter().map(|s| s.to_string()).collect(),
        config: TaskConfig::new()
            .with_retry_strategy(RetryStrategy::None)
            .with_timeout(10),
        metadata: HashMap::new(),
    }
}

/// 创建带延迟的测试任务（用于测试取消）
fn create_slow_task(id: &str, deps: Vec<&str>, delay_ms: u64) -> Task {
    use std::collections::HashMap;
    let mut task = Task {
        id: id.to_string(),
        name: format!("Slow Task {}", id),
        task_type: TaskType::Custom(format!("slow-{}", delay_ms)),
        dependencies: deps.iter().map(|s| s.to_string()).collect(),
        config: TaskConfig::new()
            .with_retry_strategy(RetryStrategy::None)
            .with_timeout(30),
        metadata: HashMap::new(),
    };
    task.metadata.insert("delay_ms".to_string(), delay_ms.to_string());
    task
}

#[tokio::test]
async fn test_cancel_workflow() {
    // 创建包含多个任务的工作流，使用更多任务以确保有足够时间取消
    let mut workflow = Workflow::new("test-cancel", "Test Cancel Workflow");
    workflow.add_task(create_test_task("A", vec![]));
    workflow.add_task(create_test_task("B", vec!["A"]));
    workflow.add_task(create_test_task("C", vec!["B"]));
    workflow.add_task(create_test_task("D", vec!["C"]));
    workflow.add_task(create_test_task("E", vec!["D"]));

    let orchestrator = Arc::new(WorkflowOrchestrator::new(workflow).unwrap());
    let executor = TaskExecutor::new();

    // 先设置取消标志，然后开始执行
    orchestrator.cancel().await;

    // 执行工作流
    let result = orchestrator.execute(&executor).await;

    // 验证结果是取消错误
    assert!(result.is_err());
    let err = result.unwrap_err();
    assert!(
        err.to_string().contains("cancelled"),
        "Expected 'cancelled' in error, got: {}",
        err
    );
}

#[tokio::test]
async fn test_pause_and_resume() {
    // 创建工作流
    let mut workflow = Workflow::new("test-pause", "Test Pause Workflow");
    workflow.add_task(create_test_task("A", vec![]));
    workflow.add_task(create_test_task("B", vec!["A"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    // 先设置暂停标志，然后开始执行
    orchestrator.pause().await;

    // 执行工作流（应该会暂停）
    let result = orchestrator.execute(&executor).await;
    assert!(result.is_err());
    let err = result.unwrap_err();
    assert!(
        err.to_string().contains("paused"),
        "Expected 'paused' in error, got: {}",
        err
    );

    // 验证状态是暂停
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, agent_workflow::workflow::ControlState::Paused);

    // 恢复
    orchestrator.resume().await;
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, agent_workflow::workflow::ControlState::Running);

    // 注意：实际恢复执行需要重新调用 execute()
    // 这里只是验证状态改变
}

#[tokio::test]
async fn test_get_control_state() {
    let mut workflow = Workflow::new("test-state", "Test State");
    workflow.add_task(create_test_task("A", vec![]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();

    // 初始状态应该是 Running
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, agent_workflow::workflow::orchestrator::ControlState::Running);

    // 请求取消后
    orchestrator.cancel().await;
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, agent_workflow::workflow::orchestrator::ControlState::CancelRequested);
}

#[tokio::test]
async fn test_cancel_before_execution() {
    // 创建工作流
    let mut workflow = Workflow::new("test-cancel-early", "Test Early Cancel");
    workflow.add_task(create_test_task("A", vec![]));
    workflow.add_task(create_test_task("B", vec!["A"]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    // 在执行前就取消
    orchestrator.cancel().await;

    // 尝试执行
    let result = orchestrator.execute(&executor).await;

    // 应该立即返回取消错误
    assert!(result.is_err());
    let err = result.unwrap_err();
    assert!(err.to_string().contains("cancelled"));
}

#[tokio::test]
async fn test_multiple_pause_requests() {
    let mut workflow = Workflow::new("test-multi-pause", "Test Multiple Pause");
    workflow.add_task(create_test_task("A", vec![]));

    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();

    // 多次请求暂停
    orchestrator.pause().await;
    orchestrator.pause().await;
    orchestrator.pause().await;

    // 状态应该仍然是 PauseRequested（因为还没执行）
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, agent_workflow::workflow::orchestrator::ControlState::PauseRequested);
}
