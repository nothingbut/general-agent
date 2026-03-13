//! 通知渠道实现

use super::models::{Notification, NotificationPriority};
use async_trait::async_trait;
use std::process::Stdio;
use tokio::process::Command;

/// 通知渠道 Trait
#[async_trait]
pub trait NotificationChannel: Send + Sync {
    /// 发送通知
    async fn send(&self, notification: &Notification) -> Result<bool, String>;

    /// 检查渠道是否可用
    fn is_available(&self) -> bool;

    /// 获取渠道名称
    fn name(&self) -> &str;
}

/// 终端通知渠道
#[derive(Debug)]
pub struct TerminalChannel {
    name: String,
}

impl TerminalChannel {
    pub fn new() -> Self {
        Self {
            name: "terminal".to_string(),
        }
    }

    fn get_priority_style(&self, priority: NotificationPriority) -> (&str, &str) {
        match priority {
            NotificationPriority::Critical => ("🔴", "CRITICAL"),
            NotificationPriority::High => ("🟡", "HIGH"),
            NotificationPriority::Normal => ("🔵", "NORMAL"),
            NotificationPriority::Low => ("⚪", "LOW"),
        }
    }
}

impl Default for TerminalChannel {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait]
impl NotificationChannel for TerminalChannel {
    async fn send(&self, notification: &Notification) -> Result<bool, String> {
        let (icon, level) = self.get_priority_style(notification.priority);

        // 使用 ANSI 颜色代码
        let color = match notification.priority {
            NotificationPriority::Critical => "\x1b[31m", // 红色
            NotificationPriority::High => "\x1b[33m",     // 黄色
            NotificationPriority::Normal => "\x1b[36m",   // 青色
            NotificationPriority::Low => "\x1b[37m",      // 白色
        };
        let reset = "\x1b[0m";

        println!();
        println!("{}┌─────────────────────────────────────────┐{}", color, reset);
        println!(
            "{}{} {} - {}{}",
            color, icon, level, notification.title, reset
        );
        println!("{}├─────────────────────────────────────────┤{}", color, reset);
        println!("{}{}{}", color, notification.message, reset);
        println!("{}└─────────────────────────────────────────┘{}", color, reset);
        println!();

        Ok(true)
    }

    fn is_available(&self) -> bool {
        true
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// 桌面通知渠道
#[derive(Debug)]
pub struct DesktopChannel {
    name: String,
    system: String,
}

impl DesktopChannel {
    pub fn new() -> Self {
        let system = std::env::consts::OS.to_string();
        Self {
            name: "desktop".to_string(),
            system,
        }
    }

    async fn send_macos(&self, notification: &Notification) -> Result<bool, String> {
        let script = format!(
            r#"display notification "{}" with title "{}" sound name "default""#,
            notification.message, notification.title
        );

        let output = Command::new("osascript")
            .arg("-e")
            .arg(&script)
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .output()
            .await
            .map_err(|e| format!("Failed to execute osascript: {}", e))?;

        if output.status.success() {
            Ok(true)
        } else {
            Err(format!(
                "osascript failed: {}",
                String::from_utf8_lossy(&output.stderr)
            ))
        }
    }

    async fn send_linux(&self, notification: &Notification) -> Result<bool, String> {
        let urgency = match notification.priority {
            NotificationPriority::Critical => "critical",
            NotificationPriority::High => "normal",
            NotificationPriority::Normal => "normal",
            NotificationPriority::Low => "low",
        };

        let output = Command::new("notify-send")
            .arg("-u")
            .arg(urgency)
            .arg(&notification.title)
            .arg(&notification.message)
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .output()
            .await
            .map_err(|e| format!("Failed to execute notify-send: {}", e))?;

        if output.status.success() {
            Ok(true)
        } else {
            Err(format!(
                "notify-send failed: {}",
                String::from_utf8_lossy(&output.stderr)
            ))
        }
    }

    async fn send_windows(&self, notification: &Notification) -> Result<bool, String> {
        let script = format!(
            r#"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
$template = @"
<toast><visual><binding template="ToastText02">
<text id="1">{}</text>
<text id="2">{}</text>
</binding></visual></toast>
"@
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = New-Object Windows.UI.Notifications.ToastNotification $xml
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("General Agent").Show($toast)
"#,
            notification.title, notification.message
        );

        let output = Command::new("powershell")
            .arg("-Command")
            .arg(&script)
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .output()
            .await
            .map_err(|e| format!("Failed to execute powershell: {}", e))?;

        if output.status.success() {
            Ok(true)
        } else {
            Err(format!(
                "powershell failed: {}",
                String::from_utf8_lossy(&output.stderr)
            ))
        }
    }
}

impl Default for DesktopChannel {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait]
impl NotificationChannel for DesktopChannel {
    async fn send(&self, notification: &Notification) -> Result<bool, String> {
        match self.system.as_str() {
            "macos" => self.send_macos(notification).await,
            "linux" => self.send_linux(notification).await,
            "windows" => self.send_windows(notification).await,
            _ => Err(format!("Unsupported system: {}", self.system)),
        }
    }

    fn is_available(&self) -> bool {
        match self.system.as_str() {
            "macos" => true,
            "linux" => {
                // 检查 notify-send 是否存在
                std::process::Command::new("which")
                    .arg("notify-send")
                    .output()
                    .map(|output| output.status.success())
                    .unwrap_or(false)
            }
            "windows" => true,
            _ => false,
        }
    }

    fn name(&self) -> &str {
        &self.name
    }
}

/// 日志通知渠道
#[derive(Debug)]
pub struct LogChannel {
    name: String,
}

impl LogChannel {
    pub fn new() -> Self {
        Self {
            name: "log".to_string(),
        }
    }
}

impl Default for LogChannel {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait]
impl NotificationChannel for LogChannel {
    async fn send(&self, notification: &Notification) -> Result<bool, String> {
        match notification.priority {
            NotificationPriority::Critical => {
                tracing::error!("[CRITICAL] {} - {}", notification.title, notification.message);
            }
            NotificationPriority::High => {
                tracing::warn!("[HIGH] {} - {}", notification.title, notification.message);
            }
            NotificationPriority::Normal => {
                tracing::info!("[NORMAL] {} - {}", notification.title, notification.message);
            }
            NotificationPriority::Low => {
                tracing::debug!("[LOW] {} - {}", notification.title, notification.message);
            }
        }

        Ok(true)
    }

    fn is_available(&self) -> bool {
        true
    }

    fn name(&self) -> &str {
        &self.name
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_terminal_channel() {
        let channel = TerminalChannel::new();
        assert!(channel.is_available());
        assert_eq!(channel.name(), "terminal");

        let notification = Notification::new("wf-1", "task-1", "测试", "终端通知测试");
        let result = channel.send(&notification).await;
        assert!(result.is_ok());
        assert!(result.unwrap());
    }

    #[tokio::test]
    async fn test_log_channel() {
        let channel = LogChannel::new();
        assert!(channel.is_available());
        assert_eq!(channel.name(), "log");

        let notification =
            Notification::new("wf-1", "task-1", "测试", "日志通知测试")
                .with_priority(NotificationPriority::High);
        let result = channel.send(&notification).await;
        assert!(result.is_ok());
    }

    #[test]
    fn test_desktop_channel_availability() {
        let channel = DesktopChannel::new();
        assert_eq!(channel.name(), "desktop");

        // 在不同系统上可用性不同
        #[cfg(target_os = "macos")]
        assert!(channel.is_available());

        #[cfg(target_os = "linux")]
        {
            // 取决于是否安装了 notify-send
            let _ = channel.is_available();
        }

        #[cfg(target_os = "windows")]
        assert!(channel.is_available());
    }

    #[test]
    fn test_priority_styles() {
        let channel = TerminalChannel::new();

        let (icon, level) = channel.get_priority_style(NotificationPriority::Critical);
        assert_eq!(icon, "🔴");
        assert_eq!(level, "CRITICAL");

        let (icon, level) = channel.get_priority_style(NotificationPriority::High);
        assert_eq!(icon, "🟡");
        assert_eq!(level, "HIGH");
    }
}
