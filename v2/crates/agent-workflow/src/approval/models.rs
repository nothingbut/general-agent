//! 审批系统数据模型

use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};

/// 审批策略类型
///
/// 定义了工作流中任务的审批方式。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ApprovalStrategy {
    /// 自动审批 - 总是通过，无需人工干预
    Auto,

    /// 手动审批 - 需要用户明确确认
    Manual {
        /// 提示消息，向用户说明审批的内容
        prompt: String,
        /// 可选项（如果为空，则默认是 Yes/No）
        options: Option<Vec<String>>,
    },

    /// 阈值审批 - 基于条件自动决策
    ///
    /// 根据上下文数据评估条件，然后选择对应的审批策略。
    Threshold {
        /// 条件表达式（如 "cost < 100", "priority == 'high'"）
        condition: String,
        /// 条件满足时的策略
        on_pass: Box<ApprovalStrategy>,
        /// 条件不满足时的策略
        on_fail: Box<ApprovalStrategy>,
    },
}

/// 审批决策
///
/// 表示审批的最终决定。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub enum ApprovalDecision {
    /// 批准 - 允许任务继续执行
    Approved,
    /// 拒绝 - 阻止任务执行
    Rejected,
    /// 修改 - 批准但带有修改参数
    Modified(String),
}

/// 审批请求
///
/// 表示一个待审批的请求，包含任务上下文和审批策略。
#[derive(Debug, Clone)]
pub struct ApprovalRequest {
    /// 请求 ID
    pub id: String,
    /// 任务 ID
    pub task_id: String,
    /// 工作流 ID
    pub workflow_id: String,
    /// 审批策略
    pub strategy: ApprovalStrategy,
    /// 上下文数据（用于条件评估）
    pub context: serde_json::Value,
    /// 创建时间
    pub created_at: DateTime<Utc>,
}

impl ApprovalRequest {
    /// 创建新的审批请求
    pub fn new(
        task_id: String,
        workflow_id: String,
        strategy: ApprovalStrategy,
        context: serde_json::Value,
    ) -> Self {
        Self {
            id: uuid::Uuid::new_v4().to_string(),
            task_id,
            workflow_id,
            strategy,
            context,
            created_at: Utc::now(),
        }
    }
}

/// 审批响应
///
/// 表示对审批请求的回应。
#[derive(Debug, Clone)]
pub struct ApprovalResponse {
    /// 对应的请求 ID
    pub request_id: String,
    /// 审批决策
    pub decision: ApprovalDecision,
    /// 决策理由（可选）
    pub reason: Option<String>,
    /// 批准时间
    pub approved_at: DateTime<Utc>,
}

impl ApprovalResponse {
    /// 创建批准响应
    pub fn approved(request_id: String, reason: Option<String>) -> Self {
        Self {
            request_id,
            decision: ApprovalDecision::Approved,
            reason,
            approved_at: Utc::now(),
        }
    }

    /// 创建拒绝响应
    pub fn rejected(request_id: String, reason: Option<String>) -> Self {
        Self {
            request_id,
            decision: ApprovalDecision::Rejected,
            reason,
            approved_at: Utc::now(),
        }
    }

    /// 创建修改响应
    pub fn modified(request_id: String, modification: String, reason: Option<String>) -> Self {
        Self {
            request_id,
            decision: ApprovalDecision::Modified(modification),
            reason,
            approved_at: Utc::now(),
        }
    }
}

/// 审批记录（用于持久化）
///
/// 保存到数据库的审批历史记录。
#[derive(Debug, Clone)]
pub struct ApprovalRecord {
    /// 记录 ID
    pub id: String,
    /// 任务 ID
    pub task_id: String,
    /// 工作流 ID
    pub workflow_id: String,
    /// 审批策略（JSON 序列化）
    pub strategy: String,
    /// 审批决策
    pub decision: String,
    /// 决策理由
    pub reason: Option<String>,
    /// 创建时间
    pub created_at: DateTime<Utc>,
    /// 批准时间
    pub approved_at: Option<DateTime<Utc>>,
}

impl ApprovalRecord {
    /// 从请求和响应创建记录
    pub fn from_request_response(
        request: &ApprovalRequest,
        response: &ApprovalResponse,
    ) -> Result<Self, serde_json::Error> {
        Ok(Self {
            id: request.id.clone(),
            task_id: request.task_id.clone(),
            workflow_id: request.workflow_id.clone(),
            strategy: serde_json::to_string(&request.strategy)?,
            decision: format!("{:?}", response.decision),
            reason: response.reason.clone(),
            created_at: request.created_at,
            approved_at: Some(response.approved_at),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_approval_request_creation() {
        let request = ApprovalRequest::new(
            "task-1".to_string(),
            "wf-1".to_string(),
            ApprovalStrategy::Auto,
            serde_json::json!({}),
        );

        assert_eq!(request.task_id, "task-1");
        assert_eq!(request.workflow_id, "wf-1");
        assert_eq!(request.strategy, ApprovalStrategy::Auto);
    }

    #[test]
    fn test_approval_response_approved() {
        let response = ApprovalResponse::approved(
            "req-1".to_string(),
            Some("LGTM".to_string()),
        );

        assert_eq!(response.request_id, "req-1");
        assert_eq!(response.decision, ApprovalDecision::Approved);
        assert_eq!(response.reason, Some("LGTM".to_string()));
    }

    #[test]
    fn test_approval_response_rejected() {
        let response = ApprovalResponse::rejected(
            "req-1".to_string(),
            Some("Not authorized".to_string()),
        );

        assert_eq!(response.decision, ApprovalDecision::Rejected);
        assert_eq!(response.reason, Some("Not authorized".to_string()));
    }

    #[test]
    fn test_approval_strategy_serialization() {
        let strategy = ApprovalStrategy::Manual {
            prompt: "Approve this?".to_string(),
            options: Some(vec!["Yes".to_string(), "No".to_string()]),
        };

        let json = serde_json::to_string(&strategy).unwrap();
        assert!(json.contains("Manual"));
        assert!(json.contains("Approve this?"));
    }

    #[test]
    fn test_threshold_strategy() {
        let strategy = ApprovalStrategy::Threshold {
            condition: "cost < 100".to_string(),
            on_pass: Box::new(ApprovalStrategy::Auto),
            on_fail: Box::new(ApprovalStrategy::Manual {
                prompt: "High cost, approve?".to_string(),
                options: None,
            }),
        };

        match &strategy {
            ApprovalStrategy::Threshold { condition, on_pass, on_fail } => {
                assert_eq!(condition, "cost < 100");
                assert_eq!(**on_pass, ApprovalStrategy::Auto);
                assert!(matches!(**on_fail, ApprovalStrategy::Manual { .. }));
            }
            _ => panic!("Expected Threshold strategy"),
        }
    }
}
