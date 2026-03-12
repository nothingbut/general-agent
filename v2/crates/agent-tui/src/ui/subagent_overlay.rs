//! Subagent overlay component for displaying subagent status

use agent_workflow::subagent::{SubagentOrchestrator, SubagentState};
use ratatui::{
    layout::Rect,
    Frame,
};
use std::sync::Arc;
use uuid::Uuid;

/// View mode for the overlay
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ViewMode {
    /// Show subagents for the current session only
    CurrentSession,
    /// Show all subagents across all sessions
    Global,
}

/// Subagent overlay component
pub struct SubagentOverlay {
    /// Whether the overlay is visible
    visible: bool,

    /// Current view mode
    view_mode: ViewMode,

    /// Current session ID (for filtering in CurrentSession mode)
    current_session_id: Option<Uuid>,

    /// Index of the selected item in the list
    selected_index: usize,

    /// Whether to show detailed view
    show_details: bool,

    /// Reference to the orchestrator for fetching subagent states
    orchestrator: Arc<SubagentOrchestrator>,
}

impl SubagentOverlay {
    /// Create a new SubagentOverlay
    pub fn new(orchestrator: Arc<SubagentOrchestrator>) -> Self {
        Self {
            visible: false,
            view_mode: ViewMode::CurrentSession,
            current_session_id: None,
            selected_index: 0,
            show_details: false,
            orchestrator,
        }
    }

    /// Toggle visibility
    pub fn toggle_visible(&mut self) {
        self.visible = !self.visible;
    }

    /// Check if overlay is visible
    pub fn is_visible(&self) -> bool {
        self.visible
    }

    /// Get current view mode
    pub fn view_mode(&self) -> ViewMode {
        self.view_mode
    }

    /// Toggle between view modes
    pub fn toggle_view_mode(&mut self) {
        self.view_mode = match self.view_mode {
            ViewMode::CurrentSession => ViewMode::Global,
            ViewMode::Global => ViewMode::CurrentSession,
        };
        // Reset selection when switching views
        self.selected_index = 0;
    }

    /// Set the current session ID for filtering
    pub fn set_current_session(&mut self, session_id: Uuid) {
        self.current_session_id = Some(session_id);
    }

    /// Get the selected index
    pub fn selected_index(&self) -> usize {
        self.selected_index
    }

    /// Move selection up
    pub fn move_up(&mut self) {
        if self.selected_index > 0 {
            self.selected_index -= 1;
        }
    }

    /// Move selection down
    pub fn move_down(&mut self, max_items: usize) {
        if max_items > 0 && self.selected_index < max_items - 1 {
            self.selected_index += 1;
        }
    }

    /// Toggle details view
    pub fn toggle_details(&mut self) {
        self.show_details = !self.show_details;
    }

    /// Get filtered subagent states based on view mode
    pub fn get_filtered_states(&self) -> Vec<(Uuid, SubagentState)> {
        match self.view_mode {
            ViewMode::CurrentSession => {
                if let Some(session_id) = self.current_session_id {
                    self.orchestrator
                        .get_subagent_states(session_id)
                        .into_iter()
                        .map(|state| (state.session_id(), state))
                        .collect()
                } else {
                    Vec::new()
                }
            }
            ViewMode::Global => {
                self.orchestrator
                    .get_all_states()
                    .into_iter()
                    .map(|state| (state.session_id(), state))
                    .collect()
            }
        }
    }

    /// Render the overlay
    pub fn render(&self, f: &mut Frame, area: Rect) {
        use ratatui::{
            layout::{Alignment, Constraint, Layout},
            style::{Color, Modifier, Style},
            text::{Line, Span},
            widgets::{Block, Borders, List, ListItem, Paragraph},
        };

        // Don't render if not visible
        if !self.visible {
            return;
        }

        // Calculate centered popup area (60% width, 80% height)
        let popup_width = (area.width as f32 * 0.6) as u16;
        let popup_height = (area.height as f32 * 0.8) as u16;
        let popup_x = (area.width.saturating_sub(popup_width)) / 2;
        let popup_y = (area.height.saturating_sub(popup_height)) / 2;

        let popup_area = Rect {
            x: area.x + popup_x,
            y: area.y + popup_y,
            width: popup_width,
            height: popup_height,
        };

        // Get filtered states
        let states = self.get_filtered_states();

        // Create title with mode indicator
        let mode_str = match self.view_mode {
            ViewMode::CurrentSession => "Current Session",
            ViewMode::Global => "Global",
        };
        let title = format!(" Subagent Overlay [{}] ", mode_str);

        // Create block
        let block = Block::default()
            .title(title)
            .borders(Borders::ALL)
            .border_style(Style::default().fg(Color::Cyan));

        // Split popup area: list + help bar
        let chunks = Layout::default()
            .constraints([Constraint::Min(3), Constraint::Length(3)])
            .split(block.inner(popup_area));

        // Render the block background
        f.render_widget(block, popup_area);

        // Create list items
        let items: Vec<ListItem> = states
            .iter()
            .enumerate()
            .map(|(idx, (session_id, state))| {
                let status_str = format!("{:?}", state.status());
                let progress_pct = (state.progress() * 100.0) as u8;

                // Format: [Session ID prefix] Status (Progress%)
                let session_id_short = &session_id.to_string()[..8];
                let content = format!(
                    "[{}] {} ({}%)",
                    session_id_short,
                    status_str,
                    progress_pct
                );

                let style = if idx == self.selected_index {
                    Style::default()
                        .fg(Color::Black)
                        .bg(Color::Cyan)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(Color::White)
                };

                ListItem::new(Line::from(Span::styled(content, style)))
            })
            .collect();

        // Render list
        let list = List::new(items);
        f.render_widget(list, chunks[0]);

        // Create help text
        let help_text = vec![
            Line::from(vec![
                Span::styled("[↑/↓] ", Style::default().fg(Color::Yellow)),
                Span::raw("Navigate  "),
                Span::styled("[Enter] ", Style::default().fg(Color::Yellow)),
                Span::raw("Details  "),
                Span::styled("[Tab] ", Style::default().fg(Color::Yellow)),
                Span::raw("Switch View  "),
                Span::styled("[Esc] ", Style::default().fg(Color::Yellow)),
                Span::raw("Close"),
            ]),
        ];

        let help_paragraph = Paragraph::new(help_text)
            .alignment(Alignment::Center)
            .style(Style::default().fg(Color::Gray));

        f.render_widget(help_paragraph, chunks[1]);
    }
}
