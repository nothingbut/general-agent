use agent_core::models::Message;
use async_trait::async_trait;

use crate::Result;

pub mod sliding_window;
pub mod semantic;
pub mod hierarchical;

pub use sliding_window::SlidingWindowStrategy;
pub use semantic::SemanticStrategy;
pub use hierarchical::{HierarchicalStrategy, HierarchicalConfig};

/// 压缩策略 trait
#[async_trait]
pub trait CompressionStrategy: Send + Sync {
    /// 压缩消息列表
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>>;

    /// 策略名称
    fn name(&self) -> &str;

    /// 估算压缩后的 token 数
    fn estimate_tokens(&self, messages: &[Message]) -> usize;
}

/// 策略类型枚举
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum StrategyType {
    SlidingWindow,
    Semantic,
    Hierarchical,
}

impl StrategyType {
    pub fn as_str(&self) -> &str {
        match self {
            StrategyType::SlidingWindow => "sliding_window",
            StrategyType::Semantic => "semantic",
            StrategyType::Hierarchical => "hierarchical",
        }
    }
}
