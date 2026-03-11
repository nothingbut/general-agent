# Phase 6: TUI 终端界面设计文档

**日期：** 2026-03-05
**版本：** 1.0
**状态：** 已批准
**预计工期：** 8-10 天

---

## 1. 项目概述

### 1.1 目标

为 General Agent 添加终端用户界面（TUI），提供：
- 命令行快速查询模式（一次性查询）
- 交互式 TUI 模式（持续对话）
- 与 Web 界面完全共享会话和数据

### 1.2 设计原则

- **并存互补** - TUI 与 Web 界面并存，用户根据场景选择
- **共享核心** - 复用现有核心模块，不重复实现业务逻辑
- **会话共享** - TUI 和 Web 使用相同数据库，会话完全互通
- **体验优先** - 快速响应、友好提示、优雅降级

---

## 2. 整体架构

### 2.1 架构图

```
┌──────────────────────────────────────────────────────┐
│  命令行入口 (src/cli/__main__.py)                      │
│  ├─ 解析参数 (Typer)                                  │
│  └─ 路由到对应模式                                     │
└──────┬───────────────────────────────┬───────────────┘
       │                               │
   ┌───▼────────┐              ┌───────▼──────────┐
   │ 命令行模式   │              │  TUI 模式        │
   │ (Quick)    │              │  (Interactive)   │
   │ • 打印结果  │              │  • Textual App   │
   │ • 退出     │              │  • 会话管理      │
   └───┬────────┘              └───────┬──────────┘
       │                               │
       └───────────┬───────────────────┘
                   │
           ┌───────▼──────────┐
           │  共享核心模块      │
           │  • AgentExecutor │
           │  • Database      │
           │  • Router        │
           │  • LLM Client    │
           │  • Skills/MCP    │
           │  • RAG Engine    │
           └──────────────────┘
                   │
           ┌───────▼──────────┐
           │  SQLite 数据库    │
           │  data/           │
           │  general_agent.db│
           └──────────────────┘
```

### 2.2 双模式设计

**模式切换规则：**
```bash
agent [query]          # 命令行模式：打印结果后退出
agent --tui            # TUI 模式：进入交互式界面
agent --tui --session=xxx  # TUI 模式：加载指定会话
```

**设计理由：**
- 明确的行为区分（`--tui` 标志）
- 默认快速查询，满足"快速问答"场景
- TUI 模式满足"长期会话"需求

---

## 3. 组件设计

### 3.1 命令行入口 (`src/cli/__main__.py`)

**职责：**
- 解析命令行参数
- 路由到对应模式（Quick / TUI）
- 初始化日志和配置

**接口设计：**
```python
import typer
from typing import Optional

cli = typer.Typer(
    name="agent",
    help="General Agent CLI - 智能助理命令行工具"
)

@cli.command()
def main(
    query: Optional[str] = typer.Argument(
        None,
        help="查询内容（命令行模式）"
    ),
    tui: bool = typer.Option(
        False,
        "--tui",
        help="启动交互式 TUI 界面"
    ),
    session: Optional[str] = typer.Option(
        None,
        "--session",
        help="指定会话 ID"
    ),
    verbose: bool = typer.Option(
        False,
        "--verbose",
        "-v",
        help="显示详细日志"
    )
):
    """General Agent - 智能助理"""

    if tui:
        # 进入 TUI 模式
        from .app import run_tui
        run_tui(session, verbose)
    elif query:
        # 快速查询模式
        from .quick import run_quick_query
        result = asyncio.run(run_quick_query(query, session, verbose))
        typer.echo(result)
    else:
        # 无参数，显示帮助
        typer.echo("用法：")
        typer.echo("  agent '你的问题'     # 快速查询")
        typer.echo("  agent --tui          # 交互式界面")
        raise typer.Exit(0)

if __name__ == "__main__":
    cli()
```

---

### 3.2 快速查询模式 (`src/cli/quick.py`)

**职责：**
- 接收用户查询
- 调用 AgentExecutor 执行
- 打印结果后退出

**实现要点：**
```python
async def run_quick_query(
    query: str,
    session_id: Optional[str],
    verbose: bool
) -> str:
    """快速查询模式"""

    # 1. 初始化核心组件
    db = await initialize_database()
    executor = await initialize_executor(db, verbose)

    # 2. 生成或使用会话 ID
    if not session_id:
        session_id = f"cli-{uuid.uuid4()}"

    try:
        # 3. 执行查询
        result = await executor.execute(query, session_id)

        # 4. 返回响应
        return result["response"]

    finally:
        # 5. 清理资源
        await db.close()
```

**错误处理：**
- Ollama 连接失败 → 显示错误和解决建议
- 超时 → 显示超时提示
- 其他异常 → 显示错误详情

---

### 3.3 TUI 应用 (`src/cli/app.py`)

**职责：**
- 提供交互式对话界面
- 管理会话（创建、切换、列表）
- 实时显示消息流

**界面布局：**

```
┌─────────────────────────────────────────────────────┐
│ General Agent - Session: session-abc123       [Help]│ <- Header
├─────────────────────────────────────────────────────┤
│ 🧑 User: 今天天气如何？                              │
│                                                     │
│ 🤖 Agent: 我无法实时获取天气信息，但我可以...        │
│                                                     │
│ 🧑 User: 帮我总结这个文档                            │
│                                                     │
│ 🤖 Agent: [RAG] 正在检索文档...                      │
│                                                     │ <- MessageList (可滚动)
│         文档主要讨论了以下内容：                      │
│         1. ...                                      │
│         2. ...                                      │
│                                                     │
├─────────────────────────────────────────────────────┤
│ > 输入消息... _                                      │ <- Input
├─────────────────────────────────────────────────────┤
│ Enter=发送 Ctrl+N=新会话 Ctrl+L=会话列表 Ctrl+Q=退出 │ <- Footer
└─────────────────────────────────────────────────────┘
```

**Textual App 结构：**
```python
from textual.app import App, ComposeResult
from textual.widgets import Header, Footer, Input
from .widgets import MessageList, SessionList

class AgentTUI(App):
    """General Agent TUI 主应用"""

    CSS_PATH = "app.css"
    BINDINGS = [
        ("ctrl+q", "quit", "退出"),
        ("ctrl+n", "new_session", "新会话"),
        ("ctrl+l", "list_sessions", "会话列表"),
        ("ctrl+k", "clear_screen", "清屏"),
    ]

    def __init__(self, session_id: Optional[str] = None):
        super().__init__()
        self.session_id = session_id
        self.db = None
        self.executor = None

    async def on_mount(self) -> None:
        """应用启动时初始化"""
        # 初始化核心组件
        self.db = await initialize_database()
        self.executor = await initialize_executor(self.db)

        # 加载会话
        if self.session_id:
            await self.load_session(self.session_id)
        else:
            await self.show_session_list()

    def compose(self) -> ComposeResult:
        """组装界面组件"""
        yield Header()
        yield MessageList(id="messages")
        yield Input(placeholder="输入消息...", id="input")
        yield Footer()

    async def on_input_submitted(self, event: Input.Submitted) -> None:
        """处理用户输入"""
        message = event.value
        if not message.strip():
            return

        # 清空输入框
        self.query_one("#input").value = ""

        # 添加用户消息到界面
        message_list = self.query_one("#messages")
        message_list.add_message("user", message)

        # 显示"正在思考"提示
        message_list.add_thinking_indicator()

        try:
            # 执行查询
            result = await self.executor.execute(message, self.session_id)

            # 移除"正在思考"提示
            message_list.remove_thinking_indicator()

            # 添加 Agent 响应
            message_list.add_message("agent", result["response"])

        except Exception as e:
            # 错误处理
            message_list.remove_thinking_indicator()
            message_list.add_error(str(e))

    async def action_new_session(self) -> None:
        """创建新会话"""
        self.session_id = f"session-{uuid.uuid4()}"
        await self.load_session(self.session_id)
        self.query_one("#messages").clear()
        self.notify("已创建新会话")

    async def action_list_sessions(self) -> None:
        """显示会话列表"""
        await self.push_screen(SessionList(self.db))
```

---

### 3.4 自定义组件

#### MessageList (`src/cli/widgets/message_list.py`)

**职责：**
- 显示消息历史
- 支持滚动、复制
- 区分用户/Agent 消息

**关键功能：**
```python
class MessageList(Static):
    """消息列表组件"""

    def add_message(self, role: str, content: str) -> None:
        """添加消息"""
        # 根据角色选择图标和样式
        icon = "🧑" if role == "user" else "🤖"
        style = "user-message" if role == "user" else "agent-message"

        # 添加到界面
        message = Text(f"{icon} {role.title()}: {content}")
        self.mount(Label(message, classes=style))

        # 自动滚动到底部
        self.scroll_end()

    def add_thinking_indicator(self) -> None:
        """显示"正在思考"动画"""
        self.mount(Label("🤖 Agent: 正在思考... ", id="thinking"))

    def remove_thinking_indicator(self) -> None:
        """移除"正在思考"动画"""
        try:
            self.query_one("#thinking").remove()
        except:
            pass
```

#### SessionList (`src/cli/widgets/session_list.py`)

**职责：**
- 显示所有会话
- 支持选择/创建会话

**界面：**
```
┌─────────────────────────────────────────────────────┐
│ 选择会话                                      [Esc 返回]│
├─────────────────────────────────────────────────────┤
│ ● session-abc123  "今天天气如何？"  (5分钟前)        │
│   session-def456  "帮我总结文档"   (1小时前)         │
│   session-ghi789  "创建提醒"       (昨天)            │
│                                                     │
│ [N] 新建会话                                         │
└─────────────────────────────────────────────────────┘
```

---

## 4. 数据流设计

### 4.1 TUI 消息流

```
用户输入 "你好"
    ↓
Input.on_submitted()
    ↓
AgentTUI.on_input_submitted()
    ↓
MessageList.add_message("user", "你好")
    ↓
AgentExecutor.execute("你好", session_id)
    ↓
Router → 判断意图 → 选择处理器
    ↓
[Skill / MCP / RAG / Simple]
    ↓
LLMClient.generate()
    ↓
Database.save_message()
    ↓
返回响应 {"response": "你好！有什么我可以帮助你的吗？"}
    ↓
MessageList.add_message("agent", response)
    ↓
界面更新
```

### 4.2 会话管理流程

```
启动 TUI
    ↓
检查 --session 参数
    ↓
    ├─ 有参数 → 加载指定会话
    │   ↓
    │   从 Database 读取历史消息
    │   ↓
    │   显示在 MessageList
    │
    └─ 无参数 → 显示会话列表
        ↓
        用户选择会话或创建新会话
        ↓
        进入对话循环
```

### 4.3 数据库共享

**会话表（sessions）：**
```sql
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    title TEXT,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);
```

**消息表（messages）：**
```sql
CREATE TABLE messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT,
    role TEXT,  -- 'user' or 'assistant'
    content TEXT,
    timestamp TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);
```

**TUI 和 Web 完全共享：**
- 同一 SQLite 文件：`data/general_agent.db`
- 同一会话 ID 格式
- SQLite 文件锁自动处理并发

---

## 5. 错误处理

### 5.1 启动检查

```python
async def startup_checks() -> None:
    """TUI 启动前的检查"""

    # 1. 检查数据库
    db_path = Path("data/general_agent.db")
    if not db_path.parent.exists():
        typer.echo("❌ 数据目录不存在，正在创建...")
        db_path.parent.mkdir(parents=True)

    # 2. 检查 Ollama（如果配置了）
    if use_ollama:
        try:
            await check_ollama_connection()
        except ConnectionError:
            typer.echo("❌ 无法连接到 Ollama 服务")
            typer.echo("💡 提示：运行 'ollama serve' 启动服务")
            typer.echo("💡 或在 .env 中设置 USE_OLLAMA=false 使用 Mock 模式")
            raise typer.Exit(1)

    # 3. 检查配置文件
    if not Path(".env").exists():
        typer.echo("⚠️  .env 文件不存在，使用默认配置")
```

### 5.2 运行时错误处理

| 错误类型 | 触发条件 | 处理方式 |
|---------|---------|---------|
| **Ollama 连接失败** | 服务未启动 | 在消息列表显示错误，提示启动 Ollama |
| **数据库锁定** | TUI 和 Web 同时写入 | 自动重试（最多 3 次，间隔 100ms） |
| **网络超时** | LLM 响应慢 | 显示"等待响应..."，允许 Ctrl+C 取消 |
| **会话不存在** | 无效 session_id | 提示并创建新会话 |
| **未知异常** | 其他错误 | 捕获、记录日志、显示错误详情 |

### 5.3 优雅退出

```python
async def on_shutdown(self) -> None:
    """应用关闭时清理资源"""
    try:
        # 保存当前状态
        await self.save_state()

        # 关闭数据库
        if self.db:
            await self.db.close()

        # 显示再见消息
        self.notify("再见！👋")

    except Exception as e:
        logger.error(f"Shutdown error: {e}")
```

---

## 6. 测试策略

### 6.1 单元测试

**测试文件结构：**
```
tests/cli/
├── __init__.py
├── test_commands.py      # 命令行参数解析
├── test_quick.py         # 快速查询模式
├── test_app.py           # TUI 应用逻辑
└── test_widgets.py       # 自定义组件
```

**测试用例：**

**test_commands.py:**
```python
def test_parse_quick_query():
    """测试快速查询参数解析"""
    result = runner.invoke(cli, ["你好"])
    assert result.exit_code == 0

def test_parse_tui_mode():
    """测试 TUI 模式参数解析"""
    # 模拟测试（不真正启动 TUI）
    pass

def test_parse_session_option():
    """测试 --session 选项"""
    pass
```

**test_quick.py:**
```python
@pytest.mark.asyncio
async def test_quick_query_success():
    """测试快速查询成功"""
    result = await run_quick_query("测试", None, False)
    assert isinstance(result, str)
    assert len(result) > 0

@pytest.mark.asyncio
async def test_quick_query_with_session():
    """测试指定会话的快速查询"""
    result = await run_quick_query("测试", "test-session", False)
    assert result is not None
```

### 6.2 集成测试

```python
@pytest.mark.asyncio
async def test_tui_web_session_sharing():
    """测试 TUI 和 Web 会话共享"""

    # 1. TUI 创建会话和消息
    session_id = "test-shared-session"
    # ... TUI 发送消息 ...

    # 2. 通过 Web API 读取同一会话
    async with httpx.AsyncClient() as client:
        response = await client.get(f"/api/sessions/{session_id}")
        assert response.status_code == 200
        messages = response.json()["messages"]
        assert len(messages) > 0
```

### 6.3 手动测试清单

**Phase 6 完成前需验证：**

- [ ] **命令行模式**
  - [ ] `agent "测试查询"` 正常返回
  - [ ] `agent "长文本查询..."` 正常换行显示
  - [ ] Ollama 未启动时显示友好错误

- [ ] **TUI 模式**
  - [ ] `agent --tui` 正常启动界面
  - [ ] 发送消息正常显示和响应
  - [ ] 长消息自动换行和滚动
  - [ ] "正在思考"动画正常显示和消失

- [ ] **会话管理**
  - [ ] Ctrl+N 创建新会话
  - [ ] Ctrl+L 显示会话列表
  - [ ] 切换会话正常加载历史
  - [ ] `agent --tui --session=xxx` 加载指定会话

- [ ] **快捷键**
  - [ ] Enter 发送消息
  - [ ] Ctrl+K 清屏
  - [ ] Ctrl+Q 退出
  - [ ] Esc 关闭弹窗

- [ ] **会话共享**
  - [ ] TUI 创建会话 → Web 端可见
  - [ ] Web 创建会话 → TUI 可加载
  - [ ] 消息完全同步

- [ ] **错误处理**
  - [ ] 网络超时友好提示
  - [ ] 无效会话 ID 自动处理
  - [ ] 未捕获异常不崩溃

### 6.4 覆盖率目标

- **核心逻辑** - ≥ 80%
- **TUI 组件** - ≥ 70%（部分依赖手动测试）
- **命令行入口** - 100%

---

## 7. 技术细节

### 7.1 依赖更新

**pyproject.toml:**
```toml
[project.optional-dependencies]
cli = [
    "textual>=0.47.0",      # TUI 框架
    "typer>=0.9.0",         # 命令行参数解析
    "rich>=13.7.0",         # 终端美化（Textual 依赖）
]
```

**安装命令：**
```bash
pip install -e ".[cli]"
# 或完整安装
pip install -e ".[dev,rag,cli]"
```

### 7.2 脚本入口

**pyproject.toml:**
```toml
[project.scripts]
agent = "src.cli.__main__:main"
```

安装后可直接使用 `agent` 命令。

### 7.3 配置共享

TUI 与 Web 共享所有配置：

- `.env` - 环境变量
  ```
  USE_OLLAMA=true
  OLLAMA_BASE_URL=http://localhost:11434
  OLLAMA_MODEL=llama3.2:latest
  OLLAMA_TIMEOUT=120.0
  MCP_ENABLED=true
  ```

- `config/mcp_config.yaml` - MCP 配置
- `config/rag_config.yaml` - RAG 配置
- `data/general_agent.db` - SQLite 数据库

### 7.4 样式定制

**app.css (Textual CSS):**
```css
/* 用户消息 */
.user-message {
    background: #2e3440;
    color: #88c0d0;
    padding: 1;
    margin: 1 0;
}

/* Agent 消息 */
.agent-message {
    background: #3b4252;
    color: #a3be8c;
    padding: 1;
    margin: 1 0;
}

/* 输入框 */
Input {
    border: solid #5e81ac;
}

Input:focus {
    border: solid #88c0d0;
}

/* 会话列表 */
SessionList > .session-item:hover {
    background: #4c566a;
}
```

---

## 8. 实施计划

### Phase 6.1: 基础框架（2天）

**目标：** 搭建命令行入口和快速查询模式

**任务：**
1. 创建 `src/cli/` 目录结构
2. 实现 `__main__.py` - 命令行参数解析
3. 实现 `quick.py` - 快速查询模式
4. 实现核心组件初始化（Database、Executor）
5. 添加基础测试（`test_commands.py`, `test_quick.py`）
6. 更新 `pyproject.toml` - 添加依赖和脚本入口

**验收标准：**
- ✅ `agent "测试"` 返回正确响应
- ✅ `agent --help` 显示帮助信息
- ✅ 测试覆盖率 ≥ 80%

---

### Phase 6.2: TUI 核心（3天）

**目标：** 实现 Textual 交互界面和消息流

**任务：**
1. 实现 `app.py` - Textual App 主框架
2. 实现 `widgets/message_list.py` - 消息列表组件
3. 实现消息发送/接收流程
4. 添加"正在思考"动画
5. 实现基础样式（`app.css`）
6. 添加 TUI 测试（`test_app.py`）

**验收标准：**
- ✅ `agent --tui` 启动界面
- ✅ 发送消息正常响应
- ✅ 消息列表滚动正常
- ✅ 样式美观

---

### Phase 6.3: 会话管理（2天）

**目标：** 实现会话创建、切换、列表功能

**任务：**
1. 实现 `widgets/session_list.py` - 会话选择器
2. 实现会话创建（Ctrl+N）
3. 实现会话切换逻辑
4. 实现会话历史加载
5. 实现 `--session` 参数支持
6. 添加会话管理测试

**验收标准：**
- ✅ Ctrl+N 创建新会话
- ✅ Ctrl+L 显示会话列表
- ✅ 会话切换正常
- ✅ `--session` 参数工作

---

### Phase 6.4: 完善与测试（2天）

**目标：** 完善功能、错误处理、文档

**任务：**
1. 实现所有快捷键（Ctrl+K, Ctrl+Q 等）
2. 完善错误处理（启动检查、运行时错误）
3. 添加集成测试（TUI ↔ Web 会话共享）
4. 手动测试所有功能
5. 更新文档（README.md、用户指南）
6. 代码审查和优化

**验收标准：**
- ✅ 所有快捷键工作
- ✅ 错误提示友好
- ✅ 集成测试通过
- ✅ 文档完整
- ✅ 代码覆盖率 ≥ 80%

---

## 9. 验收标准

### 9.1 功能验收

- [ ] **命令行模式**
  - [ ] 快速查询返回正确响应
  - [ ] 错误友好提示
  - [ ] 支持 `--session` 参数

- [ ] **TUI 模式**
  - [ ] 界面美观、响应流畅
  - [ ] 消息发送/接收正常
  - [ ] 会话管理功能完整
  - [ ] 快捷键全部工作

- [ ] **会话共享**
  - [ ] TUI ↔ Web 完全共享
  - [ ] 数据库并发访问正常

### 9.2 质量验收

- [ ] 测试覆盖率 ≥ 80%
- [ ] 所有测试通过
- [ ] 无已知 Bug
- [ ] 代码符合项目规范（Ruff、MyPy 检查通过）

### 9.3 文档验收

- [ ] README.md 更新（添加 TUI 使用说明）
- [ ] 创建 TUI 用户指南（`docs/tui.md`）
- [ ] API 文档更新（如有变更）

---

## 10. 风险评估

| 风险 | 影响 | 概率 | 缓解措施 |
|-----|------|------|---------|
| Textual 学习曲线 | 中 | 中 | 先阅读官方文档和示例，从简单组件开始 |
| 并发访问冲突 | 低 | 低 | SQLite 文件锁机制，加上读写分离 |
| 性能问题 | 低 | 低 | 异步处理，避免阻塞主线程 |
| 跨平台兼容性 | 低 | 低 | Textual 已处理跨平台问题 |
| 终端大小限制 | 低 | 中 | 响应式布局，最小尺寸警告 |

---

## 11. 未来扩展

Phase 6 完成后可考虑的增强功能：

1. **富文本支持** - Markdown 渲染、语法高亮
2. **多会话标签** - 类似浏览器标签，快速切换
3. **配置界面** - TUI 内修改设置（模型、超时等）
4. **插件管理** - TUI 内查看/启用/禁用 Skills
5. **主题切换** - 暗色/亮色主题
6. **导出功能** - 导出会话为 Markdown/PDF

---

## 12. 总结

Phase 6 将为 General Agent 添加完整的 TUI 支持，提供：

✅ **双模式** - 快速查询 + 交互式界面
✅ **会话共享** - 与 Web 完全互通
✅ **优雅体验** - 友好提示、快捷键、美观样式
✅ **可靠性** - 完整测试、错误处理

预计工期：**8-10 天**
实施顺序：**基础框架 → TUI 核心 → 会话管理 → 完善测试**

---

**文档创建时间：** 2026-03-05
**下一步：** 创建详细实施计划（`phase6-implementation-plan.md`）
