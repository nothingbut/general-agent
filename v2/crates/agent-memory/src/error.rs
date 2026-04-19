use thiserror::Error;

#[derive(Error, Debug)]
pub enum MemoryError {
    #[error("数据库错误: {0}")]
    Database(String),

    #[error("记忆不存在: {0}")]
    NotFound(String),

    #[error("无效的记忆类型: {0}")]
    InvalidType(String),

    #[error("序列化错误: {0}")]
    Serialization(#[from] serde_json::Error),

    #[error("其他错误: {0}")]
    Other(#[from] anyhow::Error),
}

impl From<sqlx::Error> for MemoryError {
    fn from(e: sqlx::Error) -> Self {
        MemoryError::Database(e.to_string())
    }
}

impl From<sqlx::migrate::MigrateError> for MemoryError {
    fn from(e: sqlx::migrate::MigrateError) -> Self {
        MemoryError::Database(e.to_string())
    }
}

pub type Result<T> = std::result::Result<T, MemoryError>;
