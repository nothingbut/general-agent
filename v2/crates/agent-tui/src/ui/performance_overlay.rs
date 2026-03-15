//! Performance monitoring overlay component

use agent_workflow::workflow::performance::{PerformanceMonitor, WorkflowMetrics};
use ratatui::{
    layout::{Alignment, Constraint, Layout, Rect},
    style::{Color, Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Paragraph},
    Frame,
};
use std::cell::RefCell;
use std::sync::{Arc, Mutex};

/// Performance overlay component for displaying workflow performance metrics
pub struct PerformanceOverlay {
    /// Whether the overlay is visible
    visible: bool,

    /// Index of the currently selected workflow
    selected_workflow_index: usize,

    /// Reference to the performance monitor
    monitor: Arc<Mutex<PerformanceMonitor>>,

    /// Cached workflow list (to avoid frequent queries) - uses RefCell for interior mutability
    cache: RefCell<WorkflowCache>,
}

/// Cache for workflow list
struct WorkflowCache {
    workflows: Vec<String>,
    last_update: std::time::Instant,
    duration: std::time::Duration,
}

impl PerformanceOverlay {
    /// Create a new PerformanceOverlay
    pub fn new(monitor: Arc<Mutex<PerformanceMonitor>>) -> Self {
        Self {
            visible: false,
            selected_workflow_index: 0,
            monitor,
            cache: RefCell::new(WorkflowCache {
                workflows: Vec::new(),
                last_update: std::time::Instant::now(),
                duration: std::time::Duration::from_millis(500), // 500ms cache
            }),
        }
    }

    /// Toggle visibility
    pub fn toggle_visible(&mut self) {
        self.visible = !self.visible;
    }

    /// Check if overlay is visible
    pub fn is_visible(&self) -> bool {
        self.visible
    }

    /// Get list of workflow IDs from the monitor (with caching)
    pub fn get_workflow_list(&self) -> Vec<String> {
        let mut cache = self.cache.borrow_mut();

        // Check if cache is still valid
        if cache.last_update.elapsed() < cache.duration && !cache.workflows.is_empty() {
            return cache.workflows.clone();
        }

        // Update cache with timeout handling
        match self.monitor.lock() {
            Ok(monitor) => {
                cache.workflows = monitor.get_all_workflow_ids();
                cache.last_update = std::time::Instant::now();
                cache.workflows.clone()
            }
            Err(e) => {
                eprintln!(
                    "PerformanceOverlay: Failed to acquire monitor lock: {:?}",
                    e
                );
                // Return cached data even if stale
                cache.workflows.clone()
            }
        }
    }

    /// Force refresh the workflow list cache
    pub fn refresh_cache(&self) {
        if let Ok(monitor) = self.monitor.lock() {
            let mut cache = self.cache.borrow_mut();
            cache.workflows = monitor.get_all_workflow_ids();
            cache.last_update = std::time::Instant::now();
        }
    }

    /// Get metrics for the currently selected workflow
    pub fn get_current_metrics(&self) -> Option<WorkflowMetrics> {
        let workflows = self.get_workflow_list();
        if workflows.is_empty() {
            return None;
        }

        // Ensure index is within bounds
        let workflow_id = &workflows[self.selected_workflow_index % workflows.len()];

        match self.monitor.lock() {
            Ok(monitor) => monitor.get_workflow_metrics(workflow_id).cloned(),
            Err(e) => {
                eprintln!(
                    "PerformanceOverlay: Failed to acquire monitor lock for metrics: {:?}",
                    e
                );
                None
            }
        }
    }

    /// Move to next workflow
    pub fn next_workflow(&mut self) {
        let count = self.get_workflow_list().len();
        if count > 0 {
            self.selected_workflow_index = (self.selected_workflow_index + 1) % count;
        }
    }

    /// Move to previous workflow
    pub fn prev_workflow(&mut self) {
        let count = self.get_workflow_list().len();
        if count > 0 {
            self.selected_workflow_index = self
                .selected_workflow_index
                .checked_sub(1)
                .unwrap_or(count - 1);
        }
    }

    /// Jump to first workflow (Home key)
    pub fn first_workflow(&mut self) {
        let count = self.get_workflow_list().len();
        if count > 0 {
            self.selected_workflow_index = 0;
        }
    }

    /// Jump to last workflow (End key)
    pub fn last_workflow(&mut self) {
        let count = self.get_workflow_list().len();
        if count > 0 {
            self.selected_workflow_index = count - 1;
        }
    }

    /// Render the overlay
    pub fn render(&self, f: &mut Frame, area: Rect) {
        // Don't render if not visible
        if !self.visible {
            return;
        }

        // Calculate centered popup area (70% width, 70% height)
        let popup_width = (area.width as f32 * 0.7).min(80.0) as u16;
        let popup_height = (area.height as f32 * 0.7).min(30.0) as u16;
        let popup_x = (area.width.saturating_sub(popup_width)) / 2;
        let popup_y = (area.height.saturating_sub(popup_height)) / 2;

        let popup_area = Rect {
            x: area.x + popup_x,
            y: area.y + popup_y,
            width: popup_width,
            height: popup_height,
        };

        // Get current metrics
        let workflows = self.get_workflow_list();
        let metrics_opt = self.get_current_metrics();

        // Create title
        let title = if workflows.is_empty() {
            " 性能监控 - 无数据 ".to_string()
        } else {
            format!(
                " 性能监控 [{}/{}] ",
                self.selected_workflow_index + 1,
                workflows.len()
            )
        };

        // Create block
        let block = Block::default()
            .title(title)
            .borders(Borders::ALL)
            .border_style(Style::default().fg(Color::Cyan));

        // Split popup area: content + help bar
        let chunks = Layout::default()
            .constraints([Constraint::Min(3), Constraint::Length(3)])
            .split(block.inner(popup_area));

        // Render the block background
        f.render_widget(block, popup_area);

        // Render content
        if let Some(metrics) = metrics_opt {
            self.render_metrics(f, chunks[0], &metrics);
        } else {
            self.render_no_data(f, chunks[0]);
        }

        // Render help bar
        self.render_help(f, chunks[1]);
    }

    /// Render performance metrics
    fn render_metrics(&self, f: &mut Frame, area: Rect, metrics: &WorkflowMetrics) {
        // Split into sections
        let chunks = Layout::default()
            .constraints([
                Constraint::Length(3), // Header
                Constraint::Length(7), // Execution metrics
                Constraint::Length(7), // Task time statistics
                Constraint::Min(3),    // Resource usage
            ])
            .split(area);

        // Header: Workflow ID and Status
        let status_color = Self::get_status_color(&metrics);
        let header_lines = vec![
            Line::from(vec![
                Span::raw("工作流 ID: "),
                Span::styled(&metrics.workflow_id, Style::default().fg(Color::Yellow)),
            ]),
            Line::from(vec![
                Span::raw("状态: "),
                Span::styled(
                    Self::format_status(&metrics),
                    Style::default()
                        .fg(status_color)
                        .add_modifier(Modifier::BOLD),
                ),
            ]),
        ];
        let header = Paragraph::new(header_lines);
        f.render_widget(header, chunks[0]);

        // Execution metrics
        let exec_block = Block::default()
            .title(" 执行指标 ")
            .borders(Borders::ALL)
            .border_style(Style::default().fg(Color::Green));

        let exec_lines = vec![
            Line::from(format!("总耗时: {}ms", metrics.total_duration_ms)),
            Line::from(format!(
                "任务: {}/{} ({:.1}%)",
                metrics.completed_tasks,
                metrics.total_tasks,
                (metrics.completed_tasks as f64 / metrics.total_tasks as f64 * 100.0)
            )),
            Line::from(format!("失败: {}", metrics.failed_tasks)),
            Line::from(format!("吞吐量: {:.2} 任务/秒", metrics.throughput)),
        ];
        let exec_content = Paragraph::new(exec_lines).block(exec_block);
        f.render_widget(exec_content, chunks[1]);

        // Task time statistics
        let stats_block = Block::default()
            .title(" 任务时间统计 ")
            .borders(Borders::ALL)
            .border_style(Style::default().fg(Color::Blue));

        let stats_lines = vec![
            Line::from(format!("平均: {:.2}ms", metrics.avg_task_duration_ms)),
            Line::from(format!(
                "中位数 (P50): {:.2}ms",
                metrics.median_task_duration_ms
            )),
            Line::from(format!("P95: {:.2}ms", metrics.p95_task_duration_ms)),
            Line::from(format!("P99: {:.2}ms", metrics.p99_task_duration_ms)),
        ];
        let stats_content = Paragraph::new(stats_lines).block(stats_block);
        f.render_widget(stats_content, chunks[2]);

        // Resource usage
        let resource_block = Block::default()
            .title(" 资源使用 ")
            .borders(Borders::ALL)
            .border_style(Style::default().fg(Color::Magenta));

        let resource_lines = vec![
            Line::from(format!("峰值内存: {:.2} MB", metrics.peak_memory_mb)),
            Line::from(format!("平均 CPU: {:.2}%", metrics.avg_cpu_percent)),
        ];
        let resource_content = Paragraph::new(resource_lines).block(resource_block);
        f.render_widget(resource_content, chunks[3]);
    }

    /// Render "no data" message
    fn render_no_data(&self, f: &mut Frame, area: Rect) {
        let message = Paragraph::new(vec![
            Line::from(""),
            Line::from("没有可用的工作流数据"),
            Line::from(""),
            Line::from("请先执行一个工作流以查看性能指标"),
        ])
        .alignment(Alignment::Center)
        .style(Style::default().fg(Color::Gray));

        f.render_widget(message, area);
    }

    /// Render help bar
    fn render_help(&self, f: &mut Frame, area: Rect) {
        let help_block = Block::default()
            .borders(Borders::TOP)
            .border_style(Style::default().fg(Color::DarkGray));

        let help_text = Paragraph::new(Line::from(vec![
            Span::styled("[Tab]", Style::default().fg(Color::Yellow)),
            Span::raw(" 切换  "),
            Span::styled("[Home/End]", Style::default().fg(Color::Yellow)),
            Span::raw(" 首/尾  "),
            Span::styled("[R]", Style::default().fg(Color::Yellow)),
            Span::raw(" 刷新  "),
            Span::styled("[Esc]", Style::default().fg(Color::Yellow)),
            Span::raw(" 关闭"),
        ]))
        .block(help_block)
        .alignment(Alignment::Center);

        f.render_widget(help_text, area);
    }

    /// Get status color based on workflow state
    fn get_status_color(metrics: &WorkflowMetrics) -> Color {
        if metrics.completed_at.is_some() {
            if metrics.failed_tasks > 0 {
                Color::Red // Completed with failures
            } else {
                Color::Green // Completed successfully
            }
        } else {
            Color::Blue // Running
        }
    }

    /// Format status string
    fn format_status(metrics: &WorkflowMetrics) -> String {
        if metrics.completed_at.is_some() {
            if metrics.failed_tasks > 0 {
                "● 已完成（有失败）".to_string()
            } else {
                "● 已完成".to_string()
            }
        } else {
            "● 运行中".to_string()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_overlay_creation() {
        let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let overlay = PerformanceOverlay::new(monitor);

        assert!(!overlay.is_visible());
        assert_eq!(overlay.selected_workflow_index, 0);
    }

    #[test]
    fn test_overlay_toggle() {
        let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let mut overlay = PerformanceOverlay::new(monitor);

        assert!(!overlay.is_visible());

        overlay.toggle_visible();
        assert!(overlay.is_visible());

        overlay.toggle_visible();
        assert!(!overlay.is_visible());
    }

    #[test]
    fn test_workflow_navigation() {
        let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let mut overlay = PerformanceOverlay::new(monitor.clone());

        // Add some workflows
        {
            let mut mon = monitor.lock().unwrap();
            mon.start_workflow("wf-1", 5);
            mon.start_workflow("wf-2", 3);
            mon.start_workflow("wf-3", 7);
        }

        // Test navigation
        assert_eq!(overlay.selected_workflow_index, 0);

        overlay.next_workflow();
        assert_eq!(overlay.selected_workflow_index, 1);

        overlay.next_workflow();
        assert_eq!(overlay.selected_workflow_index, 2);

        overlay.next_workflow(); // Should wrap to 0
        assert_eq!(overlay.selected_workflow_index, 0);

        overlay.prev_workflow(); // Should wrap to 2
        assert_eq!(overlay.selected_workflow_index, 2);
    }

    #[test]
    fn test_no_data_handling() {
        let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let overlay = PerformanceOverlay::new(monitor);

        let workflows = overlay.get_workflow_list();
        assert!(workflows.is_empty());

        let metrics = overlay.get_current_metrics();
        assert!(metrics.is_none());
    }
}
