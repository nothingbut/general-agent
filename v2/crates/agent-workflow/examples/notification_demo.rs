//! 通知系统演示
//!
//! 运行方式：cargo run --example notification_demo

use agent_workflow::notification::{
    LogChannel, Notification, NotificationManager, NotificationPriority, TerminalChannel,
};
use std::sync::Arc;

#[tokio::main]
async fn main() {
    // 初始化日志
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::INFO)
        .init();

    println!("=== 通知系统演示 ===\n");

    // 场景 1: 基本通知
    println!("【场景 1】基本终端通知");
    demo_basic_notification().await;
    tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;

    // 场景 2: 多渠道通知
    println!("\n【场景 2】多渠道通知（终端 + 日志）");
    demo_multi_channel_notification().await;
    tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;

    // 场景 3: 不同优先级通知
    println!("\n【场景 3】不同优先级通知");
    demo_priority_notifications().await;
    tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;

    // 场景 4: 智能优先级推断
    println!("\n【场景 4】智能优先级推断（根据工具名称）");
    demo_smart_priority().await;
    tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;

    // 场景 5: 批量通知
    println!("\n【场景 5】批量通知");
    demo_batch_notifications().await;

    println!("\n=== 演示结束 ===");
}

/// 场景 1: 基本通知
async fn demo_basic_notification() {
    let manager = NotificationManager::new();
    manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    let notification = Notification::new(
        "workflow-001",
        "task-001",
        "任务开始",
        "开始执行数据处理任务",
    );

    manager.send(&notification).await;
}

/// 场景 2: 多渠道通知
async fn demo_multi_channel_notification() {
    let manager = NotificationManager::new();
    manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;
    manager.register_channel(Arc::new(LogChannel::new())).await;

    let notification = Notification::new(
        "workflow-002",
        "task-002",
        "任务完成",
        "数据处理任务已成功完成，处理了 1000 条记录",
    )
    .with_channels(vec!["terminal".to_string(), "log".to_string()]);

    let results = manager.send(&notification).await;
    println!("发送结果: {:?}", results);
}

/// 场景 3: 不同优先级通知
async fn demo_priority_notifications() {
    let manager = NotificationManager::new();
    manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    // Critical 优先级
    let critical = Notification::new(
        "workflow-003",
        "task-003",
        "危险操作警告",
        "即将删除 /tmp/important_data 目录",
    )
    .with_priority(NotificationPriority::Critical);
    manager.send(&critical).await;
    tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;

    // High 优先级
    let high = Notification::new(
        "workflow-003",
        "task-004",
        "配置更新",
        "系统配置已更新，需要重启服务",
    )
    .with_priority(NotificationPriority::High);
    manager.send(&high).await;
    tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;

    // Normal 优先级
    let normal = Notification::new(
        "workflow-003",
        "task-005",
        "数据查询",
        "查询返回 50 条记录",
    )
    .with_priority(NotificationPriority::Normal);
    manager.send(&normal).await;
    tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;

    // Low 优先级
    let low = Notification::new(
        "workflow-003",
        "task-006",
        "后台任务",
        "日志清理任务已完成",
    )
    .with_priority(NotificationPriority::Low);
    manager.send(&low).await;
}

/// 场景 4: 智能优先级推断
async fn demo_smart_priority() {
    let manager = NotificationManager::new();
    manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    // 根据工具名称推断优先级
    let tools = vec![
        ("mcp:filesystem:delete", "删除文件操作"),
        ("mcp:filesystem:write_file", "写入文件操作"),
        ("mcp:filesystem:read_file", "读取文件操作"),
        ("custom:sync", "同步数据操作"),
    ];

    for (tool_name, description) in tools {
        let priority = NotificationPriority::from_tool_name(tool_name);
        let notification = Notification::new(
            "workflow-004",
            "task-auto",
            format!("工具调用: {}", tool_name),
            format!("{} (自动推断优先级: {:?})", description, priority),
        )
        .with_priority(priority);

        manager.send(&notification).await;
        tokio::time::sleep(tokio::time::Duration::from_millis(500)).await;
    }
}

/// 场景 5: 批量通知
async fn demo_batch_notifications() {
    let manager = NotificationManager::new();
    manager
        .register_channel(Arc::new(TerminalChannel::new()))
        .await;

    let notifications = vec![
        Notification::new(
            "workflow-005",
            "task-101",
            "子任务 1",
            "数据加载完成",
        ),
        Notification::new(
            "workflow-005",
            "task-102",
            "子任务 2",
            "数据转换完成",
        ),
        Notification::new(
            "workflow-005",
            "task-103",
            "子任务 3",
            "数据保存完成",
        ),
    ];

    println!("批量发送 {} 条通知...", notifications.len());
    let results = manager.send_batch(&notifications).await;
    println!("批量发送完成: {:?}", results);
}
