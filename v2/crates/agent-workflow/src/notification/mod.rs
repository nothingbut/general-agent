//! 工作流通知系统
//!
//! 提供多渠道通知功能，支持终端、桌面、日志等通知方式。
//!
//! # 功能特性
//!
//! - **多渠道支持**: Terminal（终端）、Desktop（桌面）、Log（日志）
//! - **优先级管理**: Critical、High、Normal、Low 四级优先级
//! - **智能推断**: 根据工具名称自动推断通知优先级
//! - **异步发送**: 支持并发和批量发送
//! - **跨平台**: 支持 macOS、Linux、Windows
//!
//! # 使用示例
//!
//! ```rust
//! use agent_workflow::notification::{
//!     NotificationManager, Notification, NotificationPriority,
//!     TerminalChannel, LogChannel
//! };
//! use std::sync::Arc;
//!
//! #[tokio::main]
//! async fn main() {
//!     // 创建管理器
//!     let manager = NotificationManager::new();
//!
//!     // 注册渠道
//!     manager.register_channel(Arc::new(TerminalChannel::new())).await;
//!     manager.register_channel(Arc::new(LogChannel::new())).await;
//!
//!     // 创建通知
//!     let notification = Notification::new(
//!         "workflow-1",
//!         "task-1",
//!         "任务完成",
//!         "任务已成功完成"
//!     )
//!     .with_priority(NotificationPriority::High)
//!     .with_channels(vec!["terminal".to_string(), "log".to_string()]);
//!
//!     // 发送通知
//!     let results = manager.send(&notification).await;
//!     println!("发送结果: {:?}", results);
//! }
//! ```
//!
//! # 优先级推断
//!
//! ```rust
//! use agent_workflow::notification::NotificationPriority;
//!
//! // 危险操作 -> Critical
//! assert_eq!(
//!     NotificationPriority::from_tool_name("mcp:filesystem:delete"),
//!     NotificationPriority::Critical
//! );
//!
//! // 修改操作 -> High
//! assert_eq!(
//!     NotificationPriority::from_tool_name("mcp:filesystem:write_file"),
//!     NotificationPriority::High
//! );
//!
//! // 查询操作 -> Normal
//! assert_eq!(
//!     NotificationPriority::from_tool_name("mcp:filesystem:read_file"),
//!     NotificationPriority::Normal
//! );
//! ```

pub mod channels;
pub mod manager;
pub mod models;

pub use channels::{DesktopChannel, LogChannel, NotificationChannel, TerminalChannel};
pub use manager::NotificationManager;
pub use models::{Notification, NotificationPriority};
