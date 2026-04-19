//! UI 组件模块

pub mod chat_window;
pub mod colors;
pub mod command_palette;
pub mod help_overlay;
pub mod input_box;
pub mod layout;
pub mod markdown;
pub mod notification;
pub mod performance_overlay;
pub mod session_list;
pub mod side_panel;
pub mod status_bar;
pub mod subagent_overlay;
pub mod theme;

pub use chat_window::render_chat_window;
pub use colors::AppColors;
pub use command_palette::{CommandAction, CommandPalette};
pub use help_overlay::HelpOverlay;
pub use input_box::render_input_box;
pub use layout::{calculate_layout, AppLayout};
pub use markdown::render_markdown;
pub use notification::NotificationManager;
pub use performance_overlay::PerformanceOverlay;
pub use session_list::render_session_list;
pub use side_panel::SidePanel;
pub use status_bar::{render_info_bar, render_status_bar};
pub use subagent_overlay::{SubagentOverlay, ViewMode};
pub use theme::Theme;
