//! 性能监控框架
//!
//! 本模块实现了工作流和任务的性能监控：
//! - 执行时间监控（任务级、工作流级）
//! - 资源使用监控（内存、CPU）
//! - 性能指标收集
//! - 性能报告生成
//!
//! # 示例
//!
//! ```rust
//! use agent_workflow::workflow::performance::*;
//!
//! // 创建性能监控器
//! let mut monitor = PerformanceMonitor::new();
//!
//! // 监控工作流
//! monitor.start_workflow("wf-1", 3);
//! // ... 执行任务 ...
//! monitor.complete_workflow("wf-1");
//!
//! // 获取指标
//! let metrics = monitor.get_workflow_metrics("wf-1").unwrap();
//! println!("总耗时: {}ms", metrics.total_duration_ms);
//! ```

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::Instant;

/// 工作流级别性能指标
///
/// 记录整个工作流的执行情况和资源使用。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WorkflowMetrics {
    /// 工作流 ID
    pub workflow_id: String,
    /// 总任务数
    pub total_tasks: u32,
    /// 已完成任务数
    pub completed_tasks: u32,
    /// 失败任务数
    pub failed_tasks: u32,
    /// 开始时间
    pub started_at: DateTime<Utc>,
    /// 完成时间（可选）
    pub completed_at: Option<DateTime<Utc>>,
    /// 总执行时间（毫秒）
    pub total_duration_ms: u64,
    /// 吞吐量（任务/秒）
    pub throughput: f64,
    /// 平均任务执行时间（毫秒）
    pub avg_task_duration_ms: f64,
    /// 任务执行时间中位数（毫秒）
    pub median_task_duration_ms: f64,
    /// 任务执行时间 P95（毫秒）
    pub p95_task_duration_ms: f64,
    /// 任务执行时间 P99（毫秒）
    pub p99_task_duration_ms: f64,
    /// 峰值内存使用（MB）
    pub peak_memory_mb: f64,
    /// 平均 CPU 使用率（百分比）
    pub avg_cpu_percent: f64,
}

impl WorkflowMetrics {
    /// 创建新的工作流指标
    pub fn new(workflow_id: String, total_tasks: u32) -> Self {
        Self {
            workflow_id,
            total_tasks,
            completed_tasks: 0,
            failed_tasks: 0,
            started_at: Utc::now(),
            completed_at: None,
            total_duration_ms: 0,
            throughput: 0.0,
            avg_task_duration_ms: 0.0,
            median_task_duration_ms: 0.0,
            p95_task_duration_ms: 0.0,
            p99_task_duration_ms: 0.0,
            peak_memory_mb: 0.0,
            avg_cpu_percent: 0.0,
        }
    }

    /// 完成工作流
    pub fn complete(&mut self, task_durations: &[u64]) {
        self.completed_at = Some(Utc::now());

        // 计算总执行时间
        if let Some(completed) = self.completed_at {
            self.total_duration_ms = (completed - self.started_at).num_milliseconds() as u64;
        }

        // 计算吞吐量（任务/秒）
        // 使用 task_durations 的长度而不是 completed_tasks，因为 complete() 可能在设置 completed_tasks 之前被调用
        let completed_count = if self.completed_tasks > 0 {
            self.completed_tasks
        } else {
            task_durations.len() as u32
        };

        if self.total_duration_ms > 0 {
            self.throughput = (completed_count as f64) / (self.total_duration_ms as f64 / 1000.0);
        }

        // 计算任务执行时间统计
        if !task_durations.is_empty() {
            self.avg_task_duration_ms = task_durations.iter().sum::<u64>() as f64 / task_durations.len() as f64;

            // 计算百分位数
            let mut sorted = task_durations.to_vec();
            sorted.sort_unstable();

            self.median_task_duration_ms = percentile(&sorted, 50.0);
            self.p95_task_duration_ms = percentile(&sorted, 95.0);
            self.p99_task_duration_ms = percentile(&sorted, 99.0);
        }
    }

    /// 生成性能报告
    pub fn generate_report(&self) -> String {
        format!(
            r#"工作流性能报告
================
工作流 ID: {}
总任务数: {}
已完成: {} | 失败: {}
总耗时: {}ms
吞吐量: {:.2} 任务/秒

任务执行时间:
  - 平均: {:.2}ms
  - 中位数: {:.2}ms
  - P95: {:.2}ms
  - P99: {:.2}ms

资源使用:
  - 峰值内存: {:.2}MB
  - 平均 CPU: {:.2}%
"#,
            self.workflow_id,
            self.total_tasks,
            self.completed_tasks,
            self.failed_tasks,
            self.total_duration_ms,
            self.throughput,
            self.avg_task_duration_ms,
            self.median_task_duration_ms,
            self.p95_task_duration_ms,
            self.p99_task_duration_ms,
            self.peak_memory_mb,
            self.avg_cpu_percent
        )
    }
}

/// 任务级别性能指标
///
/// 记录单个任务的执行情况。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskMetrics {
    /// 任务 ID
    pub task_id: String,
    /// 任务名称
    pub task_name: String,
    /// 工作流 ID
    pub workflow_id: String,
    /// 开始时间
    pub started_at: DateTime<Utc>,
    /// 完成时间（可选）
    pub completed_at: Option<DateTime<Utc>>,
    /// 执行时间（毫秒）
    pub duration_ms: u64,
    /// 任务状态
    pub status: String,
    /// 重试次数
    pub retry_count: u32,
    /// 内存使用（字节）
    pub memory_used_bytes: Option<u64>,
    /// CPU 时间（毫秒）
    pub cpu_time_ms: Option<u64>,
}

impl TaskMetrics {
    /// 创建新的任务指标
    pub fn new(task_id: String, task_name: String, workflow_id: String) -> Self {
        Self {
            task_id,
            task_name,
            workflow_id,
            started_at: Utc::now(),
            completed_at: None,
            duration_ms: 0,
            status: "pending".to_string(),
            retry_count: 0,
            memory_used_bytes: None,
            cpu_time_ms: None,
        }
    }

    /// 完成任务
    pub fn complete(&mut self, status: String, duration_ms: u64) {
        self.completed_at = Some(Utc::now());
        self.status = status;
        self.duration_ms = duration_ms;
    }
}

/// 性能监控器
///
/// 负责收集和管理工作流和任务的性能指标。
#[derive(Debug, Clone)]
pub struct PerformanceMonitor {
    /// 工作流指标
    workflow_metrics: HashMap<String, WorkflowMetrics>,
    /// 任务指标
    task_metrics: HashMap<String, Vec<TaskMetrics>>,
    /// 工作流开始时间
    workflow_start_times: HashMap<String, Instant>,
}

impl PerformanceMonitor {
    /// 创建新的性能监控器
    pub fn new() -> Self {
        Self {
            workflow_metrics: HashMap::new(),
            task_metrics: HashMap::new(),
            workflow_start_times: HashMap::new(),
        }
    }

    /// 开始监控工作流
    pub fn start_workflow(&mut self, workflow_id: impl Into<String>, total_tasks: u32) {
        let workflow_id = workflow_id.into();
        let metrics = WorkflowMetrics::new(workflow_id.clone(), total_tasks);

        self.workflow_metrics.insert(workflow_id.clone(), metrics);
        self.workflow_start_times.insert(workflow_id.clone(), Instant::now());
        self.task_metrics.insert(workflow_id, Vec::new());
    }

    /// 完成工作流监控
    pub fn complete_workflow(&mut self, workflow_id: &str) {
        if let Some(metrics) = self.workflow_metrics.get_mut(workflow_id) {
            // 获取所有任务的执行时间
            let task_durations: Vec<u64> = self
                .task_metrics
                .get(workflow_id)
                .map(|tasks| tasks.iter().map(|t| t.duration_ms).collect())
                .unwrap_or_default();

            metrics.complete(&task_durations);

            // 获取系统资源使用情况
            #[cfg(target_os = "linux")]
            {
                use std::fs;
                if let Ok(status) = fs::read_to_string("/proc/self/status") {
                    for line in status.lines() {
                        if line.starts_with("VmRSS:") {
                            if let Some(mem) = line.split_whitespace().nth(1) {
                                if let Ok(mem_kb) = mem.parse::<u64>() {
                                    metrics.peak_memory_mb = mem_kb as f64 / 1024.0;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            // 对于非 Linux 系统，使用默认值或估算值
            #[cfg(not(target_os = "linux"))]
            {
                // 简单估算：假设每个任务使用 1MB 内存
                metrics.peak_memory_mb = metrics.total_tasks as f64;
            }

            // CPU 使用率估算（基于执行时间）
            if let Some(start) = self.workflow_start_times.get(workflow_id) {
                let wall_time = start.elapsed().as_secs_f64();
                let cpu_time = metrics.total_duration_ms as f64 / 1000.0;
                if wall_time > 0.0 {
                    metrics.avg_cpu_percent = (cpu_time / wall_time) * 100.0;
                }
            }
        }
    }

    /// 记录任务开始
    pub fn start_task(&mut self, workflow_id: &str, task_id: impl Into<String>, task_name: impl Into<String>) {
        let task_id = task_id.into();
        let task_name = task_name.into();

        let task = TaskMetrics::new(task_id, task_name, workflow_id.to_string());

        if let Some(tasks) = self.task_metrics.get_mut(workflow_id) {
            tasks.push(task);
        }
    }

    /// 记录任务完成
    pub fn complete_task(
        &mut self,
        workflow_id: &str,
        task_id: &str,
        status: String,
        duration_ms: u64,
        retry_count: u32,
    ) {
        // 更新任务指标
        if let Some(tasks) = self.task_metrics.get_mut(workflow_id) {
            if let Some(task) = tasks.iter_mut().find(|t| t.task_id == task_id) {
                task.complete(status.clone(), duration_ms);
                task.retry_count = retry_count;
            }
        }

        // 更新工作流指标
        if let Some(metrics) = self.workflow_metrics.get_mut(workflow_id) {
            if status == "completed" {
                metrics.completed_tasks += 1;
            } else if status.starts_with("failed") {
                metrics.failed_tasks += 1;
            }
        }
    }

    /// 获取工作流指标
    pub fn get_workflow_metrics(&self, workflow_id: &str) -> Option<&WorkflowMetrics> {
        self.workflow_metrics.get(workflow_id)
    }

    /// 获取任务指标列表
    pub fn get_task_metrics(&self, workflow_id: &str) -> Option<&Vec<TaskMetrics>> {
        self.task_metrics.get(workflow_id)
    }

    /// 生成性能报告
    pub fn generate_report(&self, workflow_id: &str) -> Option<String> {
        self.get_workflow_metrics(workflow_id)
            .map(|metrics| metrics.generate_report())
    }

    /// 获取所有工作流的汇总统计
    pub fn get_summary(&self) -> PerformanceSummary {
        let total_workflows = self.workflow_metrics.len();
        let total_tasks: u32 = self.workflow_metrics.values().map(|m| m.total_tasks).sum();
        let completed_tasks: u32 = self.workflow_metrics.values().map(|m| m.completed_tasks).sum();
        let failed_tasks: u32 = self.workflow_metrics.values().map(|m| m.failed_tasks).sum();

        let avg_duration_ms = if total_workflows > 0 {
            self.workflow_metrics
                .values()
                .map(|m| m.total_duration_ms)
                .sum::<u64>() as f64
                / total_workflows as f64
        } else {
            0.0
        };

        let avg_throughput = if total_workflows > 0 {
            self.workflow_metrics
                .values()
                .map(|m| m.throughput)
                .sum::<f64>()
                / total_workflows as f64
        } else {
            0.0
        };

        PerformanceSummary {
            total_workflows,
            total_tasks,
            completed_tasks,
            failed_tasks,
            avg_workflow_duration_ms: avg_duration_ms,
            avg_throughput,
        }
    }
}

impl Default for PerformanceMonitor {
    fn default() -> Self {
        Self::new()
    }
}

/// 性能汇总统计
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PerformanceSummary {
    /// 总工作流数
    pub total_workflows: usize,
    /// 总任务数
    pub total_tasks: u32,
    /// 已完成任务数
    pub completed_tasks: u32,
    /// 失败任务数
    pub failed_tasks: u32,
    /// 平均工作流执行时间（毫秒）
    pub avg_workflow_duration_ms: f64,
    /// 平均吞吐量（任务/秒）
    pub avg_throughput: f64,
}

impl PerformanceSummary {
    /// 生成汇总报告
    pub fn generate_report(&self) -> String {
        format!(
            r#"性能汇总报告
============
总工作流数: {}
总任务数: {}
已完成: {} | 失败: {}
成功率: {:.2}%

平均工作流执行时间: {:.2}ms
平均吞吐量: {:.2} 任务/秒
"#,
            self.total_workflows,
            self.total_tasks,
            self.completed_tasks,
            self.failed_tasks,
            if self.total_tasks > 0 {
                (self.completed_tasks as f64 / self.total_tasks as f64) * 100.0
            } else {
                0.0
            },
            self.avg_workflow_duration_ms,
            self.avg_throughput
        )
    }
}

/// 计算百分位数
fn percentile(sorted_data: &[u64], p: f64) -> f64 {
    if sorted_data.is_empty() {
        return 0.0;
    }

    let n = sorted_data.len();
    let rank = (p / 100.0) * (n as f64 - 1.0);
    let lower = rank.floor() as usize;
    let upper = rank.ceil() as usize;

    if lower == upper {
        sorted_data[lower] as f64
    } else {
        let weight = rank - lower as f64;
        let lower_value = sorted_data[lower] as f64;
        let upper_value = sorted_data[upper] as f64;
        lower_value + weight * (upper_value - lower_value)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_workflow_metrics_creation() {
        let metrics = WorkflowMetrics::new("wf-1".to_string(), 5);
        assert_eq!(metrics.workflow_id, "wf-1");
        assert_eq!(metrics.total_tasks, 5);
        assert_eq!(metrics.completed_tasks, 0);
        assert_eq!(metrics.failed_tasks, 0);
    }

    #[test]
    fn test_workflow_metrics_complete() {
        let mut metrics = WorkflowMetrics::new("wf-1".to_string(), 3);
        let task_durations = vec![100, 200, 150];

        std::thread::sleep(std::time::Duration::from_millis(10));
        metrics.complete(&task_durations);

        assert!(metrics.completed_at.is_some());
        assert!(metrics.total_duration_ms > 0);
        assert_eq!(metrics.avg_task_duration_ms, 150.0);
        assert_eq!(metrics.median_task_duration_ms, 150.0);
    }

    #[test]
    fn test_task_metrics_creation() {
        let task = TaskMetrics::new("task-1".to_string(), "Test Task".to_string(), "wf-1".to_string());
        assert_eq!(task.task_id, "task-1");
        assert_eq!(task.task_name, "Test Task");
        assert_eq!(task.workflow_id, "wf-1");
        assert_eq!(task.status, "pending");
    }

    #[test]
    fn test_task_metrics_complete() {
        let mut task = TaskMetrics::new("task-1".to_string(), "Test Task".to_string(), "wf-1".to_string());
        task.complete("completed".to_string(), 150);

        assert!(task.completed_at.is_some());
        assert_eq!(task.status, "completed");
        assert_eq!(task.duration_ms, 150);
    }

    #[test]
    fn test_performance_monitor_workflow() {
        let mut monitor = PerformanceMonitor::new();

        // 开始工作流
        monitor.start_workflow("wf-1", 3);

        // 验证指标已创建
        let metrics = monitor.get_workflow_metrics("wf-1");
        assert!(metrics.is_some());
        assert_eq!(metrics.unwrap().total_tasks, 3);

        // 完成工作流
        monitor.complete_workflow("wf-1");

        // 验证指标已更新
        let metrics = monitor.get_workflow_metrics("wf-1");
        assert!(metrics.is_some());
        assert!(metrics.unwrap().completed_at.is_some());
    }

    #[test]
    fn test_performance_monitor_task() {
        let mut monitor = PerformanceMonitor::new();

        // 开始工作流
        monitor.start_workflow("wf-1", 2);

        // 记录任务
        monitor.start_task("wf-1", "task-1", "Task 1");
        monitor.complete_task("wf-1", "task-1", "completed".to_string(), 100, 0);

        monitor.start_task("wf-1", "task-2", "Task 2");
        monitor.complete_task("wf-1", "task-2", "completed".to_string(), 200, 1);

        // 验证任务指标
        let tasks = monitor.get_task_metrics("wf-1");
        assert!(tasks.is_some());
        assert_eq!(tasks.unwrap().len(), 2);

        // 验证工作流指标更新
        let metrics = monitor.get_workflow_metrics("wf-1");
        assert!(metrics.is_some());
        assert_eq!(metrics.unwrap().completed_tasks, 2);
    }

    #[test]
    fn test_performance_monitor_report() {
        let mut monitor = PerformanceMonitor::new();

        monitor.start_workflow("wf-1", 3);
        monitor.start_task("wf-1", "task-1", "Task 1");
        monitor.complete_task("wf-1", "task-1", "completed".to_string(), 100, 0);
        monitor.start_task("wf-1", "task-2", "Task 2");
        monitor.complete_task("wf-1", "task-2", "completed".to_string(), 200, 0);
        monitor.complete_workflow("wf-1");

        let report = monitor.generate_report("wf-1");
        assert!(report.is_some());

        let report_text = report.unwrap();
        assert!(report_text.contains("wf-1"));
        assert!(report_text.contains("总任务数: 3"));
    }

    #[test]
    fn test_performance_summary() {
        let mut monitor = PerformanceMonitor::new();

        // 工作流 1
        monitor.start_workflow("wf-1", 2);
        monitor.start_task("wf-1", "task-1", "Task 1");
        monitor.complete_task("wf-1", "task-1", "completed".to_string(), 100, 0);
        monitor.complete_workflow("wf-1");

        // 工作流 2
        monitor.start_workflow("wf-2", 3);
        monitor.start_task("wf-2", "task-2", "Task 2");
        monitor.complete_task("wf-2", "task-2", "completed".to_string(), 150, 0);
        monitor.start_task("wf-2", "task-3", "Task 3");
        monitor.complete_task("wf-2", "task-3", "failed".to_string(), 50, 2);
        monitor.complete_workflow("wf-2");

        // 获取汇总
        let summary = monitor.get_summary();
        assert_eq!(summary.total_workflows, 2);
        assert_eq!(summary.total_tasks, 5);
        assert_eq!(summary.completed_tasks, 2);
        assert_eq!(summary.failed_tasks, 1);

        // 生成汇总报告
        let report = summary.generate_report();
        assert!(report.contains("总工作流数: 2"));
        assert!(report.contains("总任务数: 5"));
    }

    #[test]
    fn test_percentile() {
        let data = vec![100, 150, 200, 250, 300];

        assert_eq!(percentile(&data, 0.0), 100.0);
        assert_eq!(percentile(&data, 50.0), 200.0);
        assert_eq!(percentile(&data, 100.0), 300.0);

        // P95 应该在 250 和 300 之间
        let p95 = percentile(&data, 95.0);
        assert!(p95 >= 250.0 && p95 <= 300.0);
    }

    #[test]
    fn test_percentile_empty() {
        let data: Vec<u64> = vec![];
        assert_eq!(percentile(&data, 50.0), 0.0);
    }

    #[test]
    fn test_percentile_single() {
        let data = vec![100];
        assert_eq!(percentile(&data, 0.0), 100.0);
        assert_eq!(percentile(&data, 50.0), 100.0);
        assert_eq!(percentile(&data, 100.0), 100.0);
    }
}
