# MCP 和 RAG 集成实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为 General Agent V2 添加 MCP 工具调用能力和 RAG 上下文增强

**Architecture:**
- 创建两个独立 crate（agent-mcp, agent-rag）
- 通过 trait 抽象集成到 agent-workflow
- MCP 工具调用需用户确认，RAG 自动检索增强上下文

**Tech Stack:**
- MCP: JSON-RPC over stdio, serde_json
- RAG: Qdrant (向量存储), Ollama (embeddings)

**并行开发:** 本计划包含两条独立路径，可由不同开发者并行执行

---

## 路径 A: agent-mcp 实现

### Task A1: 在 agent-core 中定义 MCP trait

**Files:**
- Create: `crates/agent-core/src/traits/mcp.rs`
- Modify: `crates/agent-core/src/traits/mod.rs`

**Step 1: 写 trait 定义**

```rust
// crates/agent-core/src/traits/mcp.rs
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
```

**Step 2: 导出 trait**

```rust
// crates/agent-core/src/traits/mod.rs
// 在文件末尾添加
pub mod mcp;
pub use mcp::{MCPClient, ToolDefinition};
```

**Step 3: 编译验证**

Run: `cargo build -p agent-core`
Expected: SUCCESS

**Step 4: 提交**

```bash
git add crates/agent-core/src/traits/mcp.rs crates/agent-core/src/traits/mod.rs
git commit -m "feat(core): 添加 MCP trait 定义"
```

---

### Task A2: 创建 agent-mcp crate 脚手架

**Files:**
- Create: `crates/agent-mcp/Cargo.toml`
- Create: `crates/agent-mcp/src/lib.rs`
- Create: `crates/agent-mcp/src/error.rs`
- Modify: `Cargo.toml` (workspace)

**Step 1: 创建 Cargo.toml**

```toml
# crates/agent-mcp/Cargo.toml
[package]
name = "agent-mcp"
version.workspace = true
edition.workspace = true

[dependencies]
agent-core = { path = "../agent-core" }
tokio = { workspace = true, features = ["process", "io-util", "sync"] }
serde = { workspace = true }
serde_json = { workspace = true }
async-trait = { workspace = true }
thiserror = { workspace = true }
tracing = { workspace = true }

[dev-dependencies]
tokio = { workspace = true, features = ["test-util", "macros"] }
```

**Step 2: 创建错误类型**

```rust
// crates/agent-mcp/src/error.rs
use std::time::Duration;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum MCPError {
    #[error("MCP server connection failed: {0}")]
    ConnectionFailed(String),

    #[error("Tool not found: {0}")]
    ToolNotFound(String),

    #[error("Tool call failed: {0}")]
    ToolCallFailed(String),

    #[error("Protocol error: {0}")]
    ProtocolError(String),

    #[error("Timeout after {0:?}")]
    Timeout(Duration),

    #[error("Permission denied: {0}")]
    PermissionDenied(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

pub type Result<T> = std::result::Result<T, MCPError>;
```

**Step 3: 创建 lib.rs**

```rust
// crates/agent-mcp/src/lib.rs
pub mod error;

pub use error::{MCPError, Result};
```

**Step 4: 添加到 workspace**

```toml
# Cargo.toml (根目录)
[workspace]
members = [
    # ... 现有成员
    "crates/agent-mcp",
]
```

**Step 5: 编译验证**

Run: `cargo build -p agent-mcp`
Expected: SUCCESS

**Step 6: 提交**

```bash
git add crates/agent-mcp/ Cargo.toml
git commit -m "feat(mcp): 创建 agent-mcp crate 脚手架"
```

---

### Task A3: 实现 JSON-RPC 协议

**Files:**
- Create: `crates/agent-mcp/src/protocol.rs`
- Modify: `crates/agent-mcp/src/lib.rs`
- Create: `crates/agent-mcp/tests/protocol_tests.rs`

**Step 1: 写协议测试**

```rust
// crates/agent-mcp/tests/protocol_tests.rs
use agent_mcp::protocol::*;
use serde_json::json;

#[test]
fn test_request_serialization() {
    let req = JsonRpcMessage::Request {
        jsonrpc: "2.0".to_string(),
        id: 1,
        method: "initialize".to_string(),
        params: json!({"protocolVersion": "2024-11-05"}),
    };

    let serialized = serde_json::to_string(&req).unwrap();
    assert!(serialized.contains("\"method\":\"initialize\""));
}

#[test]
fn test_response_deserialization() {
    let json_str = r#"{"jsonrpc":"2.0","id":1,"result":{"success":true}}"#;
    let msg: JsonRpcMessage = serde_json::from_str(json_str).unwrap();

    match msg {
        JsonRpcMessage::Response { id, result, .. } => {
            assert_eq!(id, 1);
            assert!(result.is_object());
        }
        _ => panic!("Expected Response"),
    }
}
```

**Step 2: 运行测试确认失败**

Run: `cargo test -p agent-mcp protocol_tests`
Expected: FAIL with "protocol module not found"

**Step 3: 实现协议结构**

```rust
// crates/agent-mcp/src/protocol.rs
use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(untagged)]
pub enum JsonRpcMessage {
    Request {
        jsonrpc: String,
        id: u64,
        method: String,
        params: Value,
    },
    Response {
        jsonrpc: String,
        id: u64,
        result: Value,
    },
    Error {
        jsonrpc: String,
        id: u64,
        error: JsonRpcError,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct JsonRpcError {
    pub code: i32,
    pub message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

impl JsonRpcMessage {
    pub fn request(id: u64, method: String, params: Value) -> Self {
        Self::Request {
            jsonrpc: "2.0".to_string(),
            id,
            method,
            params,
        }
    }

    pub fn response(id: u64, result: Value) -> Self {
        Self::Response {
            jsonrpc: "2.0".to_string(),
            id,
            result,
        }
    }
}
```

**Step 4: 导出模块**

```rust
// crates/agent-mcp/src/lib.rs
pub mod error;
pub mod protocol;

pub use error::{MCPError, Result};
pub use protocol::{JsonRpcMessage, JsonRpcError};
```

**Step 5: 运行测试确认通过**

Run: `cargo test -p agent-mcp protocol_tests`
Expected: PASS (2 tests)

**Step 6: 提交**

```bash
git add crates/agent-mcp/
git commit -m "feat(mcp): 实现 JSON-RPC 协议"
```

---

### Task A4: 实现 Stdio 传输层

**Files:**
- Create: `crates/agent-mcp/src/transport.rs`
- Modify: `crates/agent-mcp/src/lib.rs`
- Create: `crates/agent-mcp/tests/transport_tests.rs`

**Step 1: 写传输层测试**

```rust
// crates/agent-mcp/tests/transport_tests.rs
use agent_mcp::transport::*;
use agent_mcp::protocol::JsonRpcMessage;
use serde_json::json;

#[tokio::test]
async fn test_stdio_transport_creation() {
    // 使用 echo 作为简单测试进程
    let result = StdioTransport::new("echo", &[], &std::collections::HashMap::new());
    assert!(result.is_ok());
}
```

**Step 2: 运行测试确认失败**

Run: `cargo test -p agent-mcp transport_tests`
Expected: FAIL with "transport module not found"

**Step 3: 实现传输层**

```rust
// crates/agent-mcp/src/transport.rs
use async_trait::async_trait;
use std::collections::HashMap;
use std::process::Stdio;
use std::sync::atomic::{AtomicU64, Ordering};
use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader};
use tokio::process::{Child, ChildStdin, ChildStdout, Command};
use crate::{JsonRpcMessage, Result, MCPError};

#[async_trait]
pub trait Transport: Send + Sync {
    async fn send(&mut self, msg: JsonRpcMessage) -> Result<()>;
    async fn recv(&mut self) -> Result<JsonRpcMessage>;
}

pub struct StdioTransport {
    _process: Child,
    stdin: ChildStdin,
    stdout: BufReader<ChildStdout>,
    next_id: AtomicU64,
}

impl StdioTransport {
    pub fn new(command: &str, args: &[String], env: &HashMap<String, String>) -> Result<Self> {
        let mut cmd = Command::new(command);
        cmd.args(args);
        cmd.envs(env);
        cmd.stdin(Stdio::piped());
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::inherit());

        let mut process = cmd.spawn()
            .map_err(|e| MCPError::ConnectionFailed(e.to_string()))?;

        let stdin = process.stdin.take()
            .ok_or_else(|| MCPError::ConnectionFailed("Failed to get stdin".to_string()))?;
        let stdout = process.stdout.take()
            .ok_or_else(|| MCPError::ConnectionFailed("Failed to get stdout".to_string()))?;

        Ok(Self {
            _process: process,
            stdin,
            stdout: BufReader::new(stdout),
            next_id: AtomicU64::new(1),
        })
    }

    pub fn next_id(&self) -> u64 {
        self.next_id.fetch_add(1, Ordering::SeqCst)
    }
}

#[async_trait]
impl Transport for StdioTransport {
    async fn send(&mut self, msg: JsonRpcMessage) -> Result<()> {
        let json = serde_json::to_string(&msg)?;
        self.stdin.write_all(json.as_bytes()).await?;
        self.stdin.write_all(b"\n").await?;
        self.stdin.flush().await?;
        Ok(())
    }

    async fn recv(&mut self) -> Result<JsonRpcMessage> {
        let mut line = String::new();
        self.stdout.read_line(&mut line).await?;

        if line.is_empty() {
            return Err(MCPError::ConnectionFailed("EOF".to_string()));
        }

        let msg = serde_json::from_str(&line)?;
        Ok(msg)
    }
}
```

**Step 4: 导出模块**

```rust
// crates/agent-mcp/src/lib.rs
pub mod error;
pub mod protocol;
pub mod transport;

pub use error::{MCPError, Result};
pub use protocol::{JsonRpcMessage, JsonRpcError};
pub use transport::{Transport, StdioTransport};
```

**Step 5: 运行测试确认通过**

Run: `cargo test -p agent-mcp transport_tests`
Expected: PASS

**Step 6: 提交**

```bash
git add crates/agent-mcp/
git commit -m "feat(mcp): 实现 Stdio 传输层"
```

---

### Task A5: 实现 MCP 客户端

**Files:**
- Create: `crates/agent-mcp/src/client.rs`
- Create: `crates/agent-mcp/src/config.rs`
- Modify: `crates/agent-mcp/src/lib.rs`
- Create: `crates/agent-mcp/tests/client_tests.rs`

**Step 1: 写客户端测试**

```rust
// crates/agent-mcp/tests/client_tests.rs
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
```

**Step 2: 运行测试确认失败**

Run: `cargo test -p agent-mcp client_tests`
Expected: FAIL with "MCPServerConfig not found"

**Step 3: 实现配置**

```rust
// crates/agent-mcp/src/config.rs
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::Duration;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MCPServerConfig {
    pub name: String,
    pub command: String,
    pub args: Vec<String>,
    pub env: HashMap<String, String>,

    #[serde(default = "default_timeout", with = "humantime_serde")]
    pub timeout: Duration,
}

fn default_timeout() -> Duration {
    Duration::from_secs(30)
}

impl MCPServerConfig {
    pub fn new(name: String, command: String) -> Self {
        Self {
            name,
            command,
            args: vec![],
            env: HashMap::new(),
            timeout: default_timeout(),
        }
    }
}
```

**Step 4: 实现客户端**

```rust
// crates/agent-mcp/src/client.rs
use agent_core::traits::{MCPClient, ToolDefinition};
use async_trait::async_trait;
use serde_json::{json, Value};
use std::sync::Arc;
use tokio::sync::RwLock;
use tokio::time::timeout;

use crate::{config::MCPServerConfig, transport::{StdioTransport, Transport}, JsonRpcMessage, Result, MCPError};

pub struct DefaultMCPClient {
    transport: Arc<RwLock<StdioTransport>>,
    config: MCPServerConfig,
    tools: RwLock<Vec<ToolDefinition>>,
}

impl DefaultMCPClient {
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
```

**Step 5: 添加依赖**

```toml
# crates/agent-mcp/Cargo.toml
[dependencies]
# ... 现有依赖
humantime-serde = "1.1"
```

**Step 6: 导出模块**

```rust
// crates/agent-mcp/src/lib.rs
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
```

**Step 7: 运行测试确认通过**

Run: `cargo test -p agent-mcp`
Expected: PASS

**Step 8: 提交**

```bash
git add crates/agent-mcp/
git commit -m "feat(mcp): 实现 MCP 客户端"
```

---

## 路径 B: agent-rag 实现

### Task B1: 在 agent-core 中定义 RAG trait

**Files:**
- Create: `crates/agent-core/src/traits/rag.rs`
- Modify: `crates/agent-core/src/traits/mod.rs`

**Step 1: 写 trait 定义**

```rust
// crates/agent-core/src/traits/rag.rs
use async_trait::async_trait;
use std::collections::HashMap;
use crate::error::Result;

/// 文档
#[derive(Debug, Clone)]
pub struct Document {
    pub id: String,
    pub content: String,
    pub metadata: HashMap<String, String>,
}

/// RAG 检索器抽象
#[async_trait]
pub trait RAGRetriever: Send + Sync {
    /// 检索相关文档
    async fn retrieve(&self, query: &str, top_k: usize) -> Result<Vec<Document>>;

    /// 索引新文档
    async fn index_document(&self, doc: Document) -> Result<()>;
}
```

**Step 2: 导出 trait**

```rust
// crates/agent-core/src/traits/mod.rs
// 在文件末尾添加
pub mod rag;
pub use rag::{RAGRetriever, Document};
```

**Step 3: 编译验证**

Run: `cargo build -p agent-core`
Expected: SUCCESS

**Step 4: 提交**

```bash
git add crates/agent-core/src/traits/rag.rs crates/agent-core/src/traits/mod.rs
git commit -m "feat(core): 添加 RAG trait 定义"
```

---

### Task B2: 创建 agent-rag crate 脚手架

**Files:**
- Create: `crates/agent-rag/Cargo.toml`
- Create: `crates/agent-rag/src/lib.rs`
- Create: `crates/agent-rag/src/error.rs`
- Modify: `Cargo.toml` (workspace)

**Step 1: 创建 Cargo.toml**

```toml
# crates/agent-rag/Cargo.toml
[package]
name = "agent-rag"
version.workspace = true
edition.workspace = true

[dependencies]
agent-core = { path = "../agent-core" }
agent-llm = { path = "../agent-llm" }
tokio = { workspace = true }
serde = { workspace = true }
serde_json = { workspace = true }
async-trait = { workspace = true }
thiserror = { workspace = true }
tracing = { workspace = true }

# Qdrant 客户端
qdrant-client = "1.7"

[dev-dependencies]
tokio = { workspace = true, features = ["test-util", "macros"] }
```

**Step 2: 创建错误类型**

```rust
// crates/agent-rag/src/error.rs
use thiserror::Error;

#[derive(Debug, Error)]
pub enum RAGError {
    #[error("Document loading failed: {0}")]
    LoadingFailed(String),

    #[error("Embedding generation failed: {0}")]
    EmbeddingFailed(String),

    #[error("Vector store error: {0}")]
    VectorStoreError(String),

    #[error("Retrieval failed: {0}")]
    RetrievalFailed(String),

    #[error("Chunking failed: {0}")]
    ChunkingFailed(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

pub type Result<T> = std::result::Result<T, RAGError>;
```

**Step 3: 创建 lib.rs**

```rust
// crates/agent-rag/src/lib.rs
pub mod error;

pub use error::{RAGError, Result};
```

**Step 4: 添加到 workspace**

```toml
# Cargo.toml (根目录)
[workspace]
members = [
    # ... 现有成员
    "crates/agent-rag",
]
```

**Step 5: 编译验证**

Run: `cargo build -p agent-rag`
Expected: SUCCESS

**Step 6: 提交**

```bash
git add crates/agent-rag/ Cargo.toml
git commit -m "feat(rag): 创建 agent-rag crate 脚手架"
```

---

### Task B3: 实现文档加载器

**Files:**
- Create: `crates/agent-rag/src/loader.rs`
- Modify: `crates/agent-rag/src/lib.rs`
- Create: `crates/agent-rag/tests/loader_tests.rs`

**Step 1: 写加载器测试**

```rust
// crates/agent-rag/tests/loader_tests.rs
use agent_rag::loader::*;
use agent_core::traits::Document;
use std::fs;
use tempfile::TempDir;

#[tokio::test]
async fn test_markdown_loader() {
    let temp_dir = TempDir::new().unwrap();
    let file_path = temp_dir.path().join("test.md");
    fs::write(&file_path, "# Test\nContent").unwrap();

    let loader = MarkdownLoader;
    let docs = loader.load(file_path.to_str().unwrap()).await.unwrap();

    assert_eq!(docs.len(), 1);
    assert!(docs[0].content.contains("Test"));
}
```

**Step 2: 运行测试确认失败**

Run: `cargo test -p agent-rag loader_tests`
Expected: FAIL with "loader module not found"

**Step 3: 实现加载器**

```rust
// crates/agent-rag/src/loader.rs
use agent_core::traits::Document;
use async_trait::async_trait;
use std::collections::HashMap;
use tokio::fs;
use crate::{Result, RAGError};

#[async_trait]
pub trait DocumentLoader: Send + Sync {
    async fn load(&self, source: &str) -> Result<Vec<Document>>;
}

pub struct MarkdownLoader;

#[async_trait]
impl DocumentLoader for MarkdownLoader {
    async fn load(&self, source: &str) -> Result<Vec<Document>> {
        let content = fs::read_to_string(source).await
            .map_err(|e| RAGError::LoadingFailed(e.to_string()))?;

        let mut metadata = HashMap::new();
        metadata.insert("type".to_string(), "markdown".to_string());
        metadata.insert("source".to_string(), source.to_string());

        let doc = Document {
            id: source.to_string(),
            content,
            metadata,
        };

        Ok(vec![doc])
    }
}

pub struct TextLoader;

#[async_trait]
impl DocumentLoader for TextLoader {
    async fn load(&self, source: &str) -> Result<Vec<Document>> {
        let content = fs::read_to_string(source).await
            .map_err(|e| RAGError::LoadingFailed(e.to_string()))?;

        let mut metadata = HashMap::new();
        metadata.insert("type".to_string(), "text".to_string());
        metadata.insert("source".to_string(), source.to_string());

        let doc = Document {
            id: source.to_string(),
            content,
            metadata,
        };

        Ok(vec![doc])
    }
}
```

**Step 4: 添加测试依赖**

```toml
# crates/agent-rag/Cargo.toml
[dev-dependencies]
tokio = { workspace = true, features = ["test-util", "macros", "fs"] }
tempfile = "3.8"
```

**Step 5: 导出模块**

```rust
// crates/agent-rag/src/lib.rs
pub mod error;
pub mod loader;

pub use error::{RAGError, Result};
pub use loader::{DocumentLoader, MarkdownLoader, TextLoader};
```

**Step 6: 运行测试确认通过**

Run: `cargo test -p agent-rag loader_tests`
Expected: PASS

**Step 7: 提交**

```bash
git add crates/agent-rag/
git commit -m "feat(rag): 实现文档加载器"
```

---

### Task B4: 实现文本分块器

**Files:**
- Create: `crates/agent-rag/src/chunker.rs`
- Modify: `crates/agent-rag/src/lib.rs`
- Create: `crates/agent-rag/tests/chunker_tests.rs`

**Step 1: 写分块器测试**

```rust
// crates/agent-rag/tests/chunker_tests.rs
use agent_rag::chunker::*;
use agent_core::traits::Document;
use std::collections::HashMap;

#[test]
fn test_fixed_chunking() {
    let doc = Document {
        id: "test".to_string(),
        content: "a".repeat(1000),
        metadata: HashMap::new(),
    };

    let config = ChunkConfig {
        chunk_size: 100,
        overlap: 10,
        strategy: ChunkStrategy::Fixed,
    };

    let chunker = Chunker::new(config);
    let chunks = chunker.chunk(&doc);

    assert!(chunks.len() > 1);
    assert!(chunks[0].content.len() <= 100);
}
```

**Step 2: 运行测试确认失败**

Run: `cargo test -p agent-rag chunker_tests`
Expected: FAIL with "chunker module not found"

**Step 3: 实现分块器**

```rust
// crates/agent-rag/src/chunker.rs
use agent_core::traits::Document;

#[derive(Debug, Clone, Copy)]
pub enum ChunkStrategy {
    Fixed,
    Semantic,
    Recursive,
}

#[derive(Debug, Clone)]
pub struct ChunkConfig {
    pub chunk_size: usize,
    pub overlap: usize,
    pub strategy: ChunkStrategy,
}

impl Default for ChunkConfig {
    fn default() -> Self {
        Self {
            chunk_size: 512,
            overlap: 50,
            strategy: ChunkStrategy::Fixed,
        }
    }
}

#[derive(Debug, Clone)]
pub struct Chunk {
    pub id: String,
    pub content: String,
    pub doc_id: String,
    pub start_idx: usize,
    pub end_idx: usize,
}

pub struct Chunker {
    config: ChunkConfig,
}

impl Chunker {
    pub fn new(config: ChunkConfig) -> Self {
        Self { config }
    }

    pub fn chunk(&self, doc: &Document) -> Vec<Chunk> {
        match self.config.strategy {
            ChunkStrategy::Fixed => self.chunk_fixed(doc),
            ChunkStrategy::Semantic => vec![], // TODO
            ChunkStrategy::Recursive => vec![], // TODO
        }
    }

    fn chunk_fixed(&self, doc: &Document) -> Vec<Chunk> {
        let chars: Vec<char> = doc.content.chars().collect();
        let mut chunks = Vec::new();
        let stride = self.config.chunk_size.saturating_sub(self.config.overlap);

        let mut start = 0;
        let mut chunk_idx = 0;

        while start < chars.len() {
            let end = (start + self.config.chunk_size).min(chars.len());
            let content: String = chars[start..end].iter().collect();

            chunks.push(Chunk {
                id: format!("{}_{}", doc.id, chunk_idx),
                content,
                doc_id: doc.id.clone(),
                start_idx: start,
                end_idx: end,
            });

            start += stride;
            chunk_idx += 1;

            if stride == 0 {
                break;
            }
        }

        chunks
    }
}
```

**Step 4: 导出模块**

```rust
// crates/agent-rag/src/lib.rs
pub mod error;
pub mod loader;
pub mod chunker;

pub use error::{RAGError, Result};
pub use loader::{DocumentLoader, MarkdownLoader, TextLoader};
pub use chunker::{Chunker, ChunkConfig, ChunkStrategy, Chunk};
```

**Step 5: 运行测试确认通过**

Run: `cargo test -p agent-rag chunker_tests`
Expected: PASS

**Step 6: 提交**

```bash
git add crates/agent-rag/
git commit -m "feat(rag): 实现文本分块器"
```

---

## 路径 C: 集成到 agent-workflow

### Task C1: 扩展 ConversationFlow 支持 MCP

**Files:**
- Modify: `crates/agent-workflow/src/conversation_flow.rs`
- Modify: `crates/agent-workflow/Cargo.toml`

**Step 1: 添加依赖**

```toml
# crates/agent-workflow/Cargo.toml
[dependencies]
# ... 现有依赖
agent-mcp = { path = "../agent-mcp", optional = true }
agent-rag = { path = "../agent-rag", optional = true }

[features]
default = []
mcp = ["dep:agent-mcp"]
rag = ["dep:agent-rag"]
```

**Step 2: 扩展 ConversationFlow 结构**

```rust
// crates/agent-workflow/src/conversation_flow.rs
// 在 ConversationFlow 结构体中添加
pub struct ConversationFlow {
    // ... 现有字段

    #[cfg(feature = "mcp")]
    mcp_clients: Option<Arc<Vec<Arc<dyn agent_core::traits::MCPClient>>>>,

    #[cfg(feature = "rag")]
    rag_retriever: Option<Arc<dyn agent_core::traits::RAGRetriever>>,
}

impl ConversationFlow {
    // 添加方法
    #[cfg(feature = "mcp")]
    pub fn with_mcp(mut self, clients: Vec<Arc<dyn agent_core::traits::MCPClient>>) -> Self {
        self.mcp_clients = Some(Arc::new(clients));
        self
    }

    #[cfg(feature = "rag")]
    pub fn with_rag(mut self, retriever: Arc<dyn agent_core::traits::RAGRetriever>) -> Self {
        self.rag_retriever = Some(retriever);
        self
    }
}
```

**Step 3: 编译验证**

Run: `cargo build -p agent-workflow --features mcp,rag`
Expected: SUCCESS

**Step 4: 提交**

```bash
git add crates/agent-workflow/
git commit -m "feat(workflow): 扩展 ConversationFlow 支持 MCP 和 RAG"
```

---

## 后续步骤

本计划完成了 Phase 1 的核心功能：
- ✅ MCP 基础协议和客户端
- ✅ RAG 文档加载和分块
- ✅ ConversationFlow 集成点

**Phase 2 任务** (单独计划)：
- Embedding 生成（Ollama）
- Qdrant 向量存储集成
- RAG 检索器实现
- MCP 工具调用流程
- 用户确认机制

---

**计划状态:** ✅ Phase 1 准备就绪
**预计时间:** 3-4 天（并行开发可缩短到 2 天）
