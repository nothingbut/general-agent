//! 集成工作流演示 - 展示审批和通知系统
//!
//! 运行方式：cargo run -p agent-workflow --example integrated_workflow_demo

use agent_workflow::approval::{ApprovalManager, ApprovalResponse};
use agent_workflow::notification::{NotificationManager, TerminalChannel};
use agent_workflow::workflow::*;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 初始化日志
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::INFO)
        .init();

    println!("=== 集成工作流演示 ===\n");

    // 场景 1: 简单工作流（无审批无通知）
    println!("【场景 1】简单工作流 - 无审批无通知");
    run_simple_workflow().await?;
    tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;

    // 场景 2: 带通知的工作流
    println!("\n【场景 2】带通知的工作流");
    run_workflow_with_notifications().await?;
    tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;

    // 场景 3: 带审批和通知的工作流
    println!("\n【场景 3】带审批和通知的工作流");
    run_workflow_with_approval_and_notifications().await?;

    println!("\n=== 演示结束 ===");
    Ok(())
}

/// 场景 1: 简单工作流
async fn run_simple_workflow() -> Result<(), Box<dyn std::error::Error>> {
    // 创建工作流
    let mut workflow = Workflow::new("simple-wf", "简单工作流");

    // 添加任务
    workflow.add_task(Task::new(
        "task-1",
        "任务 1",
        TaskType::Custom("步骤1".to_string()),
    ));
    workflow.add_task(
        Task::new(
            "task-2",
            "任务 2",
            TaskType::Custom("步骤2".to_string()),
        )
        .with_dependency("task-1"),
    );

    // 创建编排器
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let executor = TaskExecutor::new();

    // 执行工作流
    let result = orchestrator.execute(&executor).await?;

    println!(
        "✓ 工作流完成，耗时: {}ms, 完成任务: {}",
        result.execution_time_ms,
        result.task_results.len()
    );

    Ok(())
}

/// 场景 2: 带通知的工作流
async fn run_workflow_with_notifications() -> Result<(), Box<dyn std::error::Error>> {
    // 创建通知管理器
    let notification_manager = Arc::new(NotificationManager::new());
    notification_manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    // 创建工作流
    let mut workflow = Workflow::new("notify-wf", "通知工作流");
    workflow.add_task(Task::new(
        "task-1",
        "数据加载",
        TaskType::Custom("load_data".to_string()),
    ));
    workflow.add_task(
        Task::new(
            "task-2",
            "数据处理",
            TaskType::Custom("process_data".to_string()),
        )
        .with_dependency("task-1"),
    );
    workflow.add_task(
        Task::new(
            "task-3",
            "数据保存",
            TaskType::Custom("save_data".to_string()),
        )
        .with_dependency("task-2"),
    );

    // 创建编排器（带通知）
    let orchestrator = WorkflowOrchestrator::new(workflow)?
        .with_notification_manager(notification_manager);
    let executor = TaskExecutor::new();

    // 执行工作流
    let result = orchestrator.execute(&executor).await?;

    println!(
        "\n✓ 工作流完成，耗时: {}ms, 完成任务: {}",
        result.execution_time_ms,
        result.task_results.len()
    );

    Ok(())
}

/// 场景 3: 带审批和通知的工作流
async fn run_workflow_with_approval_and_notifications() -> Result<(), Box<dyn std::error::Error>> {
    // 创建审批管理器（简化版本，使用 Auto 策略）
    let approval_manager = Arc::new(ApprovalManager::new());

    // 创建通知管理器
    let notification_manager = Arc::new(NotificationManager::new());
    notification_manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    // 创建工作流
    let mut workflow = Workflow::new("full-wf", "完整工作流");
    workflow.add_task(Task::new(
        "task-1",
        "初始化系统",
        TaskType::Custom("init".to_string()),
    ));
    workflow.add_task(
        Task::new(
            "task-2",
            "执行主任务",
            TaskType::Custom("main_task".to_string()),
        )
        .with_dependency("task-1"),
    );
    workflow.add_task(
        Task::new(
            "task-3",
            "清理资源",
            TaskType::Custom("cleanup".to_string()),
        )
        .with_dependency("task-2"),
    );

    // 创建编排器（带审批和通知）
    let orchestrator = WorkflowOrchestrator::new(workflow)?
        .with_approval_manager(approval_manager)
        .with_notification_manager(notification_manager);
    let executor = TaskExecutor::new();

    // 执行工作流
    let result = orchestrator.execute(&executor).await?;

    println!(
        "\n✓ 工作流完成，耗时: {}ms, 完成任务: {}",
        result.execution_time_ms,
        result.task_results.len()
    );

    Ok(())
}
