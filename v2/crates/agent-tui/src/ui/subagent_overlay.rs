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

    /// Render the overlay (placeholder for Task 7)
    pub fn render(&self, _f: &mut Frame, _area: Rect) {
        // Will be implemented in Task 7
    }
}
