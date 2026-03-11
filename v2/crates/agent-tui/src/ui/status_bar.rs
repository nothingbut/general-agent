//! 状态栏组件

use ratatui::{
    layout::Rect,
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::Paragraph,
    Frame,
};

use crate::state::AppState;
use super::colors::AppColors;

/// 渲染顶部状态栏
pub fn render_status_bar(f: &mut Frame, area: Rect, _state: &AppState) {
    let spans = vec![
        Span::styled(
            "General Agent V2",
            Style::default()
                .fg(AppColors::INFO)
                .add_modifier(Modifier::BOLD),
        ),
        Span::raw(" | "),
        Span::styled("Ollama (qwen3.5)", Style::default().fg(AppColors::SELECTED)),
        Span::raw("  "),
        Span::styled(
            "Ctrl+H 帮助",
            Style::default()
                .fg(AppColors::NORMAL)
                .add_modifier(Modifier::DIM),
        ),
    ];

    let paragraph = Paragraph::new(Line::from(spans))
        .style(Style::default().fg(AppColors::NORMAL));

    f.render_widget(paragraph, area);
}

/// 渲染底部信息栏
pub fn render_info_bar(f: &mut Frame, area: Rect, state: &AppState) {
    let session_count = state.sessions().len();
    let message_count = state
        .selected_session_id()
        .and_then(|id| state.get_messages(id))
        .map(|m| m.len())
        .unwrap_or(0);

    let left_text = format!("会话: {session_count} | 消息: {message_count}");
    let right_text = "ESC: 取消焦点";

    let left_span = Span::raw(left_text);
    let right_span = Span::styled(
        right_text,
        Style::default()
            .fg(AppColors::NORMAL)
            .add_modifier(Modifier::DIM),
    );

    // 计算右侧文本位置
    let padding = area
        .width
        .saturating_sub(left_span.content.len() as u16)
        .saturating_sub(right_span.content.len() as u16);

    let spans = vec![
        left_span,
        Span::raw(" ".repeat(padding as usize)),
        right_span,
    ];

    let paragraph = Paragraph::new(Line::from(spans))
        .style(Style::default().fg(AppColors::NORMAL));

    f.render_widget(paragraph, area);
}
