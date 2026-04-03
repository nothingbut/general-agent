# General Agent

通用Agent系统，支持：
- ✅ **Skill System**（技能系统，已完成）
- ✅ **MCP Integration**（Model Context Protocol，已完成）
- ✅ **RAG**（检索增强生成，已完成）

## 功能特性

### Phase 1: Foundation ✅
- 基础聊天功能
- 会话上下文管理
- 简洁Web界面
- SQLite持久化存储

### Phase 2: Skill System ✅
- 技能定义（YAML + Markdown）
- 技能加载器（.ignore支持）
- 技能注册表（命名空间解析）
- 技能执行器（参数验证、LLM集成）
- Router集成（@skill 和 /skill 语法）
- 5个示例技能（personal, productivity）

### Phase 3: MCP Integration ✅
- MCP SDK集成（mcp>=0.9.0）
- 配置系统（YAML配置文件）
- 连接管理器（懒加载单例模式）
- 三层安全系统（allowed/denied/confirm）
- 路径白名单（防止目录遍历）
- 工具执行器（发现和缓存）
- 审计日志（数据库持久化）
- Filesystem MCP服务器支持

### Phase 4: Real MCP Server Connections ✅
- stdio_client集成（启动真实MCP服务器）
- AsyncExitStack上下文管理
- 健康检查和自动恢复
- 优雅关闭处理
- ClientSession API集成

### Phase 5: RAG Engine ✅
- 文档管理系统（Markdown/PDF/Text）
- 混合检索引擎（关键词+语义）
- 查询重写与路由
- 引用生成和验证
- Web界面集成

### Phase 6: TUI 终端界面 ✅
- 命令行快速查询模式
- 交互式 TUI 界面（基于 Textual）
- 会话管理（创建、切换、列表）
- 与 Web 完全共享会话数据

### Phase 7: Agent Workflow System ✅
- 工作流编排器（DAG 依赖解析和并行执行）
- 任务执行引擎（重试、超时、取消、暂停/恢复）
- 条件执行、动态任务生成、优先级调度
- 审批管理系统（Manual/Auto/Threshold 策略）
- Rich TUI 审批界面（多选项交互）
- 多渠道通知系统（终端/桌面/日志）
- 完整性能监控框架（指标收集、链路追踪）
- Markdown/JSON 报告生成器
- 智能告警系统（6 种告警规则）
- 实时监控面板（实时/快照/摘要模式）

详细文档见：
- [工作流系统设计](docs/plans/2026-03-06-phase7-agent-workflow.md)
- [监控面板设计](docs/plans/2026-03-09-monitoring-dashboard.md)
- [审批 UI 文档](docs/workflow/approval-ui.md)
- [通知系统文档](docs/workflow/notification-system.md)
- [集成测试文档](docs/workflow/integration-tests.md)

### 未来版本
- 高级功能（Phase 8+）

## 快速开始

### 1. 安装依赖

```bash
# 使用uv（推荐）
uv pip install -e ".[dev]"

# 或使用pip
pip install -e ".[dev]"
```

### 2. 配置 LLM（可选）

```bash
# 使用本地 Ollama 模型（推荐）
cp .env.example .env
# 编辑 .env，设置 USE_OLLAMA=true

# 详细配置指南：docs/OLLAMA_SETUP.md
```

### 3. 运行测试

```bash
pytest
```

### 4. 启动服务

```bash
uvicorn src.main:app --reload
```

访问 http://localhost:8000

### 5. 使用 TUI（终端界面）

**安装 CLI 依赖：**
```bash
pip install -e ".[cli]"
```

**快速查询模式：**
```bash
agent "你的问题"
```

**交互式 TUI 模式：**
```bash
agent --tui
```

**快捷键：**
- `Enter` - 发送消息
- `Ctrl+N` - 新建会话
- `Ctrl+L` - 会话列表
- `Ctrl+K` - 清屏
- `Ctrl+Q` - 退出

详细使用指南：[docs/tui.md](docs/tui.md)

### 6. 使用技能

```bash
# 简单问候
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "test"}'

# 创建提醒（带参数）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Buy milk\" time=\"5pm\"", "session_id": "test"}'
```

查看所有可用技能：
```bash
ls skills/personal/
ls skills/productivity/
```

详细文档见：[docs/skills.md](docs/skills.md)

### 7. 使用 MCP 工具

**前提条件：**
1. 配置 `config/mcp_config.yaml`（参考 docs/mcp.md）
2. 设置环境变量：`export MCP_ENABLED=true`
3. 安装 Node.js（用于 MCP filesystem server）

**使用示例：**

```bash
# 读取文件
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@mcp:filesystem:read_file path=\"/tmp/test.txt\"", "session_id": "test"}'

# 列出目录
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@mcp:filesystem:list_directory path=\"/tmp\"", "session_id": "test"}'
```

**安全特性：**
- 三层权限系统（allowed/denied/confirm）
- 目录白名单保护
- 路径遍历防护
- 审计日志记录

详细文档见：[docs/mcp.md](docs/mcp.md)

### 8. API文档

启动服务后访问：
- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

## API使用

### 聊天接口

```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Hello",
    "session_id": "optional-session-id"
  }'
```

响应：
```json
{
  "response": "我收到了你的消息：「Hello」...",
  "session_id": "sess-abc123",
  "plan_type": "simple_query"
}
```

## 项目结构

```
general-agent/
├── src/
│   ├── core/          # 核心模块（router, executor, context, llm_client）
│   ├── skills/        # 技能系统（models, parser, loader, registry, executor）
│   ├── mcp/           # MCP集成（config, connection_manager, security, tool_executor）
│   ├── storage/       # 存储层（database, models）
│   ├── api/           # API路由
│   └── main.py        # 应用入口
├── skills/            # 技能定义
│   ├── personal/      # 个人生产力技能
│   ├── productivity/  # 工作和任务管理技能
│   └── .ignore        # 忽略模式
├── config/            # 配置文件
│   └── mcp_config.yaml # MCP服务器配置
├── tests/             # 测试（150+个测试，80%+覆盖率）
├── templates/         # HTML模板
├── static/            # 静态资源
└── docs/              # 文档
```

## 开发

### 运行测试（带覆盖率）

```bash
pytest --cov=src --cov-report=html
```

### 代码检查

```bash
ruff check src/
mypy src/
```

## 文档

- 📖 **[技能系统文档](docs/skills.md)** - 完整的技能系统指南
- 🖥️ **[TUI 使用指南](docs/tui.md)** - 终端界面用户指南
- 📚 **[RAG 使用指南](docs/RAG_GUIDE.md)** - 检索增强生成用户指南
- 🔌 **[MCP集成文档](docs/mcp.md)** - MCP集成用户指南
- 🤖 **[Ollama配置指南](docs/OLLAMA_SETUP.md)** - 本地模型配置
- ✅ **[验收测试指南](docs/ACCEPTANCE_TEST.md)** - Phase 1&2 验收
- 📋 **[设计文档](docs/plans/2026-03-02-general-agent-design.md)** - 系统架构设计
- 🚀 **[Phase 1 计划](docs/plans/2026-03-02-phase1-foundation.md)** - 基础设施实现
- 🔧 **[Phase 3 计划](docs/plans/2026-03-04-mcp-integration.md)** - MCP集成实现

## 技能系统快速入门

技能系统允许您定义可复用的提示词模板，并通过简单的语法调用：

```bash
# 使用技能
@greeting                                    # 简单问候
@reminder task='Buy milk' time='5pm'        # 创建提醒
@note content='Meeting notes' category='work'  # 记笔记
@task title='Review PR' priority='high'     # 任务管理
```

**创建自定义技能：**
1. 在 `skills/` 目录创建 `.md` 文件
2. 添加 YAML frontmatter 定义参数
3. 编写提示词模板（使用 `{param}` 占位符）
4. 重启服务即可使用

详见：[技能系统文档](docs/skills.md)

## 许可证

MIT
