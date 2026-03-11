use agent_tui::event::{AppEvent, EventHandler};
use agent_tui::state::FocusArea;
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};

// 退出事件测试（全局快捷键，任何焦点都可以）
#[test]
fn test_quit_event_ctrl_c() {
    let key = KeyEvent::new(KeyCode::Char('c'), KeyModifiers::CONTROL);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::Quit)));
}

#[test]
fn test_quit_event_ctrl_q() {
    let key = KeyEvent::new(KeyCode::Char('q'), KeyModifiers::CONTROL);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::Quit)));
}

// 焦点切换测试（全局快捷键，任何焦点都可以）
#[test]
fn test_focus_switch_tab() {
    let key = KeyEvent::new(KeyCode::Tab, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::SwitchFocus)));
}

#[test]
fn test_focus_switch_esc() {
    let key = KeyEvent::new(KeyCode::Esc, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::SwitchFocus)));
}

// 导航键测试（SessionList 焦点）
#[test]
fn test_navigation_vim_keys() {
    let key_j = KeyEvent::new(KeyCode::Char('j'), KeyModifiers::NONE);
    let event_j = EventHandler::map_key_event(key_j, FocusArea::SessionList);
    assert!(matches!(event_j, Some(AppEvent::MoveDown)));

    let key_k = KeyEvent::new(KeyCode::Char('k'), KeyModifiers::NONE);
    let event_k = EventHandler::map_key_event(key_k, FocusArea::SessionList);
    assert!(matches!(event_k, Some(AppEvent::MoveUp)));
}

#[test]
fn test_navigation_arrow_keys() {
    let key_down = KeyEvent::new(KeyCode::Down, KeyModifiers::NONE);
    let event_down = EventHandler::map_key_event(key_down, FocusArea::SessionList);
    assert!(matches!(event_down, Some(AppEvent::MoveDown)));

    let key_up = KeyEvent::new(KeyCode::Up, KeyModifiers::NONE);
    let event_up = EventHandler::map_key_event(key_up, FocusArea::SessionList);
    assert!(matches!(event_up, Some(AppEvent::MoveUp)));
}

#[test]
fn test_select_enter() {
    let key = KeyEvent::new(KeyCode::Enter, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::Select)));
}

// 会话操作测试（SessionList 焦点）
#[test]
fn test_new_session_n() {
    let key = KeyEvent::new(KeyCode::Char('n'), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::NewSession)));
}

#[test]
fn test_new_session_ctrl_n() {
    let key = KeyEvent::new(KeyCode::Char('n'), KeyModifiers::CONTROL);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::NewSession)));
}

#[test]
fn test_delete_session() {
    let key = KeyEvent::new(KeyCode::Char('d'), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::DeleteSession)));
}

#[test]
fn test_refresh_r() {
    let key = KeyEvent::new(KeyCode::Char('r'), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::Refresh)));
}

#[test]
fn test_refresh_f5() {
    let key = KeyEvent::new(KeyCode::F(5), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(matches!(event, Some(AppEvent::Refresh)));
}

// 输入操作测试（InputBox 焦点）
#[test]
fn test_input_char() {
    let key = KeyEvent::new(KeyCode::Char('a'), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::Input('a'))));
}

#[test]
fn test_input_char_with_shift() {
    let key = KeyEvent::new(KeyCode::Char('A'), KeyModifiers::SHIFT);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::Input('A'))));
}

#[test]
fn test_input_number() {
    let key = KeyEvent::new(KeyCode::Char('5'), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::Input('5'))));
}

#[test]
fn test_input_space() {
    let key = KeyEvent::new(KeyCode::Char(' '), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::Input(' '))));
}

#[test]
fn test_backspace() {
    let key = KeyEvent::new(KeyCode::Backspace, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::Backspace)));
}

#[test]
fn test_cursor_left() {
    let key = KeyEvent::new(KeyCode::Left, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::CursorLeft)));
}

#[test]
fn test_cursor_right() {
    let key = KeyEvent::new(KeyCode::Right, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::InputBox);
    assert!(matches!(event, Some(AppEvent::CursorRight)));
}

// 未映射键测试
#[test]
fn test_unmapped_key_returns_none() {
    let key = KeyEvent::new(KeyCode::Char('x'), KeyModifiers::CONTROL);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(event.is_none());
}

#[test]
fn test_unmapped_function_key() {
    let key = KeyEvent::new(KeyCode::F(1), KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(event.is_none());
}

#[test]
fn test_unmapped_alt_key() {
    let key = KeyEvent::new(KeyCode::Char('a'), KeyModifiers::ALT);
    let event = EventHandler::map_key_event(key, FocusArea::SessionList);
    assert!(event.is_none());
}
