//! 通知模型定义

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use uuid::Uuid;

/// 通知优先级
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum NotificationPriority {
    /// 关键优先级（删除、执行等危险操作）
    Critical,
    /// 高优先级（写入、更新、创建等修改操作）
    High,
    /// 普通优先级（读取、列表等查询操作）
    Normal,
    /// 低优先级
    Low,
}

impl NotificationPriority {
    /// 根据工具名称推断优先级
    pub fn from_tool_name(tool_name: &str) -> Self {
        let tool_lower = tool_name.to_lowercase();

        // 危险操作 -> Critical
        if tool_lower.contains("delete")
            || tool_lower.contains("remove")
            || tool_lower.contains("drop")
            || tool_lower.contains("execute")
            || tool_lower.contains("run")
            || tool_lower.contains("shell")
        {
            return Self::Critical;
        }

        // 修改操作 -> High
        if tool_lower.contains("write")
            || tool_lower.contains("update")
            || tool_lower.contains("create")
            || tool_lower.contains("modify")
        {
            return Self::High;
        }

        // 查询操作 -> Normal
        if tool_lower.contains("read")
            || tool_lower.contains("list")
            || tool_lower.contains("get")
            || tool_lower.contains("search")
        {
            return Self::Normal;
        }

        // 默认 -> Normal
        Self::Normal
    }
}

impl Default for NotificationPriority {
    fn default() -> Self {
        Self::Normal
    }
}

/// 通知模型
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Notification {
    /// 通知 ID
    pub id: String,
    /// 工作流 ID
    pub workflow_id: String,
    /// 任务 ID
    pub task_id: String,
    /// 标题
    pub title: String,
    /// 消息内容
    pub message: String,
    /// 优先级
    pub priority: NotificationPriority,
    /// 通知渠道（terminal, desktop, log）
    pub channels: Vec<String>,
    /// 创建时间
    pub created_at: DateTime<Utc>,
    /// 是否已读
    pub read: bool,
    /// 元数据
    #[serde(default)]
    pub metadata: HashMap<String, String>,
}

impl Notification {
    /// 创建新通知
    pub fn new(
        workflow_id: impl Into<String>,
        task_id: impl Into<String>,
        title: impl Into<String>,
        message: impl Into<String>,
    ) -> Self {
        Self {
            id: Uuid::new_v4().to_string(),
            workflow_id: workflow_id.into(),
            task_id: task_id.into(),
            title: title.into(),
            message: message.into(),
            priority: NotificationPriority::default(),
            channels: vec!["terminal".to_string()],
            created_at: Utc::now(),
            read: false,
            metadata: HashMap::new(),
        }
    }

    /// 设置优先级
    pub fn with_priority(mut self, priority: NotificationPriority) -> Self {
        self.priority = priority;
        self
    }

    /// 设置通知渠道
    pub fn with_channels(mut self, channels: Vec<String>) -> Self {
        self.channels = channels;
        self
    }

    /// 添加元数据
    pub fn with_metadata(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.metadata.insert(key.into(), value.into());
        self
    }

    /// 标记为已读
    pub fn mark_as_read(&mut self) {
        self.read = true;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_priority_from_tool_name() {
        assert_eq!(
            NotificationPriority::from_tool_name("mcp:filesystem:delete"),
            NotificationPriority::Critical
        );
        assert_eq!(
            NotificationPriority::from_tool_name("mcp:filesystem:write_file"),
            NotificationPriority::High
        );
        assert_eq!(
            NotificationPriority::from_tool_name("mcp:filesystem:read_file"),
            NotificationPriority::Normal
        );
        assert_eq!(
            NotificationPriority::from_tool_name("custom_tool"),
            NotificationPriority::Normal
        );
    }

    #[test]
    fn test_notification_creation() {
        let notification = Notification::new("wf-1", "task-1", "测试", "这是测试消息");

        assert!(!notification.id.is_empty());
        assert_eq!(notification.workflow_id, "wf-1");
        assert_eq!(notification.task_id, "task-1");
        assert_eq!(notification.title, "测试");
        assert_eq!(notification.message, "这是测试消息");
        assert_eq!(notification.priority, NotificationPriority::Normal);
        assert_eq!(notification.channels, vec!["terminal"]);
        assert!(!notification.read);
    }

    #[test]
    fn test_notification_builder() {
        let notification = Notification::new("wf-1", "task-1", "测试", "消息")
            .with_priority(NotificationPriority::High)
            .with_channels(vec!["terminal".to_string(), "desktop".to_string()])
            .with_metadata("key1", "value1");

        assert_eq!(notification.priority, NotificationPriority::High);
        assert_eq!(notification.channels.len(), 2);
        assert_eq!(notification.metadata.get("key1").unwrap(), "value1");
    }

    #[test]
    fn test_mark_as_read() {
        let mut notification = Notification::new("wf-1", "task-1", "测试", "消息");
        assert!(!notification.read);

        notification.mark_as_read();
        assert!(notification.read);
    }

    #[test]
    fn test_priority_default() {
        let priority = NotificationPriority::default();
        assert_eq!(priority, NotificationPriority::Normal);
    }
}
