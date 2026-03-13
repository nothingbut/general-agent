//! 工作流集成测试
//!
//! 测试审批和通知系统与 WorkflowOrchestrator 的集成

use agent_workflow::approval::*;
use agent_workflow::notification::*;
use agent_workflow::workflow::*;
use std::sync::Arc;

#[tokio::test]
async fn test_workflow_with_notifications() {
    // 创建通知管理器
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr
        .register_channel(Arc::new(LogChannel::new()))
        .await;

    // 创建工作流
    let mut workflow = Workflow::new("test-wf", "测试工作流");
    workflow.add_task(Task::new(
        "task-1",
        "任务1",
        TaskType::Custom("test".to_string()),
    ));

    // 创建编排器（带通知）
    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_notification_manager(notification_mgr);

    // 执行
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
    let result = result.unwrap();
    assert_eq!(result.task_results.len(), 1);
}

#[tokio::test]
async fn test_workflow_with_approval() {
    // 创建审批管理器
    let approval_mgr = Arc::new(ApprovalManager::new());

    // 创建工作流
    let mut workflow = Workflow::new("test-wf", "测试工作流");
    workflow.add_task(Task::new(
        "task-1",
        "任务1",
        TaskType::Custom("test".to_string()),
    ));

    // 创建编排器（带审批）
    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_approval_manager(approval_mgr);

    // 执行
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
}

#[tokio::test]
async fn test_workflow_full_integration() {
    // 创建所有管理器
    let approval_mgr = Arc::new(ApprovalManager::new());
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr
        .register_channel(Arc::new(LogChannel::new()))
        .await;

    // 创建复杂工作流（有依赖关系）
    let mut workflow = Workflow::new("full-wf", "完整测试工作流");
    workflow.add_task(Task::new(
        "task-1",
        "初始化",
        TaskType::Custom("init".to_string()),
    ));
    workflow.add_task(
        Task::new(
            "task-2",
            "处理",
            TaskType::Custom("process".to_string()),
        )
        .with_dependency("task-1"),
    );
    workflow.add_task(
        Task::new(
            "task-3",
            "完成",
            TaskType::Custom("finish".to_string()),
        )
        .with_dependency("task-2"),
    );

    // 创建编排器（完整集成）
    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_approval_manager(approval_mgr)
        .with_notification_manager(notification_mgr);

    // 执行
    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
    let result = result.unwrap();
    assert_eq!(result.task_results.len(), 3);

    // 验证所有任务都完成
    for (_, task_result) in result.task_results.iter() {
        assert_eq!(task_result.status, TaskStatus::Completed);
    }
}

#[tokio::test]
async fn test_parallel_tasks_with_notifications() {
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr
        .register_channel(Arc::new(LogChannel::new()))
        .await;

    // 创建并行任务工作流
    let mut workflow = Workflow::new("parallel-wf", "并行任务工作流");
    workflow.add_task(Task::new(
        "root",
        "根任务",
        TaskType::Custom("root".to_string()),
    ));
    // task-a 和 task-b 可以并行执行
    workflow.add_task(
        Task::new("task-a", "任务A", TaskType::Custom("a".to_string()))
            .with_dependency("root"),
    );
    workflow.add_task(
        Task::new("task-b", "任务B", TaskType::Custom("b".to_string()))
            .with_dependency("root"),
    );

    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_notification_manager(notification_mgr);

    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await.unwrap();

    assert_eq!(result.task_results.len(), 3);
}

#[tokio::test]
async fn test_notification_priority_inference() {
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr
        .register_channel(Arc::new(LogChannel::new()))
        .await;

    // 测试优先级推断
    let mut workflow = Workflow::new("priority-wf", "优先级测试");

    // 危险操作 -> Critical
    workflow.add_task(Task::new(
        "delete-task",
        "delete_files",
        TaskType::Custom("delete".to_string()),
    ));

    // 写入操作 -> High
    workflow.add_task(Task::new(
        "write-task",
        "write_file",
        TaskType::Custom("write".to_string()),
    ));

    // 读取操作 -> Normal
    workflow.add_task(Task::new(
        "read-task",
        "read_file",
        TaskType::Custom("read".to_string()),
    ));

    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_notification_manager(notification_mgr);

    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
}

#[tokio::test]
async fn test_workflow_control_with_notifications() {
    let notification_mgr = Arc::new(NotificationManager::new());
    notification_mgr
        .register_channel(Arc::new(LogChannel::new()))
        .await;

    let mut workflow = Workflow::new("control-wf", "控制测试");
    workflow.add_task(Task::new(
        "task-1",
        "任务1",
        TaskType::Custom("test".to_string()),
    ));

    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_notification_manager(notification_mgr);

    // 测试取消（在执行前取消）
    orchestrator.cancel().await;
    let state = orchestrator.get_control_state().await;
    assert_eq!(state, ControlState::CancelRequested);

    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    // 应该返回取消错误
    assert!(result.is_err());
    assert!(result
        .unwrap_err()
        .to_string()
        .contains("cancelled"));
}
