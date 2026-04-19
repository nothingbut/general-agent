# CLAUDE.md

这个文件为 Claude Code (claude.ai/code) 提供在本仓库中工作的指南。

## 项目概述

General Agent V2 是一个高性能、类型安全的通用 AI Agent 框架，基于 Rust 构建。

- **当前版本**: V2 (Rust) - 活跃开发 ⭐
- **历史版本**: v1/ (Python) 和 v3/ (.NET) 为参考版本

## 重要提示

1. **使用中文**：本项目所有对话、文档和注释都使用中文
2. **聚焦 V2**：所有开发工作都在 `v2/` 目录下进行
3. **测试覆盖率**：保持 80%+ 的测试覆盖率，844+ 个测试全部通过

## 构建和测试命令

### 日常开发命令

```bash
# 工作目录
cd v2

# 构建项目
cargo build                         # Debug 构建
cargo build --release               # Release 构建

# 运行 CLI
cargo run --package agent-cli -- --help
cargo run --package agent-cli -- new --title "测试会话"
cargo run --package agent-cli -- chat <session-id>

# 测试
cargo test                          # 运行所有测试（821个）
cargo test -- --nocapture           # 带输出
cargo test --package agent-workflow # 特定 crate
cargo test test_full_conversation   # 单个测试

# 代码质量
cargo fmt                           # 格式化代码
cargo clippy                        # Lint 检查
cargo clippy -- -D warnings         # 警告即错误

# 监听文件变化
cargo watch -x check -x test
```

## 项目架构

V2 采用 **Workspace 多 Crate 架构**，分层设计、职责分离。

### Crate 结构

```
v2/
├── crates/
│   ├── agent-core/                   # 核心模型、Traits、错误类型
│   ├── agent-storage/                # SQLite 持久化层（sqlx）
│   ├── agent-llm/                    # LLM 客户端（Anthropic + Ollama）
│   ├── agent-skills/                 # 技能系统（加载、注册、执行）
│   ├── agent-mcp/                    # MCP 协议客户端
│   ├── agent-rag/                    # RAG 检索（Qdrant + Ollama Embedding）
│   ├── agent-context-compression/    # 上下文压缩（Token 计数 + 多策略）
│   ├── agent-memory/                 # 长期记忆（5 种类型 + 向量检索）
│   ├── agent-file-storage/           # 文件上传（权限 + 版本控制）
│   ├── agent-skill-extraction/       # 技能抽取（LLM 驱动）
│   ├── agent-scheduled-tasks/        # 计划任务（Cron + 自然语言）
│   ├── agent-multi-agent/            # 多 Agent 协作（注册表 + 路由 + 4 种策略）
│   ├── agent-workflow/               # 业务编排层（集成所有功能模块）
│   ├── agent-api/                    # Web API（Axum + SSE + OpenAPI）
│   ├── agent-tui/                    # TUI 界面（Ratatui）
│   └── agent-cli/                    # CLI 工具（Clap）
├── docs/                             # 文档
│   ├── V2_ROADMAP_2026.md           # 发展路线图
│   ├── V2_PLANNING_SUMMARY.md       # 规划总结
│   └── plans/                        # 实施计划
└── Cargo.toml                        # Workspace 根配置
```

### 分层架构

```
┌──────────────────────────────────────────────────┐
│    agent-cli / agent-tui / agent-api  (界面层)    │
├──────────────────────────────────────────────────┤
│              agent-workflow (业务编排层)            │
│   SessionManager + ConversationFlow               │
│   集成 Skills / MCP / RAG / Memory / MultiAgent   │
├───────┬───────┬───────┬───────┬──────────────────┤
│agent- │agent- │agent- │agent- │  agent-context-  │
│ llm   │ mcp   │ rag   │memory │  compression     │
├───────┼───────┴───────┴───────┼──────────────────┤
│ agent-multi-agent │ agent-file-storage │ storage  │
├───────────────────┴────────────────────┴─────────┤
│  agent-skills │ agent-scheduled-tasks │ storage   │
├───────────────┴────────────────────┴─────────────┤
│              agent-core (核心层)                    │
│         模型 + Traits + 错误类型                    │
└──────────────────────────────────────────────────┘
```

**依赖方向**：界面层 → 业务编排层 → 功能模块 → 核心层

### 关键设计模式

#### 1. LLM 客户端（agent-llm）
- **LlmClient trait**：`async fn complete()` + `async fn stream()`
- **多提供商**：AnthropicClient + OllamaClient
- **流式响应**：`CompletionStream` trait（`next() -> Result<Option<StreamChunk>>`）

#### 2. 会话管理（agent-workflow）
- **SessionManager**：创建、加载、列表、删除会话
- **ConversationFlow**：对话编排（同步 + 流式）
- **可选功能**：通过 Cargo features 启用（memory, file-storage, compression, mcp, rag）

#### 3. 技能系统（agent-skills）
- **定义格式**：YAML frontmatter + Markdown 模板
- **调用语法**：`@skill-name` 或 `/skill-name`
- **SkillRegistry**：注册、查找、列表
- **SkillExecutor**：参数替换执行

#### 4. 上下文压缩（agent-context-compression）
- **Token 计数**：tiktoken-rs 多模型支持
- **三种策略**：滑动窗口、语义压缩、分层压缩
- **自动触发**：超过阈值自动压缩
- **缓存层**：LRU 缓存 + TTL 过期

#### 5. 长期记忆（agent-memory）
- **五种类型**：User, Feedback, Project, Reference, Knowledge
- **混合检索**：关键词搜索 + 语义相似度
- **LLM 驱动提取**：自动从对话中提取记忆

#### 6. 文件上传（agent-file-storage）
- **三级权限**：Private / Shared / Public
- **版本控制**：每次上传创建新版本
- **对话引用**：`@file:filename` 语法

#### 7. Web API（agent-api）
- **框架**：Axum 0.7 + tower-http
- **28 个端点**：sessions / chat / skills / memory / files
- **SSE 流式**：`/api/v1/chat/:session_id/stream`
- **OpenAPI**：utoipa 4 + Swagger UI（`/swagger-ui/`）
- **路由语法**：使用 `/:param`（非 `/{param}`）

#### 8. 计划任务（agent-scheduled-tasks）
- **调度**：Cron 表达式 + 中文自然语言
- **三种类型**：技能调用 / 记忆提醒 / 自定义命令
- **重试机制**：指数退避

#### 9. 多 Agent 协作（agent-multi-agent）
- **Agent trait**：`info()` + `handle_message()` + `execute_task()`
- **AgentRegistry**：注册、发现、按能力查找（DashMap 并发安全）
- **MessageRouter**：`tokio::sync::mpsc` 点对点消息路由
- **4 种协作策略**：Parallel / Sequential / Voting / Pipeline
- **内置 Agent**：Search、Analysis、Summary（LLM 驱动）
- **API 端点**：`GET /api/v1/agents` + `POST /api/v1/agents/collaborate`
- **集成方式**：通过 `agent-workflow` 的 `multi-agent` feature flag 启用

## LLM 配置

### 使用 Ollama（推荐用于开发）

```bash
ollama pull qwen3.5:0.8b            # 对话模型
ollama pull nomic-embed-text        # Embedding 模型
ollama serve

cargo run --package agent-cli -- chat <session-id>
```

### 使用 Anthropic Claude

```bash
export ANTHROPIC_API_KEY=sk-ant-xxx
cargo run --package agent-cli -- --provider anthropic chat <session-id>
```

## 环境变量

```bash
export AGENT_DB=./agent.db           # 数据库路径
export AGENT_PROVIDER=ollama         # LLM 提供商
export ANTHROPIC_API_KEY=sk-ant-xxx  # Anthropic API Key
export OLLAMA_MODEL=qwen3.5:0.8b    # Ollama 模型
export OLLAMA_BASE_URL=http://localhost:11434
```

## 外部服务依赖

```bash
# Qdrant（Memory/RAG 向量检索需要）
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant

# Ollama（LLM + Embedding）
ollama serve
```

## 路线图进度

### Phase 3: V3 功能对齐 ✅ 已完成
- ✅ 上下文压缩系统（Week 1-2）
- ✅ 长期记忆系统（Week 3-4）
- ✅ 文件上传系统（Week 5-6）
- ✅ 技能抽取系统（Week 7）
- ✅ 计划任务系统（Week 8-9）

### Phase 4: V2 独特优势建设 ✅ 已完成
- ✅ TUI 界面（Week 10-11）
- ✅ Web API 服务（Week 12-15）- 30 端点 + OpenAPI + SSE
- ✅ 多 Agent 协作（Week 16-18）- 4 种策略 + 注册表 + 消息路由

### Phase 5: 生态建设（Q4 2026）
- ⏳ 插件系统（WASM）
- ⏳ 企业级功能
- ⏳ 社区驱动

## 相关文档

- **[V2 README](v2/README.md)** - 项目概览和快速开始
- **[V2 路线图](v2/docs/V2_ROADMAP_2026.md)** - 发展路线图
- **[V2 规划总结](v2/docs/V2_PLANNING_SUMMARY.md)** - 规划概要
- **[V2 vs V3 差距分析](v2/docs/V2_VS_V3_GAP_ANALYSIS.md)** - 功能对比
- **[架构设计](v2/docs/ARCHITECTURE.md)** - 详细架构说明
- **[API 文档](v2/docs/api/api-reference.md)** - API 参考
- **[上下文压缩](v2/docs/plans/context-compression-implementation-plan.md)** - 压缩实施计划
