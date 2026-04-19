//! 主题系统 — 深色/浅色切换

use ratatui::style::Color;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ThemeMode {
    Dark,
    Light,
}

#[derive(Debug, Clone, Copy)]
pub struct Theme {
    pub mode: ThemeMode,
    pub selected: Color,
    pub normal: Color,
    pub focus: Color,
    pub error: Color,
    pub warning: Color,
    pub info: Color,
    pub bg: Color,
    pub fg: Color,
    pub border: Color,
    pub border_focus: Color,
    pub user_msg: Color,
    pub assistant_msg: Color,
    pub system_msg: Color,
    pub code_bg: Color,
    pub code_fg: Color,
    pub dim: Color,
    pub accent: Color,
}

impl Theme {
    pub fn dark() -> Self {
        Self {
            mode: ThemeMode::Dark,
            selected: Color::Cyan,
            normal: Color::Gray,
            focus: Color::Green,
            error: Color::Red,
            warning: Color::Yellow,
            info: Color::Blue,
            bg: Color::Reset,
            fg: Color::White,
            border: Color::Gray,
            border_focus: Color::Green,
            user_msg: Color::Blue,
            assistant_msg: Color::Cyan,
            system_msg: Color::DarkGray,
            code_bg: Color::Rgb(40, 40, 40),
            code_fg: Color::Rgb(200, 200, 200),
            dim: Color::DarkGray,
            accent: Color::Magenta,
        }
    }

    pub fn light() -> Self {
        Self {
            mode: ThemeMode::Light,
            selected: Color::DarkGray,
            normal: Color::Black,
            focus: Color::Rgb(0, 128, 0),
            error: Color::Rgb(180, 0, 0),
            warning: Color::Rgb(180, 140, 0),
            info: Color::Rgb(0, 0, 180),
            bg: Color::White,
            fg: Color::Black,
            border: Color::DarkGray,
            border_focus: Color::Rgb(0, 128, 0),
            user_msg: Color::Rgb(0, 0, 160),
            assistant_msg: Color::Rgb(0, 128, 128),
            system_msg: Color::Gray,
            code_bg: Color::Rgb(240, 240, 240),
            code_fg: Color::Rgb(40, 40, 40),
            dim: Color::Gray,
            accent: Color::Rgb(128, 0, 128),
        }
    }

    pub fn toggle(self) -> Self {
        match self.mode {
            ThemeMode::Dark => Self::light(),
            ThemeMode::Light => Self::dark(),
        }
    }

    pub fn mode_name(&self) -> &'static str {
        match self.mode {
            ThemeMode::Dark => "深色",
            ThemeMode::Light => "浅色",
        }
    }
}

impl Default for Theme {
    fn default() -> Self {
        Self::dark()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_is_dark() {
        let theme = Theme::default();
        assert_eq!(theme.mode, ThemeMode::Dark);
    }

    #[test]
    fn test_toggle_dark_to_light() {
        let dark = Theme::dark();
        let light = dark.toggle();
        assert_eq!(light.mode, ThemeMode::Light);
    }

    #[test]
    fn test_toggle_light_to_dark() {
        let light = Theme::light();
        let dark = light.toggle();
        assert_eq!(dark.mode, ThemeMode::Dark);
    }

    #[test]
    fn test_toggle_roundtrip() {
        let original = Theme::dark();
        let toggled = original.toggle().toggle();
        assert_eq!(toggled.mode, ThemeMode::Dark);
    }

    #[test]
    fn test_mode_name() {
        assert_eq!(Theme::dark().mode_name(), "深色");
        assert_eq!(Theme::light().mode_name(), "浅色");
    }

    #[test]
    fn test_dark_theme_colors() {
        let theme = Theme::dark();
        assert_eq!(theme.selected, Color::Cyan);
        assert_eq!(theme.focus, Color::Green);
        assert_eq!(theme.error, Color::Red);
    }

    #[test]
    fn test_light_theme_colors() {
        let theme = Theme::light();
        assert_eq!(theme.fg, Color::Black);
        assert_eq!(theme.bg, Color::White);
    }
}
