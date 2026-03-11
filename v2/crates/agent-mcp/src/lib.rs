//! Agent MCP - Model Context Protocol 实现

pub mod error;
pub mod protocol;
pub mod transport;
pub mod client;
pub mod config;

pub use error::{MCPError, Result};
pub use protocol::{JsonRpcMessage, JsonRpcError};
pub use transport::{Transport, StdioTransport};
pub use client::DefaultMCPClient;
pub use config::MCPServerConfig;
