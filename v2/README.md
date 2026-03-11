# General Agent V2 🦀

> 高性能、类型安全的 AI Agent 框架，使用 Rust 构建

**状态:** ✅ Phase 2 Stage 4-5 完成（MCP + RAG 集成）
**版本:** 0.2.0
**更新日期:** 2026-03-11

---

## ✨ 特性

### 核心功能
- 🚀 **高性能** - Rust 原生性能，零成本抽象
- 🔒 **类型安全** - 编译时类型检查，消除运行时错误
- 🎯 **多 LLM 支持** - Anthropic Claude + Ollama 本地模型
- 💬 **流式响应** - 实时流式输出，更好的用户体验
- 💾 **持久化** - SQLite 数据库，完整的会话历史
- 📦 **单一二进制** - 无依赖部署，开箱即用

### 扩展能力
- 🧩 **技能系统** - 可复用的提示词模板，`@skill` 调用语法
- 🔌 **MCP 集成** - Model Context Protocol 工具调用支持
- 📚 **RAG 系统** - 文档检索增强生成（Qdrant + Ollama Embedding）
- 🧪 **高测试覆盖** - 46 个测试，包括集成测试，覆盖率 > 80%

---

## 🚀 快速开始

### 前置条件

- Rust 1.75+
- Ollama（如果使用本地模型）

### 安装

```bash
# 克隆仓库
git clone https://github.com/yourusername/general-agent.git
cd general-agent/v2

# 构建
cargo build --release

# 运行
./target/release/agent --help
```

### 基本使用

```bash
# 1. 启动 Ollama（使用本地模型）
ollama pull qwen3.5:0.8b
ollama serve

# 2. 创建新会话
./target/release/agent new --title "我的第一个会话"
# 输出: ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

# 3. 开始对话
./target/release/agent chat <session-id>

# 4. 使用技能系统
./target/release/agent --skills-dir ./crates/agent-skills/examples/test_skills chat <session-id>
# 在对话中输入: @greeting user_name='Alice'
```

### 使用 Anthropic Claude

```bash
export ANTHROPIC_API_KEY=sk-ant-xxx
./target/release/agent --provider anthropic chat <session-id>
```

### 使用 RAG（文档检索增强）

```bash
# 1. 启动 Qdrant 向量数据库
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 2. 启动 Ollama（用于 embedding）
ollama pull nomic-embed-text
ollama serve

# 3. 索引文档
./target/release/agent rag index --collection docs ./path/to/documents

# 4. 使用 RAG 增强的对话
./target/release/agent chat <session-id> --rag --collection docs
```

### 使用 MCP 工具

```bash
# 1. 启动 MCP 服务器
mcp-server-filesystem --root /path/to/files &

# 2. 配置 MCP
cat > mcp-config.json << EOF
{
  "servers": [
    {
      "name": "filesystem",
      "command": "mcp-server-filesystem",
      "args": ["--root", "/path/to/files"]
    }
  ]
}
EOF

# 3. 使用 MCP 工具
./target/release/agent chat <session-id> --mcp mcp-config.json
```

---

## 📖 核心概念

### 会话管理

会话是对话的容器，包含完整的消息历史：

```bash
# 创建会话
agent new --title "项目讨论"

# 列出会话
agent list --limit 10

# 搜索会话
agent search "项目"

# 删除会话
agent delete <session-id>
```

### 技能系统

技能是可复用的提示词模板，支持参数化：

**定义技能** (`greeting.md`):
```markdown
---
name: greeting
description: Greet the user
parameters:
  - name: user_name
    type: string
    required: true
---

Hello {user_name}! How can I help you today?
```

**使用技能**:
```bash
# @ 语法
@greeting user_name='Alice'

# / 语法
/greeting user_name='Bob'
```

### 流式响应

实时流式输出，更自然的对话体验：

```bash
agent chat <session-id> --stream
```

---

## 🏗️ 架构

### 分层设计

```
┌──────────────────────────────────────────────┐
│         agent-cli + agent-tui (界面层)        │
│              CLI 工具 / TUI 界面              │
├──────────────────────────────────────────────┤
│           agent-workflow (业务层)             │
│     SessionManager + ConversationFlow         │
│         (集成 Skills + MCP + RAG)            │
├─────────┬──────────┬──────────┬──────────────┤
│agent-llm│agent-mcp │agent-rag │agent-storage │
│LLM 客户端│MCP 客户端│RAG 检索器│SQLite 持久化 │
├─────────┴──────────┴──────────┴──────────────┤
│   agent-skills  │      agent-core (核心)      │
│     技能系统     │    模型 + Traits + 错误     │
└─────────────────┴────────────────────────────┘
```

### Crate 说明

| Crate | 功能 | 状态 |
|-------|------|------|
| `agent-core` | 核心模型、Traits、错误类型 | ✅ 完成 |
| `agent-storage` | SQLite 持久化层 | ✅ 完成 |
| `agent-llm` | LLM 客户端（Anthropic + Ollama） | ✅ 完成 |
| `agent-skills` | 技能系统（加载、注册、执行） | ✅ 完成 |
| `agent-workflow` | 业务逻辑（会话管理、对话流程） | ✅ 完成 |
| `agent-mcp` | MCP 协议和客户端 | ✅ 完成 |
| `agent-rag` | RAG 检索器（Qdrant + Ollama） | ✅ 完成 |
| `agent-cli` | 命令行工具 | ✅ 完成 |
| `agent-tui` | 终端 UI（Ratatui） | ⏳ 计划中 |

**测试统计:**
- agent-workflow: 36 个测试（包括 RAG + MCP 集成）
- agent-rag: 10 个测试（包括 6 个集成测试）
- 总计: 46 个测试，覆盖率 > 80%

---

## 📚 文档

- [架构设计](docs/ARCHITECTURE.md) - 详细的架构说明
- [技能系统](docs/SKILLS.md) - 技能系统使用指南
- [API 文档](docs/API.md) - 公共 API 文档
- [开发指南](docs/DEVELOPMENT.md) - 贡献指南
- [交接文档](docs/progress/session-handoff.md) - 项目交接
- [Phase 2 路线图](docs/plans/v2-phase2-roadmap.md) - 后续计划

---

## 🔧 配置

### 环境变量

```bash
# 数据库路径
export AGENT_DB=./agent.db

# LLM 提供商（anthropic/ollama）
export AGENT_PROVIDER=ollama

# Anthropic API Key
export ANTHROPIC_API_KEY=sk-ant-xxx

# Ollama 配置
export OLLAMA_MODEL=qwen3.5:0.8b
export OLLAMA_BASE_URL=http://localhost:11434
```

### 命令行参数

```bash
agent --help
agent --provider ollama --ollama-model qwen2.5:0.5b chat <id>
agent --skills-dir ./skills chat <id>
```

---

## 🧪 开发

### 运行测试

```bash
# 所有测试
cargo test

# 特定 crate
cargo test --package agent-workflow

# 带输出
cargo test -- --nocapture

# 单个测试
cargo test test_full_conversation_flow
```

### 开发模式

```bash
# 监听文件变化自动重新编译
cargo watch -x check -x test

# 运行 CLI（开发模式）
cargo run --package agent-cli -- new --title "测试"
```

### 代码格式化

```bash
cargo fmt
cargo clippy
```

---

## 📊 性能

### 测试性能

- **测试数量:** 158 个
- **测试时间:** ~1.3 秒
- **平均单测试:** ~8ms
- **并发安全:** ✅ 通过

### 运行时性能

- **启动时间:** < 100ms
- **内存占用:** < 10MB (空闲)
- **并发支持:** 原生 async/await

---

## 🗺️ 路线图

### ✅ Phase 1: 基础功能（已完成 - 2026-03-10）
- ✅ 会话管理
- ✅ LLM 集成（Anthropic + Ollama）
- ✅ 流式响应
- ✅ 技能系统
- ✅ CLI 工具
- ✅ 单元测试

### 🚧 Phase 2: 功能扩展（进行中）
- ✅ Stage 1: 集成测试（46 个测试）
- 🔄 Stage 2: 文档完善（进行中）
- ⏳ Stage 3: TUI 界面（Ratatui）
- ✅ Stage 4: MCP 集成（已完成 - 2026-03-11）
  - ✅ JSON-RPC 协议实现
  - ✅ Stdio 传输层
  - ✅ MCP 客户端
  - ✅ ConversationFlow 集成
  - ✅ 6 个集成测试
- ✅ Stage 5: RAG 系统（已完成 - 2026-03-11）
  - ✅ 文档加载器（Markdown）
  - ✅ 文本分块器
  - ✅ Ollama Embedding（768维）
  - ✅ Qdrant 向量存储
  - ✅ RAG 检索器
  - ✅ ConversationFlow 集成
  - ✅ 10 个测试（6 个集成测试）

### 📅 Phase 3: 生态扩展（计划中）
- ⏸️ Web API 服务
- ⏸️ 工作流系统
- ⏸️ 多 Agent 协作
- ⏸️ 插件系统

---

## 🤝 贡献

欢迎贡献！请查看 [DEVELOPMENT.md](docs/DEVELOPMENT.md)

### 贡献指南

1. Fork 项目
2. 创建特性分支 (`git checkout -b feature/amazing`)
3. 提交更改 (`git commit -m 'feat: add amazing feature'`)
4. 推送分支 (`git push origin feature/amazing`)
5. 创建 Pull Request

### 提交规范

使用 Conventional Commits 格式：

```
feat: 新功能
fix: 错误修复
docs: 文档更新
test: 测试相关
refactor: 代码重构
perf: 性能优化
chore: 构建/工具相关
```

---

## 📝 更新日志

### v0.2.0 (2026-03-11) - Phase 2 功能扩展

**新增**
- ✅ MCP（Model Context Protocol）完整实现
  - JSON-RPC 2.0 协议
  - Stdio 传输层
  - 工具发现和调用
  - ConversationFlow 集成
- ✅ RAG（检索增强生成）完整实现
  - Markdown 文档加载
  - 智能文本分块
  - Ollama Embedding（nomic-embed-text, 768维）
  - Qdrant 向量存储
  - 语义检索
  - ConversationFlow 集成
- ✅ 集成测试套件
  - RAG 集成测试（6个）
  - MCP 集成测试（6个）
  - ConversationFlow 扩展测试（10个）

**修复**
- 🐛 修复 Qdrant UUID 兼容性问题
- 🐛 修复 Docker 凭证配置问题

**优化**
- 📚 完整的测试文档
- 📈 测试覆盖率保持 > 80%

### v0.1.0 (2026-03-10) - Phase 1 基础功能

**新增**
- ✅ 完整的会话管理系统
- ✅ Anthropic Claude 集成
- ✅ Ollama 本地模型支持
- ✅ 流式响应（两种 LLM）
- ✅ 技能系统完整实现
- ✅ CLI 工具（5 个命令）

**优化**
- 🚀 默认模型更新为 qwen3.5:0.8b
- 📈 测试覆盖率 > 80%
- 📚 完整文档

---

## 📄 许可证

MIT License - 详见 [LICENSE](../LICENSE)

---

## 💬 联系方式

- **Issues:** [GitHub Issues](https://github.com/yourusername/general-agent/issues)
- **Discussions:** [GitHub Discussions](https://github.com/yourusername/general-agent/discussions)

---

## 🙏 致谢

感谢所有贡献者和以下开源项目：

- [Tokio](https://tokio.rs/) - 异步运行时
- [SQLx](https://github.com/launchbadge/sqlx) - SQL 工具包
- [Anthropic](https://www.anthropic.com/) - Claude API
- [Ollama](https://ollama.com/) - 本地 LLM
- [Clap](https://github.com/clap-rs/clap) - CLI 框架

---

**Built with ❤️ and 🦀 Rust**
