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
