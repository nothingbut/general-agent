//! 通知系统 - 临时消息提示

use ratatui::{
    layout::Rect,
    style::{Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Clear, Paragraph},
    Frame,
};
use std::time::{Duration, Instant};

use super::colors::AppColors;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum NotificationLevel {
    Info,
    Success,
    Warning,
    Error,
}

#[derive(Debug, Clone)]
pub struct Notification {
    pub message: String,
    pub level: NotificationLevel,
    created_at: Instant,
    duration: Duration,
}

impl Notification {
    fn new(message: String, level: NotificationLevel, duration: Duration) -> Self {
        Self {
            message,
            level,
            created_at: Instant::now(),
            duration,
        }
    }

    pub fn is_expired(&self) -> bool {
        self.created_at.elapsed() >= self.duration
    }
}

pub struct NotificationManager {
    notifications: Vec<Notification>,
    max_visible: usize,
}

impl NotificationManager {
    pub fn new() -> Self {
        Self {
            notifications: Vec::new(),
            max_visible: 3,
        }
    }

    pub fn info(&mut self, message: impl Into<String>) {
        self.push(message.into(), NotificationLevel::Info, Duration::from_secs(3));
    }

    pub fn success(&mut self, message: impl Into<String>) {
        self.push(message.into(), NotificationLevel::Success, Duration::from_secs(3));
    }

    pub fn warning(&mut self, message: impl Into<String>) {
        self.push(message.into(), NotificationLevel::Warning, Duration::from_secs(5));
    }

    pub fn error(&mut self, message: impl Into<String>) {
        self.push(message.into(), NotificationLevel::Error, Duration::from_secs(8));
    }

    fn push(&mut self, message: String, level: NotificationLevel, duration: Duration) {
        self.notifications
            .push(Notification::new(message, level, duration));
    }

    pub fn tick(&mut self) {
        self.notifications.retain(|n| !n.is_expired());
    }

    pub fn has_notifications(&self) -> bool {
        !self.notifications.is_empty()
    }

    pub fn render(&self, f: &mut Frame, area: Rect) {
        if self.notifications.is_empty() {
            return;
        }

        let visible: Vec<&Notification> = self
            .notifications
            .iter()
            .rev()
            .take(self.max_visible)
            .collect();

        let height = visible.len() as u16 + 2;
        let width = 50u16.min(area.width.saturating_sub(4));

        let x = area.width.saturating_sub(width).saturating_sub(2);
        let y = area.height.saturating_sub(height).saturating_sub(2);

        let popup = Rect::new(x, y, width, height);

        f.render_widget(Clear, popup);

        let block = Block::default()
            .borders(Borders::ALL)
            .border_style(Style::default().fg(AppColors::NORMAL))
            .title(" 通知 ");

        let lines: Vec<Line> = visible
            .iter()
            .map(|n| {
                let (icon, color) = match n.level {
                    NotificationLevel::Info => ("ℹ", AppColors::INFO),
                    NotificationLevel::Success => ("✓", AppColors::FOCUS),
                    NotificationLevel::Warning => ("⚠", AppColors::WARNING),
                    NotificationLevel::Error => ("✗", AppColors::ERROR),
                };

                Line::from(vec![
                    Span::styled(
                        format!(" {} ", icon),
                        Style::default().fg(color).add_modifier(Modifier::BOLD),
                    ),
                    Span::raw(&n.message),
                ])
            })
            .collect();

        let paragraph = Paragraph::new(lines).block(block);

        f.render_widget(paragraph, popup);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_notification_manager_push() {
        let mut mgr = NotificationManager::new();
        assert!(!mgr.has_notifications());

        mgr.info("测试消息");
        assert!(mgr.has_notifications());
    }

    #[test]
    fn test_notification_levels() {
        let mut mgr = NotificationManager::new();
        mgr.info("信息");
        mgr.success("成功");
        mgr.warning("警告");
        mgr.error("错误");
        assert_eq!(mgr.notifications.len(), 4);
    }

    #[test]
    fn test_notification_expiry() {
        let n = Notification::new(
            "测试".into(),
            NotificationLevel::Info,
            Duration::from_millis(0),
        );
        assert!(n.is_expired());
    }

    #[test]
    fn test_notification_not_expired() {
        let n = Notification::new(
            "测试".into(),
            NotificationLevel::Info,
            Duration::from_secs(60),
        );
        assert!(!n.is_expired());
    }

    #[test]
    fn test_tick_removes_expired() {
        let mut mgr = NotificationManager::new();
        mgr.push(
            "过期".into(),
            NotificationLevel::Info,
            Duration::from_millis(0),
        );
        mgr.push(
            "活跃".into(),
            NotificationLevel::Info,
            Duration::from_secs(60),
        );

        mgr.tick();
        assert_eq!(mgr.notifications.len(), 1);
        assert_eq!(mgr.notifications[0].message, "活跃");
    }
}
