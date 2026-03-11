# Stage 3: TUI 界面实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标:** 为 General Agent V2 构建基于 Ratatui 的终端用户界面，实现多会话管理、流式响应、快捷键操作。

**架构:** 单线程事件循环 + tokio 异步后台任务，使用集中式状态管理（AppState），通过 mpsc channels 进行 UI ↔ Backend 通信。采用分栏布局（左侧会话列表 + 右侧聊天窗口）。

**技术栈:** Ratatui 0.26, Crossterm 0.27, Tokio, agent-workflow

---

## Phase 1: 基础框架

### Task 1.1: 配置依赖和模块结构

**文件:**
- Modify: `v2/crates/agent-tui/Cargo.toml`
- Create: `v2/crates/agent-tui/src/app.rs`
- Create: `v2/crates/agent-tui/src/state.rs`
- Create: `v2/crates/agent-tui/src/event.rs`
- Create: `v2/crates/agent-tui/src/backend.rs`
- Create: `v2/crates/agent-tui/src/ui/mod.rs`
- Modify: `v2/crates/agent-tui/src/lib.rs`

**Step 1: 更新 Cargo.toml 依赖**

Modify `v2/crates/agent-tui/Cargo.toml`:
```toml
[package]
name = "agent-tui"
version.workspace = true
edition.workspace = true

[dependencies]
# 核心依赖
agent-core = { path = "../agent-core" }
agent-workflow = { path = "../agent-workflow" }
agent-storage = { path = "../agent-storage" }
agent-llm = { path = "../agent-llm" }

# TUI
ratatui.workspace = true
crossterm.workspace = true

# 异步
tokio.workspace = true
futures.workspace = true

# 工具
uuid.workspace = true
chrono.workspace = true
anyhow.workspace = true
thiserror.workspace = true
tracing.workspace = true

[dev-dependencies]
tokio = { workspace = true, features = ["test-util"] }
```

**Step 2: 创建基础模块结构**

Create `v2/crates/agent-tui/src/lib.rs`:
```rust
//! Agent TUI - Terminal User Interface
//!
//! 提供基于 Ratatui 的多会话对话界面

pub mod app;
pub mod state;
pub mod event;
pub mod backend;
pub mod ui;

pub use app::TuiApp;
pub use state::{AppState, FocusArea};

use agent_core::error::Result;

/// TUI 错误类型
#[derive(Debug, thiserror::Error)]
pub enum TuiError {
    #[error("终端初始化失败: {0}")]
    TerminalSetupFailed(String),

    #[error("数据库初始化失败: {0}")]
    DatabaseInitFailed(String),

    #[error("LLM 连接失败: {0}")]
    LLMConnectionFailed(String),

    #[error("会话未找到: {0}")]
    SessionNotFound(uuid::Uuid),

    #[error("消息发送失败: {0}")]
    MessageSendFailed(String),

    #[error("IO 错误: {0}")]
    Io(#[from] std::io::Error),

    #[error("其他错误: {0}")]
    Other(#[from] anyhow::Error),
}

pub type TuiResult<T> = std::result::Result<T, TuiError>;
```

**Step 3: 构建验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功（模块文件稍后创建）

**Step 4: Commit**

```bash
git add v2/crates/agent-tui/Cargo.toml v2/crates/agent-tui/src/lib.rs
git commit -m "feat(tui): 配置基础依赖和模块结构"
```

---

### Task 1.2: 实现状态管理（AppState）

**文件:**
- Create: `v2/crates/agent-tui/src/state.rs`
- Create: `v2/crates/agent-tui/tests/state_tests.rs`

**Step 1: 编写状态测试**

Create `v2/crates/agent-tui/tests/state_tests.rs`:
```rust
use agent_tui::state::{AppState, FocusArea, SessionState};
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
```

**Step 2: 运行测试验证失败**

Run: `cargo test -p agent-tui --test state_tests`
Expected: FAIL - 模块未找到

**Step 3: 实现 AppState**

Create `v2/crates/agent-tui/src/state.rs`:
```rust
//! 应用状态管理

use uuid::Uuid;
use std::collections::HashMap;

/// 焦点区域
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FocusArea {
    SessionList,
    InputBox,
}

/// 会话状态
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SessionState {
    Idle,
    WaitingResponse,
    Streaming,
    Error(String),
}

/// 会话项
#[derive(Debug, Clone)]
pub struct SessionItem {
    pub id: Uuid,
    pub title: Option<String>,
    pub state: SessionState,
    pub unread_count: usize,
}

/// 消息项
#[derive(Debug, Clone)]
pub struct MessageItem {
    pub role: String,
    pub content: String,
    pub timestamp: chrono::DateTime<chrono::Utc>,
}

/// 应用状态
#[derive(Debug)]
pub struct AppState {
    /// 当前焦点
    pub focus: FocusArea,

    /// 会话列表
    sessions: Vec<SessionItem>,

    /// 选中的会话索引
    selected_index: usize,

    /// 会话消息缓存
    messages: HashMap<Uuid, Vec<MessageItem>>,

    /// 输入框内容
    pub input: String,

    /// 输入框光标位置
    pub cursor_pos: usize,

    /// 错误信息
    pub error: Option<String>,
}

impl Default for AppState {
    fn default() -> Self {
        Self {
            focus: FocusArea::SessionList,
            sessions: Vec::new(),
            selected_index: 0,
            messages: HashMap::new(),
            input: String::new(),
            cursor_pos: 0,
            error: None,
        }
    }
}

impl AppState {
    /// 创建新状态
    pub fn new() -> Self {
        Self::default()
    }

    /// 切换焦点
    pub fn next_focus(&mut self) {
        self.focus = match self.focus {
            FocusArea::SessionList => FocusArea::InputBox,
            FocusArea::InputBox => FocusArea::SessionList,
        };
    }

    /// 添加会话
    pub fn add_session(&mut self, id: Uuid, title: String) {
        self.sessions.push(SessionItem {
            id,
            title: Some(title),
            state: SessionState::Idle,
            unread_count: 0,
        });
    }

    /// 选择会话
    pub fn select_session(&mut self, index: usize) {
        if index < self.sessions.len() {
            self.selected_index = index;

            // 清除未读计数
            if let Some(session) = self.sessions.get_mut(index) {
                session.unread_count = 0;
            }
        }
    }

    /// 获取选中的会话 ID
    pub fn selected_session_id(&self) -> Option<Uuid> {
        self.sessions.get(self.selected_index).map(|s| s.id)
    }

    /// 获取会话状态
    pub fn get_session_state(&self, session_id: Uuid) -> Option<SessionState> {
        self.sessions
            .iter()
            .find(|s| s.id == session_id)
            .map(|s| s.state.clone())
    }

    /// 设置会话状态
    pub fn set_session_state(&mut self, session_id: Uuid, state: SessionState) {
        if let Some(session) = self.sessions.iter_mut().find(|s| s.id == session_id) {
            session.state = state;
        }
    }

    /// 获取会话列表
    pub fn sessions(&self) -> &[SessionItem] {
        &self.sessions
    }

    /// 获取选中索引
    pub fn selected_index(&self) -> usize {
        self.selected_index
    }

    /// 向上导航
    pub fn move_up(&mut self) {
        if self.selected_index > 0 {
            self.selected_index -= 1;
        }
    }

    /// 向下导航
    pub fn move_down(&mut self) {
        if self.selected_index + 1 < self.sessions.len() {
            self.selected_index += 1;
        }
    }

    /// 获取会话消息
    pub fn get_messages(&self, session_id: Uuid) -> Option<&Vec<MessageItem>> {
        self.messages.get(&session_id)
    }

    /// 添加消息
    pub fn add_message(&mut self, session_id: Uuid, message: MessageItem) {
        self.messages
            .entry(session_id)
            .or_insert_with(Vec::new)
            .push(message);
    }

    /// 清空输入
    pub fn clear_input(&mut self) {
        self.input.clear();
        self.cursor_pos = 0;
    }

    /// 输入字符
    pub fn input_char(&mut self, c: char) {
        self.input.insert(self.cursor_pos, c);
        self.cursor_pos += 1;
    }

    /// 删除字符
    pub fn delete_char(&mut self) {
        if self.cursor_pos > 0 {
            self.input.remove(self.cursor_pos - 1);
            self.cursor_pos -= 1;
        }
    }

    /// 移动光标
    pub fn move_cursor_left(&mut self) {
        if self.cursor_pos > 0 {
            self.cursor_pos -= 1;
        }
    }

    pub fn move_cursor_right(&mut self) {
        if self.cursor_pos < self.input.len() {
            self.cursor_pos += 1;
        }
    }
}
```

**Step 4: 运行测试验证通过**

Run: `cargo test -p agent-tui --test state_tests`
Expected: PASS - 所有测试通过

**Step 5: Commit**

```bash
git add v2/crates/agent-tui/src/state.rs v2/crates/agent-tui/tests/state_tests.rs
git commit -m "feat(tui): 实现应用状态管理和测试"
```

---

### Task 1.3: 实现事件处理

**文件:**
- Create: `v2/crates/agent-tui/src/event.rs`
- Create: `v2/crates/agent-tui/tests/event_tests.rs`

**Step 1: 编写事件测试**

Create `v2/crates/agent-tui/tests/event_tests.rs`:
```rust
use agent_tui::event::{AppEvent, EventHandler};
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};

#[test]
fn test_quit_event() {
    let key = KeyEvent::new(KeyCode::Char('c'), KeyModifiers::CONTROL);
    let event = EventHandler::map_key_event(key);
    assert!(matches!(event, Some(AppEvent::Quit)));
}

#[test]
fn test_focus_switch() {
    let key = KeyEvent::new(KeyCode::Tab, KeyModifiers::NONE);
    let event = EventHandler::map_key_event(key);
    assert!(matches!(event, Some(AppEvent::SwitchFocus)));
}

#[test]
fn test_navigation_keys() {
    let key_j = KeyEvent::new(KeyCode::Char('j'), KeyModifiers::NONE);
    let event_j = EventHandler::map_key_event(key_j);
    assert!(matches!(event_j, Some(AppEvent::MoveDown)));

    let key_k = KeyEvent::new(KeyCode::Char('k'), KeyModifiers::NONE);
    let event_k = EventHandler::map_key_event(key_k);
    assert!(matches!(event_k, Some(AppEvent::MoveUp)));
}
```

**Step 2: 运行测试验证失败**

Run: `cargo test -p agent-tui --test event_tests`
Expected: FAIL - 模块未找到

**Step 3: 实现事件处理**

Create `v2/crates/agent-tui/src/event.rs`:
```rust
//! 事件处理

use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};

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
    /// 映射键盘事件到应用事件
    pub fn map_key_event(key: KeyEvent) -> Option<AppEvent> {
        match (key.code, key.modifiers) {
            // 全局快捷键
            (KeyCode::Char('c'), KeyModifiers::CONTROL) => Some(AppEvent::Quit),
            (KeyCode::Char('q'), KeyModifiers::CONTROL) => Some(AppEvent::Quit),
            (KeyCode::Tab, KeyModifiers::NONE) => Some(AppEvent::SwitchFocus),
            (KeyCode::Esc, _) => Some(AppEvent::SwitchFocus),

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

            // 输入相关
            (KeyCode::Backspace, _) => Some(AppEvent::Backspace),
            (KeyCode::Left, _) => Some(AppEvent::CursorLeft),
            (KeyCode::Right, _) => Some(AppEvent::CursorRight),
            (KeyCode::Char(c), KeyModifiers::NONE) => Some(AppEvent::Input(c)),
            (KeyCode::Char(c), KeyModifiers::SHIFT) => Some(AppEvent::Input(c)),

            _ => None,
        }
    }
}
```

**Step 4: 运行测试验证通过**

Run: `cargo test -p agent-tui --test event_tests`
Expected: PASS

**Step 5: Commit**

```bash
git add v2/crates/agent-tui/src/event.rs v2/crates/agent-tui/tests/event_tests.rs
git commit -m "feat(tui): 实现事件处理和快捷键映射"
```

---

### Task 1.4: 实现后台通信

**文件:**
- Create: `v2/crates/agent-tui/src/backend.rs`

**Step 1: 实现后台消息类型**

Create `v2/crates/agent-tui/src/backend.rs`:
```rust
//! 后台任务管理

use uuid::Uuid;

/// 后台更新（Backend → UI）
#[derive(Debug, Clone)]
pub enum BackendUpdate {
    /// 段落完成（逐段显示）
    ParagraphComplete {
        session_id: Uuid,
        paragraph: String,
    },

    /// 响应完成
    ResponseComplete {
        session_id: Uuid,
    },

    /// 错误
    Error {
        session_id: Uuid,
        error: String,
    },

    /// 会话列表更新
    SessionsLoaded {
        sessions: Vec<SessionInfo>,
    },

    /// 消息加载完成
    MessagesLoaded {
        session_id: Uuid,
        messages: Vec<MessageInfo>,
    },
}

/// 后台命令（UI → Backend）
#[derive(Debug, Clone)]
pub enum BackendCommand {
    /// 发送消息
    SendMessage {
        session_id: Uuid,
        content: String,
    },

    /// 创建会话
    CreateSession {
        title: Option<String>,
    },

    /// 删除会话
    DeleteSession {
        session_id: Uuid,
    },

    /// 加载会话列表
    LoadSessions,

    /// 加载消息
    LoadMessages {
        session_id: Uuid,
    },
}

/// 会话信息
#[derive(Debug, Clone)]
pub struct SessionInfo {
    pub id: Uuid,
    pub title: Option<String>,
    pub updated_at: chrono::DateTime<chrono::Utc>,
}

/// 消息信息
#[derive(Debug, Clone)]
pub struct MessageInfo {
    pub role: String,
    pub content: String,
    pub timestamp: chrono::DateTime<chrono::Utc>,
}
```

**Step 2: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 3: Commit**

```bash
git add v2/crates/agent-tui/src/backend.rs
git commit -m "feat(tui): 实现后台通信消息类型"
```

---

## Phase 2: UI 组件

### Task 2.1: 实现 UI 模块基础

**文件:**
- Create: `v2/crates/agent-tui/src/ui/layout.rs`
- Create: `v2/crates/agent-tui/src/ui/colors.rs`
- Modify: `v2/crates/agent-tui/src/ui/mod.rs`

**Step 1: 创建 UI 模块结构**

Create `v2/crates/agent-tui/src/ui/mod.rs`:
```rust
//! UI 组件模块

pub mod layout;
pub mod colors;

pub use layout::calculate_layout;
pub use colors::AppColors;
```

Create `v2/crates/agent-tui/src/ui/colors.rs`:
```rust
//! 颜色主题

use ratatui::style::Color;

pub struct AppColors;

impl AppColors {
    pub const SELECTED: Color = Color::Cyan;
    pub const NORMAL: Color = Color::Gray;
    pub const FOCUS: Color = Color::Green;
    pub const ERROR: Color = Color::Red;
    pub const WARNING: Color = Color::Yellow;
    pub const INFO: Color = Color::Blue;
}
```

**Step 2: 实现布局计算**

Create `v2/crates/agent-tui/src/ui/layout.rs`:
```rust
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
```

**Step 3: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 4: Commit**

```bash
git add v2/crates/agent-tui/src/ui/
git commit -m "feat(tui): 实现 UI 布局和颜色主题"
```

---

### Task 2.2: 实现会话列表组件

**文件:**
- Create: `v2/crates/agent-tui/src/ui/session_list.rs`
- Modify: `v2/crates/agent-tui/src/ui/mod.rs`

**Step 1: 实现会话列表渲染**

Create `v2/crates/agent-tui/src/ui/session_list.rs`:
```rust
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
                .as_deref()
                .unwrap_or(&session.id.to_string()[..8]);

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
```

Update `v2/crates/agent-tui/src/ui/mod.rs`:
```rust
//! UI 组件模块

pub mod layout;
pub mod colors;
pub mod session_list;

pub use layout::{calculate_layout, AppLayout};
pub use colors::AppColors;
pub use session_list::render_session_list;
```

**Step 2: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 3: Commit**

```bash
git add v2/crates/agent-tui/src/ui/session_list.rs v2/crates/agent-tui/src/ui/mod.rs
git commit -m "feat(tui): 实现会话列表渲染组件"
```

---

### Task 2.3: 实现聊天窗口组件

**文件:**
- Create: `v2/crates/agent-tui/src/ui/chat_window.rs`
- Modify: `v2/crates/agent-tui/src/ui/mod.rs`

**Step 1: 实现聊天窗口渲染**

Create `v2/crates/agent-tui/src/ui/chat_window.rs`:
```rust
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

        // 检查是否正在生成
        let generating = matches!(
            state.get_session_state(session_id),
            Some(crate::state::SessionState::Streaming)
        );

        let mut all_lines = title_line;
        all_lines.extend(message_lines);

        if generating {
            all_lines.push(Line::from(""));
            all_lines.push(Line::from(Span::styled(
                "[正在生成...]",
                Style::default()
                    .fg(AppColors::WARNING)
                    .add_modifier(Modifier::ITALIC),
            )));
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

    let paragraph = Paragraph::new(lines)
        .block(block)
        .wrap(Wrap { trim: false });

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
```

Update `v2/crates/agent-tui/src/ui/mod.rs`:
```rust
//! UI 组件模块

pub mod layout;
pub mod colors;
pub mod session_list;
pub mod chat_window;

pub use layout::{calculate_layout, AppLayout};
pub use colors::AppColors;
pub use session_list::render_session_list;
pub use chat_window::render_chat_window;
```

**Step 2: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 3: Commit**

```bash
git add v2/crates/agent-tui/src/ui/chat_window.rs v2/crates/agent-tui/src/ui/mod.rs
git commit -m "feat(tui): 实现聊天窗口渲染组件"
```

---

### Task 2.4: 实现输入框和状态栏组件

**文件:**
- Create: `v2/crates/agent-tui/src/ui/input_box.rs`
- Create: `v2/crates/agent-tui/src/ui/status_bar.rs`
- Modify: `v2/crates/agent-tui/src/ui/mod.rs`

**Step 1: 实现输入框**

Create `v2/crates/agent-tui/src/ui/input_box.rs`:
```rust
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
```

**Step 2: 实现状态栏**

Create `v2/crates/agent-tui/src/ui/status_bar.rs`:
```rust
//! 状态栏组件

use ratatui::{
    layout::{Alignment, Rect},
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
```

Update `v2/crates/agent-tui/src/ui/mod.rs`:
```rust
//! UI 组件模块

pub mod layout;
pub mod colors;
pub mod session_list;
pub mod chat_window;
pub mod input_box;
pub mod status_bar;

pub use layout::{calculate_layout, AppLayout};
pub use colors::AppColors;
pub use session_list::render_session_list;
pub use chat_window::render_chat_window;
pub use input_box::render_input_box;
pub use status_bar::{render_status_bar, render_info_bar};
```

**Step 3: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 4: Commit**

```bash
git add v2/crates/agent-tui/src/ui/
git commit -m "feat(tui): 实现输入框和状态栏组件"
```

---

## Phase 3: 应用主循环

### Task 3.1: 实现 TuiApp 核心

**文件:**
- Create: `v2/crates/agent-tui/src/app.rs`
- Create: `v2/crates/agent-tui/tests/app_tests.rs`

**Step 1: 编写应用测试**

Create `v2/crates/agent-tui/tests/app_tests.rs`:
```rust
use agent_tui::{AppState, FocusArea};
use agent_tui::event::AppEvent;

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
    use uuid::Uuid;

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
```

**Step 2: 运行测试**

Run: `cargo test -p agent-tui --test app_tests`
Expected: PASS（测试基于已实现的 state）

**Step 3: 实现 TuiApp**

Create `v2/crates/agent-tui/src/app.rs`:
```rust
//! TUI 应用主结构

use crate::{
    backend::{BackendCommand, BackendUpdate},
    event::{AppEvent, EventHandler},
    state::{AppState, FocusArea, MessageItem, SessionState},
    ui,
    TuiResult,
};
use crossterm::{
    event::{self, Event, KeyEvent},
    terminal::{disable_raw_mode, enable_raw_mode, EnterAlternateScreen, LeaveAlternateScreen},
    ExecutableCommand,
};
use ratatui::{backend::CrosstermBackend, Terminal};
use std::io::{self, Stdout};
use tokio::sync::mpsc;

/// TUI 应用
pub struct TuiApp {
    /// 应用状态
    state: AppState,

    /// 终端
    terminal: Terminal<CrosstermBackend<Stdout>>,

    /// 后台命令发送器
    backend_tx: mpsc::UnboundedSender<BackendCommand>,

    /// 后台更新接收器
    backend_rx: mpsc::UnboundedReceiver<BackendUpdate>,

    /// 是否应该退出
    should_quit: bool,
}

impl TuiApp {
    /// 创建新应用
    pub fn new() -> TuiResult<(Self, mpsc::UnboundedReceiver<BackendCommand>)> {
        // 设置终端
        enable_raw_mode()?;
        let mut stdout = io::stdout();
        stdout.execute(EnterAlternateScreen)?;
        let backend = CrosstermBackend::new(stdout);
        let terminal = Terminal::new(backend)?;

        // 创建通信通道
        let (backend_tx, backend_cmd_rx) = mpsc::unbounded_channel();
        let (backend_update_tx, backend_rx) = mpsc::unbounded_channel();

        let app = Self {
            state: AppState::new(),
            terminal,
            backend_tx,
            backend_rx,
            should_quit: false,
        };

        Ok((app, backend_cmd_rx))
    }

    /// 运行应用
    pub async fn run(&mut self) -> TuiResult<()> {
        // 初始加载会话列表
        self.backend_tx.send(BackendCommand::LoadSessions)?;

        loop {
            // 渲染 UI
            self.draw()?;

            // 处理事件
            tokio::select! {
                // 处理键盘事件
                Ok(true) = self.handle_events() => {
                    if self.should_quit {
                        break;
                    }
                }

                // 处理后台更新
                Some(update) = self.backend_rx.recv() => {
                    self.handle_backend_update(update);
                }
            }
        }

        Ok(())
    }

    /// 绘制 UI
    fn draw(&mut self) -> TuiResult<()> {
        self.terminal.draw(|f| {
            let layout = ui::calculate_layout(f.area());

            ui::render_status_bar(f, layout.status_bar, &self.state);
            ui::render_session_list(f, layout.session_list, &self.state);
            ui::render_chat_window(f, layout.chat_window, &self.state);
            ui::render_input_box(f, layout.input_box, &self.state);
            ui::render_info_bar(f, layout.info_bar, &self.state);
        })?;

        Ok(())
    }

    /// 处理事件
    async fn handle_events(&mut self) -> TuiResult<bool> {
        if event::poll(std::time::Duration::from_millis(100))? {
            if let Event::Key(key) = event::read()? {
                if let Some(app_event) = EventHandler::map_key_event(key) {
                    self.handle_app_event(app_event)?;
                }
            }
            Ok(true)
        } else {
            Ok(false)
        }
    }

    /// 处理应用事件
    fn handle_app_event(&mut self, event: AppEvent) -> TuiResult<()> {
        match event {
            AppEvent::Quit => {
                self.should_quit = true;
            }

            AppEvent::SwitchFocus => {
                self.state.next_focus();
            }

            AppEvent::MoveUp => {
                if matches!(self.state.focus, FocusArea::SessionList) {
                    self.state.move_up();
                }
            }

            AppEvent::MoveDown => {
                if matches!(self.state.focus, FocusArea::SessionList) {
                    self.state.move_down();
                }
            }

            AppEvent::Select => {
                if matches!(self.state.focus, FocusArea::SessionList) {
                    let index = self.state.selected_index();
                    self.state.select_session(index);

                    // 加载消息
                    if let Some(session_id) = self.state.selected_session_id() {
                        self.backend_tx
                            .send(BackendCommand::LoadMessages { session_id })?;
                    }
                }
            }

            AppEvent::Input(c) => {
                if matches!(self.state.focus, FocusArea::InputBox) {
                    self.state.input_char(c);
                }
            }

            AppEvent::Backspace => {
                if matches!(self.state.focus, FocusArea::InputBox) {
                    self.state.delete_char();
                }
            }

            AppEvent::SendMessage => {
                if matches!(self.state.focus, FocusArea::InputBox) {
                    if let Some(session_id) = self.state.selected_session_id() {
                        let content = self.state.input.clone();
                        if !content.is_empty() {
                            self.backend_tx.send(BackendCommand::SendMessage {
                                session_id,
                                content,
                            })?;

                            self.state.clear_input();
                            self.state.set_session_state(
                                session_id,
                                SessionState::WaitingResponse,
                            );
                        }
                    }
                }
            }

            AppEvent::ClearInput => {
                self.state.clear_input();
            }

            AppEvent::CursorLeft => {
                self.state.move_cursor_left();
            }

            AppEvent::CursorRight => {
                self.state.move_cursor_right();
            }

            AppEvent::NewSession => {
                // TODO: 实现新建会话对话框
                self.backend_tx.send(BackendCommand::CreateSession { title: None })?;
            }

            AppEvent::DeleteSession => {
                if let Some(session_id) = self.state.selected_session_id() {
                    // TODO: 添加确认对话框
                    self.backend_tx
                        .send(BackendCommand::DeleteSession { session_id })?;
                }
            }

            AppEvent::Refresh => {
                self.backend_tx.send(BackendCommand::LoadSessions)?;
            }
        }

        Ok(())
    }

    /// 处理后台更新
    fn handle_backend_update(&mut self, update: BackendUpdate) {
        match update {
            BackendUpdate::SessionsLoaded { sessions } => {
                // 更新会话列表
                for session in sessions {
                    self.state.add_session(session.id, session.title.unwrap_or_default());
                }
            }

            BackendUpdate::MessagesLoaded { session_id, messages } => {
                // 加载消息
                for msg in messages {
                    self.state.add_message(
                        session_id,
                        MessageItem {
                            role: msg.role,
                            content: msg.content,
                            timestamp: msg.timestamp,
                        },
                    );
                }
            }

            BackendUpdate::ParagraphComplete { session_id, paragraph } => {
                // 添加段落
                self.state.add_message(
                    session_id,
                    MessageItem {
                        role: "assistant".to_string(),
                        content: paragraph,
                        timestamp: chrono::Utc::now(),
                    },
                );

                self.state.set_session_state(session_id, SessionState::Streaming);
            }

            BackendUpdate::ResponseComplete { session_id } => {
                self.state.set_session_state(session_id, SessionState::Idle);
            }

            BackendUpdate::Error { session_id, error } => {
                self.state
                    .set_session_state(session_id, SessionState::Error(error));
            }
        }
    }
}

impl Drop for TuiApp {
    fn drop(&mut self) {
        // 清理终端
        let _ = disable_raw_mode();
        let _ = self
            .terminal
            .backend_mut()
            .execute(LeaveAlternateScreen);
    }
}
```

**Step 4: 编译验证**

Run: `cargo build -p agent-tui`
Expected: 编译成功

**Step 5: Commit**

```bash
git add v2/crates/agent-tui/src/app.rs v2/crates/agent-tui/tests/app_tests.rs
git commit -m "feat(tui): 实现 TuiApp 核心事件循环"
```

---

## Phase 4: 后台集成

### Task 4.1: 实现后台任务管理器

**文件:**
- Create: `v2/crates/agent-tui/examples/tui_demo.rs`

**Step 1: 创建演示程序**

Create `v2/crates/agent-tui/examples/tui_demo.rs`:
```rust
//! TUI 演示程序
//!
//! 运行: cargo run -p agent-tui --example tui_demo

use agent_core::traits::llm::LLMClient;
use agent_llm::OllamaClient;
use agent_storage::{repository::*, Database};
use agent_tui::{backend::*, TuiApp};
use agent_workflow::{ConversationConfig, ConversationFlow, SessionManager};
use anyhow::Result;
use std::sync::Arc;
use tokio::sync::mpsc;

#[tokio::main]
async fn main() -> Result<()> {
    // 初始化日志
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::from_default_env()
                .add_directive(tracing::Level::INFO.into()),
        )
        .init();

    // 初始化数据库
    let db = Database::new("agent_tui_demo.db").await?;
    db.migrate().await?;

    let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
    let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));
    let session_manager = Arc::new(SessionManager::new(session_repo, message_repo));

    // 创建 LLM 客户端
    let config = agent_llm::ollama::OllamaConfig::new("qwen3.5:0.8b".to_string())
        .with_base_url("http://localhost:11434".to_string());
    let llm_client: Arc<dyn LLMClient> = Arc::new(OllamaClient::new(config)?);

    // 创建对话流程
    let conversation_config = ConversationConfig::default();
    let conversation_flow = Arc::new(ConversationFlow::new(
        session_manager.clone(),
        llm_client.clone(),
        conversation_config,
    ));

    // 创建 TUI 应用
    let (mut app, mut backend_rx) = TuiApp::new()?;

    // 启动后台任务
    let backend_task = tokio::spawn(async move {
        run_backend(backend_rx, session_manager, conversation_flow).await
    });

    // 运行应用
    let result = app.run().await;

    // 等待后台任务结束
    backend_task.abort();

    result.map_err(|e| anyhow::anyhow!(e))
}

/// 运行后台任务
async fn run_backend(
    mut cmd_rx: mpsc::UnboundedReceiver<BackendCommand>,
    session_manager: Arc<SessionManager>,
    conversation_flow: Arc<ConversationFlow>,
) {
    while let Some(cmd) = cmd_rx.recv().await {
        match cmd {
            BackendCommand::LoadSessions => {
                // 加载会话列表
                if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                    let session_infos: Vec<SessionInfo> = sessions
                        .into_iter()
                        .map(|s| SessionInfo {
                            id: s.id,
                            title: s.title,
                            updated_at: s.updated_at,
                        })
                        .collect();

                    // TODO: 发送到 UI
                    tracing::info!("Loaded {} sessions", session_infos.len());
                }
            }

            BackendCommand::LoadMessages { session_id } => {
                // 加载消息
                if let Ok(messages) = session_manager.list_messages(session_id, 50).await {
                    let message_infos: Vec<MessageInfo> = messages
                        .into_iter()
                        .map(|m| MessageInfo {
                            role: m.role.to_string(),
                            content: m.content,
                            timestamp: m.created_at,
                        })
                        .collect();

                    // TODO: 发送到 UI
                    tracing::info!("Loaded {} messages", message_infos.len());
                }
            }

            BackendCommand::SendMessage { session_id, content } => {
                // 发送消息
                tracing::info!("Sending message: {}", content);

                // TODO: 调用 conversation_flow
                // TODO: 处理流式响应
                // TODO: 发送段落到 UI
            }

            BackendCommand::CreateSession { title } => {
                // 创建会话
                if let Ok(session) = session_manager.create_session(title).await {
                    tracing::info!("Created session: {}", session.id);
                    // TODO: 刷新会话列表
                }
            }

            BackendCommand::DeleteSession { session_id } => {
                // 删除会话
                if let Ok(_) = session_manager.delete_session(session_id).await {
                    tracing::info!("Deleted session: {}", session_id);
                    // TODO: 刷新会话列表
                }
            }
        }
    }
}
```

**Step 2: 运行演示（手动测试）**

Run: `cargo run -p agent-tui --example tui_demo`
Expected:
- TUI 界面启动
- 显示空的会话列表
- 可以使用快捷键导航
- Ctrl+C 退出

**Step 3: Commit**

```bash
git add v2/crates/agent-tui/examples/tui_demo.rs
git commit -m "feat(tui): 添加 TUI 演示程序（后台框架）"
```

---

### Task 4.2: 完善后台通信

**文件:**
- Modify: `v2/crates/agent-tui/examples/tui_demo.rs`
- Modify: `v2/crates/agent-tui/src/app.rs`

**Step 1: 完善后台任务，添加 UI 更新通道**

Modify `v2/crates/agent-tui/examples/tui_demo.rs`:
```rust
// 在 main 函数中添加更新通道
let (update_tx, update_rx) = mpsc::unbounded_channel();

// 修改 TuiApp 创建
let (mut app, backend_rx) = TuiApp::new_with_channel(update_rx)?;

// 修改后台任务
let backend_task = tokio::spawn(async move {
    run_backend(backend_rx, update_tx, session_manager, conversation_flow).await
});

// 修改 run_backend 函数签名
async fn run_backend(
    mut cmd_rx: mpsc::UnboundedReceiver<BackendCommand>,
    update_tx: mpsc::UnboundedSender<BackendUpdate>,
    session_manager: Arc<SessionManager>,
    conversation_flow: Arc<ConversationFlow>,
) {
    while let Some(cmd) = cmd_rx.recv().await {
        match cmd {
            BackendCommand::LoadSessions => {
                if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                    let session_infos: Vec<SessionInfo> = sessions
                        .into_iter()
                        .map(|s| SessionInfo {
                            id: s.id,
                            title: s.title,
                            updated_at: s.updated_at,
                        })
                        .collect();

                    let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                        sessions: session_infos,
                    });
                }
            }

            BackendCommand::LoadMessages { session_id } => {
                if let Ok(messages) = session_manager.list_messages(session_id, 50).await {
                    let message_infos: Vec<MessageInfo> = messages
                        .into_iter()
                        .map(|m| MessageInfo {
                            role: m.role.to_string(),
                            content: m.content,
                            timestamp: m.created_at,
                        })
                        .collect();

                    let _ = update_tx.send(BackendUpdate::MessagesLoaded {
                        session_id,
                        messages: message_infos,
                    });
                }
            }

            BackendCommand::SendMessage { session_id, content } => {
                // 克隆以便在异步任务中使用
                let flow = conversation_flow.clone();
                let tx = update_tx.clone();

                tokio::spawn(async move {
                    match flow.send_message_stream(session_id, content).await {
                        Ok((mut stream, context)) => {
                            let mut buffer = String::new();

                            while let Ok(Some(chunk)) = stream.next().await {
                                buffer.push_str(&chunk.delta);

                                // 检测段落边界
                                if buffer.contains("\n\n") || buffer.ends_with("。\n") {
                                    let paragraph = buffer.clone();
                                    buffer.clear();

                                    let _ = tx.send(BackendUpdate::ParagraphComplete {
                                        session_id,
                                        paragraph,
                                    });
                                }
                            }

                            // 发送剩余内容
                            if !buffer.is_empty() {
                                let _ = tx.send(BackendUpdate::ParagraphComplete {
                                    session_id,
                                    paragraph: buffer,
                                });
                            }

                            let _ = tx.send(BackendUpdate::ResponseComplete { session_id });
                        }
                        Err(e) => {
                            let _ = tx.send(BackendUpdate::Error {
                                session_id,
                                error: e.to_string(),
                            });
                        }
                    }
                });
            }

            BackendCommand::CreateSession { title } => {
                if let Ok(_session) = session_manager.create_session(title).await {
                    // 刷新会话列表
                    if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                        let session_infos: Vec<SessionInfo> = sessions
                            .into_iter()
                            .map(|s| SessionInfo {
                                id: s.id,
                                title: s.title,
                                updated_at: s.updated_at,
                            })
                            .collect();

                        let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                            sessions: session_infos,
                        });
                    }
                }
            }

            BackendCommand::DeleteSession { session_id } => {
                if let Ok(_) = session_manager.delete_session(session_id).await {
                    // 刷新会话列表
                    if let Ok(sessions) = session_manager.list_sessions(10, 0).await {
                        let session_infos: Vec<SessionInfo> = sessions
                            .into_iter()
                            .map(|s| SessionInfo {
                                id: s.id,
                                title: s.title,
                                updated_at: s.updated_at,
                            })
                            .collect();

                        let _ = update_tx.send(BackendUpdate::SessionsLoaded {
                            sessions: session_infos,
                        });
                    }
                }
            }
        }
    }
}
```

**Step 2: 更新 TuiApp 以支持外部更新通道**

Modify `v2/crates/agent-tui/src/app.rs` - 添加新的构造方法:
```rust
impl TuiApp {
    // ... 保留原有的 new() 方法 ...

    /// 使用外部更新通道创建应用
    pub fn new_with_channel(
        backend_rx: mpsc::UnboundedReceiver<BackendUpdate>,
    ) -> TuiResult<(Self, mpsc::UnboundedReceiver<BackendCommand>)> {
        // 设置终端
        enable_raw_mode()?;
        let mut stdout = io::stdout();
        stdout.execute(EnterAlternateScreen)?;
        let backend = CrosstermBackend::new(stdout);
        let terminal = Terminal::new(backend)?;

        // 创建命令通道
        let (backend_tx, backend_cmd_rx) = mpsc::unbounded_channel();

        let app = Self {
            state: AppState::new(),
            terminal,
            backend_tx,
            backend_rx,
            should_quit: false,
        };

        Ok((app, backend_cmd_rx))
    }
}
```

**Step 3: 测试完整流程**

Run: `cargo run -p agent-tui --example tui_demo`

手动测试:
- [ ] 启动后自动加载会话列表
- [ ] j/k 导航会话
- [ ] Enter 选择会话并加载消息
- [ ] Tab 切换到输入框
- [ ] 输入消息并 Enter 发送
- [ ] 观察流式响应显示
- [ ] n 创建新会话
- [ ] Ctrl+C 退出

**Step 4: Commit**

```bash
git add v2/crates/agent-tui/
git commit -m "feat(tui): 完善后台通信和流式响应处理"
```

---

## Phase 5: 测试和文档

### Task 5.1: 完善测试覆盖

**文件:**
- Create: `v2/crates/agent-tui/tests/integration_tests.rs`

**Step 1: 编写集成测试**

Create `v2/crates/agent-tui/tests/integration_tests.rs`:
```rust
//! TUI 集成测试

use agent_tui::{
    backend::{BackendCommand, BackendUpdate, MessageInfo, SessionInfo},
    state::{AppState, SessionState},
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
        agent_tui::state::MessageItem {
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
```

**Step 2: 运行测试**

Run: `cargo test -p agent-tui`
Expected: 所有测试通过

**Step 3: 检查测试覆盖率**

Run: `cargo tarpaulin -p agent-tui --out Stdout`
Expected: 覆盖率 > 70%

**Step 4: Commit**

```bash
git add v2/crates/agent-tui/tests/integration_tests.rs
git commit -m "test(tui): 添加集成测试覆盖"
```

---

### Task 5.2: 编写使用文档

**文件:**
- Create: `v2/crates/agent-tui/README.md`

**Step 1: 创建文档**

Create `v2/crates/agent-tui/README.md`:
```markdown
# Agent TUI

基于 Ratatui 的终端用户界面，为 General Agent V2 提供现代化的多会话对话体验。

## 特性

- 分栏布局（会话列表 + 聊天窗口）
- 实时流式响应显示
- 会话状态标注（新消息、等待回复、错误）
- Vim 风格快捷键
- 异步后台任务处理

## 快捷键

### 全局

- `Ctrl+C` / `Ctrl+Q`: 退出应用
- `Tab`: 切换焦点
- `ESC`: 取消焦点

### 会话列表

- `j` / `↓`: 下一个会话
- `k` / `↑`: 上一个会话
- `Enter`: 选择会话
- `n`: 新建会话
- `d`: 删除会话
- `r` / `F5`: 刷新列表

### 输入框

- 输入文字
- `Enter`: 发送消息
- `Backspace`: 删除字符
- `←` / `→`: 移动光标

## 使用示例

```rust
use agent_tui::TuiApp;

#[tokio::main]
async fn main() -> Result<()> {
    let (mut app, backend_rx) = TuiApp::new()?;

    // 启动后台任务
    tokio::spawn(async move {
        // 处理后台命令
    });

    // 运行应用
    app.run().await?;

    Ok(())
}
```

## 架构

- **单线程事件循环**: 主 UI 线程处理渲染和事件
- **异步后台任务**: Tokio spawn 处理 LLM 调用
- **消息传递**: mpsc channels 进行 UI ↔ Backend 通信

## 运行演示

```bash
cargo run -p agent-tui --example tui_demo
```

## 测试

```bash
cargo test -p agent-tui
```

## License

MIT
```

**Step 2: Commit**

```bash
git add v2/crates/agent-tui/README.md
git commit -m "docs(tui): 添加使用文档"
```

---

### Task 5.3: 最终验收

**手动测试清单:**

Run: `cargo run -p agent-tui --example tui_demo`

验收检查:
- [ ] 启动应用，界面正常显示
- [ ] 会话列表正确渲染（状态标注、选中高亮）
- [ ] 焦点切换正常（Tab、ESC）
- [ ] 快捷键全部工作（j/k、Enter、n、d）
- [ ] 创建新会话成功
- [ ] 发送消息，接收流式响应
- [ ] 响应逐段显示，无闪烁
- [ ] 切换会话，内容正确加载
- [ ] 错误处理（模拟 LLM 失败）
- [ ] 终端大小调整后布局正确
- [ ] Ctrl+C 正常退出，终端恢复

**Step 1: 运行完整测试套件**

Run: `cargo test -p agent-tui --all-targets`
Expected: 所有测试通过

**Step 2: 构建发布版本**

Run: `cargo build -p agent-tui --release`
Expected: 编译成功，无警告

**Step 3: 最终提交**

```bash
git add v2/crates/agent-tui/
git commit -m "feat(tui): Stage 3 TUI 界面完成

完成功能:
- 分栏布局（会话列表 + 聊天窗口）
- 流式响应逐段显示
- 完整的快捷键系统
- 会话状态管理和标注
- 后台异步任务处理
- 测试覆盖率 > 70%

验收通过，可投入使用。"
```

---

## 总结

**预计时间**: 15-21 小时（2-3 天）

**任务分解**:
- Phase 1: 基础框架（4 个任务）
- Phase 2: UI 组件（4 个任务）
- Phase 3: 应用主循环（1 个任务）
- Phase 4: 后台集成（2 个任务）
- Phase 5: 测试和文档（3 个任务）

**总计**: 14 个任务，每个任务 1-2 小时

**技术要点**:
- TDD 方法（测试先行）
- 增量开发（每个任务独立提交）
- 关注点分离（UI、状态、事件、后台）
- 完整测试覆盖

**下一步**: Stage 4 - MCP 集成
