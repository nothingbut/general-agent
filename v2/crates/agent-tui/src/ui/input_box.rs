//! 输入框组件

use ratatui::{
    layout::Rect,
    style::Style,
    text::{Line, Span},
    widgets::{Block, Borders, Paragraph},
    Frame,
};

use crate::state::{AppState, FocusArea};
use super::colors::AppColors;

/// 渲染输入框
pub fn render_input_box(f: &mut Frame, area: Rect, state: &AppState) {
    let is_focused = matches!(state.focus, FocusArea::InputBox);

    let border_color = if is_focused {
        AppColors::FOCUS
    } else {
        AppColors::NORMAL
    };

    let block = Block::default()
        .borders(Borders::ALL)
        .border_style(Style::default().fg(border_color))
        .title("输入");

    // 构建显示文本（带光标）
    let text = if is_focused {
        let before = &state.input[..state.cursor_pos];
        let after = &state.input[state.cursor_pos..];

        Line::from(vec![
            Span::raw(before),
            Span::styled("█", Style::default().fg(AppColors::FOCUS)),
            Span::raw(after),
        ])
    } else {
        Line::from(Span::raw(&state.input))
    };

    let paragraph = Paragraph::new(vec![text]).block(block);

    f.render_widget(paragraph, area);
}
