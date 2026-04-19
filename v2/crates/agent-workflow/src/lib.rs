//! Workflow 层实现
//!
//! 提供会话管理、对话流程等高层功能

pub mod command_parser;
pub mod conversation_flow;
pub mod session_manager;
pub mod subagent;
pub mod runtime;
pub mod workflow;
pub mod approval;
pub mod notification;

pub use command_parser::{parse_subagent_command, SubagentCommand};
pub use conversation_flow::{ConversationConfig, ConversationFlow, StreamContext};
pub use session_manager::SessionManager;
pub use runtime::AgentRuntime;

#[cfg(feature = "compression")]
pub use agent_context_compression;

#[cfg(feature = "file-storage")]
pub use agent_file_storage;

#[cfg(feature = "memory")]
pub use agent_memory;

#[cfg(feature = "multi-agent")]
pub mod multi_agent_service;
#[cfg(feature = "multi-agent")]
pub use agent_multi_agent;
#[cfg(feature = "multi-agent")]
pub use multi_agent_service::MultiAgentService;
