//! 记忆/文件侧边面板 — 可切换显示记忆或文件列表

use ratatui::{
    layout::Rect,
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, List, ListItem, Paragraph, Tabs},
    Frame,
};

use super::colors::AppColors;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SidePanelTab {
    Memory,
    File,
}

#[derive(Debug, Clone)]
pub struct MemoryEntry {
    pub id: String,
    pub memory_type: String,
    pub content: String,
}

#[derive(Debug, Clone)]
pub struct FileEntry {
    pub id: String,
    pub filename: String,
    pub size_display: String,
    pub access_level: String,
}

pub struct SidePanel {
    visible: bool,
    tab: SidePanelTab,
    memories: Vec<MemoryEntry>,
    files: Vec<FileEntry>,
    selected_index: usize,
}

impl SidePanel {
    pub fn new() -> Self {
        Self {
            visible: false,
            tab: SidePanelTab::Memory,
            memories: Vec::new(),
            files: Vec::new(),
            selected_index: 0,
        }
    }

    pub fn is_visible(&self) -> bool {
        self.visible
    }

    pub fn toggle_visible(&mut self) {
        self.visible = !self.visible;
    }

    pub fn show_memory(&mut self) {
        self.visible = true;
        self.tab = SidePanelTab::Memory;
        self.selected_index = 0;
    }

    pub fn show_files(&mut self) {
        self.visible = true;
        self.tab = SidePanelTab::File;
        self.selected_index = 0;
    }

    pub fn toggle_tab(&mut self) {
        self.tab = match self.tab {
            SidePanelTab::Memory => SidePanelTab::File,
            SidePanelTab::File => SidePanelTab::Memory,
        };
        self.selected_index = 0;
    }

    pub fn tab(&self) -> SidePanelTab {
        self.tab
    }

    pub fn set_memories(&mut self, memories: Vec<MemoryEntry>) {
        self.memories = memories;
        self.selected_index = 0;
    }

    pub fn set_files(&mut self, files: Vec<FileEntry>) {
        self.files = files;
        self.selected_index = 0;
    }

    pub fn move_up(&mut self) {
        if self.selected_index > 0 {
            self.selected_index -= 1;
        }
    }

    pub fn move_down(&mut self) {
        let len = match self.tab {
            SidePanelTab::Memory => self.memories.len(),
            SidePanelTab::File => self.files.len(),
        };
        if self.selected_index + 1 < len {
            self.selected_index += 1;
        }
    }

    pub fn selected_index(&self) -> usize {
        self.selected_index
    }

    pub fn selected_memory(&self) -> Option<&MemoryEntry> {
        if self.tab == SidePanelTab::Memory {
            self.memories.get(self.selected_index)
        } else {
            None
        }
    }

    pub fn selected_file(&self) -> Option<&FileEntry> {
        if self.tab == SidePanelTab::File {
            self.files.get(self.selected_index)
        } else {
            None
        }
    }

    pub fn render(&self, f: &mut Frame, area: Rect) {
        if !self.visible {
            return;
        }

        let block = Block::default()
            .borders(Borders::ALL)
            .border_style(Style::default().fg(AppColors::INFO));

        let inner = block.inner(area);
        f.render_widget(block, area);

        if inner.height < 3 {
            return;
        }

        let tab_area = Rect { height: 1, ..inner };
        let list_area = Rect {
            y: inner.y + 1,
            height: inner.height.saturating_sub(1),
            ..inner
        };

        let tab_titles = vec![
            Span::styled(
                " 记忆 ",
                if self.tab == SidePanelTab::Memory {
                    Style::default()
                        .fg(AppColors::SELECTED)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(AppColors::NORMAL)
                },
            ),
            Span::styled(
                " 文件 ",
                if self.tab == SidePanelTab::File {
                    Style::default()
                        .fg(AppColors::SELECTED)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(AppColors::NORMAL)
                },
            ),
        ];

        let tabs = Tabs::new(tab_titles.into_iter().map(Line::from).collect::<Vec<_>>())
            .select(match self.tab {
                SidePanelTab::Memory => 0,
                SidePanelTab::File => 1,
            })
            .divider(Span::raw("|"));

        f.render_widget(tabs, tab_area);

        match self.tab {
            SidePanelTab::Memory => self.render_memories(f, list_area),
            SidePanelTab::File => self.render_files(f, list_area),
        }
    }

    fn render_memories(&self, f: &mut Frame, area: Rect) {
        if self.memories.is_empty() {
            let empty = Paragraph::new(Line::from(Span::styled(
                "暂无记忆",
                Style::default()
                    .fg(AppColors::NORMAL)
                    .add_modifier(Modifier::ITALIC),
            )));
            f.render_widget(empty, area);
            return;
        }

        let items: Vec<ListItem> = self
            .memories
            .iter()
            .enumerate()
            .map(|(i, mem)| {
                let is_selected = i == self.selected_index;
                let style = if is_selected {
                    Style::default()
                        .fg(AppColors::SELECTED)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(AppColors::NORMAL)
                };

                let type_color = match mem.memory_type.as_str() {
                    "User" => AppColors::INFO,
                    "Feedback" => AppColors::WARNING,
                    "Project" => AppColors::FOCUS,
                    "Reference" => AppColors::SELECTED,
                    _ => AppColors::NORMAL,
                };

                let marker = if is_selected { "▸" } else { " " };
                let preview: String = mem.content.chars().take(30).collect();

                ListItem::new(vec![
                    Line::from(vec![
                        Span::styled(marker, style),
                        Span::styled(
                            format!(" [{}] ", mem.memory_type),
                            Style::default().fg(type_color),
                        ),
                    ]),
                    Line::from(Span::styled(
                        format!("  {}", preview),
                        Style::default().fg(AppColors::NORMAL),
                    )),
                ])
            })
            .collect();

        let list = List::new(items);
        f.render_widget(list, area);
    }

    fn render_files(&self, f: &mut Frame, area: Rect) {
        if self.files.is_empty() {
            let empty = Paragraph::new(Line::from(Span::styled(
                "暂无文件",
                Style::default()
                    .fg(AppColors::NORMAL)
                    .add_modifier(Modifier::ITALIC),
            )));
            f.render_widget(empty, area);
            return;
        }

        let items: Vec<ListItem> = self
            .files
            .iter()
            .enumerate()
            .map(|(i, file)| {
                let is_selected = i == self.selected_index;
                let style = if is_selected {
                    Style::default()
                        .fg(AppColors::SELECTED)
                        .add_modifier(Modifier::BOLD)
                } else {
                    Style::default().fg(AppColors::NORMAL)
                };

                let access_color = match file.access_level.as_str() {
                    "Public" => AppColors::FOCUS,
                    "Shared" => AppColors::WARNING,
                    _ => AppColors::NORMAL,
                };

                let marker = if is_selected { "▸" } else { " " };

                ListItem::new(vec![
                    Line::from(vec![
                        Span::styled(marker, style),
                        Span::styled(format!(" {}", file.filename), style),
                    ]),
                    Line::from(vec![
                        Span::styled(
                            format!("  {} ", file.size_display),
                            Style::default()
                                .fg(AppColors::NORMAL)
                                .add_modifier(Modifier::DIM),
                        ),
                        Span::styled(
                            &file.access_level,
                            Style::default().fg(access_color),
                        ),
                    ]),
                ])
            })
            .collect();

        let list = List::new(items);
        f.render_widget(list, area);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_side_panel_toggle() {
        let mut panel = SidePanel::new();
        assert!(!panel.is_visible());

        panel.toggle_visible();
        assert!(panel.is_visible());

        panel.toggle_visible();
        assert!(!panel.is_visible());
    }

    #[test]
    fn test_side_panel_tabs() {
        let mut panel = SidePanel::new();
        assert_eq!(panel.tab(), SidePanelTab::Memory);

        panel.toggle_tab();
        assert_eq!(panel.tab(), SidePanelTab::File);

        panel.toggle_tab();
        assert_eq!(panel.tab(), SidePanelTab::Memory);
    }

    #[test]
    fn test_show_memory_panel() {
        let mut panel = SidePanel::new();
        panel.show_memory();
        assert!(panel.is_visible());
        assert_eq!(panel.tab(), SidePanelTab::Memory);
    }

    #[test]
    fn test_show_file_panel() {
        let mut panel = SidePanel::new();
        panel.show_files();
        assert!(panel.is_visible());
        assert_eq!(panel.tab(), SidePanelTab::File);
    }

    #[test]
    fn test_memory_navigation() {
        let mut panel = SidePanel::new();
        panel.set_memories(vec![
            MemoryEntry {
                id: "1".into(),
                memory_type: "User".into(),
                content: "记忆1".into(),
            },
            MemoryEntry {
                id: "2".into(),
                memory_type: "Project".into(),
                content: "记忆2".into(),
            },
            MemoryEntry {
                id: "3".into(),
                memory_type: "Feedback".into(),
                content: "记忆3".into(),
            },
        ]);

        assert_eq!(panel.selected_index(), 0);
        panel.move_down();
        assert_eq!(panel.selected_index(), 1);
        panel.move_down();
        assert_eq!(panel.selected_index(), 2);
        panel.move_down();
        assert_eq!(panel.selected_index(), 2);
        panel.move_up();
        assert_eq!(panel.selected_index(), 1);
    }

    #[test]
    fn test_file_navigation() {
        let mut panel = SidePanel::new();
        panel.show_files();
        panel.set_files(vec![
            FileEntry {
                id: "1".into(),
                filename: "test.txt".into(),
                size_display: "1.2 KB".into(),
                access_level: "Private".into(),
            },
            FileEntry {
                id: "2".into(),
                filename: "data.json".into(),
                size_display: "3.4 MB".into(),
                access_level: "Public".into(),
            },
        ]);

        assert_eq!(panel.selected_index(), 0);
        assert!(panel.selected_file().is_some());
        assert_eq!(panel.selected_file().unwrap().filename, "test.txt");

        panel.move_down();
        assert_eq!(panel.selected_file().unwrap().filename, "data.json");
    }

    #[test]
    fn test_selected_memory() {
        let mut panel = SidePanel::new();
        panel.set_memories(vec![MemoryEntry {
            id: "1".into(),
            memory_type: "User".into(),
            content: "test".into(),
        }]);

        assert!(panel.selected_memory().is_some());
        assert_eq!(panel.selected_memory().unwrap().content, "test");
    }

    #[test]
    fn test_empty_selection() {
        let panel = SidePanel::new();
        assert!(panel.selected_memory().is_none());
        assert!(panel.selected_file().is_none());
    }
}
