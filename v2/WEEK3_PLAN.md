# Week 3 执行计划：审批和通知系统

**日期**: 2026-03-13 - 2026-03-20
**目标**: 实现审批管理和通知系统
**状态**: 准备开始
**前置条件**: ✅ Week 2 完成（Orchestrator + Executor + 控制功能）

---

## 🎯 本周目标

完成 Workflow 系统的审批和通知功能：
1. 审批管理器（ApprovalManager）
2. 审批策略（Manual/Auto/Threshold）
3. 通知系统（Notification channels）
4. 集成测试
5. 文档和示例

**验收标准**: 能够在工作流中配置审批策略，并通过多渠道发送通知

---

## 📅 Day-by-Day 计划

### Day 1: 审批系统核心模型（3-4 小时）

#### Task 1.1: 创建目录结构
```bash
cd v2/crates/agent-workflow/src
mkdir -p approval notification
touch approval/mod.rs approval/models.rs approval/manager.rs approval/strategies.rs
touch notification/mod.rs notification/models.rs notification/channels.rs notification/router.rs
```

#### Task 1.2: 定义审批模型
创建 `approval/models.rs`:
```rust
use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};

/// 审批策略类型
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ApprovalStrategy {
    /// 自动审批 - 总是通过
    Auto,
    /// 手动审批 - 需要用户确认
    Manual {
        /// 提示消息
        prompt: String,
        /// 可选项（如果为空，则是 Yes/No）
        options: Option<Vec<String>>,
    },
    /// 阈值审批 - 基于条件自动决策
    Threshold {
        /// 条件表达式（如 "cost < 100"）
        condition: String,
        /// 条件满足时的策略
        on_pass: Box<ApprovalStrategy>,
        /// 条件不满足时的策略
        on_fail: Box<ApprovalStrategy>,
    },
}

/// 审批决策
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ApprovalDecision {
    /// 批准
    Approved,
    /// 拒绝
    Rejected,
    /// 修改（带参数）
    Modified(String),
}

/// 审批请求
#[derive(Debug, Clone)]
pub struct ApprovalRequest {
    pub id: String,
    pub task_id: String,
    pub workflow_id: String,
    pub strategy: ApprovalStrategy,
    pub context: serde_json::Value,
    pub created_at: DateTime<Utc>,
}

/// 审批响应
#[derive(Debug, Clone)]
pub struct ApprovalResponse {
    pub request_id: String,
    pub decision: ApprovalDecision,
    pub reason: Option<String>,
    pub approved_at: DateTime<Utc>,
}

/// 审批记录（用于持久化）
#[derive(Debug, Clone)]
pub struct ApprovalRecord {
    pub id: String,
    pub task_id: String,
    pub workflow_id: String,
    pub strategy: String, // JSON
    pub decision: String,
    pub reason: Option<String>,
    pub created_at: DateTime<Utc>,
    pub approved_at: Option<DateTime<Utc>>,
}
```

**验收**: `cargo check` 通过

---

### Day 2: 审批管理器实现（4-5 小时）

#### Task 2.1: 实现 ApprovalManager
创建 `approval/manager.rs`:
```rust
use super::models::*;
use anyhow::Result;
use std::collections::HashMap;
use tokio::sync::RwLock;
use std::sync::Arc;

pub struct ApprovalManager {
    /// 待处理的审批请求
    pending: Arc<RwLock<HashMap<String, ApprovalRequest>>>,
    /// 审批历史
    history: Arc<RwLock<Vec<ApprovalRecord>>>,
}

impl ApprovalManager {
    pub fn new() -> Self {
        Self {
            pending: Arc::new(RwLock::new(HashMap::new())),
            history: Arc::new(RwLock::new(Vec::new())),
        }
    }

    /// 请求审批
    pub async fn request_approval(
        &self,
        task_id: String,
        workflow_id: String,
        strategy: ApprovalStrategy,
        context: serde_json::Value,
    ) -> Result<ApprovalRequest> {
        let request = ApprovalRequest {
            id: uuid::Uuid::new_v4().to_string(),
            task_id,
            workflow_id,
            strategy,
            context,
            created_at: chrono::Utc::now(),
        };

        let mut pending = self.pending.write().await;
        pending.insert(request.id.clone(), request.clone());

        Ok(request)
    }

    /// 处理审批（根据策略自动或手动）
    pub async fn process_approval(
        &self,
        request: &ApprovalRequest,
    ) -> Result<ApprovalResponse> {
        match &request.strategy {
            ApprovalStrategy::Auto => {
                // 自动批准
                Ok(ApprovalResponse {
                    request_id: request.id.clone(),
                    decision: ApprovalDecision::Approved,
                    reason: Some("Auto-approved".to_string()),
                    approved_at: chrono::Utc::now(),
                })
            }
            ApprovalStrategy::Manual { prompt, options } => {
                // 手动审批 - 需要外部输入
                // 这里只是标记为待处理，实际决策由调用者提供
                Err(anyhow::anyhow!("Manual approval required: {}", prompt))
            }
            ApprovalStrategy::Threshold { condition, on_pass, on_fail } => {
                // 评估条件
                let passed = self.evaluate_condition(condition, &request.context)?;
                let next_strategy = if passed { on_pass } else { on_fail };

                // 递归处理
                let next_request = ApprovalRequest {
                    strategy: (**next_strategy).clone(),
                    ..request.clone()
                };
                self.process_approval(&next_request).await
            }
        }
    }

    /// 提交手动审批决策
    pub async fn submit_decision(
        &self,
        request_id: &str,
        decision: ApprovalDecision,
        reason: Option<String>,
    ) -> Result<ApprovalResponse> {
        // 从待处理中移除
        let mut pending = self.pending.write().await;
        let request = pending.remove(request_id)
            .ok_or_else(|| anyhow::anyhow!("Request not found: {}", request_id))?;

        let response = ApprovalResponse {
            request_id: request_id.to_string(),
            decision: decision.clone(),
            reason: reason.clone(),
            approved_at: chrono::Utc::now(),
        };

        // 保存到历史
        let record = ApprovalRecord {
            id: request.id,
            task_id: request.task_id,
            workflow_id: request.workflow_id,
            strategy: serde_json::to_string(&request.strategy)?,
            decision: format!("{:?}", decision),
            reason,
            created_at: request.created_at,
            approved_at: Some(response.approved_at),
        };

        let mut history = self.history.write().await;
        history.push(record);

        Ok(response)
    }

    /// 评估条件表达式（简化版）
    fn evaluate_condition(&self, condition: &str, context: &serde_json::Value) -> Result<bool> {
        // 简化实现：支持简单的比较
        // 例如: "cost < 100"
        // TODO: 使用完整的表达式引擎（如 rhai）
        Ok(true) // 暂时总是返回 true
    }

    /// 获取待处理的审批
    pub async fn get_pending(&self) -> Vec<ApprovalRequest> {
        let pending = self.pending.read().await;
        pending.values().cloned().collect()
    }

    /// 获取审批历史
    pub async fn get_history(&self, workflow_id: &str) -> Vec<ApprovalRecord> {
        let history = self.history.read().await;
        history.iter()
            .filter(|r| r.workflow_id == workflow_id)
            .cloned()
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_auto_approval() {
        let manager = ApprovalManager::new();

        let request = manager.request_approval(
            "task-1".to_string(),
            "wf-1".to_string(),
            ApprovalStrategy::Auto,
            serde_json::json!({}),
        ).await.unwrap();

        let response = manager.process_approval(&request).await.unwrap();
        assert_eq!(response.decision, ApprovalDecision::Approved);
    }

    #[tokio::test]
    async fn test_manual_approval() {
        let manager = ApprovalManager::new();

        let request = manager.request_approval(
            "task-1".to_string(),
            "wf-1".to_string(),
            ApprovalStrategy::Manual {
                prompt: "Approve this?".to_string(),
                options: None,
            },
            serde_json::json!({}),
        ).await.unwrap();

        // 应该失败，需要手动输入
        assert!(manager.process_approval(&request).await.is_err());

        // 提交决策
        let response = manager.submit_decision(
            &request.id,
            ApprovalDecision::Approved,
            Some("LGTM".to_string()),
        ).await.unwrap();

        assert_eq!(response.decision, ApprovalDecision::Approved);
    }
}
```

**验收**: `cargo test -p agent-workflow approval` 通过

---

### Day 3: 通知系统实现（4-5 小时）

#### Task 3.1: 定义通知模型
创建 `notification/models.rs`:
```rust
use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};

/// 通知级别
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum NotificationLevel {
    Info,
    Success,
    Warning,
    Error,
}

/// 通知渠道
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum NotificationChannel {
    /// 终端输出
    Terminal,
    /// 桌面通知（系统托盘）
    Desktop,
    /// 日志文件
    Log { path: String },
    /// Webhook
    Webhook { url: String },
}

/// 通知消息
#[derive(Debug, Clone)]
pub struct Notification {
    pub id: String,
    pub level: NotificationLevel,
    pub title: String,
    pub message: String,
    pub channels: Vec<NotificationChannel>,
    pub metadata: serde_json::Value,
    pub created_at: DateTime<Utc>,
}

impl Notification {
    pub fn new(level: NotificationLevel, title: impl Into<String>, message: impl Into<String>) -> Self {
        Self {
            id: uuid::Uuid::new_v4().to_string(),
            level,
            title: title.into(),
            message: message.into(),
            channels: vec![NotificationChannel::Terminal], // 默认终端
            metadata: serde_json::json!({}),
            created_at: chrono::Utc::now(),
        }
    }

    pub fn with_channels(mut self, channels: Vec<NotificationChannel>) -> Self {
        self.channels = channels;
        self
    }
}
```

#### Task 3.2: 实现通知渠道
创建 `notification/channels.rs`:
```rust
use super::models::*;
use anyhow::Result;

/// 终端通知
pub struct TerminalChannel;

impl TerminalChannel {
    pub async fn send(&self, notification: &Notification) -> Result<()> {
        let prefix = match notification.level {
            NotificationLevel::Info => "ℹ️",
            NotificationLevel::Success => "✅",
            NotificationLevel::Warning => "⚠️",
            NotificationLevel::Error => "❌",
        };

        println!("{} {} - {}", prefix, notification.title, notification.message);
        Ok(())
    }
}

/// 桌面通知
pub struct DesktopChannel;

impl DesktopChannel {
    pub async fn send(&self, notification: &Notification) -> Result<()> {
        // 使用 notify-rust crate 发送系统通知
        // 简化实现：打印到终端
        println!("[Desktop] {} - {}", notification.title, notification.message);
        Ok(())
    }
}

/// 日志通知
pub struct LogChannel {
    path: String,
}

impl LogChannel {
    pub fn new(path: String) -> Self {
        Self { path }
    }

    pub async fn send(&self, notification: &Notification) -> Result<()> {
        use std::fs::OpenOptions;
        use std::io::Write;

        let mut file = OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.path)?;

        writeln!(
            file,
            "[{}] {:?} - {} - {}",
            notification.created_at.format("%Y-%m-%d %H:%M:%S"),
            notification.level,
            notification.title,
            notification.message
        )?;

        Ok(())
    }
}
```

#### Task 3.3: 实现通知路由器
创建 `notification/router.rs`:
```rust
use super::models::*;
use super::channels::*;
use anyhow::Result;

pub struct NotificationRouter {
    terminal: TerminalChannel,
    desktop: DesktopChannel,
}

impl NotificationRouter {
    pub fn new() -> Self {
        Self {
            terminal: TerminalChannel,
            desktop: DesktopChannel,
        }
    }

    /// 发送通知到指定渠道
    pub async fn send(&self, notification: &Notification) -> Result<()> {
        for channel in &notification.channels {
            match channel {
                NotificationChannel::Terminal => {
                    self.terminal.send(notification).await?;
                }
                NotificationChannel::Desktop => {
                    self.desktop.send(notification).await?;
                }
                NotificationChannel::Log { path } => {
                    let log_channel = LogChannel::new(path.clone());
                    log_channel.send(notification).await?;
                }
                NotificationChannel::Webhook { url } => {
                    // TODO: 实现 Webhook
                    println!("[Webhook] Would send to: {}", url);
                }
            }
        }
        Ok(())
    }

    /// 批量发送
    pub async fn send_batch(&self, notifications: Vec<Notification>) -> Result<()> {
        for notification in notifications {
            self.send(&notification).await?;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_send_notification() {
        let router = NotificationRouter::new();

        let notification = Notification::new(
            NotificationLevel::Success,
            "Task Completed",
            "Task A finished successfully"
        );

        router.send(&notification).await.unwrap();
    }

    #[tokio::test]
    async fn test_send_to_multiple_channels() {
        let router = NotificationRouter::new();

        let notification = Notification::new(
            NotificationLevel::Info,
            "Test",
            "Message"
        ).with_channels(vec![
            NotificationChannel::Terminal,
            NotificationChannel::Desktop,
        ]);

        router.send(&notification).await.unwrap();
    }
}
```

**验收**: `cargo test -p agent-workflow notification` 通过

---

### Day 4: 集成到 Orchestrator（3-4 小时）

#### Task 4.1: 扩展 WorkflowOrchestrator
在 `orchestrator.rs` 中添加审批和通知支持:
```rust
use super::approval::{ApprovalManager, ApprovalStrategy};
use super::notification::{NotificationRouter, Notification, NotificationLevel};

pub struct WorkflowOrchestrator {
    // ... 现有字段
    approval_manager: Option<Arc<ApprovalManager>>,
    notification_router: Option<Arc<NotificationRouter>>,
}

impl WorkflowOrchestrator {
    pub fn with_approval(mut self, manager: Arc<ApprovalManager>) -> Self {
        self.approval_manager = Some(manager);
        self
    }

    pub fn with_notification(mut self, router: Arc<NotificationRouter>) -> Self {
        self.notification_router = Some(router);
        self
    }

    /// 在任务执行前请求审批
    async fn request_task_approval(&self, task: &Task) -> Result<()> {
        if let Some(ref manager) = self.approval_manager {
            // 检查任务是否需要审批
            if let Some(strategy) = self.get_approval_strategy(task) {
                let request = manager.request_approval(
                    task.id.clone(),
                    self.workflow_id.clone(),
                    strategy,
                    serde_json::json!({ "task": task }),
                ).await?;

                let response = manager.process_approval(&request).await?;

                if response.decision != ApprovalDecision::Approved {
                    bail!("Task approval rejected: {:?}", response.reason);
                }
            }
        }
        Ok(())
    }

    /// 发送任务完成通知
    async fn notify_task_completed(&self, task_id: &str, result: &TaskResult) {
        if let Some(ref router) = self.notification_router {
            let level = match result.status {
                TaskStatus::Completed => NotificationLevel::Success,
                TaskStatus::Failed(_) => NotificationLevel::Error,
                _ => NotificationLevel::Info,
            };

            let notification = Notification::new(
                level,
                format!("Task {} completed", task_id),
                result.output.clone().unwrap_or_default()
            );

            let _ = router.send(&notification).await;
        }
    }
}
```

**验收**: 集成测试通过

---

### Day 5: 测试和文档（3-4 小时）

#### Task 5.1: 集成测试
创建 `tests/approval_notification_test.rs`:
```rust
use agent_workflow::workflow::*;
use agent_workflow::approval::*;
use agent_workflow::notification::*;
use std::sync::Arc;

#[tokio::test]
async fn test_workflow_with_approval() {
    let mut workflow = Workflow::new("test-wf", "Test Workflow");
    workflow.add_task(Task::new("A", "Task A", TaskType::Custom("test".to_string())));

    let approval_manager = Arc::new(ApprovalManager::new());
    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_approval(approval_manager.clone());

    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
}

#[tokio::test]
async fn test_workflow_with_notification() {
    let mut workflow = Workflow::new("test-wf", "Test Workflow");
    workflow.add_task(Task::new("A", "Task A", TaskType::Custom("test".to_string())));

    let notification_router = Arc::new(NotificationRouter::new());
    let orchestrator = WorkflowOrchestrator::new(workflow)
        .unwrap()
        .with_notification(notification_router);

    let executor = TaskExecutor::new();
    let result = orchestrator.execute(&executor).await;

    assert!(result.is_ok());
}
```

#### Task 5.2: 文档
创建 `docs/approval_notification.md`:
- 审批系统使用指南
- 通知系统配置
- 示例代码

**验收**:
- 所有测试通过
- 文档清晰完整

---

## 📊 Week 3 验收标准

### 功能验收
- [ ] ApprovalManager 支持 Auto/Manual/Threshold 策略
- [ ] 通知系统支持 Terminal/Desktop/Log 渠道
- [ ] WorkflowOrchestrator 集成审批和通知
- [ ] 手动审批可以提交决策
- [ ] 阈值审批可以条件判断
- [ ] 多渠道通知正常工作

### 代码质量
- [ ] 所有单元测试通过
- [ ] 至少 2 个集成测试通过
- [ ] `cargo clippy` 无警告
- [ ] `cargo fmt` 格式正确

### 文档
- [ ] 审批系统使用文档
- [ ] 通知系统配置文档
- [ ] 有完整示例

---

## 🚧 已知限制（本周）

1. **条件表达式引擎简化**
   - 只支持简单的条件判断
   - 下周可以集成 `rhai` 或 `evalexpr` crate

2. **桌面通知未实现**
   - 需要添加 `notify-rust` 依赖
   - 跨平台支持需要测试

3. **审批 UI 未集成**
   - 暂时只有 API
   - 下周集成到 agent-tui

4. **Webhook 通知未实现**
   - 需要 HTTP 客户端
   - 下周添加 `reqwest` 支持

---

## 📝 Week 4 预览

下周将完成性能监控和 Subagent 系统：
- 性能监控框架
- 指标收集和存储
- 实时监控面板
- Subagent 编排器
- 并行任务执行

---

**创建日期**: 2026-03-13
**执行周期**: Week 3
**预计完成**: 2026-03-20
