//! 审批管理器
//!
//! 管理工作流中的审批请求和决策。

use super::models::*;
use super::strategies::ConditionEvaluator;
use anyhow::{Result, bail};
use std::collections::HashMap;
use tokio::sync::RwLock;
use std::sync::Arc;

/// 审批管理器
///
/// 负责处理审批请求、管理待处理审批、保存审批历史。
pub struct ApprovalManager {
    /// 待处理的审批请求
    pending: Arc<RwLock<HashMap<String, ApprovalRequest>>>,
    /// 审批历史记录
    history: Arc<RwLock<Vec<ApprovalRecord>>>,
}

impl ApprovalManager {
    /// 创建新的审批管理器
    pub fn new() -> Self {
        Self {
            pending: Arc::new(RwLock::new(HashMap::new())),
            history: Arc::new(RwLock::new(Vec::new())),
        }
    }

    /// 请求审批
    ///
    /// 创建一个新的审批请求并添加到待处理列表。
    ///
    /// # 参数
    ///
    /// - `task_id` - 任务 ID
    /// - `workflow_id` - 工作流 ID
    /// - `strategy` - 审批策略
    /// - `context` - 上下文数据（用于条件评估）
    ///
    /// # 返回
    ///
    /// 创建的审批请求
    pub async fn request_approval(
        &self,
        task_id: String,
        workflow_id: String,
        strategy: ApprovalStrategy,
        context: serde_json::Value,
    ) -> Result<ApprovalRequest> {
        let request = ApprovalRequest::new(task_id, workflow_id, strategy, context);

        let mut pending = self.pending.write().await;
        pending.insert(request.id.clone(), request.clone());

        Ok(request)
    }

    /// 处理审批请求
    ///
    /// 根据审批策略自动处理或标记为需要手动审批。
    ///
    /// # 参数
    ///
    /// - `request` - 审批请求
    ///
    /// # 返回
    ///
    /// 审批响应
    ///
    /// # 错误
    ///
    /// - 如果是手动审批策略，返回错误提示需要用户输入
    /// - 如果条件评估失败，返回错误
    pub async fn process_approval(&self, request: &ApprovalRequest) -> Result<ApprovalResponse> {
        match &request.strategy {
            ApprovalStrategy::Auto => {
                // 自动批准
                let response = ApprovalResponse::approved(
                    request.id.clone(),
                    Some("Auto-approved".to_string()),
                );

                // 保存到历史
                self.save_to_history(request, &response).await?;

                Ok(response)
            }
            ApprovalStrategy::Manual { prompt, options } => {
                // 手动审批 - 需要外部输入
                // 请求将保留在 pending 中，等待 submit_decision()
                let options_str = if let Some(opts) = options {
                    format!(" Options: {:?}", opts)
                } else {
                    String::new()
                };
                bail!("Manual approval required: {}{}", prompt, options_str)
            }
            ApprovalStrategy::Threshold { condition, on_pass, on_fail } => {
                // 评估条件
                let passed = ConditionEvaluator::evaluate(condition, &request.context)?;
                let next_strategy = if passed { on_pass } else { on_fail };

                // 递归处理下一个策略 - 使用 Box::pin 避免无限大小的 future
                let next_request = ApprovalRequest {
                    strategy: (**next_strategy).clone(),
                    ..request.clone()
                };
                Box::pin(self.process_approval(&next_request)).await
            }
        }
    }

    /// 提交手动审批决策
    ///
    /// 用于提交用户的审批决策。
    ///
    /// # 参数
    ///
    /// - `request_id` - 请求 ID
    /// - `decision` - 审批决策
    /// - `reason` - 决策理由（可选）
    ///
    /// # 返回
    ///
    /// 审批响应
    ///
    /// # 错误
    ///
    /// - 如果请求不存在，返回错误
    pub async fn submit_decision(
        &self,
        request_id: &str,
        decision: ApprovalDecision,
        reason: Option<String>,
    ) -> Result<ApprovalResponse> {
        // 从待处理中移除
        let mut pending = self.pending.write().await;
        let request = pending
            .remove(request_id)
            .ok_or_else(|| anyhow::anyhow!("Approval request not found: {}", request_id))?;

        // 创建响应
        let response = ApprovalResponse {
            request_id: request_id.to_string(),
            decision,
            reason,
            approved_at: chrono::Utc::now(),
        };

        // 保存到历史（需要释放 pending 锁）
        drop(pending);
        self.save_to_history(&request, &response).await?;

        Ok(response)
    }

    /// 获取待处理的审批请求
    ///
    /// 返回所有待处理的审批请求列表。
    pub async fn get_pending(&self) -> Vec<ApprovalRequest> {
        let pending = self.pending.read().await;
        pending.values().cloned().collect()
    }

    /// 获取审批历史
    ///
    /// 返回指定工作流的审批历史记录。
    ///
    /// # 参数
    ///
    /// - `workflow_id` - 工作流 ID
    pub async fn get_history(&self, workflow_id: &str) -> Vec<ApprovalRecord> {
        let history = self.history.read().await;
        history
            .iter()
            .filter(|r| r.workflow_id == workflow_id)
            .cloned()
            .collect()
    }

    /// 获取所有历史记录
    pub async fn get_all_history(&self) -> Vec<ApprovalRecord> {
        let history = self.history.read().await;
        history.clone()
    }

    /// 清空待处理请求（用于测试）
    pub async fn clear_pending(&self) {
        let mut pending = self.pending.write().await;
        pending.clear();
    }

    /// 保存到历史记录
    async fn save_to_history(&self, request: &ApprovalRequest, response: &ApprovalResponse) -> Result<()> {
        let record = ApprovalRecord::from_request_response(request, response)?;
        let mut history = self.history.write().await;
        history.push(record);
        Ok(())
    }
}

impl Default for ApprovalManager {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_auto_approval() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Auto,
                serde_json::json!({}),
            )
            .await
            .unwrap();

        let response = manager.process_approval(&request).await.unwrap();
        assert_eq!(response.decision, ApprovalDecision::Approved);
        assert_eq!(response.reason, Some("Auto-approved".to_string()));
    }

    #[tokio::test]
    async fn test_manual_approval_required() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Manual {
                    prompt: "Approve this task?".to_string(),
                    options: None,
                },
                serde_json::json!({}),
            )
            .await
            .unwrap();

        // 应该失败，需要手动输入
        let result = manager.process_approval(&request).await;
        assert!(result.is_err());
        let err = result.unwrap_err();
        assert!(err.to_string().contains("Manual approval required"));
    }

    #[tokio::test]
    async fn test_submit_manual_decision() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Manual {
                    prompt: "Approve?".to_string(),
                    options: None,
                },
                serde_json::json!({}),
            )
            .await
            .unwrap();

        // 提交批准决策
        let response = manager
            .submit_decision(
                &request.id,
                ApprovalDecision::Approved,
                Some("LGTM".to_string()),
            )
            .await
            .unwrap();

        assert_eq!(response.decision, ApprovalDecision::Approved);
        assert_eq!(response.reason, Some("LGTM".to_string()));

        // 验证已从待处理中移除
        let pending = manager.get_pending().await;
        assert!(pending.is_empty());

        // 验证已添加到历史
        let history = manager.get_history("wf-1").await;
        assert_eq!(history.len(), 1);
    }

    #[tokio::test]
    async fn test_submit_rejection() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Manual {
                    prompt: "Approve?".to_string(),
                    options: None,
                },
                serde_json::json!({}),
            )
            .await
            .unwrap();

        // 提交拒绝决策
        let response = manager
            .submit_decision(
                &request.id,
                ApprovalDecision::Rejected,
                Some("Not authorized".to_string()),
            )
            .await
            .unwrap();

        assert_eq!(response.decision, ApprovalDecision::Rejected);
    }

    #[tokio::test]
    async fn test_threshold_approval_pass() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Threshold {
                    condition: "cost < 100".to_string(),
                    on_pass: Box::new(ApprovalStrategy::Auto),
                    on_fail: Box::new(ApprovalStrategy::Manual {
                        prompt: "High cost, approve?".to_string(),
                        options: None,
                    }),
                },
                serde_json::json!({"cost": 50}),
            )
            .await
            .unwrap();

        // 条件满足，应该自动批准
        let response = manager.process_approval(&request).await.unwrap();
        assert_eq!(response.decision, ApprovalDecision::Approved);
    }

    #[tokio::test]
    async fn test_threshold_approval_fail() {
        let manager = ApprovalManager::new();

        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Threshold {
                    condition: "cost < 100".to_string(),
                    on_pass: Box::new(ApprovalStrategy::Auto),
                    on_fail: Box::new(ApprovalStrategy::Manual {
                        prompt: "High cost, approve?".to_string(),
                        options: None,
                    }),
                },
                serde_json::json!({"cost": 150}),
            )
            .await
            .unwrap();

        // 条件不满足，应该需要手动审批
        let result = manager.process_approval(&request).await;
        assert!(result.is_err());
        let err = result.unwrap_err();
        assert!(err.to_string().contains("High cost"));
    }

    #[tokio::test]
    async fn test_get_pending() {
        let manager = ApprovalManager::new();

        // 创建几个待处理请求
        manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Manual {
                    prompt: "Approve 1?".to_string(),
                    options: None,
                },
                serde_json::json!({}),
            )
            .await
            .unwrap();

        manager
            .request_approval(
                "task-2".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Manual {
                    prompt: "Approve 2?".to_string(),
                    options: None,
                },
                serde_json::json!({}),
            )
            .await
            .unwrap();

        let pending = manager.get_pending().await;
        assert_eq!(pending.len(), 2);
    }

    #[tokio::test]
    async fn test_get_history() {
        let manager = ApprovalManager::new();

        // 创建并批准一个请求
        let request = manager
            .request_approval(
                "task-1".to_string(),
                "wf-1".to_string(),
                ApprovalStrategy::Auto,
                serde_json::json!({}),
            )
            .await
            .unwrap();

        manager.process_approval(&request).await.unwrap();

        // 验证历史
        let history = manager.get_history("wf-1").await;
        assert_eq!(history.len(), 1);
        assert_eq!(history[0].task_id, "task-1");
    }
}
