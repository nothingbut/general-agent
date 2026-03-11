//! 事件处理

use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use crate::state::FocusArea;

/// 应用事件
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AppEvent {
    /// 退出应用
    Quit,
    /// 切换焦点
    SwitchFocus,
    /// 向上移动
    MoveUp,
    /// 向下移动
    MoveDown,
    /// 选择/确认
    Select,
    /// 输入字符
    Input(char),
    /// 删除字符
    Backspace,
    /// 发送消息
    SendMessage,
    /// 清空输入
    ClearInput,
    /// 光标左移
    CursorLeft,
    /// 光标右移
    CursorRight,
    /// 新建会话
    NewSession,
    /// 删除会话
    DeleteSession,
    /// 刷新
    Refresh,
}

/// 事件处理器
pub struct EventHandler;

impl EventHandler {
    /// 映射键盘事件到应用事件（根据焦点状态）
    pub fn map_key_event(key: KeyEvent, focus: FocusArea) -> Option<AppEvent> {
        match (key.code, key.modifiers) {
            // 全局快捷键
            (KeyCode::Char('c'), KeyModifiers::CONTROL) => Some(AppEvent::Quit),
            (KeyCode::Char('q'), KeyModifiers::CONTROL) => Some(AppEvent::Quit),
            (KeyCode::Tab, KeyModifiers::NONE) => Some(AppEvent::SwitchFocus),
            (KeyCode::Esc, _) => Some(AppEvent::SwitchFocus),

            // 根据焦点区域映射不同的行为
            _ => match focus {
                FocusArea::SessionList => Self::map_session_list_key(key),
                FocusArea::InputBox => Self::map_input_box_key(key),
            }
        }
    }

    /// 会话列表焦点时的键盘映射
    fn map_session_list_key(key: KeyEvent) -> Option<AppEvent> {
        match (key.code, key.modifiers) {
            // 导航
            (KeyCode::Char('j'), KeyModifiers::NONE) => Some(AppEvent::MoveDown),
            (KeyCode::Char('k'), KeyModifiers::NONE) => Some(AppEvent::MoveUp),
            (KeyCode::Down, _) => Some(AppEvent::MoveDown),
            (KeyCode::Up, _) => Some(AppEvent::MoveUp),
            (KeyCode::Enter, _) => Some(AppEvent::Select),

            // 会话操作
            (KeyCode::Char('n'), KeyModifiers::NONE) => Some(AppEvent::NewSession),
            (KeyCode::Char('n'), KeyModifiers::CONTROL) => Some(AppEvent::NewSession),
            (KeyCode::Char('d'), KeyModifiers::NONE) => Some(AppEvent::DeleteSession),
            (KeyCode::Char('r'), KeyModifiers::NONE) => Some(AppEvent::Refresh),
            (KeyCode::F(5), _) => Some(AppEvent::Refresh),

            _ => None,
        }
    }

    /// 输入框焦点时的键盘映射
    fn map_input_box_key(key: KeyEvent) -> Option<AppEvent> {
        match (key.code, key.modifiers) {
            // Enter发送消息
            (KeyCode::Enter, _) => Some(AppEvent::Select),

            // 输入相关
            (KeyCode::Backspace, _) => Some(AppEvent::Backspace),
            (KeyCode::Left, _) => Some(AppEvent::CursorLeft),
            (KeyCode::Right, _) => Some(AppEvent::CursorRight),

            // 所有普通字符都作为输入
            (KeyCode::Char(c), KeyModifiers::NONE) => Some(AppEvent::Input(c)),
            (KeyCode::Char(c), KeyModifiers::SHIFT) => Some(AppEvent::Input(c)),

            _ => None,
        }
    }
}
