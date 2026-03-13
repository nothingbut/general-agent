//! 审批系统模块
//!
//! 提供工作流审批功能，支持自动、手动和条件审批。

pub mod models;
pub mod manager;
pub mod strategies;

// 重新导出核心类型
pub use models::{
    ApprovalStrategy, ApprovalDecision, ApprovalRequest, ApprovalResponse, ApprovalRecord,
};
pub use manager::ApprovalManager;
