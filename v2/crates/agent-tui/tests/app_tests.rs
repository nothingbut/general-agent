use agent_tui::{AppState, FocusArea};
use uuid::Uuid;

#[test]
fn test_handle_switch_focus() {
    let mut state = AppState::default();
    assert_eq!(state.focus, FocusArea::SessionList);

    // 模拟焦点切换
    state.next_focus();
    assert_eq!(state.focus, FocusArea::InputBox);
}

#[test]
fn test_handle_navigation() {
    let mut state = AppState::default();

    state.add_session(Uuid::new_v4(), "会话1".to_string());
    state.add_session(Uuid::new_v4(), "会话2".to_string());
    state.add_session(Uuid::new_v4(), "会话3".to_string());

    assert_eq!(state.selected_index(), 0);

    state.move_down();
    assert_eq!(state.selected_index(), 1);

    state.move_down();
    assert_eq!(state.selected_index(), 2);

    // 边界测试
    state.move_down();
    assert_eq!(state.selected_index(), 2);

    state.move_up();
    assert_eq!(state.selected_index(), 1);
}
