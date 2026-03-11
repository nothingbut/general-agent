# Phase 2 测试指南

## 概述

本文档提供 MCP 和 RAG 功能的测试指南。

## 前置条件

### 1. Ollama 服务
```bash
# 安装 Ollama
curl -fsSL https://ollama.com/install.sh | sh

# 拉取 embedding 模型
ollama pull nomic-embed-text

# 启动服务（默认端口 11434）
ollama serve
```

### 2. Qdrant 服务
```bash
# 使用 Docker 启动
docker run -p 6333:6333 -p 6334:6334 \
  -v $(pwd)/qdrant_storage:/qdrant/storage:z \
  qdrant/qdrant
```

## RAG 功能测试

### 配置示例

```rust
use agent_rag::*;

// 1. 创建 Embedder
let embedder = Arc::new(OllamaEmbedder::with_defaults());

// 2. 创建 Vector Store
let vector_store = Arc::new(QdrantStore::with_defaults().await?);

// 3. 创建集合
vector_store.create_collection("documents", 768).await?;

// 4. 创建 Retriever
let retriever = DefaultRAGRetriever::new(
    embedder,
    vector_store,
    "documents".to_string(),
);

// 5. 索引文档
let loader = MarkdownLoader;
retriever.index_document_with_loader(
    &loader,
    "path/to/doc.md",
    ChunkConfig::default(),
).await?;

// 6. 检索
let results = retriever.retrieve("查询问题", 5).await?;
```

### 测试步骤

1. **启动服务**：确保 Ollama 和 Qdrant 运行
2. **索引文档**：运行文档索引脚本
3. **测试检索**：验证检索结果准确性
4. **性能测试**：测试大规模文档检索

## MCP 功能测试

### 配置示例

```rust
use agent_mcp::*;

// 1. 配置 MCP 服务器
let config = MCPServerConfig {
    name: "test-server".to_string(),
    command: "mcp-server".to_string(),
    args: vec![],
    env: HashMap::new(),
    timeout: Duration::from_secs(30),
};

// 2. 连接客户端
let client = DefaultMCPClient::connect(config).await?;

// 3. 列出工具
let tools = client.list_tools().await?;

// 4. 调用工具
let result = client.call_tool(
    "tool_name",
    json!({"param": "value"}),
).await?;
```

### 测试步骤

1. **启动 MCP 服务器**：运行测试服务器
2. **连接测试**：验证客户端连接
3. **工具列表**：检查工具发现
4. **工具调用**：测试工具执行

## 集成测试

### ConversationFlow with RAG

```rust
let flow = ConversationFlow::new(session_manager, llm_client, config)
    .with_rag(retriever);

// RAG 会自动增强对话上下文
let response = flow.send_message(session_id, "问题".to_string()).await?;
```

### ConversationFlow with MCP

```rust
let flow = ConversationFlow::new(session_manager, llm_client, config)
    .with_mcp(vec![mcp_client]);

// MCP 工具可被 LLM 调用
let response = flow.send_message(session_id, "使用工具".to_string()).await?;
```

## 编译和运行

```bash
# 编译
cargo build --release --all-features

# 运行 TUI
./target/release/agent-tui

# 运行 CLI
./target/release/agent-cli --help
```

## 故障排查

### Ollama 连接失败
- 检查服务是否运行：`curl http://localhost:11434/api/version`
- 检查模型是否存在：`ollama list`

### Qdrant 连接失败
- 检查服务是否运行：`curl http://localhost:6333/health`
- 检查端口是否正确：6333 (HTTP) 或 6334 (gRPC)

### MCP 协议错误
- 验证服务器支持 JSON-RPC 2.0
- 检查 stdio 通信是否正常
- 查看错误日志

## 下一步

- 添加更多 embedding 模型支持
- 实现语义分块策略
- 添加 MCP 工具权限管理
- 优化向量检索性能
