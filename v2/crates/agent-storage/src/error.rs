//! Storage 层错误类型定义

use thiserror::Error;

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Error, Debug)]
pub enum Error {
    #[error("Database error: {0}")]
    Database(#[from] sqlx::Error),

    #[error("Migration error: {0}")]
    Migration(#[from] sqlx::migrate::MigrateError),

    #[error("Serialization error: {0}")]
    Serialization(#[from] serde_json::Error),

    #[error("Not found: {0}")]
    NotFound(String),

    #[error("Core error: {0}")]
    Core(#[from] agent_core::Error),
}

// 转换为 agent_core::Error
impl From<Error> for agent_core::Error {
    fn from(err: Error) -> Self {
        match err {
            Error::Database(e) => agent_core::Error::Database(e.to_string()),
            Error::Migration(e) => agent_core::Error::Database(e.to_string()),
            Error::Serialization(e) => agent_core::Error::Serde(e),
            Error::NotFound(msg) => agent_core::Error::SessionNotFound(msg),
            Error::Core(e) => e,
        }
    }
}
