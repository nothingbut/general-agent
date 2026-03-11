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
