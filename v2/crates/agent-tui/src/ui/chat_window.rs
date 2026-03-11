//! 聊天窗口组件

use ratatui::{
    layout::Rect,
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Paragraph, Wrap},
    Frame,
};

use crate::state::{AppState, MessageItem};
use super::colors::AppColors;

/// 渲染聊天窗口
pub fn render_chat_window(f: &mut Frame, area: Rect, state: &AppState) {
    let block = Block::default()
        .borders(Borders::ALL)
        .border_style(Style::default().fg(AppColors::NORMAL))
        .title("聊天窗口");

    // 获取当前会话的消息
    let lines = if let Some(session_id) = state.selected_session_id() {
        // 获取会话标题
        let title_line = state
            .sessions()
            .iter()
            .find(|s| s.id == session_id)
            .and_then(|s| s.title.as_ref())
            .map(|title| {
                vec![
                    Line::from(Span::styled(
                        format!("─ {} ", title),
                        Style::default()
                            .fg(AppColors::INFO)
                            .add_modifier(Modifier::BOLD),
                    )),
                    Line::from(""),
                ]
            })
            .unwrap_or_default();

        // 获取消息列表
        let message_lines: Vec<Line> = state
            .get_messages(session_id)
            .map(|messages| {
                messages
                    .iter()
                    .flat_map(|msg| format_message(msg))
                    .collect()
            })
            .unwrap_or_default();

        let mut all_lines = title_line;
        all_lines.extend(message_lines);

        // 显示流式响应内容
        if let Some(streaming_content) = state.get_streaming_content(session_id) {
            if !streaming_content.is_empty() {
                all_lines.push(Line::from(""));
                all_lines.push(Line::from(vec![
                    Span::styled(
                        "assistant: ",
                        Style::default()
                            .fg(AppColors::SELECTED)
                            .add_modifier(Modifier::BOLD),
                    ),
                    Span::raw(streaming_content),
                ]));
                all_lines.push(Line::from(Span::styled(
                    "[生成中...]",
                    Style::default()
                        .fg(AppColors::WARNING)
                        .add_modifier(Modifier::ITALIC),
                )));
            }
        }

        all_lines
    } else {
        vec![Line::from(Span::styled(
            "请选择一个会话",
            Style::default()
                .fg(AppColors::NORMAL)
                .add_modifier(Modifier::ITALIC),
        ))]
    };

    // 获取滚动偏移
    let scroll_offset = if let Some(session_id) = state.selected_session_id() {
        state.scroll_offset.get(&session_id).copied().unwrap_or(0)
    } else {
        0
    };

    let paragraph = Paragraph::new(lines)
        .block(block)
        .wrap(Wrap { trim: false })
        .scroll((scroll_offset, 0));

    f.render_widget(paragraph, area);
}

/// 格式化单条消息
fn format_message(msg: &MessageItem) -> Vec<Line> {
    let role_color = if msg.role == "user" {
        AppColors::INFO
    } else {
        AppColors::SELECTED
    };

    let timestamp = msg.timestamp.format("%Y-%m-%d %H:%M").to_string();

    vec![
        Line::from(vec![
            Span::styled(
                format!("{}: ", msg.role),
                Style::default()
                    .fg(role_color)
                    .add_modifier(Modifier::BOLD),
            ),
            Span::raw(&msg.content),
        ]),
        Line::from(Span::styled(
            timestamp,
            Style::default()
                .fg(AppColors::NORMAL)
                .add_modifier(Modifier::DIM),
        )),
        Line::from(""),
    ]
}
