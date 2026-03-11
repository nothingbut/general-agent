//! MCP 客户端实现

use agent_core::traits::{MCPClient, ToolDefinition};
use async_trait::async_trait;
use serde_json::{json, Value};
use std::sync::Arc;
use tokio::sync::RwLock;
use tokio::time::timeout;

use crate::{config::MCPServerConfig, transport::{StdioTransport, Transport}, JsonRpcMessage, Result, MCPError};

/// 默认 MCP 客户端实现
pub struct DefaultMCPClient {
    transport: Arc<RwLock<StdioTransport>>,
    config: MCPServerConfig,
    tools: RwLock<Vec<ToolDefinition>>,
}

impl DefaultMCPClient {
    /// 连接到 MCP 服务器
    pub async fn connect(config: MCPServerConfig) -> Result<Self> {
        // 创建传输层
        let mut transport = StdioTransport::new(
            &config.command,
            &config.args,
            &config.env,
        )?;

        // 发送 initialize 请求
        let init_msg = JsonRpcMessage::request(
            transport.next_id(),
            "initialize".to_string(),
            json!({
                "protocolVersion": "2024-11-05",
                "clientInfo": {
                    "name": "general-agent-v2",
                    "version": "0.1.0"
                }
            })
        );

        transport.send(init_msg).await?;
        let _response = transport.recv().await?;

        // 获取工具列表
        let list_msg = JsonRpcMessage::request(
            transport.next_id(),
            "tools/list".to_string(),
            json!({}),
        );

        transport.send(list_msg).await?;
        let response = transport.recv().await?;

        let tools = Self::parse_tools(response)?;

        Ok(Self {
            transport: Arc::new(RwLock::new(transport)),
            config,
            tools: RwLock::new(tools),
        })
    }

    /// 解析工具列表
    fn parse_tools(response: JsonRpcMessage) -> Result<Vec<ToolDefinition>> {
        match response {
            JsonRpcMessage::Response { result, .. } => {
                let tools_array = result.get("tools")
                    .and_then(|t| t.as_array())
                    .ok_or_else(|| MCPError::ProtocolError("Invalid tools response".to_string()))?;

                let tools = tools_array.iter()
                    .filter_map(|t| {
                        Some(ToolDefinition {
                            name: t.get("name")?.as_str()?.to_string(),
                            description: t.get("description")?.as_str()?.to_string(),
                            input_schema: t.get("inputSchema")?.clone(),
                        })
                    })
                    .collect();

                Ok(tools)
            }
            _ => Err(MCPError::ProtocolError("Expected Response".to_string())),
        }
    }
}

#[async_trait]
impl MCPClient for DefaultMCPClient {
    async fn list_tools(&self) -> agent_core::error::Result<Vec<ToolDefinition>> {
        Ok(self.tools.read().await.clone())
    }

    async fn call_tool(&self, name: &str, args: Value) -> agent_core::error::Result<Value> {
        let mut transport = self.transport.write().await;

        let call_msg = JsonRpcMessage::request(
            transport.next_id(),
            "tools/call".to_string(),
            json!({
                "name": name,
                "arguments": args
            }),
        );

        let call_with_timeout = async {
            transport.send(call_msg).await
                .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

            let response = transport.recv().await
                .map_err(|e| agent_core::error::Error::External(e.to_string()))?;

            match response {
                JsonRpcMessage::Response { result, .. } => Ok(result),
                JsonRpcMessage::Error { error, .. } => {
                    Err(agent_core::error::Error::External(error.message))
                }
                _ => Err(agent_core::error::Error::External("Invalid response".to_string())),
            }
        };

        timeout(self.config.timeout, call_with_timeout)
            .await
            .map_err(|_| agent_core::error::Error::External("Timeout".to_string()))?
    }
}
