//! 核心 trait 定义

pub mod llm;
pub mod repository;
pub mod mcp;
pub mod rag;

pub use llm::LLMClient;
pub use repository::{MessageRepository, SessionRepository};
pub use mcp::{MCPClient, ToolDefinition};
pub use rag::{RAGRetriever, Document};
