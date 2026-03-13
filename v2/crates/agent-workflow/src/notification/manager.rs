//! 通知管理器

use super::channels::NotificationChannel;
use super::models::Notification;
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;

/// 通知管理器
#[derive(Clone)]
pub struct NotificationManager {
    channels: Arc<RwLock<HashMap<String, Arc<dyn NotificationChannel>>>>,
}

impl NotificationManager {
    /// 创建新的通知管理器
    pub fn new() -> Self {
        Self {
            channels: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    /// 注册通知渠道
    pub async fn register_channel(&self, channel: Arc<dyn NotificationChannel>) {
        if !channel.is_available() {
            tracing::warn!("Channel '{}' is not available", channel.name());
            return;
        }

        let name = channel.name().to_string();
        let mut channels = self.channels.write().await;
        channels.insert(name.clone(), channel);
        tracing::info!("Registered notification channel: {}", name);
    }

    /// 注销通知渠道
    pub async fn unregister_channel(&self, name: &str) {
        let mut channels = self.channels.write().await;
        if channels.remove(name).is_some() {
            tracing::info!("Unregistered notification channel: {}", name);
        }
    }

    /// 发送通知
    pub async fn send(&self, notification: &Notification) -> HashMap<String, bool> {
        let mut results = HashMap::new();
        let channels = self.channels.read().await;

        for channel_name in &notification.channels {
            if let Some(channel) = channels.get(channel_name) {
                match channel.send(notification).await {
                    Ok(success) => {
                        results.insert(channel_name.clone(), success);
                    }
                    Err(e) => {
                        tracing::error!("Failed to send notification via {}: {}", channel_name, e);
                        results.insert(channel_name.clone(), false);
                    }
                }
            } else {
                tracing::warn!("Channel '{}' not found", channel_name);
                results.insert(channel_name.clone(), false);
            }
        }

        results
    }

    /// 批量发送通知
    pub async fn send_batch(&self, notifications: &[Notification]) -> Vec<HashMap<String, bool>> {
        let mut results = Vec::new();
        for notification in notifications {
            results.push(self.send(notification).await);
        }
        results
    }

    /// 获取可用的通知渠道列表
    pub async fn get_available_channels(&self) -> Vec<String> {
        let channels = self.channels.read().await;
        channels.keys().cloned().collect()
    }

    /// 获取渠道数量
    pub async fn channel_count(&self) -> usize {
        let channels = self.channels.read().await;
        channels.len()
    }

    /// 检查渠道是否已注册
    pub async fn has_channel(&self, name: &str) -> bool {
        let channels = self.channels.read().await;
        channels.contains_key(name)
    }
}

impl Default for NotificationManager {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::notification::channels::{LogChannel, TerminalChannel};
    use crate::notification::models::NotificationPriority;

    #[tokio::test]
    async fn test_manager_creation() {
        let manager = NotificationManager::new();
        assert_eq!(manager.channel_count().await, 0);
    }

    #[tokio::test]
    async fn test_register_channel() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;

        assert_eq!(manager.channel_count().await, 1);
        assert!(manager.has_channel("terminal").await);
    }

    #[tokio::test]
    async fn test_unregister_channel() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;
        assert_eq!(manager.channel_count().await, 1);

        manager.unregister_channel("terminal").await;
        assert_eq!(manager.channel_count().await, 0);
        assert!(!manager.has_channel("terminal").await);
    }

    #[tokio::test]
    async fn test_send_notification() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;

        let notification = Notification::new("wf-1", "task-1", "测试", "这是一条测试通知")
            .with_channels(vec!["terminal".to_string()]);

        let results = manager.send(&notification).await;
        assert_eq!(results.len(), 1);
        assert_eq!(*results.get("terminal").unwrap(), true);
    }

    #[tokio::test]
    async fn test_send_to_missing_channel() {
        let manager = NotificationManager::new();

        let notification = Notification::new("wf-1", "task-1", "测试", "消息")
            .with_channels(vec!["missing".to_string()]);

        let results = manager.send(&notification).await;
        assert_eq!(results.len(), 1);
        assert_eq!(*results.get("missing").unwrap(), false);
    }

    #[tokio::test]
    async fn test_send_to_multiple_channels() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        let log = Arc::new(LogChannel::new());
        manager.register_channel(terminal).await;
        manager.register_channel(log).await;

        let notification = Notification::new("wf-1", "task-1", "测试", "多渠道通知")
            .with_channels(vec!["terminal".to_string(), "log".to_string()]);

        let results = manager.send(&notification).await;
        assert_eq!(results.len(), 2);
        assert_eq!(*results.get("terminal").unwrap(), true);
        assert_eq!(*results.get("log").unwrap(), true);
    }

    #[tokio::test]
    async fn test_send_batch() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;

        let notifications = vec![
            Notification::new("wf-1", "task-1", "通知1", "第一条通知")
                .with_channels(vec!["terminal".to_string()]),
            Notification::new("wf-1", "task-2", "通知2", "第二条通知")
                .with_channels(vec!["terminal".to_string()]),
        ];

        let results = manager.send_batch(&notifications).await;
        assert_eq!(results.len(), 2);
        assert_eq!(*results[0].get("terminal").unwrap(), true);
        assert_eq!(*results[1].get("terminal").unwrap(), true);
    }

    #[tokio::test]
    async fn test_get_available_channels() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        let log = Arc::new(LogChannel::new());
        manager.register_channel(terminal).await;
        manager.register_channel(log).await;

        let channels = manager.get_available_channels().await;
        assert_eq!(channels.len(), 2);
        assert!(channels.contains(&"terminal".to_string()));
        assert!(channels.contains(&"log".to_string()));
    }

    #[tokio::test]
    async fn test_notification_with_priority() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;

        let notification = Notification::new("wf-1", "task-1", "高优先级", "重要通知")
            .with_priority(NotificationPriority::Critical)
            .with_channels(vec!["terminal".to_string()]);

        let results = manager.send(&notification).await;
        assert_eq!(*results.get("terminal").unwrap(), true);
    }

    #[tokio::test]
    async fn test_concurrent_sends() {
        let manager = NotificationManager::new();

        let terminal = Arc::new(TerminalChannel::new());
        manager.register_channel(terminal).await;

        let manager_clone1 = manager.clone();
        let manager_clone2 = manager.clone();

        let handle1 = tokio::spawn(async move {
            let notification = Notification::new("wf-1", "task-1", "并发1", "消息1")
                .with_channels(vec!["terminal".to_string()]);
            manager_clone1.send(&notification).await
        });

        let handle2 = tokio::spawn(async move {
            let notification = Notification::new("wf-1", "task-2", "并发2", "消息2")
                .with_channels(vec!["terminal".to_string()]);
            manager_clone2.send(&notification).await
        });

        let results1 = handle1.await.unwrap();
        let results2 = handle2.await.unwrap();

        assert_eq!(*results1.get("terminal").unwrap(), true);
        assert_eq!(*results2.get("terminal").unwrap(), true);
    }
}
