//! MCP (Model Context Protocol) trait 定义

use async_trait::async_trait;
use serde_json::Value;
use crate::error::Result;

/// MCP 工具定义
#[derive(Debug, Clone)]
pub struct ToolDefinition {
    pub name: String,
    pub description: String,
    pub input_schema: Value,
}

/// MCP 客户端抽象
#[async_trait]
pub trait MCPClient: Send + Sync {
    /// 列出所有可用工具
    async fn list_tools(&self) -> Result<Vec<ToolDefinition>>;

    /// 调用指定工具
    async fn call_tool(&self, name: &str, args: Value) -> Result<Value>;
}
