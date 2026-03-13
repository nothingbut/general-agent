# CLAUDE.md

这个文件为 Claude Code (claude.ai/code) 提供在本仓库中工作的指南。

## 项目概述

General Agent 是一个通用 AI Agent 系统，支持技能系统、MCP 集成、RAG 和工作流编排。项目包含两个版本：

- **Python 版本**（根目录）：功能完整，用于快速迭代和原型验证
- **Rust V2 版本**（`v2/`）：高性能重写，类型安全，适合生产环境

## 重要提示

1. **使用中文**：本项目所有对话、文档和注释都使用中文
2. **双版本协调**：修改核心逻辑时需同时考虑 Python 和 Rust 版本的一致性
3. **测试覆盖率**：保持 80% 以上的测试覆盖率

## 构建和测试命令

### Python 版本

```bash
# 安装依赖
uv pip install -e ".[dev]"          # 基础依赖
uv pip install -e ".[rag]"          # RAG 功能
uv pip install -e ".[cli]"          # TUI 界面

# 运行 Web 服务
uvicorn src.main:app --reload       # 开发模式
uvicorn src.main:app --host 0.0.0.0 --port 8000  # 生产模式

# 运行 TUI
agent --tui                         # 交互式终端界面
agent "你的问题"                    # 快速查询模式

# 测试
pytest                              # 运行所有测试
pytest --cov=src --cov-report=html  # 带覆盖率
pytest tests/test_specific.py       # 单个文件
pytest -k test_function_name        # 单个测试

# 代码检查
ruff check src/                     # Linter
ruff format src/                    # 格式化
mypy src/                           # 类型检查
```

### Rust V2 版本

```bash
# 构建
cargo build --release               # 生产构建
cargo build                         # 开发构建

# 运行 CLI
./target/release/agent new --title "会话标题"
./target/release/agent chat <session-id>
./target/release/agent --help

# 运行 TUI
cargo run -p agent-tui --example tui_demo

# 测试
cargo test                          # 所有测试
cargo test -p agent-workflow        # 特定 crate
cargo test -- --nocapture           # 显示输出
cargo test test_specific            # 单个测试

# 开发工具
cargo watch -x check -x test        # 监听文件变化
cargo fmt                           # 格式化
cargo clippy                        # Linter
```

## 项目架构

### Python 版本架构

```
src/
├── core/               # 核心组件
│   ├── router.py      # 消息路由（@skill, /command, plain text）
│   ├── executor.py    # 执行引擎
│   ├── context.py     # 上下文管理
│   └── llm_client.py  # LLM 客户端（支持 Anthropic 和 Ollama）
├── skills/            # 技能系统
│   ├── models.py      # 技能模型定义
│   ├── parser.py      # YAML + Markdown 解析
│   ├── loader.py      # 技能加载（支持 .ignore）
│   ├── registry.py    # 技能注册表
│   └── executor.py    # 技能执行器
├── mcp/               # MCP 集成
│   ├── config.py      # 配置系统
│   ├── connection_manager.py  # 连接管理（懒加载单例）
│   ├── security.py    # 三层安全系统（allowed/denied/confirm）
│   └── tool_executor.py       # 工具执行器
├── rag/               # RAG 系统
│   ├── loaders/       # 文档加载器（Markdown, PDF）
│   ├── storage/       # 向量存储（ChromaDB）
│   ├── retrieval/     # 检索器（混合检索）
│   └── query.py       # 查询重写和路由
├── workflow/          # 工作流系统
│   ├── orchestrator.py    # DAG 编排器
│   ├── executor.py        # 任务执行引擎
│   ├── approval.py        # 审批管理
│   ├── notification.py    # 通知系统
│   └── performance/       # 性能监控
├── storage/           # 数据持久化
│   ├── database.py    # SQLite 操作
│   └── models.py      # 数据模型
├── api/               # API 路由
└── cli/               # CLI 工具
```

### Rust V2 架构

```
v2/crates/
├── agent-core         # 核心模型、Traits、错误类型
├── agent-storage      # SQLite 持久化（SQLx）
├── agent-llm          # LLM 客户端（Anthropic + Ollama 流式）
├── agent-skills       # 技能系统（加载、注册、执行）
├── agent-mcp          # MCP 协议（JSON-RPC + Stdio）
├── agent-rag          # RAG 检索（Qdrant + Ollama Embedding）
├── agent-workflow     # 业务逻辑（SessionManager + ConversationFlow）
├── agent-cli          # 命令行工具
├── agent-tui          # 终端 UI（Ratatui）
└── agent-api          # Web API 服务（计划中）
```

**分层设计原则**：
- 界面层（agent-cli, agent-tui）依赖业务层
- 业务层（agent-workflow）集成各个功能模块
- 功能模块（agent-llm, agent-mcp, agent-rag）依赖核心层
- 核心层（agent-core, agent-storage）无外部依赖

### 关键设计模式

1. **技能系统**：
   - YAML frontmatter 定义参数
   - Markdown 正文作为提示词模板
   - 支持 `@skill` 和 `/skill` 两种调用语法
   - 命名空间解析（personal/greeting → @personal:greeting）

2. **MCP 集成**：
   - JSON-RPC 2.0 协议
   - Stdio 传输层（启动真实 MCP 服务器）
   - 三层安全系统：allowed/denied/confirm
   - 路径白名单防止目录遍历

3. **RAG 系统**：
   - 文档加载 → 分块 → Embedding → 向量存储
   - 混合检索：关键词（BM25）+ 语义（向量相似度）
   - 查询重写和路由
   - 引用生成和验证

4. **Workflow 系统**：
   - DAG 依赖解析和并行执行
   - 任务执行引擎（重试、超时、取消、暂停/恢复）
   - 审批管理（Manual/Auto/Threshold）
   - 多渠道通知（终端/桌面/日志）

5. **Subagent 系统**：
   - 并行任务执行（每个子代理独立会话）
   - 实时监控（Ctrl+S 切换 Overlay）
   - 生命周期管理（Pending → Running → Completed/Failed）

## LLM 配置

### 使用 Ollama（推荐用于开发）

```bash
# 安装和启动
ollama pull qwen3.5:0.8b           # Python 版本默认模型
ollama pull nomic-embed-text       # RAG Embedding 模型
ollama serve

# 配置环境变量
export USE_OLLAMA=true
export OLLAMA_MODEL=qwen3.5:0.8b
export OLLAMA_BASE_URL=http://localhost:11434
```

### 使用 Anthropic Claude

```bash
export ANTHROPIC_API_KEY=sk-ant-xxx
export USE_OLLAMA=false  # 或不设置
```

## 技能系统

### 技能定义格式

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

你好 {user_name}！今天有什么我可以帮助你的吗？
```

### 技能调用语法

```bash
# @ 语法（推荐）
@greeting user_name='Alice'
@personal:reminder task='买牛奶' time='5pm'

# / 语法（命令风格）
/greeting user_name='Bob'
/productivity:task title='Review PR' priority='high'
```

### 技能目录结构

```
skills/
├── personal/          # 个人生产力技能
│   ├── greeting.md
│   ├── reminder.md
│   └── note.md
├── productivity/      # 工作任务管理
│   ├── task.md
│   └── meeting.md
└── .ignore           # 忽略模式（类似 .gitignore）
```

## 测试策略

### Python 测试

- **单元测试**：`tests/skills/`, `tests/storage/`, `tests/rag/`
- **集成测试**：`tests/test_e2e.py`, `tests/test_mcp_e2e.py`
- **验收测试**：`acceptance_test.sh`
- **覆盖率目标**：80%+

### Rust 测试

- **单元测试**：每个 crate 的 `tests/` 目录
- **集成测试**：`agent-workflow/tests/integration_tests.rs`
- **示例程序**：`examples/tui_demo.rs`, `examples/workflow_*.py`
- **覆盖率目标**：80%+

### 运行集成测试前的准备

```bash
# Python MCP 集成测试
export MCP_ENABLED=true
pytest tests/test_mcp_e2e.py

# Rust RAG 集成测试（需要 Qdrant）
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant
cargo test -p agent-rag -- --ignored

# Workflow 审批 UI 测试
python tests/smoke_test_approval_ui.py
```

## 常见开发任务

### 添加新技能

1. 在 `skills/<namespace>/` 创建 `.md` 文件
2. 添加 YAML frontmatter 定义参数
3. 编写提示词模板（使用 `{param}` 占位符）
4. 重启服务即可使用（自动热加载）

### 添加新的 MCP 服务器

1. 编辑 `config/mcp_config.yaml`：
```yaml
servers:
  - name: my_server
    command: /path/to/mcp-server
    args: ["--arg1", "value1"]
    allowed_tools: ["tool1", "tool2"]
    allowed_paths: ["/safe/path"]
```

2. 重启服务

### 添加新的 Rust Crate

1. 在 `v2/Cargo.toml` 的 `[workspace.members]` 中添加
2. 创建 crate：`cargo new --lib crates/agent-newfeature`
3. 添加依赖到 `v2/Cargo.toml` 的 `[workspace.dependencies]`
4. 在新 crate 中引用工作空间依赖

### 调试 TUI 问题

```bash
# Python TUI（Textual）
agent --tui --dev  # 开发模式（显示调试信息）

# Rust TUI（Ratatui）
RUST_LOG=debug cargo run -p agent-tui --example tui_demo
```

### 性能分析

```bash
# Python
pytest --durations=10                    # 显示最慢的 10 个测试

# Rust
cargo build --release                    # 优化构建
cargo flamegraph -p agent-cli            # 生成火焰图（需要安装 flamegraph）
```

## Subagent 系统使用

在 TUI 中启动并行子任务：

```bash
# 启动 TUI
agent --tui

# 在输入框中输入
/subagent start "分析代码架构" "生成API文档" "运行测试"

# 查看子代理状态
Ctrl+S  # 切换 Subagent Monitor

# 在 Monitor 中
Tab      # 切换视图（CurrentSession ↔ Global）
Up/Down  # 导航列表
Esc      # 关闭 Monitor
```

## 数据库模式

### 核心表（Python 和 Rust 共享）

```sql
-- 会话表
sessions (
  id TEXT PRIMARY KEY,
  title TEXT,
  created_at TIMESTAMP,
  updated_at TIMESTAMP,
  session_type TEXT,      -- 'Normal' | 'Subagent'
  parent_id TEXT,         -- 父会话 ID（Subagent 专用）
  status TEXT,            -- 'pending' | 'running' | 'completed' | 'failed'
  metadata TEXT           -- JSON 元数据
)

-- 消息表
messages (
  id INTEGER PRIMARY KEY,
  session_id TEXT,
  role TEXT,              -- 'user' | 'assistant' | 'system'
  content TEXT,
  timestamp TIMESTAMP,
  metadata TEXT           -- JSON 元数据
)

-- 审计日志（MCP 专用）
mcp_audit_logs (
  id INTEGER PRIMARY KEY,
  timestamp TIMESTAMP,
  server_name TEXT,
  tool_name TEXT,
  arguments TEXT,
  result TEXT,
  session_id TEXT
)
```

## 常见问题

### 1. "Module not found" 错误

确保已安装所有依赖：
```bash
uv pip install -e ".[dev,rag,cli]"  # Python
cargo build                          # Rust
```

### 2. Ollama 连接失败

检查 Ollama 服务是否运行：
```bash
ollama list
curl http://localhost:11434/api/tags
```

### 3. MCP 工具调用失败

检查配置文件和权限：
```bash
cat config/mcp_config.yaml
export MCP_ENABLED=true
```

### 4. RAG 检索无结果

确保文档已索引：
```bash
# Python
python -m src.rag.ingest --collection docs ./path/to/docs

# Rust
./target/release/agent rag index --collection docs ./path/to/docs
```

### 5. Rust 编译错误

清理构建缓存：
```bash
cargo clean
cargo build
```

## 相关文档

- 技能系统：`docs/skills.md`
- MCP 集成：`docs/mcp.md`
- RAG 使用：`docs/RAG_GUIDE.md`
- TUI 指南：`docs/tui.md`
- Subagent 系统：`docs/features/subagent-system.md`
- Workflow 系统：`docs/plans/2026-03-06-phase7-agent-workflow.md`
- Rust V2 架构：`v2/docs/ARCHITECTURE.md`
- 开发指南：`v2/docs/DEVELOPMENT.md`
