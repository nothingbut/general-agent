use agent_mcp::transport::*;
use agent_mcp::protocol::JsonRpcMessage;
use serde_json::json;

#[tokio::test]
async fn test_stdio_transport_creation() {
    // 使用 echo 作为简单测试进程
    let result = StdioTransport::new("echo", &[], &std::collections::HashMap::new());
    assert!(result.is_ok());
}
