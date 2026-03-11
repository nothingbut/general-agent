use agent_mcp::*;

#[tokio::test]
async fn test_client_creation() {
    let config = MCPServerConfig {
        name: "test".to_string(),
        command: "echo".to_string(),
        args: vec![],
        env: std::collections::HashMap::new(),
        timeout: std::time::Duration::from_secs(5),
    };

    // 注意：这个测试需要一个真实的 MCP 服务器
    // 暂时只测试配置创建
    assert_eq!(config.name, "test");
}
