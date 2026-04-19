//! TUI 应用主结构

use crate::{
    backend::{BackendCommand, BackendUpdate},
    event::{AppEvent, EventHandler},
    state::{AppState, FocusArea, MessageItem, SessionState},
    ui::{
        self, CommandAction, CommandPalette, HelpOverlay, NotificationManager, PerformanceOverlay,
        SidePanel, SubagentOverlay, Theme,
    },
    TuiResult,
};
use agent_workflow::{
    subagent::{OrchestratorConfig, SubagentOrchestrator},
    workflow::performance::PerformanceMonitor,
};
use crossterm::{
    event::{self, Event},
    terminal::{disable_raw_mode, enable_raw_mode, EnterAlternateScreen, LeaveAlternateScreen},
    ExecutableCommand,
};
use ratatui::{backend::CrosstermBackend, Terminal};
use std::{
    io::{self, Stdout},
    sync::{Arc, Mutex},
};
use tokio::sync::mpsc;

/// TUI 应用
pub struct TuiApp {
    state: AppState,
    terminal: Terminal<CrosstermBackend<Stdout>>,
    backend_tx: mpsc::UnboundedSender<BackendCommand>,
    backend_rx: mpsc::UnboundedReceiver<BackendUpdate>,
    should_quit: bool,
    subagent_overlay: SubagentOverlay,
    performance_overlay: PerformanceOverlay,
    help_overlay: HelpOverlay,
    notifications: NotificationManager,
    command_palette: CommandPalette,
    side_panel: SidePanel,
    theme: Theme,
    #[allow(dead_code)]
    performance_monitor: Arc<Mutex<PerformanceMonitor>>,
}

impl TuiApp {
    pub fn new() -> TuiResult<(Self, mpsc::UnboundedReceiver<BackendCommand>)> {
        enable_raw_mode()?;
        let mut stdout = io::stdout();
        stdout.execute(EnterAlternateScreen)?;
        let backend = CrosstermBackend::new(stdout);
        let terminal = Terminal::new(backend)?;

        let (backend_tx, backend_cmd_rx) = mpsc::unbounded_channel();
        let (_backend_update_tx, backend_rx) = mpsc::unbounded_channel();

        let orchestrator = Arc::new(SubagentOrchestrator::new(OrchestratorConfig::default()));
        let subagent_overlay = SubagentOverlay::new(orchestrator);

        let performance_monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let performance_overlay = PerformanceOverlay::new(performance_monitor.clone());

        let app = Self {
            state: AppState::new(),
            terminal,
            backend_tx,
            backend_rx,
            should_quit: false,
            subagent_overlay,
            performance_overlay,
            help_overlay: HelpOverlay::new(),
            notifications: NotificationManager::new(),
            command_palette: CommandPalette::new(),
            side_panel: SidePanel::new(),
            theme: Theme::default(),
            performance_monitor,
        };

        Ok((app, backend_cmd_rx))
    }

    pub fn new_with_channel(
        backend_rx: mpsc::UnboundedReceiver<BackendUpdate>,
    ) -> TuiResult<(Self, mpsc::UnboundedReceiver<BackendCommand>)> {
        enable_raw_mode()?;
        let mut stdout = io::stdout();
        stdout.execute(EnterAlternateScreen)?;
        let backend = CrosstermBackend::new(stdout);
        let terminal = Terminal::new(backend)?;

        let (backend_tx, backend_cmd_rx) = mpsc::unbounded_channel();

        let orchestrator = Arc::new(SubagentOrchestrator::new(OrchestratorConfig::default()));
        let subagent_overlay = SubagentOverlay::new(orchestrator);

        let performance_monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let performance_overlay = PerformanceOverlay::new(performance_monitor.clone());

        let app = Self {
            state: AppState::new(),
            terminal,
            backend_tx,
            backend_rx,
            should_quit: false,
            subagent_overlay,
            performance_overlay,
            help_overlay: HelpOverlay::new(),
            notifications: NotificationManager::new(),
            command_palette: CommandPalette::new(),
            side_panel: SidePanel::new(),
            theme: Theme::default(),
            performance_monitor,
        };

        Ok((app, backend_cmd_rx))
    }

    pub fn with_runtime(
        runtime: Arc<agent_workflow::AgentRuntime>,
    ) -> TuiResult<(Self, crate::backend_runner::BackendRunner)> {
        let (update_tx, backend_rx) = mpsc::unbounded_channel();
        let (app, cmd_rx) = Self::new_with_channel(backend_rx)?;
        let runner = crate::backend_runner::BackendRunner::new(runtime, cmd_rx, update_tx);
        Ok((app, runner))
    }

    pub async fn run(&mut self) -> TuiResult<()> {
        let _ = self.backend_tx.send(BackendCommand::LoadSessions);

        loop {
            self.draw()?;

            if self.handle_events().await? && self.should_quit {
                break;
            }

            while let Ok(update) = self.backend_rx.try_recv() {
                self.handle_backend_update(update);
            }

            self.notifications.tick();

            tokio::time::sleep(std::time::Duration::from_millis(16)).await;
        }

        Ok(())
    }

    fn draw(&mut self) -> TuiResult<()> {
        self.terminal.draw(|f| {
            let layout = if self.side_panel.is_visible() {
                ui::calculate_layout(f.size())
            } else {
                ui::calculate_layout(f.size())
            };

            ui::render_status_bar(f, layout.status_bar, &self.state);
            ui::render_session_list(f, layout.session_list, &self.state);
            ui::render_chat_window(f, layout.chat_window, &self.state);
            ui::render_input_box(f, layout.input_box, &self.state);
            ui::render_info_bar(f, layout.info_bar, &self.state);

            if self.side_panel.is_visible() {
                let side_area = ratatui::layout::Rect {
                    x: layout.chat_window.x,
                    y: layout.chat_window.y,
                    width: layout.chat_window.width / 3,
                    height: layout.chat_window.height,
                };
                self.side_panel.render(f, side_area);
            }

            self.subagent_overlay.render(f, f.size());
            self.performance_overlay.render(f, f.size());
            self.help_overlay.render(f, f.size());
            self.command_palette.render(f, f.size());
            self.notifications.render(f, f.size());
        })?;

        Ok(())
    }

    async fn handle_events(&mut self) -> TuiResult<bool> {
        use crossterm::event::{KeyCode, KeyModifiers};

        if event::poll(std::time::Duration::from_millis(100))? {
            if let Event::Key(key) = event::read()? {
                // 命令面板优先级最高
                if self.command_palette.is_visible() {
                    match key.code {
                        KeyCode::Esc => {
                            self.command_palette.toggle_visible();
                        }
                        KeyCode::Enter => {
                            if let Some(action) = self.command_palette.confirm() {
                                self.execute_command_action(action);
                            }
                        }
                        KeyCode::Up => {
                            self.command_palette.move_up();
                        }
                        KeyCode::Down => {
                            self.command_palette.move_down();
                        }
                        KeyCode::Backspace => {
                            self.command_palette.delete_char();
                        }
                        KeyCode::Char(c) => {
                            self.command_palette.input_char(c);
                        }
                        _ => {}
                    }
                } else if self.help_overlay.is_visible() {
                    match key.code {
                        KeyCode::Esc | KeyCode::Char('h')
                            if key.modifiers.contains(KeyModifiers::CONTROL) =>
                        {
                            self.help_overlay.toggle_visible();
                        }
                        _ => {}
                    }
                } else if self.performance_overlay.is_visible() {
                    match key.code {
                        KeyCode::Esc => {
                            self.performance_overlay.toggle_visible();
                        }
                        KeyCode::Tab => {
                            self.performance_overlay.next_workflow();
                        }
                        KeyCode::Left => {
                            self.performance_overlay.prev_workflow();
                        }
                        KeyCode::Right => {
                            self.performance_overlay.next_workflow();
                        }
                        KeyCode::Home => {
                            self.performance_overlay.first_workflow();
                        }
                        KeyCode::End => {
                            self.performance_overlay.last_workflow();
                        }
                        KeyCode::Char('r') | KeyCode::F(5) => {
                            self.performance_overlay.refresh_cache();
                        }
                        _ => {}
                    }
                } else if self.subagent_overlay.is_visible() {
                    match key.code {
                        KeyCode::Esc => {
                            self.subagent_overlay.toggle_visible();
                        }
                        KeyCode::Up => {
                            self.subagent_overlay.move_up();
                        }
                        KeyCode::Down => {
                            let states = self.subagent_overlay.get_filtered_states();
                            self.subagent_overlay.move_down(states.len());
                        }
                        KeyCode::Tab => {
                            self.subagent_overlay.toggle_view_mode();
                        }
                        KeyCode::Enter => {
                            self.subagent_overlay.toggle_details();
                        }
                        _ => {}
                    }
                } else if self.side_panel.is_visible() {
                    match key.code {
                        KeyCode::Esc => {
                            self.side_panel.toggle_visible();
                        }
                        KeyCode::Tab => {
                            self.side_panel.toggle_tab();
                        }
                        KeyCode::Up | KeyCode::Char('k') => {
                            self.side_panel.move_up();
                        }
                        KeyCode::Down | KeyCode::Char('j') => {
                            self.side_panel.move_down();
                        }
                        _ => {}
                    }
                } else {
                    // 全局快捷键
                    if key.modifiers.contains(KeyModifiers::CONTROL) {
                        match key.code {
                            KeyCode::Char('k') => {
                                self.command_palette.toggle_visible();
                            }
                            KeyCode::Char('s') => {
                                self.subagent_overlay.toggle_visible();
                                if let Some(session_id) = self.current_session_id() {
                                    self.subagent_overlay.set_current_session(session_id);
                                }
                            }
                            KeyCode::Char('h') => {
                                self.help_overlay.toggle_visible();
                            }
                            KeyCode::Char('p') => {
                                self.performance_overlay.toggle_visible();
                            }
                            KeyCode::Char('t') => {
                                self.theme = self.theme.toggle();
                                self.notifications
                                    .info(format!("主题已切换: {}", self.theme.mode_name()));
                            }
                            KeyCode::Char('m') => {
                                self.side_panel.show_memory();
                                let _ = self.backend_tx.send(BackendCommand::LoadMemories);
                            }
                            KeyCode::Char('f') => {
                                self.side_panel.show_files();
                                let _ = self.backend_tx.send(BackendCommand::LoadFiles);
                            }
                            _ => {
                                if let Some(app_event) =
                                    EventHandler::map_key_event(key, self.state.focus)
                                {
                                    self.handle_app_event(app_event)?;
                                }
                            }
                        }
                    } else if let Some(app_event) =
                        EventHandler::map_key_event(key, self.state.focus)
                    {
                        self.handle_app_event(app_event)?;
                    }
                }
            }
            Ok(true)
        } else {
            Ok(false)
        }
    }

    fn execute_command_action(&mut self, action: CommandAction) {
        match action {
            CommandAction::NewSession => {
                let title = format!("会话 {}", chrono::Local::now().format("%m-%d %H:%M"));
                let _ = self
                    .backend_tx
                    .send(BackendCommand::CreateSession { title: Some(title) });
            }
            CommandAction::DeleteSession => {
                if let Some(session_id) = self.state.selected_session_id() {
                    let _ = self
                        .backend_tx
                        .send(BackendCommand::DeleteSession { session_id });
                }
            }
            CommandAction::RefreshSessions => {
                let _ = self.backend_tx.send(BackendCommand::LoadSessions);
            }
            CommandAction::ToggleHelp => {
                self.help_overlay.toggle_visible();
            }
            CommandAction::TogglePerformance => {
                self.performance_overlay.toggle_visible();
            }
            CommandAction::ToggleSubagent => {
                self.subagent_overlay.toggle_visible();
            }
            CommandAction::SwitchFocus => {
                self.state.next_focus();
            }
            CommandAction::Quit => {
                self.should_quit = true;
            }
            CommandAction::ScrollUp => {
                if let Some(id) = self.state.selected_session_id() {
                    self.state.scroll_up(id);
                }
            }
            CommandAction::ScrollDown => {
                if let Some(id) = self.state.selected_session_id() {
                    self.state.scroll_down(id);
                }
            }
            CommandAction::ClearInput => {
                self.state.clear_input();
            }
            CommandAction::ToggleTheme => {
                self.theme = self.theme.toggle();
                self.notifications
                    .info(format!("主题已切换: {}", self.theme.mode_name()));
            }
            CommandAction::ShowMemoryPanel => {
                self.side_panel.show_memory();
                let _ = self.backend_tx.send(BackendCommand::LoadMemories);
            }
            CommandAction::ShowFilePanel => {
                self.side_panel.show_files();
                let _ = self.backend_tx.send(BackendCommand::LoadFiles);
            }
        }
    }

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

                    if let Some(session_id) = self.state.selected_session_id() {
                        let _ = self
                            .backend_tx
                            .send(BackendCommand::LoadMessages { session_id });
                    }
                } else if matches!(self.state.focus, FocusArea::InputBox) {
                    if let Some(session_id) = self.state.selected_session_id() {
                        let content = self.state.input.clone();
                        if !content.is_empty() {
                            let _ = self.backend_tx.send(BackendCommand::SendMessage {
                                session_id,
                                content,
                            });

                            self.state.clear_input();
                            self.state
                                .set_session_state(session_id, SessionState::WaitingResponse);
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
                            self.state
                                .set_session_state(session_id, SessionState::WaitingResponse);
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
                if matches!(self.state.focus, FocusArea::SessionList) {
                    let title = format!("会话 {}", chrono::Local::now().format("%m-%d %H:%M"));
                    let _ = self
                        .backend_tx
                        .send(BackendCommand::CreateSession { title: Some(title) });
                }
            }

            AppEvent::DeleteSession => {
                if matches!(self.state.focus, FocusArea::SessionList) {
                    if let Some(session_id) = self.state.selected_session_id() {
                        let _ = self
                            .backend_tx
                            .send(BackendCommand::DeleteSession { session_id });
                    }
                }
            }

            AppEvent::Refresh => {
                if matches!(self.state.focus, FocusArea::SessionList) {
                    let _ = self.backend_tx.send(BackendCommand::LoadSessions);
                }
            }
        }

        Ok(())
    }

    fn handle_backend_update(&mut self, update: BackendUpdate) {
        match update {
            BackendUpdate::SessionsLoaded { sessions } => {
                for session in sessions {
                    self.state
                        .add_session(session.id, session.title.unwrap_or_default());
                }
            }

            BackendUpdate::MessagesLoaded {
                session_id,
                messages,
            } => {
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

            BackendUpdate::StreamingToken { session_id, token } => {
                self.state.append_streaming_content(session_id, &token);
                self.state
                    .set_session_state(session_id, SessionState::Streaming);
            }

            BackendUpdate::ParagraphComplete {
                session_id,
                paragraph,
            } => {
                self.state.append_streaming_content(session_id, &paragraph);
                self.state
                    .set_session_state(session_id, SessionState::Streaming);
            }

            BackendUpdate::ResponseComplete { session_id } => {
                self.state.finalize_streaming(session_id);
                self.state.set_session_state(session_id, SessionState::Idle);
                self.state.scroll_to_bottom(session_id);
                self.notifications.success("回复已完成");
            }

            BackendUpdate::Error { session_id, error } => {
                self.notifications.error(&error);
                self.state
                    .set_session_state(session_id, SessionState::Error(error));
            }

            BackendUpdate::MemoriesLoaded { memories } => {
                let entries = memories
                    .into_iter()
                    .map(|m| crate::ui::side_panel::MemoryEntry {
                        id: m.id,
                        memory_type: m.memory_type,
                        content: m.content,
                    })
                    .collect();
                self.side_panel.set_memories(entries);
            }

            BackendUpdate::FilesLoaded { files } => {
                let entries = files
                    .into_iter()
                    .map(|f| crate::ui::side_panel::FileEntry {
                        id: f.id,
                        filename: f.filename,
                        size_display: f.size_display,
                        access_level: f.access_level,
                    })
                    .collect();
                self.side_panel.set_files(entries);
            }
        }
    }

    pub fn subagent_overlay_mut(&mut self) -> &mut SubagentOverlay {
        &mut self.subagent_overlay
    }

    pub fn current_session_id(&self) -> Option<uuid::Uuid> {
        self.state.selected_session_id()
    }

    pub fn theme(&self) -> &Theme {
        &self.theme
    }
}

impl Drop for TuiApp {
    fn drop(&mut self) {
        let _ = disable_raw_mode();
        let _ = self.terminal.backend_mut().execute(LeaveAlternateScreen);
    }
}
