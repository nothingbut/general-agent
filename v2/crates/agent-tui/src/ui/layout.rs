//! 布局管理

use ratatui::layout::{Constraint, Direction, Layout, Rect};

/// 计算应用布局
pub fn calculate_layout(area: Rect) -> AppLayout {
    // 顶部状态栏 + 主体 + 底部信息栏
    let chunks = Layout::default()
        .direction(Direction::Vertical)
        .constraints([
            Constraint::Length(1),      // 状态栏
            Constraint::Min(0),         // 主体
            Constraint::Length(1),      // 底部信息栏
        ])
        .split(area);

    let status_bar = chunks[0];
    let main_area = chunks[1];
    let info_bar = chunks[2];

    // 主体分为左右两栏
    let main_chunks = Layout::default()
        .direction(Direction::Horizontal)
        .constraints([
            Constraint::Percentage(25),  // 会话列表
            Constraint::Percentage(75),  // 聊天窗口
        ])
        .split(main_area);

    let session_list = main_chunks[0];
    let chat_area = main_chunks[1];

    // 聊天区域分为窗口和输入框
    let chat_chunks = Layout::default()
        .direction(Direction::Vertical)
        .constraints([
            Constraint::Min(0),         // 聊天窗口
            Constraint::Length(3),      // 输入框
        ])
        .split(chat_area);

    let chat_window = chat_chunks[0];
    let input_box = chat_chunks[1];

    AppLayout {
        status_bar,
        session_list,
        chat_window,
        input_box,
        info_bar,
    }
}

/// 应用布局
#[derive(Debug, Clone, Copy)]
pub struct AppLayout {
    pub status_bar: Rect,
    pub session_list: Rect,
    pub chat_window: Rect,
    pub input_box: Rect,
    pub info_bar: Rect,
}
