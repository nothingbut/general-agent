//! Workflow 持久化集成测试

use agent_storage::repository::{WorkflowRepository, WorkflowRecord, TaskRecord};
use agent_storage::Database;
use chrono::Utc;

#[tokio::test]
async fn test_save_and_load_workflow() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "test-workflow".to_string(),
        name: "Test Workflow".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: Some(r#"{"key":"value"}"#.to_string()),
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(3),
        completed_tasks: Some(0),
    };

    repo.save_workflow(&workflow).await.unwrap();

    // 加载 workflow
    let loaded = repo.get_workflow("test-workflow").await.unwrap().unwrap();

    assert_eq!(loaded.id, workflow.id);
    assert_eq!(loaded.name, workflow.name);
    assert_eq!(loaded.status, workflow.status);
    assert_eq!(loaded.total_tasks, Some(3));
    assert_eq!(loaded.completed_tasks, Some(0));
}

#[tokio::test]
async fn test_save_task_with_retry_history() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 先保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-1".to_string(),
        name: "Workflow 1".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(1),
        completed_tasks: Some(0),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 保存 task 带重试历史
    let retry_history = r#"{"attempts":[{"attempt":1,"error":"timeout","delay_ms":100}],"total_retries":1,"max_retries_reached":false}"#;

    let task = TaskRecord {
        id: "task-1".to_string(),
        workflow_id: "wf-1".to_string(),
        name: "Task 1".to_string(),
        task_type: r#"{"Custom":"test"}"#.to_string(),
        status: "completed".to_string(),
        dependencies: Some(r#"[]"#.to_string()),
        result: Some("Success".to_string()),
        error: None,
        execution_time_ms: Some(150),
        retry_history: Some(retry_history.to_string()),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: Some(Utc::now()),
    };

    repo.save_task(&task).await.unwrap();

    // 加载 task
    let tasks = repo.get_tasks("wf-1").await.unwrap();

    assert_eq!(tasks.len(), 1);
    assert_eq!(tasks[0].id, "task-1");
    assert!(tasks[0].retry_history.is_some());
    assert!(tasks[0].retry_history.as_ref().unwrap().contains("timeout"));
}

#[tokio::test]
async fn test_update_checkpoint() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-checkpoint".to_string(),
        name: "Checkpoint Workflow".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(5),
        completed_tasks: Some(0),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 更新断点信息
    let checkpoint_data = r#"{"completed":["task-1","task-2"],"pending":["task-3","task-4","task-5"]}"#;
    repo.update_checkpoint("wf-checkpoint", Some("task-2"), Some(checkpoint_data))
        .await
        .unwrap();

    // 验证更新
    let loaded = repo.get_workflow("wf-checkpoint").await.unwrap().unwrap();

    assert_eq!(loaded.last_completed_task, Some("task-2".to_string()));
    assert!(loaded.checkpoint_data.is_some());
    assert!(loaded.checkpoint_data.unwrap().contains("task-1"));
}

#[tokio::test]
async fn test_update_progress() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-progress".to_string(),
        name: "Progress Workflow".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(10),
        completed_tasks: Some(0),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 更新进度
    repo.update_progress("wf-progress", 10, 5).await.unwrap();

    // 验证更新
    let loaded = repo.get_workflow("wf-progress").await.unwrap().unwrap();

    assert_eq!(loaded.total_tasks, Some(10));
    assert_eq!(loaded.completed_tasks, Some(5));
}

#[tokio::test]
async fn test_get_resumable_workflows() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存暂停的 workflow
    let workflow1 = WorkflowRecord {
        id: "wf-paused-1".to_string(),
        name: "Paused Workflow 1".to_string(),
        status: "paused".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: Some(Utc::now()),
        metadata: None,
        last_completed_task: Some("task-1".to_string()),
        checkpoint_data: Some(r#"{"checkpoint":1}"#.to_string()),
        total_tasks: Some(5),
        completed_tasks: Some(1),
    };
    repo.save_workflow(&workflow1).await.unwrap();

    // 保存完成的 workflow
    let workflow2 = WorkflowRecord {
        id: "wf-completed-1".to_string(),
        name: "Completed Workflow".to_string(),
        status: "completed".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: Some(Utc::now()),
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(3),
        completed_tasks: Some(3),
    };
    repo.save_workflow(&workflow2).await.unwrap();

    // 获取可恢复的 workflows
    let resumable = repo.get_resumable_workflows().await.unwrap();

    assert_eq!(resumable.len(), 1);
    assert_eq!(resumable[0].id, "wf-paused-1");
    assert_eq!(resumable[0].status, "paused");
    assert_eq!(resumable[0].last_completed_task, Some("task-1".to_string()));
}

#[tokio::test]
async fn test_get_pending_tasks_after() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-pending".to_string(),
        name: "Pending Tasks Workflow".to_string(),
        status: "paused".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: Some(Utc::now()),
        metadata: None,
        last_completed_task: Some("task-2".to_string()),
        checkpoint_data: None,
        total_tasks: Some(5),
        completed_tasks: Some(2),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 保存多个 tasks
    let now = Utc::now();
    for i in 1..=5 {
        let task = TaskRecord {
            id: format!("task-{}", i),
            workflow_id: "wf-pending".to_string(),
            name: format!("Task {}", i),
            task_type: r#"{"Custom":"test"}"#.to_string(),
            status: if i <= 2 { "completed" } else { "pending" }.to_string(),
            dependencies: None,
            result: if i <= 2 { Some("Success".to_string()) } else { None },
            error: None,
            execution_time_ms: if i <= 2 { Some(100) } else { None },
            retry_history: None,
            created_at: now + chrono::Duration::seconds(i as i64),
            started_at: if i <= 2 { Some(now) } else { None },
            completed_at: if i <= 2 { Some(now) } else { None },
        };
        repo.save_task(&task).await.unwrap();
    }

    // 获取 task-2 之后的待执行任务
    let pending = repo.get_pending_tasks_after("wf-pending", Some("task-2"))
        .await
        .unwrap();

    assert_eq!(pending.len(), 3);
    assert_eq!(pending[0].id, "task-3");
    assert_eq!(pending[1].id, "task-4");
    assert_eq!(pending[2].id, "task-5");

    // 获取所有待执行任务（不指定断点）
    let all_pending = repo.get_pending_tasks_after("wf-pending", None)
        .await
        .unwrap();

    assert_eq!(all_pending.len(), 3);
}

#[tokio::test]
async fn test_execution_log() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-log".to_string(),
        name: "Log Workflow".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(3),
        completed_tasks: Some(0),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 保存执行日志
    repo.save_execution_log("wf-log", None, "workflow_start", Some(r#"{"timestamp":"now"}"#))
        .await
        .unwrap();
    repo.save_execution_log("wf-log", Some("task-1"), "task_start", None)
        .await
        .unwrap();
    repo.save_execution_log("wf-log", Some("task-1"), "task_complete", Some(r#"{"result":"success"}"#))
        .await
        .unwrap();

    // 获取执行日志
    let logs = repo.get_execution_logs("wf-log", Some(10)).await.unwrap();

    assert_eq!(logs.len(), 3);
    assert_eq!(logs[0].event_type, "task_complete"); // 最新的在前
    assert_eq!(logs[0].task_id, Some("task-1".to_string()));
    assert_eq!(logs[1].event_type, "task_start");
    assert_eq!(logs[2].event_type, "workflow_start");
}

#[tokio::test]
async fn test_workflow_stats() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-stats".to_string(),
        name: "Stats Workflow".to_string(),
        status: "running".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: None,
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(5),
        completed_tasks: Some(2),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 保存多个 tasks
    let now = Utc::now();
    for i in 1..=5 {
        let task = TaskRecord {
            id: format!("task-{}", i),
            workflow_id: "wf-stats".to_string(),
            name: format!("Task {}", i),
            task_type: r#"{"Custom":"test"}"#.to_string(),
            status: match i {
                1..=2 => "completed",
                3 => "failed",
                4 => "running",
                _ => "pending",
            }.to_string(),
            dependencies: None,
            result: if i <= 2 { Some("Success".to_string()) } else { None },
            error: if i == 3 { Some("Error".to_string()) } else { None },
            execution_time_ms: if i <= 2 { Some(100 * i) } else { None },
            retry_history: if i == 2 {
                Some(r#"{"attempts":[{"attempt":1,"error":"timeout"}]}"#.to_string())
            } else {
                None
            },
            created_at: now,
            started_at: if i <= 4 { Some(now) } else { None },
            completed_at: if i <= 2 { Some(now) } else { None },
        };
        repo.save_task(&task).await.unwrap();
    }

    // 获取统计信息
    let stats = repo.get_workflow_stats("wf-stats").await.unwrap().unwrap();

    assert_eq!(stats.total_tasks, 5);
    assert_eq!(stats.completed_tasks, 2);
    assert_eq!(stats.failed_tasks, 1);
    assert_eq!(stats.running_tasks, 1);
    assert_eq!(stats.pending_tasks, 1);
    assert_eq!(stats.tasks_with_retries, 1);
    assert_eq!(stats.total_execution_time_ms, 300); // 100 + 200
}

#[tokio::test]
async fn test_delete_workflow() {
    let db = Database::in_memory().await.unwrap();
    db.migrate().await.unwrap();

    let repo = WorkflowRepository::new(db.pool().clone());

    // 保存 workflow
    let workflow = WorkflowRecord {
        id: "wf-delete".to_string(),
        name: "Delete Workflow".to_string(),
        status: "completed".to_string(),
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: Some(Utc::now()),
        paused_at: None,
        metadata: None,
        last_completed_task: None,
        checkpoint_data: None,
        total_tasks: Some(1),
        completed_tasks: Some(1),
    };
    repo.save_workflow(&workflow).await.unwrap();

    // 保存 task
    let task = TaskRecord {
        id: "task-1".to_string(),
        workflow_id: "wf-delete".to_string(),
        name: "Task 1".to_string(),
        task_type: r#"{"Custom":"test"}"#.to_string(),
        status: "completed".to_string(),
        dependencies: None,
        result: Some("Success".to_string()),
        error: None,
        execution_time_ms: Some(100),
        retry_history: None,
        created_at: Utc::now(),
        started_at: Some(Utc::now()),
        completed_at: Some(Utc::now()),
    };
    repo.save_task(&task).await.unwrap();

    // 保存执行日志
    repo.save_execution_log("wf-delete", None, "workflow_start", None)
        .await
        .unwrap();

    // 确认数据存在
    assert!(repo.get_workflow("wf-delete").await.unwrap().is_some());
    assert_eq!(repo.get_tasks("wf-delete").await.unwrap().len(), 1);
    assert_eq!(repo.get_execution_logs("wf-delete", None).await.unwrap().len(), 1);

    // 删除 workflow
    repo.delete_workflow("wf-delete").await.unwrap();

    // 确认数据已删除（级联删除）
    assert!(repo.get_workflow("wf-delete").await.unwrap().is_none());
    assert_eq!(repo.get_tasks("wf-delete").await.unwrap().len(), 0);
    assert_eq!(repo.get_execution_logs("wf-delete", None).await.unwrap().len(), 0);
}
