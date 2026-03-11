//! Agent TUI - Terminal User Interface
//!
//! 提供基于 Ratatui 的多会话对话界面

pub mod app;
pub mod state;
pub mod event;
pub mod backend;
pub mod ui;

pub use app::TuiApp;
pub use state::{AppState, FocusArea, SessionState, SessionItem, MessageItem};

/// TUI 错误类型
#[derive(Debug, thiserror::Error)]
pub enum TuiError {
    #[error("终端初始化失败: {0}")]
    TerminalSetupFailed(String),

    #[error("数据库初始化失败: {0}")]
    DatabaseInitFailed(String),

    #[error("LLM 连接失败: {0}")]
    LLMConnectionFailed(String),

    #[error("会话未找到: {0}")]
    SessionNotFound(uuid::Uuid),

    #[error("消息发送失败: {0}")]
    MessageSendFailed(String),

    #[error("IO 错误: {0}")]
    Io(#[from] std::io::Error),

    #[error("其他错误: {0}")]
    Other(#[from] anyhow::Error),
}

pub type TuiResult<T> = std::result::Result<T, TuiError>;
