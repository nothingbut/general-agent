use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};

/// 压缩历史记录
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompressionRecord {
    pub timestamp: DateTime<Utc>,
    pub strategy_used: String,
    pub original_message_count: usize,
    pub compressed_message_count: usize,
    pub original_token_count: usize,
    pub compressed_token_count: usize,
    pub compression_ratio: f64,
}

/// 压缩配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompressionConfig {
    /// 自动压缩触发阈值（消息数）
    pub auto_trigger_threshold: usize,

    /// 滑动窗口大小
    pub sliding_window_size: usize,

    /// 语义压缩目标 token 数
    pub semantic_target_tokens: usize,

    /// 是否启用自动压缩
    pub auto_compression_enabled: bool,
}

impl Default for CompressionConfig {
    fn default() -> Self {
        Self {
            auto_trigger_threshold: 15,
            sliding_window_size: 10,
            semantic_target_tokens: 2000,
            auto_compression_enabled: true,
        }
    }
}

/// 压缩结果
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompressionResult {
    pub compressed_messages: Vec<agent_core::models::Message>,
    pub original_count: usize,
    pub compressed_count: usize,
    pub original_tokens: usize,
    pub compressed_tokens: usize,
    pub compression_ratio: f64,
    pub strategy_used: Option<String>,
}
