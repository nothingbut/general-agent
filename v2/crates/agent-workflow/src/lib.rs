//! Workflow 层实现
//!
//! 提供会话管理、对话流程等高层功能

pub mod conversation_flow;
pub mod session_manager;

pub use conversation_flow::{ConversationConfig, ConversationFlow, StreamContext};
pub use session_manager::SessionManager;
