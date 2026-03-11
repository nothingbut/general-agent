//! TUI 应用主结构

use crate::{
    backend::{BackendCommand, BackendUpdate},
    event::{AppEvent, EventHandler},
    state::{AppState, FocusArea, MessageItem, SessionState},
    ui,
    TuiResult,
};
use crossterm::{
    event::{self, Event},
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
        let (_backend_update_tx, backend_rx) = mpsc::unbounded_channel();

        let app = Self {
            state: AppState::new(),
            terminal,
            backend_tx,
            backend_rx,
            should_quit: false,
        };

        Ok((app, backend_cmd_rx))
    }

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

    /// 运行应用
    pub async fn run(&mut self) -> TuiResult<()> {
        // 初始加载会话列表
        let _ = self.backend_tx.send(BackendCommand::LoadSessions);

        loop {
            // 渲染 UI
            self.draw()?;

            // 处理事件（非阻塞轮询）
            if self.handle_events().await? {
                if self.should_quit {
                    break;
                }
            }

            // 处理后台更新（非阻塞）
            if let Ok(update) = self.backend_rx.try_recv() {
                self.handle_backend_update(update);
            }

            // 短暂休眠避免 CPU 占用过高
            tokio::time::sleep(std::time::Duration::from_millis(16)).await;
        }

        Ok(())
    }

    /// 绘制 UI
    fn draw(&mut self) -> TuiResult<()> {
        self.terminal.draw(|f| {
            let layout = ui::calculate_layout(f.size());

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
                if let Some(app_event) = EventHandler::map_key_event(key, self.state.focus) {
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
                        let _ = self.backend_tx
                            .send(BackendCommand::LoadMessages { session_id });
                    }
                } else if matches!(self.state.focus, FocusArea::InputBox) {
                    // 在输入框中按Enter = 发送消息
                    if let Some(session_id) = self.state.selected_session_id() {
                        let content = self.state.input.clone();
                        if !content.is_empty() {
                            let _ = self.backend_tx.send(BackendCommand::SendMessage {
                                session_id,
                                content,
                            });

                            self.state.clear_input();
                            self.state.set_session_state(
                                session_id,
                                SessionState::WaitingResponse,
                            );
                        }
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
                            let _ = self.backend_tx.send(BackendCommand::SendMessage {
                                session_id,
                                content,
                            });

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
                // 只在会话列表焦点时触发
                if matches!(self.state.focus, FocusArea::SessionList) {
                    let title = format!("会话 {}", chrono::Local::now().format("%m-%d %H:%M"));
                    let _ = self.backend_tx.send(BackendCommand::CreateSession {
                        title: Some(title)
                    });
                }
            }

            AppEvent::DeleteSession => {
                // 只在会话列表焦点时触发
                if matches!(self.state.focus, FocusArea::SessionList) {
                    if let Some(session_id) = self.state.selected_session_id() {
                        let _ = self.backend_tx
                            .send(BackendCommand::DeleteSession { session_id });
                    }
                }
            }

            AppEvent::Refresh => {
                // 只在会话列表焦点时触发
                if matches!(self.state.focus, FocusArea::SessionList) {
                    let _ = self.backend_tx.send(BackendCommand::LoadSessions);
                }
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
                // 累积段落到缓冲区
                self.state.append_streaming_content(session_id, &paragraph);
                self.state.set_session_state(session_id, SessionState::Streaming);
            }

            BackendUpdate::ResponseComplete { session_id } => {
                // 完成流式响应，将缓冲区内容保存为一条消息
                self.state.finalize_streaming(session_id);
                self.state.set_session_state(session_id, SessionState::Idle);
                self.state.scroll_to_bottom(session_id);
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
