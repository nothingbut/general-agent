//! 帮助面板覆盖层

use ratatui::{
    layout::{Constraint, Direction, Layout, Rect},
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Clear, Paragraph, Wrap},
    Frame,
};

use super::colors::AppColors;

pub struct HelpOverlay {
    visible: bool,
}

impl HelpOverlay {
    pub fn new() -> Self {
        Self { visible: false }
    }

    pub fn is_visible(&self) -> bool {
        self.visible
    }

    pub fn toggle_visible(&mut self) {
        self.visible = !self.visible;
    }

    pub fn render(&self, f: &mut Frame, area: Rect) {
        if !self.visible {
            return;
        }

        let popup = centered_rect(60, 80, area);

        f.render_widget(Clear, popup);

        let block = Block::default()
            .borders(Borders::ALL)
            .border_style(Style::default().fg(AppColors::INFO))
            .title(Span::styled(
                " 帮助 (Ctrl+H 关闭) ",
                Style::default()
                    .fg(AppColors::INFO)
                    .add_modifier(Modifier::BOLD),
            ));

        let sections = vec![
            section_header("全局快捷键"),
            key_line("Ctrl+C / Ctrl+Q", "退出应用"),
            key_line("Tab / Esc", "切换焦点区域"),
            key_line("Ctrl+H", "显示/隐藏帮助"),
            key_line("Ctrl+S", "Subagent 面板"),
            key_line("Ctrl+P", "性能监控面板"),
            Line::from(""),
            section_header("会话列表 (左侧面板)"),
            key_line("j / ↓", "向下导航"),
            key_line("k / ↑", "向上导航"),
            key_line("Enter", "选择会话"),
            key_line("n / Ctrl+N", "新建会话"),
            key_line("d", "删除会话"),
            key_line("r / F5", "刷新列表"),
            Line::from(""),
            section_header("输入框"),
            key_line("Enter", "发送消息"),
            key_line("← / →", "移动光标"),
            key_line("Backspace", "删除字符"),
            Line::from(""),
            section_header("对话技巧"),
            tip_line("使用 @skill-name 调用技能"),
            tip_line("使用 /subagent 启动子代理"),
            tip_line("消息自动保存到数据库"),
        ];

        let paragraph = Paragraph::new(sections)
            .block(block)
            .wrap(Wrap { trim: false });

        f.render_widget(paragraph, popup);
    }
}

fn section_header(title: &str) -> Line<'_> {
    Line::from(Span::styled(
        format!("── {} ──", title),
        Style::default()
            .fg(AppColors::SELECTED)
            .add_modifier(Modifier::BOLD),
    ))
}

fn key_line<'a>(key: &'a str, desc: &'a str) -> Line<'a> {
    Line::from(vec![
        Span::styled(
            format!("  {:16}", key),
            Style::default()
                .fg(AppColors::WARNING)
                .add_modifier(Modifier::BOLD),
        ),
        Span::raw(desc),
    ])
}

fn tip_line(tip: &str) -> Line<'_> {
    Line::from(vec![
        Span::styled("  • ", Style::default().fg(AppColors::FOCUS)),
        Span::raw(tip),
    ])
}

fn centered_rect(percent_x: u16, percent_y: u16, area: Rect) -> Rect {
    let vertical = Layout::default()
        .direction(Direction::Vertical)
        .constraints([
            Constraint::Percentage((100 - percent_y) / 2),
            Constraint::Percentage(percent_y),
            Constraint::Percentage((100 - percent_y) / 2),
        ])
        .split(area);

    Layout::default()
        .direction(Direction::Horizontal)
        .constraints([
            Constraint::Percentage((100 - percent_x) / 2),
            Constraint::Percentage(percent_x),
            Constraint::Percentage((100 - percent_x) / 2),
        ])
        .split(vertical[1])[1]
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_help_overlay_toggle() {
        let mut overlay = HelpOverlay::new();
        assert!(!overlay.is_visible());

        overlay.toggle_visible();
        assert!(overlay.is_visible());

        overlay.toggle_visible();
        assert!(!overlay.is_visible());
    }

    #[test]
    fn test_centered_rect() {
        let area = Rect::new(0, 0, 100, 50);
        let popup = centered_rect(60, 80, area);
        assert!(popup.width > 0);
        assert!(popup.height > 0);
        assert!(popup.x > 0);
        assert!(popup.y > 0);
    }
}
