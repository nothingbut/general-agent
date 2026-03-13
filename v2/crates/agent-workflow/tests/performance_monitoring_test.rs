//! 性能监控框架集成测试

use agent_workflow::workflow::*;

#[test]
fn test_workflow_metrics_basic() {
    let metrics = WorkflowMetrics::new("wf-1".to_string(), 5);

    assert_eq!(metrics.workflow_id, "wf-1");
    assert_eq!(metrics.total_tasks, 5);
    assert_eq!(metrics.completed_tasks, 0);
    assert_eq!(metrics.failed_tasks, 0);
    assert_eq!(metrics.total_duration_ms, 0);
}

#[test]
fn test_workflow_metrics_complete() {
    let mut metrics = WorkflowMetrics::new("wf-test".to_string(), 3);

    // 模拟任务执行时间
    let task_durations = vec![100, 200, 150];

    // 等待一小段时间
    std::thread::sleep(std::time::Duration::from_millis(50));

    // 完成工作流
    metrics.complete(&task_durations);

    // 验证指标
    assert!(metrics.completed_at.is_some());
    assert!(metrics.total_duration_ms >= 50); // 至少等待了 50ms
    assert_eq!(metrics.avg_task_duration_ms, 150.0); // (100+200+150)/3

    // 排序后: [100, 150, 200]
    // 中位数 (50%): 150
    // P95 (95%): 150 + 0.95 * (200 - 150) = 195
    // P99 (99%): 150 + 0.99 * (200 - 150) = 199.5
    assert_eq!(metrics.median_task_duration_ms, 150.0);
    assert!(metrics.p95_task_duration_ms >= 195.0 && metrics.p95_task_duration_ms <= 200.0);
    assert!(metrics.p99_task_duration_ms >= 199.0 && metrics.p99_task_duration_ms <= 200.0);

    // 验证吞吐量
    assert!(metrics.throughput > 0.0);
}

#[test]
fn test_workflow_metrics_report() {
    let mut metrics = WorkflowMetrics::new("wf-report".to_string(), 3);
    metrics.completed_tasks = 2;
    metrics.failed_tasks = 1;
    metrics.total_duration_ms = 500;
    metrics.avg_task_duration_ms = 150.0;
    metrics.peak_memory_mb = 128.5;
    metrics.avg_cpu_percent = 45.2;

    let report = metrics.generate_report();

    // 验证报告内容
    assert!(report.contains("wf-report"));
    assert!(report.contains("总任务数: 3"));
    assert!(report.contains("已完成: 2"));
    assert!(report.contains("失败: 1"));
    assert!(report.contains("500ms"));
    assert!(report.contains("150.00ms"));
    assert!(report.contains("128.50MB"));
    assert!(report.contains("45.20%"));
}

#[test]
fn test_task_metrics_basic() {
    let task = TaskMetrics::new(
        "task-1".to_string(),
        "Test Task".to_string(),
        "wf-1".to_string(),
    );

    assert_eq!(task.task_id, "task-1");
    assert_eq!(task.task_name, "Test Task");
    assert_eq!(task.workflow_id, "wf-1");
    assert_eq!(task.status, "pending");
    assert_eq!(task.duration_ms, 0);
}

#[test]
fn test_task_metrics_complete() {
    let mut task = TaskMetrics::new(
        "task-1".to_string(),
        "Test Task".to_string(),
        "wf-1".to_string(),
    );

    task.complete("completed".to_string(), 250);

    assert!(task.completed_at.is_some());
    assert_eq!(task.status, "completed");
    assert_eq!(task.duration_ms, 250);
}

#[test]
fn test_performance_monitor_workflow_lifecycle() {
    let mut monitor = PerformanceMonitor::new();

    // 开始工作流
    monitor.start_workflow("wf-lifecycle", 3);

    // 验证指标已创建
    let metrics = monitor.get_workflow_metrics("wf-lifecycle");
    assert!(metrics.is_some());
    assert_eq!(metrics.unwrap().total_tasks, 3);

    // 等待一小段时间
    std::thread::sleep(std::time::Duration::from_millis(50));

    // 完成工作流
    monitor.complete_workflow("wf-lifecycle");

    // 验证指标已更新
    let metrics = monitor.get_workflow_metrics("wf-lifecycle");
    assert!(metrics.is_some());

    let metrics = metrics.unwrap();
    assert!(metrics.completed_at.is_some());
    assert!(metrics.total_duration_ms >= 50);
}

#[test]
fn test_performance_monitor_task_tracking() {
    let mut monitor = PerformanceMonitor::new();

    // 开始工作流
    monitor.start_workflow("wf-tracking", 3);

    // 记录任务 1
    monitor.start_task("wf-tracking", "task-1", "Task 1");
    monitor.complete_task("wf-tracking", "task-1", "completed".to_string(), 100, 0);

    // 记录任务 2（带重试）
    monitor.start_task("wf-tracking", "task-2", "Task 2");
    monitor.complete_task("wf-tracking", "task-2", "completed".to_string(), 200, 2);

    // 记录任务 3（失败）
    monitor.start_task("wf-tracking", "task-3", "Task 3");
    monitor.complete_task("wf-tracking", "task-3", "failed".to_string(), 50, 1);

    // 验证任务指标
    let tasks = monitor.get_task_metrics("wf-tracking");
    assert!(tasks.is_some());

    let tasks = tasks.unwrap();
    assert_eq!(tasks.len(), 3);

    // 验证第一个任务
    assert_eq!(tasks[0].task_id, "task-1");
    assert_eq!(tasks[0].status, "completed");
    assert_eq!(tasks[0].duration_ms, 100);
    assert_eq!(tasks[0].retry_count, 0);

    // 验证第二个任务
    assert_eq!(tasks[1].task_id, "task-2");
    assert_eq!(tasks[1].retry_count, 2);

    // 验证第三个任务
    assert_eq!(tasks[2].task_id, "task-3");
    assert_eq!(tasks[2].status, "failed");

    // 验证工作流统计
    let workflow_metrics = monitor.get_workflow_metrics("wf-tracking");
    assert!(workflow_metrics.is_some());

    let metrics = workflow_metrics.unwrap();
    assert_eq!(metrics.completed_tasks, 2); // task-1 和 task-2
    assert_eq!(metrics.failed_tasks, 1); // task-3
}

#[test]
fn test_performance_monitor_report_generation() {
    let mut monitor = PerformanceMonitor::new();

    // 创建工作流
    monitor.start_workflow("wf-report-gen", 2);
    monitor.start_task("wf-report-gen", "task-1", "Task 1");
    monitor.complete_task("wf-report-gen", "task-1", "completed".to_string(), 100, 0);
    monitor.start_task("wf-report-gen", "task-2", "Task 2");
    monitor.complete_task("wf-report-gen", "task-2", "completed".to_string(), 200, 0);

    // 等待一小段时间
    std::thread::sleep(std::time::Duration::from_millis(50));

    // 完成工作流
    monitor.complete_workflow("wf-report-gen");

    // 生成报告
    let report = monitor.generate_report("wf-report-gen");
    assert!(report.is_some());

    let report_text = report.unwrap();
    assert!(report_text.contains("工作流性能报告"));
    assert!(report_text.contains("wf-report-gen"));
    assert!(report_text.contains("总任务数: 2"));
    assert!(report_text.contains("已完成: 2"));
}

#[test]
fn test_performance_summary_multiple_workflows() {
    let mut monitor = PerformanceMonitor::new();

    // 工作流 1
    monitor.start_workflow("wf-1", 2);
    monitor.start_task("wf-1", "task-1", "Task 1");
    monitor.complete_task("wf-1", "task-1", "completed".to_string(), 100, 0);
    monitor.start_task("wf-1", "task-2", "Task 2");
    monitor.complete_task("wf-1", "task-2", "completed".to_string(), 150, 0);
    monitor.complete_workflow("wf-1");

    // 工作流 2
    monitor.start_workflow("wf-2", 3);
    monitor.start_task("wf-2", "task-3", "Task 3");
    monitor.complete_task("wf-2", "task-3", "completed".to_string(), 200, 0);
    monitor.start_task("wf-2", "task-4", "Task 4");
    monitor.complete_task("wf-2", "task-4", "failed".to_string(), 50, 2);
    monitor.start_task("wf-2", "task-5", "Task 5");
    monitor.complete_task("wf-2", "task-5", "completed".to_string(), 120, 1);
    monitor.complete_workflow("wf-2");

    // 获取汇总
    let summary = monitor.get_summary();

    assert_eq!(summary.total_workflows, 2);
    assert_eq!(summary.total_tasks, 5);
    assert_eq!(summary.completed_tasks, 4); // wf-1: 2, wf-2: 2
    assert_eq!(summary.failed_tasks, 1); // wf-2: 1

    // 生成汇总报告
    let report = summary.generate_report();
    assert!(report.contains("性能汇总报告"));
    assert!(report.contains("总工作流数: 2"));
    assert!(report.contains("总任务数: 5"));
    assert!(report.contains("已完成: 4"));
    assert!(report.contains("失败: 1"));
}

#[tokio::test]
async fn test_performance_monitor_with_real_workflow() {
    let mut monitor = PerformanceMonitor::new();

    // 创建简单工作流
    let mut workflow = Workflow::new("perf-test", "Performance Test");
    workflow.add_task(Task::new("A", "Task A", TaskType::Custom("test".to_string())));
    workflow.add_task(
        Task::new("B", "Task B", TaskType::Custom("test".to_string()))
            .with_dependency("A"),
    );

    // 开始监控
    monitor.start_workflow(&workflow.id, workflow.tasks.len() as u32);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let executor = TaskExecutor::new();

    // 记录任务执行（手动模拟）
    let wf_id = orchestrator.workflow_id();
    for task_id in ["A", "B"] {
        monitor.start_task(wf_id, task_id, task_id);
    }

    let result = orchestrator.execute(&executor).await.unwrap();

    // 记录任务完成
    for (task_id, task_result) in &result.task_results {
        let status = match task_result.status {
            TaskStatus::Completed => "completed",
            TaskStatus::Failed(_) => "failed",
            _ => "unknown",
        };

        monitor.complete_task(
            wf_id,
            task_id,
            status.to_string(),
            task_result.execution_time_ms,
            task_result.retry_history.total_retries,
        );
    }

    // 完成监控
    monitor.complete_workflow("perf-test");

    // 验证指标
    let metrics = monitor.get_workflow_metrics("perf-test");
    assert!(metrics.is_some());

    let metrics = metrics.unwrap();
    assert_eq!(metrics.total_tasks, 2);
    assert_eq!(metrics.completed_tasks, 2);
    assert!(metrics.total_duration_ms > 0);

    // 生成报告
    let report = monitor.generate_report("perf-test");
    assert!(report.is_some());
    println!("\n{}", report.unwrap());
}

#[test]
fn test_percentile_calculations() {
    let mut monitor = PerformanceMonitor::new();

    // 创建工作流，模拟不同执行时间的任务
    monitor.start_workflow("wf-percentile", 10);

    for i in 1..=10 {
        monitor.start_task("wf-percentile", &format!("task-{}", i), &format!("Task {}", i));
        monitor.complete_task(
            "wf-percentile",
            &format!("task-{}", i),
            "completed".to_string(),
            i * 100, // 100, 200, 300, ..., 1000 ms
            0,
        );
    }

    monitor.complete_workflow("wf-percentile");

    let metrics = monitor.get_workflow_metrics("wf-percentile").unwrap();

    // 验证统计指标
    // 排序后: [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000]
    // 平均值: 5500/10 = 550
    // 中位数 (50%): 第4.5个位置 = 500 + 0.5*(600-500) = 550
    // P95 (95%): 第8.55个位置 = 900 + 0.55*(1000-900) = 955
    // P99 (99%): 第8.91个位置 = 900 + 0.91*(1000-900) = 991
    assert_eq!(metrics.avg_task_duration_ms, 550.0);
    assert_eq!(metrics.median_task_duration_ms, 550.0);
    assert!(metrics.p95_task_duration_ms >= 950.0 && metrics.p95_task_duration_ms <= 960.0);
    assert!(metrics.p99_task_duration_ms >= 990.0 && metrics.p99_task_duration_ms <= 995.0);
}

#[test]
fn test_performance_monitor_edge_cases() {
    let mut monitor = PerformanceMonitor::new();

    // 测试空工作流
    monitor.start_workflow("wf-empty", 0);
    monitor.complete_workflow("wf-empty");

    let metrics = monitor.get_workflow_metrics("wf-empty").unwrap();
    assert_eq!(metrics.total_tasks, 0);
    assert_eq!(metrics.throughput, 0.0); // 无任务时吞吐量为0

    // 测试不存在的工作流
    assert!(monitor.get_workflow_metrics("non-existent").is_none());
    assert!(monitor.get_task_metrics("non-existent").is_none());
    assert!(monitor.generate_report("non-existent").is_none());
}

#[test]
fn test_performance_monitor_concurrent_workflows() {
    let mut monitor = PerformanceMonitor::new();

    // 同时监控多个工作流
    for i in 1..=5 {
        let wf_id = format!("wf-{}", i);
        monitor.start_workflow(&wf_id, 3);

        for j in 1..=3 {
            let task_id = format!("task-{}-{}", i, j);
            monitor.start_task(&wf_id, &task_id, &task_id);
            monitor.complete_task(&wf_id, &task_id, "completed".to_string(), j * 50, 0);
        }

        monitor.complete_workflow(&wf_id);
    }

    // 验证所有工作流都被正确跟踪
    for i in 1..=5 {
        let wf_id = format!("wf-{}", i);
        let metrics = monitor.get_workflow_metrics(&wf_id);
        assert!(metrics.is_some());
        assert_eq!(metrics.unwrap().total_tasks, 3);
    }

    // 验证汇总
    let summary = monitor.get_summary();
    assert_eq!(summary.total_workflows, 5);
    assert_eq!(summary.total_tasks, 15);
    assert_eq!(summary.completed_tasks, 15);
}
