use thiserror::Error;

/// 压缩系统错误类型
#[derive(Error, Debug)]
pub enum CompressionError {
    #[error("Token 计数失败: {0}")]
    TokenCountingFailed(String),

    #[error("压缩策略不存在: {0}")]
    StrategyNotFound(String),

    #[error("压缩失败: {0}")]
    CompressionFailed(String),

    #[error("LLM 调用失败: {0}")]
    LlmError(String),

    #[error("无效的配置: {0}")]
    InvalidConfig(String),

    #[error("IO 错误: {0}")]
    IoError(#[from] std::io::Error),

    #[error("序列化错误: {0}")]
    SerializationError(#[from] serde_json::Error),

    #[error("其他错误: {0}")]
    Other(#[from] anyhow::Error),
}

/// Result 类型别名
pub type Result<T> = std::result::Result<T, CompressionError>;
