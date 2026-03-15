//! Performance Overlay 演示程序
//!
//! 这个程序展示 PerformanceOverlay 的基本功能

use agent_tui::ui::PerformanceOverlay;
use agent_workflow::workflow::performance::PerformanceMonitor;
use crossterm::{
    event::{self, Event, KeyCode, KeyModifiers},
    execute,
    terminal::{disable_raw_mode, enable_raw_mode, EnterAlternateScreen, LeaveAlternateScreen},
};
use ratatui::{backend::CrosstermBackend, Terminal};
use std::{
    io,
    sync::{Arc, Mutex},
    time::Duration,
};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 设置终端
    enable_raw_mode()?;
    let mut stdout = io::stdout();
    execute!(stdout, EnterAlternateScreen)?;
    let backend = CrosstermBackend::new(stdout);
    let mut terminal = Terminal::new(backend)?;

    // 创建性能监控器并添加测试数据
    let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));

    // 添加测试工作流
    {
        let mut mon = monitor.lock().unwrap();

        // 工作流 1: 已完成
        mon.start_workflow("wf-test-001", 10);
        for i in 0..10 {
            mon.start_task("wf-test-001", format!("task-{}", i), format!("Task {}", i));
            mon.complete_task(
                "wf-test-001",
                &format!("task-{}", i),
                "completed".to_string(),
                50 + i * 10,
                0,
            );
        }
        mon.complete_workflow("wf-test-001");

        // 工作流 2: 进行中
        mon.start_workflow("wf-test-002", 20);
        for i in 0..12 {
            mon.start_task("wf-test-002", format!("task-{}", i), format!("Task {}", i));
            mon.complete_task(
                "wf-test-002",
                &format!("task-{}", i),
                "completed".to_string(),
                30 + i * 5,
                0,
            );
        }

        // 工作流 3: 有失败的任务
        mon.start_workflow("wf-test-003", 8);
        for i in 0..8 {
            mon.start_task("wf-test-003", format!("task-{}", i), format!("Task {}", i));
            let status = if i == 3 || i == 5 {
                "failed"
            } else {
                "completed"
            };
            mon.complete_task(
                "wf-test-003",
                &format!("task-{}", i),
                status.to_string(),
                40 + i * 8,
                if status == "failed" { 2 } else { 0 },
            );
        }
        mon.complete_workflow("wf-test-003");
    }

    // 创建 overlay
    let mut overlay = PerformanceOverlay::new(monitor.clone());
    overlay.toggle_visible(); // 默认打开

    // 主循环
    let mut should_quit = false;

    terminal.clear()?;

    println!("性能监控演示程序");
    println!("=================");
    println!();
    println!("已创建 3 个测试工作流：");
    println!("  1. wf-test-001: 10 个任务，已完成");
    println!("  2. wf-test-002: 20 个任务，进行中 (12/20)");
    println!("  3. wf-test-003: 8 个任务，已完成 (2 个失败)");
    println!();
    println!("按任意键开始 TUI 演示...");

    // 等待用户按键
    loop {
        if event::poll(Duration::from_millis(100))? {
            if let Event::Key(_) = event::read()? {
                break;
            }
        }
    }

    loop {
        // 渲染
        terminal.draw(|f| {
            let area = f.size();

            // 渲染一个简单的背景
            use ratatui::{
                style::{Color, Style},
                text::{Line, Span},
                widgets::{Block, Borders, Paragraph},
            };

            let bg_block = Block::default()
                .title(" 性能监控演示 ")
                .borders(Borders::ALL)
                .border_style(Style::default().fg(Color::White));

            let help_text = vec![
                Line::from(""),
                Line::from(vec![Span::styled(
                    "快捷键:",
                    Style::default().fg(Color::Yellow),
                )]),
                Line::from("  Ctrl+P: 打开/关闭性能监控面板"),
                Line::from("  Tab: 切换工作流"),
                Line::from("  Left/Right: 切换工作流"),
                Line::from("  Esc: 关闭面板"),
                Line::from("  Q: 退出程序"),
                Line::from(""),
                Line::from(vec![Span::styled(
                    "提示:",
                    Style::default().fg(Color::Cyan),
                )]),
                Line::from("  面板默认已打开，可以使用 Tab 键切换查看不同工作流的指标"),
            ];

            let help = Paragraph::new(help_text).block(bg_block);
            f.render_widget(help, area);

            // 渲染 overlay
            overlay.render(f, area);
        })?;

        // 处理事件
        if event::poll(Duration::from_millis(100))? {
            if let Event::Key(key) = event::read()? {
                if overlay.is_visible() {
                    match key.code {
                        KeyCode::Esc => {
                            overlay.toggle_visible();
                        }
                        KeyCode::Tab | KeyCode::Right => {
                            overlay.next_workflow();
                        }
                        KeyCode::Left => {
                            overlay.prev_workflow();
                        }
                        KeyCode::Char('q') => {
                            should_quit = true;
                        }
                        _ => {}
                    }
                } else {
                    // 面板关闭时的快捷键
                    if key.modifiers.contains(KeyModifiers::CONTROL)
                        && key.code == KeyCode::Char('p')
                    {
                        overlay.toggle_visible();
                    } else if key.code == KeyCode::Char('q') {
                        should_quit = true;
                    }
                }
            }
        }

        if should_quit {
            break;
        }
    }

    // 恢复终端
    disable_raw_mode()?;
    execute!(terminal.backend_mut(), LeaveAlternateScreen)?;

    println!("演示完成！");

    Ok(())
}
