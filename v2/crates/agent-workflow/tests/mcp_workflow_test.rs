//! MCP 工作流集成测试
//!
//! 本测试展示如何在工作流中集成 MCP 工具调用

use agent_workflow::workflow::*;
use agent_core::traits::{MCPClient, ToolDefinition};
use async_trait::async_trait;
use serde_json::{json, Value};
use std::sync::Arc;

/// Mock MCP 客户端用于测试
#[derive(Clone)]
struct MockMCPClient {
    server_name: String,
}

impl MockMCPClient {
    fn new(server_name: String) -> Self {
        Self { server_name }
    }
}

#[async_trait]
impl MCPClient for MockMCPClient {
    async fn list_tools(&self) -> agent_core::error::Result<Vec<ToolDefinition>> {
        Ok(vec![
            ToolDefinition {
                name: "read_file".to_string(),
                description: "Read a file".to_string(),
                input_schema: json!({"type": "object"}),
            },
            ToolDefinition {
                name: "write_file".to_string(),
                description: "Write to a file".to_string(),
                input_schema: json!({"type": "object"}),
            },
        ])
    }

    async fn call_tool(&self, name: &str, args: Value) -> agent_core::error::Result<Value> {
        // 模拟工具调用
        match name {
            "read_file" => {
                let path = args.get("path").and_then(|v| v.as_str()).unwrap_or("unknown");
                Ok(json!({
                    "content": format!("File content from {}: Hello World", path)
                }))
            }
            "write_file" => {
                let path = args.get("path").and_then(|v| v.as_str()).unwrap_or("unknown");
                let content = args.get("content").and_then(|v| v.as_str()).unwrap_or("");
                Ok(json!({
                    "success": true,
                    "message": format!("Wrote {} bytes to {}", content.len(), path)
                }))
            }
            "echo" => {
                let message = args.get("message").and_then(|v| v.as_str()).unwrap_or("");
                Ok(json!({"result": message}))
            }
            "add" => {
                let a = args.get("a").and_then(|v| v.as_i64()).unwrap_or(0);
                let b = args.get("b").and_then(|v| v.as_i64()).unwrap_or(0);
                Ok(json!({"result": a + b}))
            }
            _ => Err(agent_core::error::Error::External(format!(
                "Unknown tool: {}",
                name
            ))),
        }
    }
}

/// 创建测试用的 MCP 管理器
async fn create_test_mcp_manager() -> MCPClientManager {
    let manager = MCPClientManager::new();

    // 添加 filesystem 服务器
    manager
        .add_client(
            "filesystem".to_string(),
            Arc::new(MockMCPClient::new("filesystem".to_string())),
        )
        .await;

    // 添加 calculator 服务器
    manager
        .add_client(
            "calculator".to_string(),
            Arc::new(MockMCPClient::new("calculator".to_string())),
        )
        .await;

    // 添加 utilities 服务器
    manager
        .add_client(
            "utilities".to_string(),
            Arc::new(MockMCPClient::new("utilities".to_string())),
        )
        .await;

    manager
}

#[tokio::test]
async fn test_simple_mcp_workflow() {
    let manager = create_test_mcp_manager().await;
    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建工作流
    let mut workflow = Workflow::new("mcp-workflow", "MCP Workflow Example");

    // 任务 1: 读取文件
    let task1 = Task::new(
        "read",
        "Read File",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "read_file".to_string(),
            params: Some(json!({
                "path": "/tmp/test.txt"
            })),
        },
    );

    workflow.add_task(task1);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证结果
    assert_eq!(result.task_results.len(), 1);
    let task_result = &result.task_results["read"];
    assert_eq!(task_result.status, TaskStatus::Completed);
    assert!(task_result.output.is_some());
    let output = task_result.output.as_ref().unwrap();
    assert!(output.contains("Hello World"));
}

#[tokio::test]
async fn test_multi_mcp_workflow() {
    let manager = create_test_mcp_manager().await;
    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建工作流
    let mut workflow = Workflow::new("multi-mcp", "Multi-MCP Workflow");

    // 任务 1: 读取文件
    let task1 = Task::new(
        "read",
        "Read File",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "read_file".to_string(),
            params: Some(json!({
                "path": "/tmp/input.txt"
            })),
        },
    );

    // 任务 2: 计算（并行于任务1）
    let task2 = Task::new(
        "calculate",
        "Calculate Sum",
        TaskType::MCPToolCall {
            server_name: "calculator".to_string(),
            tool_name: "add".to_string(),
            params: Some(json!({
                "a": 10,
                "b": 20
            })),
        },
    );

    // 任务 3: 写入文件（依赖任务1和2）
    let task3 = Task::new(
        "write",
        "Write File",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "write_file".to_string(),
            params: Some(json!({
                "path": "/tmp/output.txt",
                "content": "Results processed"
            })),
        },
    )
    .with_dependency("read")
    .with_dependency("calculate");

    workflow.add_task(task1);
    workflow.add_task(task2);
    workflow.add_task(task3);

    // 执行工作流
    let orchestrator = WorkflowOrchestrator::new(workflow).unwrap();
    let result = orchestrator.execute(&executor).await.unwrap();

    // 验证结果
    assert_eq!(result.task_results.len(), 3);

    // 任务1 - 读取文件
    let read_result = &result.task_results["read"];
    assert_eq!(read_result.status, TaskStatus::Completed);

    // 任务2 - 计算
    let calc_result = &result.task_results["calculate"];
    assert_eq!(calc_result.status, TaskStatus::Completed);
    assert!(calc_result.output.as_ref().unwrap().contains("30"));

    // 任务3 - 写入文件
    let write_result = &result.task_results["write"];
    assert_eq!(write_result.status, TaskStatus::Completed);
}

#[tokio::test]
async fn test_mcp_tool_not_found() {
    let manager = create_test_mcp_manager().await;
    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建任务（工具不存在）
    let task = Task::new(
        "test",
        "Unknown Tool",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "nonexistent_tool".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());
    let error = result.error.unwrap();
    assert!(
        error.contains("Unknown tool") || error.contains("nonexistent"),
        "Expected tool not found error, got: {}",
        error
    );
}

#[tokio::test]
async fn test_mcp_server_not_found() {
    let manager = create_test_mcp_manager().await;
    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建任务（服务器不存在）
    let task = Task::new(
        "test",
        "Unknown Server",
        TaskType::MCPToolCall {
            server_name: "nonexistent_server".to_string(),
            tool_name: "some_tool".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());
    let error = result.error.unwrap();
    assert!(
        error.contains("not found") || error.contains("nonexistent"),
        "Expected server not found error, got: {}",
        error
    );
}

#[tokio::test]
async fn test_mcp_without_manager() {
    let executor = TaskExecutor::new();

    // 创建任务
    let task = Task::new(
        "test",
        "MCP Task",
        TaskType::MCPToolCall {
            server_name: "filesystem".to_string(),
            tool_name: "read_file".to_string(),
            params: None,
        },
    );

    let result = executor.execute_task(&task).await;

    assert!(matches!(result.status, TaskStatus::Failed(_)));
    assert!(result.error.is_some());
    let error = result.error.unwrap();
    assert!(
        error.contains("manager not configured"),
        "Expected manager not configured error, got: {}",
        error
    );
}

#[tokio::test]
async fn test_mcp_with_string_result() {
    let manager = create_test_mcp_manager().await;
    let executor = TaskExecutor::with_mcp_manager(manager);

    // 创建任务（echo 工具返回字符串）
    let task = Task::new(
        "echo",
        "Echo Message",
        TaskType::MCPToolCall {
            server_name: "utilities".to_string(),
            tool_name: "echo".to_string(),
            params: Some(json!({
                "message": "Hello, MCP!"
            })),
        },
    );

    let result = executor.execute_task(&task).await;

    assert_eq!(result.status, TaskStatus::Completed);
    assert!(result.output.is_some());
    let output = result.output.unwrap();
    assert!(output.contains("Hello, MCP!"));
}
