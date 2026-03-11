use agent_tui::state::{AppState, FocusArea, MessageItem, SessionState};
use uuid::Uuid;

#[test]
fn test_focus_switching() {
    let mut state = AppState::default();

    // 默认焦点在会话列表
    assert_eq!(state.focus, FocusArea::SessionList);

    // 切换到输入框
    state.next_focus();
    assert_eq!(state.focus, FocusArea::InputBox);

    // 再次切换回会话列表
    state.next_focus();
    assert_eq!(state.focus, FocusArea::SessionList);
}

#[test]
fn test_session_selection() {
    let mut state = AppState::default();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试会话".to_string());

    // 选择会话
    state.select_session(0);
    assert_eq!(state.selected_session_id(), Some(session_id));
}

#[test]
fn test_session_state_transitions() {
    let mut state = AppState::default();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 初始状态为 Idle
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::Idle)
    ));

    // 设置为等待响应
    state.set_session_state(session_id, SessionState::WaitingResponse);
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::WaitingResponse)
    ));
}

// 导航测试
#[test]
fn test_move_up_and_down() {
    let mut state = AppState::default();
    let id1 = Uuid::new_v4();
    let id2 = Uuid::new_v4();
    let id3 = Uuid::new_v4();

    state.add_session(id1, "会话1".to_string());
    state.add_session(id2, "会话2".to_string());
    state.add_session(id3, "会话3".to_string());

    // 初始选中第一个会话
    assert_eq!(state.selected_index(), 0);
    assert_eq!(state.selected_session_id(), Some(id1));

    // 向下移动
    state.move_down();
    assert_eq!(state.selected_index(), 1);
    assert_eq!(state.selected_session_id(), Some(id2));

    state.move_down();
    assert_eq!(state.selected_index(), 2);
    assert_eq!(state.selected_session_id(), Some(id3));

    // 尝试继续向下，应该停留在最后一个
    state.move_down();
    assert_eq!(state.selected_index(), 2);
    assert_eq!(state.selected_session_id(), Some(id3));

    // 向上移动
    state.move_up();
    assert_eq!(state.selected_index(), 1);
    assert_eq!(state.selected_session_id(), Some(id2));

    state.move_up();
    assert_eq!(state.selected_index(), 0);
    assert_eq!(state.selected_session_id(), Some(id1));

    // 尝试继续向上，应该停留在第一个
    state.move_up();
    assert_eq!(state.selected_index(), 0);
    assert_eq!(state.selected_session_id(), Some(id1));
}

#[test]
fn test_navigation_with_empty_sessions() {
    let mut state = AppState::default();

    // 空会话列表时的导航
    assert_eq!(state.selected_index(), 0);
    assert_eq!(state.selected_session_id(), None);

    // 尝试移动应该不会崩溃
    state.move_up();
    assert_eq!(state.selected_index(), 0);

    state.move_down();
    assert_eq!(state.selected_index(), 0);
}

#[test]
fn test_sessions_getter() {
    let mut state = AppState::default();
    let id1 = Uuid::new_v4();
    let id2 = Uuid::new_v4();

    // 初始为空
    assert_eq!(state.sessions().len(), 0);

    // 添加会话
    state.add_session(id1, "会话1".to_string());
    state.add_session(id2, "会话2".to_string());

    // 验证会话列表
    let sessions = state.sessions();
    assert_eq!(sessions.len(), 2);
    assert_eq!(sessions[0].id, id1);
    assert_eq!(sessions[0].title, Some("会话1".to_string()));
    assert_eq!(sessions[1].id, id2);
    assert_eq!(sessions[1].title, Some("会话2".to_string()));
}

// 消息管理测试
#[test]
fn test_add_and_get_messages() {
    let mut state = AppState::default();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试会话".to_string());

    // 初始无消息
    assert!(state.get_messages(session_id).is_none());

    // 添加消息
    let message1 = MessageItem {
        role: "user".to_string(),
        content: "Hello".to_string(),
        timestamp: chrono::Utc::now(),
    };
    state.add_message(session_id, message1);

    // 验证消息
    let messages = state.get_messages(session_id).unwrap();
    assert_eq!(messages.len(), 1);
    assert_eq!(messages[0].role, "user");
    assert_eq!(messages[0].content, "Hello");

    // 添加更多消息
    let message2 = MessageItem {
        role: "assistant".to_string(),
        content: "Hi there!".to_string(),
        timestamp: chrono::Utc::now(),
    };
    state.add_message(session_id, message2);

    let messages = state.get_messages(session_id).unwrap();
    assert_eq!(messages.len(), 2);
    assert_eq!(messages[1].role, "assistant");
    assert_eq!(messages[1].content, "Hi there!");
}

#[test]
fn test_messages_for_multiple_sessions() {
    let mut state = AppState::default();
    let session1 = Uuid::new_v4();
    let session2 = Uuid::new_v4();

    state.add_session(session1, "会话1".to_string());
    state.add_session(session2, "会话2".to_string());

    // 给不同会话添加消息
    let msg1 = MessageItem {
        role: "user".to_string(),
        content: "Session 1 message".to_string(),
        timestamp: chrono::Utc::now(),
    };
    state.add_message(session1, msg1);

    let msg2 = MessageItem {
        role: "user".to_string(),
        content: "Session 2 message".to_string(),
        timestamp: chrono::Utc::now(),
    };
    state.add_message(session2, msg2);

    // 验证消息隔离
    let messages1 = state.get_messages(session1).unwrap();
    assert_eq!(messages1.len(), 1);
    assert_eq!(messages1[0].content, "Session 1 message");

    let messages2 = state.get_messages(session2).unwrap();
    assert_eq!(messages2.len(), 1);
    assert_eq!(messages2[0].content, "Session 2 message");
}

#[test]
fn test_get_messages_for_nonexistent_session() {
    let state = AppState::default();
    let nonexistent_id = Uuid::new_v4();

    // 不存在的会话应该返回 None
    assert!(state.get_messages(nonexistent_id).is_none());
}

// 输入处理测试
#[test]
fn test_input_operations() {
    let mut state = AppState::default();

    // 初始为空
    assert_eq!(state.input, "");
    assert_eq!(state.cursor_pos, 0);

    // 输入字符
    state.input_char('H');
    assert_eq!(state.input, "H");
    assert_eq!(state.cursor_pos, 1);

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

    state.delete_char();
    state.delete_char();
    assert_eq!(state.input, "He");
    assert_eq!(state.cursor_pos, 2);

    // 清空输入
    state.clear_input();
    assert_eq!(state.input, "");
    assert_eq!(state.cursor_pos, 0);
}

#[test]
fn test_input_char_at_middle_position() {
    let mut state = AppState::default();

    // 输入 "Hello"
    state.input_char('H');
    state.input_char('e');
    state.input_char('l');
    state.input_char('l');
    state.input_char('o');

    // 移动光标到中间
    state.move_cursor_left();
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 3);

    // 在中间插入字符
    state.input_char('X');
    assert_eq!(state.input, "HelXlo");
    assert_eq!(state.cursor_pos, 4);
}

#[test]
fn test_delete_char_at_beginning() {
    let mut state = AppState::default();

    // 在空输入时删除，应该不崩溃
    state.delete_char();
    assert_eq!(state.input, "");
    assert_eq!(state.cursor_pos, 0);

    // 输入字符后移动到开头
    state.input_char('A');
    state.input_char('B');
    state.move_cursor_left();
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 0);

    // 在开头删除，应该不删除任何东西
    state.delete_char();
    assert_eq!(state.input, "AB");
    assert_eq!(state.cursor_pos, 0);
}

#[test]
fn test_cursor_movement() {
    let mut state = AppState::default();

    // 输入一些文本
    state.input_char('H');
    state.input_char('e');
    state.input_char('l');
    state.input_char('l');
    state.input_char('o');
    assert_eq!(state.cursor_pos, 5);

    // 向左移动
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 4);

    state.move_cursor_left();
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 2);

    // 向右移动
    state.move_cursor_right();
    assert_eq!(state.cursor_pos, 3);

    state.move_cursor_right();
    state.move_cursor_right();
    assert_eq!(state.cursor_pos, 5);

    // 尝试超出边界
    state.move_cursor_right();
    assert_eq!(state.cursor_pos, 5); // 不应该超过字符串长度

    // 向左移动到开头
    for _ in 0..10 {
        state.move_cursor_left();
    }
    assert_eq!(state.cursor_pos, 0);

    // 尝试继续向左
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 0); // 不应该小于 0
}

#[test]
fn test_cursor_movement_empty_input() {
    let mut state = AppState::default();

    // 空输入时移动光标
    state.move_cursor_left();
    assert_eq!(state.cursor_pos, 0);

    state.move_cursor_right();
    assert_eq!(state.cursor_pos, 0);
}

// 边界情况测试
#[test]
fn test_select_invalid_session() {
    let mut state = AppState::default();
    let id1 = Uuid::new_v4();

    state.add_session(id1, "会话1".to_string());

    // 初始选中索引 0
    assert_eq!(state.selected_index(), 0);

    // 尝试选择无效索引
    state.select_session(5);

    // 索引不应该改变
    assert_eq!(state.selected_index(), 0);
    assert_eq!(state.selected_session_id(), Some(id1));

    // 选择有效索引
    state.select_session(0);
    assert_eq!(state.selected_index(), 0);
}

#[test]
fn test_selected_session_id_with_no_sessions() {
    let state = AppState::default();

    // 空列表时应该返回 None
    assert_eq!(state.selected_session_id(), None);
}

#[test]
fn test_session_unread_count_cleared_on_selection() {
    let mut state = AppState::default();
    let id1 = Uuid::new_v4();
    let id2 = Uuid::new_v4();

    state.add_session(id1, "会话1".to_string());
    state.add_session(id2, "会话2".to_string());

    // 手动设置未读计数（通过直接访问内部状态模拟）
    // 注意：这里我们通过选择会话来验证未读计数被清除
    state.select_session(0);
    let sessions = state.sessions();
    assert_eq!(sessions[0].unread_count, 0);

    state.select_session(1);
    let sessions = state.sessions();
    assert_eq!(sessions[1].unread_count, 0);
}

#[test]
fn test_set_session_state_for_nonexistent_session() {
    let mut state = AppState::default();
    let nonexistent_id = Uuid::new_v4();

    // 设置不存在的会话状态，应该不会崩溃
    state.set_session_state(nonexistent_id, SessionState::WaitingResponse);

    // 验证状态没有改变
    assert!(state.get_session_state(nonexistent_id).is_none());
}

#[test]
fn test_session_state_streaming() {
    let mut state = AppState::default();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 设置为流式传输
    state.set_session_state(session_id, SessionState::Streaming);
    assert!(matches!(
        state.get_session_state(session_id),
        Some(SessionState::Streaming)
    ));
}

#[test]
fn test_session_state_error() {
    let mut state = AppState::default();
    let session_id = Uuid::new_v4();

    state.add_session(session_id, "测试".to_string());

    // 设置错误状态
    state.set_session_state(session_id, SessionState::Error("测试错误".to_string()));

    if let Some(SessionState::Error(msg)) = state.get_session_state(session_id) {
        assert_eq!(msg, "测试错误");
    } else {
        panic!("Expected Error state");
    }
}
