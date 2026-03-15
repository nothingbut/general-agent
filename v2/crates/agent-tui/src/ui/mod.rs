//! UI 组件模块

pub mod chat_window;
pub mod colors;
pub mod input_box;
pub mod layout;
pub mod performance_overlay;
pub mod session_list;
pub mod status_bar;
pub mod subagent_overlay;

pub use chat_window::render_chat_window;
pub use colors::AppColors;
pub use input_box::render_input_box;
pub use layout::{calculate_layout, AppLayout};
pub use performance_overlay::PerformanceOverlay;
pub use session_list::render_session_list;
pub use status_bar::{render_info_bar, render_status_bar};
pub use subagent_overlay::{SubagentOverlay, ViewMode};
