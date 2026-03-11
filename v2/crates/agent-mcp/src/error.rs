//! MCP 错误类型定义

use std::time::Duration;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum MCPError {
    #[error("MCP server connection failed: {0}")]
    ConnectionFailed(String),

    #[error("Tool not found: {0}")]
    ToolNotFound(String),

    #[error("Tool call failed: {0}")]
    ToolCallFailed(String),

    #[error("Protocol error: {0}")]
    ProtocolError(String),

    #[error("Timeout after {0:?}")]
    Timeout(Duration),

    #[error("Permission denied: {0}")]
    PermissionDenied(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

pub type Result<T> = std::result::Result<T, MCPError>;
