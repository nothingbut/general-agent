# Phase 2 测试指南

## 编译和运行

```bash
cd v2
cargo build --release --all-features
./target/release/agent-tui
```

## RAG 测试

需要 Ollama (localhost:11434) 和 Qdrant (localhost:6334)

## MCP 测试

需要 MCP 服务器支持 JSON-RPC 2.0

详细文档见 v2/docs/plans/2026-03-10-mcp-rag-implementation-plan.md
