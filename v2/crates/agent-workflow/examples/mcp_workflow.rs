//! MCP 工作流示例
//!
//! 展示如何使用 MCP 工具调用（使用 Mock 客户端）

use agent_workflow::workflow::*;
use agent_core::traits::{MCPClient, ToolDefinition};
use async_trait::async_trait;
use serde_json::{json, Value};
use std::sync::Arc;

/// Mock MCP 客户端
#[derive(Clone)]
struct MockMCPClient;

#[async_trait]
impl MCPClient for MockMCPClient {
    async fn list_tools(&self) -> agent_core::error::Result<Vec<ToolDefinition>> {
        Ok(vec![])
    }

    async fn call_tool(&self, name: &str, args: Value) -> agent_core::error::Result<Value> {
        match name {
            "read_file" => {
                let path = args.get("path").and_then(|v| v.as_str()).unwrap_or("unknown");
                Ok(json!(format!("Content of {}: Hello from MCP!", path)))
            }
            "calculate" => {
                let a = args.get("a").and_then(|v| v.as_i64()).unwrap_or(0);
                let b = args.get("b").and_then(|v| v.as_i64()).unwrap_or(0);
                Ok(json!(a + b))
            }
            _ => Err(agent_core::error::Error::External(format!("Unknown tool: {}", name))),
        }
    }
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    println!("🚀 MCP 工作流示例\n");

    // 创建 MCP 管理器并添加客户端
    let manager = MCPClientManager::new();
    manager.add_client("filesystem".to_string(), Arc::new(MockMCPClient)).await;
    manager.add_client("calculator".to_string(), Arc::new(MockMCPClient)).await;

    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建工作流
    let mut workflow = Workflow::new("mcp-demo", "MCP 演示工作流");

    // 任务 1: 读取文件
    let task1 = Task::new(
        "read",
        "Read File",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "read_file".to_string(),
            params: Some(json!({"path": "/tmp/data.txt"})),
        },
    );

    // 任务 2: 计算
    let task2 = Task::new(
        "calc",
        "Calculate",
        TaskType::MCPToolCall {
            server_name: "calculator".to_string(),
            tool_name: "calculate".to_string(),
            params: Some(json!({"a": 15, "b": 27})),
        },
    );

    workflow.add_task(task1);
    workflow.add_task(task2);

    println!("⚙️  执行工作流...\n");
    let orchestrator = WorkflowOrchestrator::new(workflow)?;
    let result = orchestrator.execute(&executor).await?;

    println!("✅ 完成！\n");
    for (id, res) in &result.task_results {
        println!("🔹 {}: {:?}", id, res.output);
    }

    Ok(())
}
