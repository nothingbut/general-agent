//! 会话列表组件

use ratatui::{
    layout::Rect,
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, List, ListItem},
    Frame,
};

use crate::state::{AppState, FocusArea, SessionState};
use super::colors::AppColors;

/// 渲染会话列表
pub fn render_session_list(f: &mut Frame, area: Rect, state: &AppState) {
    let is_focused = matches!(state.focus, FocusArea::SessionList);

    // 构建列表项
    let items: Vec<ListItem> = state
        .sessions()
        .iter()
        .enumerate()
        .map(|(i, session)| {
            let is_selected = i == state.selected_index();

            // 选择标记
            let marker = if is_selected { "●" } else { "○" };

            // 标题
            let title = session
                .title
                .clone()
                .unwrap_or_else(|| session.id.to_string()[..8].to_string());

            // 状态标注
            let status = match &session.state {
                SessionState::Idle => {
                    if session.unread_count > 0 {
                        format!(" ({}条新消息)", session.unread_count)
                    } else {
                        String::new()
                    }
                }
                SessionState::WaitingResponse => " ⏳ 等待回复...".to_string(),
                SessionState::Streaming => " 🔄 生成中...".to_string(),
                SessionState::Error(msg) => format!(" ❌ {}", msg),
            };

            let color = if is_selected {
                AppColors::SELECTED
            } else {
                AppColors::NORMAL
            };

            let line = Line::from(vec![
                Span::styled(marker, Style::default().fg(color)),
                Span::raw(" "),
                Span::styled(title, Style::default().fg(color)),
            ]);

            let mut lines = vec![line];

            if !status.is_empty() {
                let status_color = match &session.state {
                    SessionState::Error(_) => AppColors::ERROR,
                    SessionState::WaitingResponse => AppColors::WARNING,
                    SessionState::Idle if session.unread_count > 0 => AppColors::INFO,
                    _ => AppColors::NORMAL,
                };

                lines.push(Line::from(Span::styled(
                    format!("  {}", status),
                    Style::default().fg(status_color),
                )));
            }

            ListItem::new(lines)
        })
        .collect();

    // 边框样式
    let border_color = if is_focused {
        AppColors::FOCUS
    } else {
        AppColors::NORMAL
    };

    let block = Block::default()
        .borders(Borders::ALL)
        .border_style(Style::default().fg(border_color))
        .title("会话列表");

    let list = List::new(items).block(block);

    f.render_widget(list, area);

    // 渲染快捷键提示
    let help_area = Rect {
        y: area.bottom().saturating_sub(3),
        height: 2,
        ..area
    };

    let help_text = vec![
        Line::from(Span::raw("j/k: 导航")),
        Line::from(Span::raw("Enter: 选择")),
    ];

    let help = ratatui::widgets::Paragraph::new(help_text)
        .style(Style::default().fg(AppColors::NORMAL));

    f.render_widget(help, help_area);
}
