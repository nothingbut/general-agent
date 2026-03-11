//! TUI 集成测试

use agent_tui::{
    backend::{MessageInfo, SessionInfo},
    state::{AppState, MessageItem, SessionState},
};
use chrono::Utc;
use uuid::Uuid;

#[test]
fn test_session_loading() {
    let mut state = AppState::new();

    let session_id = Uuid::new_v4();
    let sessions = vec![SessionInfo {
        id: session_id,
        title: Some("测试会话".to_string()),
        updated_at: Utc::now(),
    }];

    // 模拟加载会话
    for session in sessions {
        state.add_session(session.id, session.title.unwrap_or_default());
    }

    assert_eq!(state.sessions().len(), 1);
    assert_eq!(state.selected_session_id(), Some(session_id));
}

#[test]
fn test_message_display() {
    let mut state = AppState::new();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 添加消息
    state.add_message(
        session_id,
        MessageItem {
            role: "user".to_string(),
            content: "测试消息".to_string(),
            timestamp: Utc::now(),
        },
    );

    let messages = state.get_messages(session_id).unwrap();
    assert_eq!(messages.len(), 1);
    assert_eq!(messages[0].content, "测试消息");
}

#[test]
fn test_streaming_state_flow() {
    let mut state = AppState::new();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 初始状态
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::Idle)
    ));

    // 等待响应
    state.set_session_state(session_id, SessionState::WaitingResponse);
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::WaitingResponse)
    ));

    // 流式响应
    state.set_session_state(session_id, SessionState::Streaming);
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::Streaming)
    ));

    // 完成
    state.set_session_state(session_id, SessionState::Idle);
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::Idle)
    ));
}

#[test]
fn test_multiple_sessions() {
    let mut state = AppState::new();

    let id1 = Uuid::new_v4();
    let id2 = Uuid::new_v4();
    let id3 = Uuid::new_v4();

    state.add_session(id1, "会话1".to_string());
    state.add_session(id2, "会话2".to_string());
    state.add_session(id3, "会话3".to_string());

    assert_eq!(state.sessions().len(), 3);

    // 默认选中第一个
    assert_eq!(state.selected_session_id(), Some(id1));

    // 导航到第二个
    state.move_down();
    assert_eq!(state.selected_session_id(), Some(id2));

    // 导航到第三个
    state.move_down();
    assert_eq!(state.selected_session_id(), Some(id3));

    // 边界测试
    state.move_down();
    assert_eq!(state.selected_session_id(), Some(id3));

    // 回到第二个
    state.move_up();
    assert_eq!(state.selected_session_id(), Some(id2));
}

#[test]
fn test_input_operations() {
    let mut state = AppState::new();

    // 输入字符
    state.input_char('H');
    state.input_char('e');
    state.input_char('l');
    state.input_char('l');
    state.input_char('o');

    assert_eq!(state.input, "Hello");
    assert_eq!(state.cursor_pos, 5);

    // 删除字符
    state.delete_char();
    assert_eq!(state.input, "Hell");
    assert_eq!(state.cursor_pos, 4);

    // 移动光标
    state.move_cursor_left();
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 2);

    // 在光标位置插入
    state.input_char('X');
    assert_eq!(state.input, "HeXll");
    assert_eq!(state.cursor_pos, 3);

    // 清空输入
    state.clear_input();
    assert_eq!(state.input, "");
    assert_eq!(state.cursor_pos, 0);
}

#[test]
fn test_error_state() {
    let mut state = AppState::new();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 设置错误状态
    state.set_session_state(session_id, SessionState::Error("连接失败".to_string()));

    match state.get_session_state(session_id) {
        Some(SessionState::Error(msg)) => {
            assert_eq!(msg, "连接失败");
        }
        _ => panic!("Expected error state"),
    }
}
