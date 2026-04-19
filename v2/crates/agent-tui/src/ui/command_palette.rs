//! 命令面板 — Ctrl+K 模糊搜索命令

use ratatui::{
    layout::{Constraint, Direction, Layout, Rect},
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Clear, List, ListItem, Paragraph},
    Frame,
};

use super::colors::AppColors;

#[derive(Debug, Clone)]
pub struct CommandEntry {
    pub name: String,
    pub description: String,
    pub shortcut: Option<String>,
    pub action: CommandAction,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CommandAction {
    NewSession,
    DeleteSession,
    RefreshSessions,
    ToggleHelp,
    TogglePerformance,
    ToggleSubagent,
    SwitchFocus,
    Quit,
    ScrollUp,
    ScrollDown,
    ClearInput,
    ToggleTheme,
    ShowMemoryPanel,
    ShowFilePanel,
}

pub struct CommandPalette {
    visible: bool,
    query: String,
    cursor_pos: usize,
    selected_index: usize,
    commands: Vec<CommandEntry>,
    filtered: Vec<usize>,
}

impl CommandPalette {
    pub fn new() -> Self {
        let commands = default_commands();
        let filtered: Vec<usize> = (0..commands.len()).collect();
        Self {
            visible: false,
            query: String::new(),
            cursor_pos: 0,
            selected_index: 0,
            commands,
            filtered,
        }
    }

    pub fn is_visible(&self) -> bool {
        self.visible
    }

    pub fn toggle_visible(&mut self) {
        self.visible = !self.visible;
        if self.visible {
            self.query.clear();
            self.cursor_pos = 0;
            self.selected_index = 0;
            self.filtered = (0..self.commands.len()).collect();
        }
    }

    pub fn input_char(&mut self, c: char) {
        self.query.insert(self.cursor_pos, c);
        self.cursor_pos += c.len_utf8();
        self.update_filter();
    }

    pub fn delete_char(&mut self) {
        if self.cursor_pos > 0 {
            let prev_char = self.query[..self.cursor_pos]
                .chars()
                .next_back()
                .unwrap();
            self.cursor_pos -= prev_char.len_utf8();
            self.query.remove(self.cursor_pos);
            self.update_filter();
        }
    }

    pub fn move_up(&mut self) {
        if self.selected_index > 0 {
            self.selected_index -= 1;
        }
    }

    pub fn move_down(&mut self) {
        if !self.filtered.is_empty() && self.selected_index + 1 < self.filtered.len() {
            self.selected_index += 1;
        }
    }

    pub fn confirm(&mut self) -> Option<CommandAction> {
        let action = self
            .filtered
            .get(self.selected_index)
            .map(|&idx| self.commands[idx].action.clone());
        self.visible = false;
        action
    }

    pub fn filtered_commands(&self) -> Vec<&CommandEntry> {
        self.filtered
            .iter()
            .map(|&idx| &self.commands[idx])
            .collect()
    }

    pub fn selected_index(&self) -> usize {
        self.selected_index
    }

    pub fn query(&self) -> &str {
        &self.query
    }

    fn update_filter(&mut self) {
        let query_lower = self.query.to_lowercase();
        self.filtered = self
            .commands
            .iter()
            .enumerate()
            .filter(|(_, cmd)| fuzzy_match(&cmd.name, &query_lower) || fuzzy_match(&cmd.description, &query_lower))
            .map(|(i, _)| i)
            .collect();
        self.selected_index = 0;
    }

    pub fn render(&self, f: &mut Frame, area: Rect) {
        if !self.visible {
            return;
        }

        let popup = centered_rect(50, 60, area);
        f.render_widget(Clear, popup);

        let block = Block::default()
            .borders(Borders::ALL)
            .border_style(Style::default().fg(AppColors::INFO))
            .title(Span::styled(
                " 命令面板 (Esc 关闭) ",
                Style::default()
                    .fg(AppColors::INFO)
                    .add_modifier(Modifier::BOLD),
            ));

        let inner = block.inner(popup);
        f.render_widget(block, popup);

        let chunks = Layout::default()
            .direction(Direction::Vertical)
            .constraints([Constraint::Length(1), Constraint::Length(1), Constraint::Min(0)])
            .split(inner);

        let search_line = Line::from(vec![
            Span::styled("> ", Style::default().fg(AppColors::FOCUS)),
            Span::raw(&self.query),
            Span::styled("█", Style::default().fg(AppColors::FOCUS)),
        ]);
        f.render_widget(Paragraph::new(search_line), chunks[0]);

        let separator = Line::from(Span::styled(
            "─".repeat(chunks[1].width as usize),
            Style::default().fg(AppColors::NORMAL),
        ));
        f.render_widget(Paragraph::new(separator), chunks[1]);

        let items: Vec<ListItem> = self
            .filtered_commands()
            .iter()
            .enumerate()
            .map(|(i, cmd)| {
                let is_selected = i == self.selected_index;
                let style = if is_selected {
                    Style::default()
                        .fg(AppColors::SELECTED)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(AppColors::NORMAL)
                };

                let marker = if is_selected { "▸ " } else { "  " };

                let shortcut_span = cmd.shortcut.as_ref().map(|s| {
                    Span::styled(
                        format!("  [{}]", s),
                        Style::default()
                            .fg(AppColors::WARNING)
                            .add_modifier(Modifier::DIM),
                    )
                });

                let mut spans = vec![
                    Span::styled(marker, style),
                    Span::styled(&cmd.name, style),
                    Span::styled(
                        format!("  {}", cmd.description),
                        Style::default().fg(AppColors::NORMAL).add_modifier(Modifier::DIM),
                    ),
                ];

                if let Some(s) = shortcut_span {
                    spans.push(s);
                }

                ListItem::new(Line::from(spans))
            })
            .collect();

        let list = List::new(items);
        f.render_widget(list, chunks[2]);
    }
}

fn fuzzy_match(haystack: &str, needle: &str) -> bool {
    if needle.is_empty() {
        return true;
    }
    let haystack_lower = haystack.to_lowercase();
    let needle_lower = needle.to_lowercase();
    let mut needle_chars = needle_lower.chars();
    let mut current = needle_chars.next();
    for h in haystack_lower.chars() {
        if let Some(n) = current {
            if h == n {
                current = needle_chars.next();
            }
        } else {
            return true;
        }
    }
    current.is_none()
}

fn default_commands() -> Vec<CommandEntry> {
    vec![
        CommandEntry {
            name: "新建会话".into(),
            description: "创建一个新的对话会话".into(),
            shortcut: Some("n".into()),
            action: CommandAction::NewSession,
        },
        CommandEntry {
            name: "删除会话".into(),
            description: "删除当前选中的会话".into(),
            shortcut: Some("d".into()),
            action: CommandAction::DeleteSession,
        },
        CommandEntry {
            name: "刷新列表".into(),
            description: "重新加载会话列表".into(),
            shortcut: Some("r / F5".into()),
            action: CommandAction::RefreshSessions,
        },
        CommandEntry {
            name: "帮助面板".into(),
            description: "显示帮助信息".into(),
            shortcut: Some("Ctrl+H".into()),
            action: CommandAction::ToggleHelp,
        },
        CommandEntry {
            name: "性能监控".into(),
            description: "查看性能统计".into(),
            shortcut: Some("Ctrl+P".into()),
            action: CommandAction::TogglePerformance,
        },
        CommandEntry {
            name: "Subagent 面板".into(),
            description: "查看子代理状态".into(),
            shortcut: Some("Ctrl+S".into()),
            action: CommandAction::ToggleSubagent,
        },
        CommandEntry {
            name: "切换焦点".into(),
            description: "在会话列表和输入框之间切换".into(),
            shortcut: Some("Tab".into()),
            action: CommandAction::SwitchFocus,
        },
        CommandEntry {
            name: "切换主题".into(),
            description: "深色/浅色主题切换".into(),
            shortcut: None,
            action: CommandAction::ToggleTheme,
        },
        CommandEntry {
            name: "记忆面板".into(),
            description: "查看和管理长期记忆".into(),
            shortcut: None,
            action: CommandAction::ShowMemoryPanel,
        },
        CommandEntry {
            name: "文件面板".into(),
            description: "查看和管理上传文件".into(),
            shortcut: None,
            action: CommandAction::ShowFilePanel,
        },
        CommandEntry {
            name: "清空输入".into(),
            description: "清除输入框内容".into(),
            shortcut: None,
            action: CommandAction::ClearInput,
        },
        CommandEntry {
            name: "退出应用".into(),
            description: "退出 Agent TUI".into(),
            shortcut: Some("Ctrl+C".into()),
            action: CommandAction::Quit,
        },
    ]
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
    fn test_fuzzy_match_basic() {
        assert!(fuzzy_match("新建会话", "新建"));
        assert!(fuzzy_match("新建会话", "会话"));
        assert!(fuzzy_match("新建会话", ""));
        assert!(!fuzzy_match("新建会话", "删除"));
    }

    #[test]
    fn test_fuzzy_match_english() {
        assert!(fuzzy_match("Subagent", "sub"));
        assert!(fuzzy_match("Subagent", "sba"));
        assert!(fuzzy_match("Subagent", "SUBAGENT"));
        assert!(!fuzzy_match("Subagent", "xyz"));
    }

    #[test]
    fn test_command_palette_toggle() {
        let mut palette = CommandPalette::new();
        assert!(!palette.is_visible());

        palette.toggle_visible();
        assert!(palette.is_visible());
        assert!(palette.query().is_empty());

        palette.toggle_visible();
        assert!(!palette.is_visible());
    }

    #[test]
    fn test_command_palette_input() {
        let mut palette = CommandPalette::new();
        palette.toggle_visible();

        palette.input_char('新');
        palette.input_char('建');
        assert_eq!(palette.query(), "新建");

        palette.delete_char();
        assert_eq!(palette.query(), "新");
    }

    #[test]
    fn test_command_palette_filter() {
        let mut palette = CommandPalette::new();
        palette.toggle_visible();

        let total = palette.filtered_commands().len();
        assert_eq!(total, 12);

        palette.input_char('退');
        palette.input_char('出');
        let filtered = palette.filtered_commands();
        assert_eq!(filtered.len(), 1);
        assert_eq!(filtered[0].action, CommandAction::Quit);
    }

    #[test]
    fn test_command_palette_navigation() {
        let mut palette = CommandPalette::new();
        palette.toggle_visible();

        assert_eq!(palette.selected_index(), 0);

        palette.move_down();
        assert_eq!(palette.selected_index(), 1);

        palette.move_down();
        assert_eq!(palette.selected_index(), 2);

        palette.move_up();
        assert_eq!(palette.selected_index(), 1);

        palette.move_up();
        palette.move_up();
        assert_eq!(palette.selected_index(), 0);
    }

    #[test]
    fn test_command_palette_confirm() {
        let mut palette = CommandPalette::new();
        palette.toggle_visible();

        let action = palette.confirm();
        assert!(action.is_some());
        assert_eq!(action.unwrap(), CommandAction::NewSession);
        assert!(!palette.is_visible());
    }

    #[test]
    fn test_command_palette_confirm_filtered() {
        let mut palette = CommandPalette::new();
        palette.toggle_visible();
        palette.input_char('退');
        palette.input_char('出');

        let action = palette.confirm();
        assert_eq!(action, Some(CommandAction::Quit));
    }

    #[test]
    fn test_centered_rect_dimensions() {
        let area = Rect::new(0, 0, 100, 50);
        let popup = centered_rect(50, 60, area);
        assert!(popup.width > 0);
        assert!(popup.height > 0);
        assert!(popup.x > 0);
        assert!(popup.y > 0);
    }
}
