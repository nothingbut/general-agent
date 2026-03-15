//! Performance Overlay 集成测试

use agent_tui::ui::PerformanceOverlay;
use agent_workflow::workflow::performance::PerformanceMonitor;
use std::sync::{Arc, Mutex};

#[test]
fn test_overlay_with_real_data() {
    // 创建监控器并添加真实工作流数据
    let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));

    {
        let mut mon = monitor.lock().unwrap();

        // 添加一个完整的工作流
        mon.start_workflow("test-wf-1", 5);
        for i in 0..5 {
            mon.start_task("test-wf-1", format!("task-{}", i), format!("Task {}", i));
            mon.complete_task(
                "test-wf-1",
                &format!("task-{}", i),
                "completed".to_string(),
                100 + i * 20,
                0,
            );
        }
        mon.complete_workflow("test-wf-1");
    }

    // 创建 overlay
    let overlay = PerformanceOverlay::new(monitor.clone());

    // 验证可以获取工作流列表
    let workflows = overlay.get_workflow_list();
    assert_eq!(workflows.len(), 1);
    assert_eq!(workflows[0], "test-wf-1");

    // 验证可以获取指标
    let metrics = overlay.get_current_metrics();
    assert!(metrics.is_some());

    let metrics = metrics.unwrap();
    assert_eq!(metrics.workflow_id, "test-wf-1");
    assert_eq!(metrics.total_tasks, 5);
    assert_eq!(metrics.completed_tasks, 5);
    assert_eq!(metrics.failed_tasks, 0);
}

#[test]
fn test_overlay_with_multiple_workflows() {
    let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));

    {
        let mut mon = monitor.lock().unwrap();

        // 添加多个工作流
        for wf_idx in 1..=3 {
            let wf_id = format!("wf-{}", wf_idx);
            mon.start_workflow(&wf_id, 3);

            for task_idx in 0..3 {
                mon.start_task(&wf_id, format!("task-{}", task_idx), format!("Task {}", task_idx));
                mon.complete_task(
                    &wf_id,
                    &format!("task-{}", task_idx),
                    "completed".to_string(),
                    50,
                    0,
                );
            }
            mon.complete_workflow(&wf_id);
        }
    }

    let mut overlay = PerformanceOverlay::new(monitor.clone());

    // 验证工作流数量
    let workflows = overlay.get_workflow_list();
    assert_eq!(workflows.len(), 3);

    // 验证可以切换工作流
    overlay.next_workflow();
    let metrics = overlay.get_current_metrics().unwrap();
    assert!(metrics.workflow_id.starts_with("wf-"));

    overlay.next_workflow();
    let metrics = overlay.get_current_metrics().unwrap();
    assert!(metrics.workflow_id.starts_with("wf-"));

    // 验证循环导航
    overlay.next_workflow();
    overlay.next_workflow(); // 应该回到第一个
    let metrics = overlay.get_current_metrics().unwrap();
    assert!(metrics.workflow_id.starts_with("wf-"));
}

#[test]
fn test_overlay_with_failed_tasks() {
    let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));

    {
        let mut mon = monitor.lock().unwrap();

        mon.start_workflow("wf-with-failures", 5);
        for i in 0..5 {
            mon.start_task("wf-with-failures", format!("task-{}", i), format!("Task {}", i));
            let status = if i == 2 { "failed" } else { "completed" };
            mon.complete_task(
                "wf-with-failures",
                &format!("task-{}", i),
                status.to_string(),
                100,
                if status == "failed" { 3 } else { 0 },
            );
        }
        mon.complete_workflow("wf-with-failures");
    }

    let overlay = PerformanceOverlay::new(monitor.clone());
    let metrics = overlay.get_current_metrics().unwrap();

    assert_eq!(metrics.completed_tasks, 4);
    assert_eq!(metrics.failed_tasks, 1);
    assert!(metrics.completed_at.is_some());
}

#[test]
fn test_overlay_metrics_calculations() {
    let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));

    {
        let mut mon = monitor.lock().unwrap();

        mon.start_workflow("metrics-test", 10);

        // 添加任务，持续时间从 10ms 到 100ms
        for i in 0..10 {
            mon.start_task("metrics-test", format!("task-{}", i), format!("Task {}", i));
            mon.complete_task(
                "metrics-test",
                &format!("task-{}", i),
                "completed".to_string(),
                (i + 1) * 10,
                0,
            );
        }
        mon.complete_workflow("metrics-test");
    }

    let overlay = PerformanceOverlay::new(monitor.clone());
    let metrics = overlay.get_current_metrics().unwrap();

    // 验证统计计算
    assert!(metrics.avg_task_duration_ms > 0.0);
    assert!(metrics.median_task_duration_ms > 0.0);
    assert!(metrics.p95_task_duration_ms > 0.0);
    assert!(metrics.p99_task_duration_ms > 0.0);

    // 验证百分位数关系
    assert!(metrics.median_task_duration_ms <= metrics.p95_task_duration_ms);
    assert!(metrics.p95_task_duration_ms <= metrics.p99_task_duration_ms);

    // 验证吞吐量
    assert!(metrics.throughput > 0.0);
}
