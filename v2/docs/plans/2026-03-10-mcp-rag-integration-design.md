# MCP 和 RAG 集成设计文档

**创建日期:** 2026-03-10
**状态:** 已批准
**版本:** 1.0

---

## 概述

本文档描述 General Agent V2 中 MCP（Model Context Protocol）客户端和 RAG（Retrieval-Augmented Generation）系统的集成设计。

### 目标

1. **MCP 集成** - 通用 MCP 客户端，支持任意 MCP 服务器，实现工具调用能力
2. **RAG 集成** - 多来源文档检索系统，自动增强对话上下文
3. **架构一致** - 遵循现有分层架构，保持模块化和可测试性
4. **并行开发** - MCP 和 RAG 独立开发，互不阻塞

### 关键决策

- **集成方式:** 独立 crate，通过 trait 抽象集成到 `agent-workflow`
- **MCP 安全:** 工具调用需用户确认
- **RAG 模式:** 自动检索增强上下文
- **向量存储:** Qdrant 本地模式
- **Embedding:** 通过 Ollama 调用本地模型

---

## 整体架构

### Crate 结构

```
v2/crates/
├── agent-mcp/          # 新建：MCP 客户端
│   ├── src/
│   │   ├── client.rs        # MCPClient trait + 实现
│   │   ├── protocol.rs      # JSON-RPC 协议
│   │   ├── transport.rs     # stdio/HTTP 传输
│   │   ├── tools.rs         # 工具定义和调用
│   │   ├── config.rs        # 服务器配置
│   │   └── error.rs         # 错误类型
│   └── Cargo.toml
│
├── agent-rag/          # 新建：RAG 检索系统
│   ├── src/
│   │   ├── retriever.rs     # RAGRetriever trait + 实现
│   │   ├── loader.rs        # 文档加载器（多格式）
│   │   ├── chunker.rs       # 文本分块策略
│   │   ├── embeddings.rs    # Embedding 生成（Ollama）
│   │   ├── vector_store.rs  # Qdrant 集成
│   │   ├── config.rs        # RAG 配置
│   │   └── error.rs         # 错误类型
│   └── Cargo.toml
│
└── agent-workflow/     # 扩展：集成 MCP + RAG
    └── src/
        └── conversation_flow.rs  # 添加 MCP/RAG 支持
```

### 依赖关系

```
agent-core (Traits 定义)
    ↓
agent-mcp, agent-rag (独立实现)
    ↓
agent-workflow (编排集成)
    ↓
agent-cli, agent-tui (用户界面)
```

### 核心 Trait 定义

在 `agent-core/src/traits/` 中定义抽象接口。

**详细内容请参考完整设计文档...**

---

## 实施计划

### Phase 1: 基础实现 (1周)

**agent-mcp (3天):**
- [ ] 定义 trait 和基础结构
- [ ] 实现 JSON-RPC 协议
- [ ] 实现 Stdio 传输
- [ ] 实现 DefaultMCPClient
- [ ] 配置系统和安全策略
- [ ] 单元测试

**agent-rag (3天):**
- [ ] 定义 trait 和基础结构
- [ ] 实现文档加载器（Markdown, Text）
- [ ] 实现分块器
- [ ] 实现 OllamaEmbeddings
- [ ] 实现 QdrantVectorStore
- [ ] 实现 DefaultRAGRetriever
- [ ] 单元测试

**集成 (1天):**
- [ ] 扩展 ConversationFlow
- [ ] 实现 RAG 自动增强
- [ ] 实现 MCP 工具调用流程
- [ ] 用户确认机制

### Phase 2: 扩展功能 (1周)

**MCP 扩展:**
- [ ] HTTP 传输支持
- [ ] 资源管理（ListResources, ReadResource）
- [ ] 连接池和重连逻辑
- [ ] 性能优化

**RAG 扩展:**
- [ ] 更多文档加载器（PDF, Code）
- [ ] 语义分块策略
- [ ] 重排序器实现
- [ ] 混合检索（关键词 + 向量）

**CLI 命令:**
- [ ] `agent mcp list` - 列出 MCP 服务器
- [ ] `agent mcp tools <server>` - 列出工具
- [ ] `agent rag index <path>` - 索引文档
- [ ] `agent rag search <query>` - 测试检索

### Phase 3: 测试和优化 (3天)

- [ ] 集成测试
- [ ] 端到端测试
- [ ] 性能测试和优化
- [ ] 文档完善
- [ ] 示例和教程

---

**文档状态:** ✅ 已批准
**下一步:** 调用 writing-plans skill 创建详细实施计划

**注:** 完整的详细设计（包括所有 API 定义、数据流、错误处理等）请参考项目 Git 历史或联系开发团队。
