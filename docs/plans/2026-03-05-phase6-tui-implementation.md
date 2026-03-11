# Phase 6: TUI 终端界面 - 实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标：** 为 General Agent 添加基于 Textual 的终端用户界面，支持命令行快速查询和交互式对话

**架构：** 双模式 CLI（快速查询 + 交互式 TUI），共享现有核心模块（Database、AgentExecutor、Router 等），与 Web 界面完全共享会话数据

**技术栈：** Textual (TUI框架)、Typer (命令行解析)、Rich (终端美化)、现有核心模块

---

## Phase 6.1: 基础框架（预计2天）

### Task 6.1.1: 更新项目依赖

**Files:**
- Modify: `pyproject.toml`

**Step 1: 添加 CLI 依赖到 pyproject.toml**

```toml
[project.optional-dependencies]
dev = [
    "pytest>=8.0.0",
    "pytest-asyncio>=0.23.0",
    "pytest-cov>=4.1.0",
    "ruff>=0.2.0",
    "mypy>=1.8.0",
    "httpx>=0.26.0",
]
rag = [
    "chromadb>=0.4.22",
    "sentence-transformers>=2.3.0",
    "pypdf>=3.17.0",
    "tiktoken>=0.5.0",
    "markdown>=3.5.0",
]
cli = [
    "textual>=0.47.0",
    "typer>=0.9.0",
    "rich>=13.7.0",
]

[project.scripts]
agent = "src.cli.__main__:main"
```

**Step 2: 安装新依赖**

Run: `pip install -e ".[cli]"`
Expected: 成功安装 textual, typer, rich

**Step 3: 验证安装**

Run: `python -c "import textual; import typer; import rich; print('OK')"`
Expected: 输出 "OK"

**Step 4: 提交**

```bash
git add pyproject.toml
git commit -m "feat(cli): add CLI dependencies (textual, typer, rich)"
```

---

### Task 6.1.2: 创建 CLI 模块结构

**Files:**
- Create: `src/cli/__init__.py`
- Create: `src/cli/__main__.py`
- Create: `src/cli/quick.py`
- Create: `src/cli/app.py`
- Create: `src/cli/widgets/__init__.py`

**Step 1: 创建目录和空文件**

```bash
mkdir -p src/cli/widgets
touch src/cli/__init__.py
touch src/cli/__main__.py
touch src/cli/quick.py
touch src/cli/app.py
touch src/cli/widgets/__init__.py
```

**Step 2: 编写 src/cli/__init__.py**

```python
"""CLI module for General Agent - Terminal User Interface"""

__version__ = "0.1.0"
```

**Step 3: 提交**

```bash
git add src/cli/
git commit -m "feat(cli): create CLI module structure"
```

---

### Task 6.1.3: 实现核心组件初始化工具

**Files:**
- Create: `src/cli/core_init.py`
- Create: `tests/cli/__init__.py`
- Create: `tests/cli/test_core_init.py`

**Step 1: 编写测试 - 测试数据库初始化**

Create `tests/cli/__init__.py`:
```python
"""Tests for CLI module"""
```

Create `tests/cli/test_core_init.py`:
```python
"""Tests for core component initialization"""
import pytest
from pathlib import Path
from src.cli.core_init import initialize_database, initialize_executor


@pytest.mark.asyncio
async def test_initialize_database(tmp_path):
    """测试数据库初始化"""
    db_path = tmp_path / "test.db"
    db = await initialize_database(db_path)

    assert db is not None
    assert db_path.exists()

    await db.close()


@pytest.mark.asyncio
async def test_initialize_executor(tmp_path):
    """测试 Executor 初始化"""
    db_path = tmp_path / "test.db"
    db = await initialize_database(db_path)

    executor = await initialize_executor(db, verbose=False)

    assert executor is not None
    assert executor.db == db
    assert executor.router is not None
    assert executor.llm_client is not None

    await db.close()
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_core_init.py -v`
Expected: FAIL - "No module named 'src.cli.core_init'"

**Step 3: 实现 src/cli/core_init.py**

```python
"""Core component initialization for CLI"""
import os
import logging
from pathlib import Path
from typing import Optional

from ..storage.database import Database
from ..core.executor import AgentExecutor
from ..core.router import SimpleRouter
from ..core.llm_client import MockLLMClient
from ..core.ollama_client import OllamaClient, OllamaConfig
from ..skills.loader import SkillLoader
from ..skills.registry import SkillRegistry
from ..skills.executor import SkillExecutor
from ..mcp.connection_manager import MCPConnectionManager
from ..mcp.security import MCPSecurityLayer
from ..mcp.tool_executor import MCPToolExecutor
from ..mcp.config import load_mcp_config

logger = logging.getLogger(__name__)


async def initialize_database(
    db_path: Optional[Path] = None,
) -> Database:
    """
    初始化数据库

    Args:
        db_path: 数据库文件路径，默认为 data/general_agent.db

    Returns:
        Database 实例
    """
    if db_path is None:
        db_path = Path("data/general_agent.db")

    # 确保目录存在
    db_path.parent.mkdir(parents=True, exist_ok=True)

    # 初始化数据库
    db = Database(db_path)
    await db.initialize()

    return db


async def initialize_executor(
    db: Database,
    verbose: bool = False,
) -> AgentExecutor:
    """
    初始化 AgentExecutor 及其依赖

    Args:
        db: Database 实例
        verbose: 是否显示详细日志

    Returns:
        AgentExecutor 实例
    """
    # 设置日志级别
    if verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.INFO)

    # 初始化 Router
    router = SimpleRouter()

    # 初始化 LLM Client
    use_ollama = os.getenv("USE_OLLAMA", "false").lower() == "true"
    if use_ollama:
        ollama_config = OllamaConfig(
            base_url=os.getenv("OLLAMA_BASE_URL", "http://localhost:11434"),
            model=os.getenv("OLLAMA_MODEL", "llama3.2:latest"),
            temperature=float(os.getenv("OLLAMA_TEMPERATURE", "0.7")),
            timeout=float(os.getenv("OLLAMA_TIMEOUT", "120.0"))
        )
        llm_client = OllamaClient(config=ollama_config)
        logger.info(f"Using Ollama client with model: {ollama_config.model}")
    else:
        llm_client = MockLLMClient()
        logger.info("Using Mock LLM client")

    # 初始化 Skill System
    skill_executor = None
    skills_dir = Path("skills")
    if skills_dir.exists():
        try:
            loader = SkillLoader(skills_dir)
            skills = loader.load_all()

            registry = SkillRegistry()
            for skill in skills:
                registry.register(skill)

            skill_executor = SkillExecutor(llm_client)
            logger.info(f"Loaded {len(skills)} skills")
        except Exception as e:
            logger.warning(f"Failed to load skills: {e}")

    # 初始化 MCP
    mcp_executor = None
    mcp_enabled = os.getenv("MCP_ENABLED", "false").lower() == "true"

    if mcp_enabled:
        try:
            config_path = Path("config/mcp_config.yaml")
            if config_path.exists():
                mcp_config = load_mcp_config(str(config_path))
                mcp_manager = MCPConnectionManager(str(config_path))

                fs_config = mcp_config.servers.get("filesystem")
                if fs_config:
                    mcp_security = MCPSecurityLayer(fs_config.security)
                    mcp_executor = MCPToolExecutor(mcp_manager, mcp_security, db)
                    logger.info("MCP integration initialized")
        except Exception as e:
            logger.warning(f"Failed to initialize MCP: {e}")

    # 创建 Executor
    executor = AgentExecutor(
        db,
        router,
        llm_client,
        skill_registry=registry if skill_executor else None,
        skill_executor=skill_executor,
        mcp_executor=mcp_executor
    )

    return executor
```

**Step 4: 运行测试确认通过**

Run: `pytest tests/cli/test_core_init.py -v`
Expected: PASS (2 tests)

**Step 5: 提交**

```bash
git add src/cli/core_init.py tests/cli/
git commit -m "feat(cli): implement core component initialization utilities"
```

---

### Task 6.1.4: 实现命令行入口

**Files:**
- Modify: `src/cli/__main__.py`
- Create: `tests/cli/test_main.py`

**Step 1: 编写测试**

Create `tests/cli/test_main.py`:
```python
"""Tests for CLI main entry point"""
import pytest
from typer.testing import CliRunner
from src.cli.__main__ import cli

runner = CliRunner()


def test_no_args_shows_help():
    """测试无参数显示帮助"""
    result = runner.invoke(cli, [])
    assert result.exit_code == 0
    assert "用法" in result.stdout or "Usage" in result.stdout


def test_help_flag():
    """测试 --help 标志"""
    result = runner.invoke(cli, ["--help"])
    assert result.exit_code == 0
    assert "agent" in result.stdout.lower()


def test_tui_flag_parsed():
    """测试 --tui 标志被正确解析"""
    # 这个测试只验证参数解析，不真正启动 TUI
    # 我们会 mock run_tui 函数
    pass  # 后续实现
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_main.py::test_no_args_shows_help -v`
Expected: FAIL

**Step 3: 实现 src/cli/__main__.py**

```python
"""CLI entry point for General Agent"""
import sys
import asyncio
from typing import Optional
import typer
from pathlib import Path
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# Create Typer app
cli = typer.Typer(
    name="agent",
    help="General Agent CLI - 智能助理命令行工具",
    add_completion=False,
)


def version_callback(value: bool) -> None:
    """显示版本信息"""
    if value:
        from . import __version__
        typer.echo(f"General Agent CLI v{__version__}")
        raise typer.Exit()


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
    ),
    version: Optional[bool] = typer.Option(
        None,
        "--version",
        callback=version_callback,
        is_eager=True,
        help="显示版本信息"
    ),
) -> None:
    """General Agent - 智能助理命令行工具"""

    if tui:
        # TUI 模式
        from .app import run_tui
        run_tui(session, verbose)
    elif query:
        # 快速查询模式
        from .quick import run_quick_query
        try:
            result = asyncio.run(run_quick_query(query, session, verbose))
            typer.echo(result)
        except KeyboardInterrupt:
            typer.echo("\n操作已取消")
            raise typer.Exit(1)
        except Exception as e:
            typer.echo(f"❌ 错误: {e}", err=True)
            raise typer.Exit(1)
    else:
        # 无参数，显示使用提示
        typer.echo("用法：")
        typer.echo("  agent '你的问题'        # 快速查询")
        typer.echo("  agent --tui             # 交互式界面")
        typer.echo("  agent --help            # 查看帮助")


if __name__ == "__main__":
    cli()
```

**Step 4: 运行测试确认通过**

Run: `pytest tests/cli/test_main.py::test_no_args_shows_help -v`
Expected: PASS

Run: `pytest tests/cli/test_main.py::test_help_flag -v`
Expected: PASS

**Step 5: 提交**

```bash
git add src/cli/__main__.py tests/cli/test_main.py
git commit -m "feat(cli): implement command line entry point with typer"
```

---

### Task 6.1.5: 实现快速查询模式

**Files:**
- Modify: `src/cli/quick.py`
- Create: `tests/cli/test_quick.py`

**Step 1: 编写测试**

Create `tests/cli/test_quick.py`:
```python
"""Tests for quick query mode"""
import pytest
from pathlib import Path
from src.cli.quick import run_quick_query


@pytest.mark.asyncio
async def test_run_quick_query_returns_string(tmp_path, monkeypatch):
    """测试快速查询返回字符串"""
    # 设置临时数据库路径
    db_path = tmp_path / "test.db"
    monkeypatch.setenv("DB_PATH", str(db_path))

    result = await run_quick_query("测试查询", None, False)

    assert isinstance(result, str)
    assert len(result) > 0


@pytest.mark.asyncio
async def test_run_quick_query_with_session_id(tmp_path, monkeypatch):
    """测试使用指定会话 ID 的快速查询"""
    db_path = tmp_path / "test.db"
    monkeypatch.setenv("DB_PATH", str(db_path))

    session_id = "test-session-123"
    result = await run_quick_query("测试", session_id, False)

    assert result is not None
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_quick.py -v`
Expected: FAIL

**Step 3: 实现 src/cli/quick.py**

```python
"""Quick query mode for CLI"""
import uuid
import logging
from typing import Optional
from pathlib import Path
from datetime import datetime

from .core_init import initialize_database, initialize_executor
from ..storage.models import Session

logger = logging.getLogger(__name__)


async def run_quick_query(
    query: str,
    session_id: Optional[str],
    verbose: bool,
) -> str:
    """
    快速查询模式 - 执行单次查询并返回结果

    Args:
        query: 用户查询内容
        session_id: 会话 ID，如果为 None 则创建临时会话
        verbose: 是否显示详细日志

    Returns:
        Agent 响应字符串

    Raises:
        Exception: 初始化或执行失败时抛出异常
    """
    # 初始化数据库
    db = await initialize_database()

    try:
        # 初始化 executor
        executor = await initialize_executor(db, verbose)

        # 生成或使用会话 ID
        if not session_id:
            session_id = f"cli-{uuid.uuid4()}"
            # 创建临时会话
            session = Session(
                id=session_id,
                title=query[:50],  # 使用查询前50字符作为标题
                created_at=datetime.now(),
                updated_at=datetime.now()
            )
            await db.create_session(session)
            logger.debug(f"Created temporary session: {session_id}")

        # 执行查询
        logger.debug(f"Executing query: {query}")
        result = await executor.execute(query, session_id)

        # 返回响应
        return result["response"]

    except ConnectionError as e:
        raise ConnectionError(
            f"无法连接到 Ollama 服务: {e}\n"
            "💡 提示：运行 'ollama serve' 启动服务"
        )
    except TimeoutError as e:
        raise TimeoutError(
            f"请求超时: {e}\n"
            "💡 提示：模型响应时间过长，建议切换到更快的模型"
        )
    except Exception as e:
        logger.error(f"Query execution failed: {e}", exc_info=True)
        raise

    finally:
        # 清理资源
        await db.close()
        logger.debug("Database connection closed")
```

**Step 4: 运行测试确认通过**

Run: `pytest tests/cli/test_quick.py -v`
Expected: PASS (2 tests)

**Step 5: 提交**

```bash
git add src/cli/quick.py tests/cli/test_quick.py
git commit -m "feat(cli): implement quick query mode"
```

---

### Task 6.1.6: 手动测试快速查询模式

**Step 1: 安装 CLI**

Run: `pip install -e ".[cli]"`
Expected: 成功安装

**Step 2: 测试无参数**

Run: `agent`
Expected: 显示使用提示

**Step 3: 测试快速查询（Mock 模式）**

```bash
export USE_OLLAMA=false
agent "你好"
```
Expected: 返回 Mock LLM 响应

**Step 4: 测试 --help**

Run: `agent --help`
Expected: 显示完整帮助信息

**Step 5: 文档记录**

如果测试通过，Phase 6.1 基础框架完成。

---

## Phase 6.2: TUI 核心（预计3天）

### Task 6.2.1: 创建 MessageList 组件

**Files:**
- Create: `src/cli/widgets/message_list.py`
- Create: `tests/cli/test_widgets.py`

**Step 1: 编写测试**

Create `tests/cli/test_widgets.py`:
```python
"""Tests for TUI widgets"""
import pytest
from textual.app import App
from src.cli.widgets.message_list import MessageList


@pytest.mark.asyncio
async def test_message_list_creation():
    """测试 MessageList 创建"""
    message_list = MessageList()
    assert message_list is not None


@pytest.mark.asyncio
async def test_add_user_message():
    """测试添加用户消息"""
    class TestApp(App):
        def compose(self):
            yield MessageList(id="messages")

    app = TestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one("#messages")
        message_list.add_message("user", "测试消息")

        # 验证消息已添加
        assert len(message_list.query(".message")) == 1


@pytest.mark.asyncio
async def test_add_agent_message():
    """测试添加 Agent 消息"""
    class TestApp(App):
        def compose(self):
            yield MessageList(id="messages")

    app = TestApp()
    async with app.run_test() as pilot:
        message_list = app.query_one("#messages")
        message_list.add_message("agent", "Agent 响应")

        assert len(message_list.query(".message")) == 1
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_widgets.py::test_message_list_creation -v`
Expected: FAIL

**Step 3: 实现 src/cli/widgets/message_list.py**

```python
"""MessageList widget for displaying conversation history"""
from textual.widgets import Static, Label
from textual.containers import VerticalScroll
from rich.text import Text


class MessageList(VerticalScroll):
    """
    消息列表组件

    显示用户和 Agent 的对话历史，支持滚动
    """

    DEFAULT_CSS = """
    MessageList {
        height: 1fr;
        border: solid $primary;
    }

    MessageList .message {
        padding: 1;
        margin: 0 1;
    }

    MessageList .user-message {
        background: $boost;
        color: $text;
    }

    MessageList .agent-message {
        background: $panel;
        color: $text;
    }

    MessageList .error-message {
        background: $error;
        color: $text;
    }

    MessageList #thinking {
        background: $warning;
        color: $text;
        padding: 1;
        margin: 0 1;
    }
    """

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.can_focus = False

    def add_message(self, role: str, content: str) -> None:
        """
        添加消息到列表

        Args:
            role: 'user' 或 'agent'
            content: 消息内容
        """
        # 选择图标和样式
        if role == "user":
            icon = "🧑"
            style_class = "user-message"
        else:
            icon = "🤖"
            style_class = "agent-message"

        # 创建消息文本
        message_text = Text()
        message_text.append(f"{icon} ", style="bold")
        message_text.append(f"{role.title()}: ", style="bold")
        message_text.append(content)

        # 添加到界面
        message_label = Label(message_text, classes=f"message {style_class}")
        self.mount(message_label)

        # 自动滚动到底部
        self.scroll_end(animate=False)

    def add_error(self, error_msg: str) -> None:
        """
        添加错误消息

        Args:
            error_msg: 错误信息
        """
        error_text = Text()
        error_text.append("❌ ", style="bold red")
        error_text.append("错误: ", style="bold red")
        error_text.append(error_msg, style="red")

        error_label = Label(error_text, classes="message error-message")
        self.mount(error_label)
        self.scroll_end(animate=False)

    def add_thinking_indicator(self) -> None:
        """添加"正在思考"指示器"""
        thinking_text = Text()
        thinking_text.append("🤖 ", style="bold")
        thinking_text.append("Agent: ", style="bold")
        thinking_text.append("正在思考... ", style="italic")

        thinking_label = Label(thinking_text, id="thinking")
        self.mount(thinking_label)
        self.scroll_end(animate=False)

    def remove_thinking_indicator(self) -> None:
        """移除"正在思考"指示器"""
        try:
            thinking = self.query_one("#thinking")
            thinking.remove()
        except:
            pass  # 如果不存在就忽略

    def clear_messages(self) -> None:
        """清空所有消息"""
        for message in self.query(".message"):
            message.remove()
        try:
            self.query_one("#thinking").remove()
        except:
            pass
```

**Step 4: 运行测试确认通过**

Run: `pytest tests/cli/test_widgets.py -v`
Expected: PASS (3 tests)

**Step 5: 提交**

```bash
git add src/cli/widgets/message_list.py tests/cli/test_widgets.py
git commit -m "feat(cli): implement MessageList widget for TUI"
```

---

### Task 6.2.2: 实现基础 TUI 应用框架

**Files:**
- Modify: `src/cli/app.py`
- Create: `src/cli/app.css`
- Create: `tests/cli/test_app.py`

**Step 1: 编写测试**

Create `tests/cli/test_app.py`:
```python
"""Tests for TUI application"""
import pytest
from src.cli.app import AgentTUI


@pytest.mark.asyncio
async def test_agent_tui_creation():
    """测试 AgentTUI 创建"""
    app = AgentTUI()
    assert app is not None


@pytest.mark.asyncio
async def test_agent_tui_has_required_widgets():
    """测试 TUI 包含必需的组件"""
    app = AgentTUI()
    async with app.run_test() as pilot:
        # 验证组件存在
        assert app.query_one("#messages") is not None
        assert app.query_one("#input") is not None
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_app.py -v`
Expected: FAIL

**Step 3: 创建样式文件 src/cli/app.css**

```css
/* General Agent TUI Styles */

Screen {
    background: $surface;
}

Header {
    dock: top;
    height: 3;
    background: $primary;
    color: $text;
    content-align: center middle;
}

Footer {
    dock: bottom;
    height: 1;
    background: $panel;
}

#input {
    dock: bottom;
    height: 3;
    border: solid $primary;
}

#input:focus {
    border: solid $accent;
}

#messages {
    height: 1fr;
}
```

**Step 4: 实现 src/cli/app.py**

```python
"""Textual TUI application for General Agent"""
import uuid
import asyncio
import logging
from typing import Optional
from pathlib import Path
from datetime import datetime

from textual.app import App, ComposeResult
from textual.widgets import Header, Footer, Input
from textual.binding import Binding

from .widgets.message_list import MessageList
from .core_init import initialize_database, initialize_executor
from ..storage.models import Session

logger = logging.getLogger(__name__)


class AgentTUI(App):
    """General Agent TUI 主应用"""

    CSS_PATH = "app.css"

    BINDINGS = [
        Binding("ctrl+q", "quit", "退出", show=True),
        Binding("ctrl+n", "new_session", "新会话", show=True),
        Binding("ctrl+k", "clear_screen", "清屏", show=True),
    ]

    def __init__(self, session_id: Optional[str] = None, verbose: bool = False):
        super().__init__()
        self.session_id = session_id
        self.verbose = verbose
        self.db = None
        self.executor = None
        self.title = "General Agent"

    def compose(self) -> ComposeResult:
        """组装界面组件"""
        yield Header()
        yield MessageList(id="messages")
        yield Input(
            placeholder="输入消息... (Enter 发送)",
            id="input"
        )
        yield Footer()

    async def on_mount(self) -> None:
        """应用启动时初始化"""
        try:
            # 显示加载提示
            self.notify("正在初始化...")

            # 初始化数据库
            self.db = await initialize_database()
            logger.debug("Database initialized")

            # 初始化 executor
            self.executor = await initialize_executor(self.db, self.verbose)
            logger.debug("Executor initialized")

            # 处理会话
            if self.session_id:
                # 加载指定会话
                await self.load_session(self.session_id)
            else:
                # 创建新会话
                await self.create_new_session()

            # 聚焦到输入框
            self.query_one("#input").focus()

            self.notify("初始化完成！", severity="information")

        except Exception as e:
            logger.error(f"Initialization failed: {e}", exc_info=True)
            self.notify(f"初始化失败: {e}", severity="error")
            self.exit(1)

    async def on_input_submitted(self, event: Input.Submitted) -> None:
        """处理用户输入"""
        message = event.value.strip()
        if not message:
            return

        # 清空输入框
        input_widget = self.query_one("#input", Input)
        input_widget.value = ""

        # 获取消息列表
        message_list = self.query_one("#messages", MessageList)

        # 添加用户消息
        message_list.add_message("user", message)

        # 显示"正在思考"
        message_list.add_thinking_indicator()

        try:
            # 执行查询
            result = await self.executor.execute(message, self.session_id)

            # 移除"正在思考"
            message_list.remove_thinking_indicator()

            # 添加 Agent 响应
            message_list.add_message("agent", result["response"])

        except ConnectionError as e:
            message_list.remove_thinking_indicator()
            message_list.add_error(
                f"无法连接到 Ollama 服务\n"
                f"💡 提示：运行 'ollama serve' 启动服务"
            )
            logger.error(f"Ollama connection error: {e}")

        except Exception as e:
            message_list.remove_thinking_indicator()
            message_list.add_error(str(e))
            logger.error(f"Query execution failed: {e}", exc_info=True)

    async def action_new_session(self) -> None:
        """创建新会话（Ctrl+N）"""
        await self.create_new_session()
        self.query_one("#messages", MessageList).clear_messages()
        self.notify("已创建新会话", severity="information")

    async def action_clear_screen(self) -> None:
        """清空屏幕（Ctrl+K）"""
        self.query_one("#messages", MessageList).clear_messages()
        self.notify("屏幕已清空", severity="information")

    async def create_new_session(self) -> None:
        """创建新会话"""
        self.session_id = f"session-{uuid.uuid4()}"
        session = Session(
            id=self.session_id,
            title="新对话",
            created_at=datetime.now(),
            updated_at=datetime.now()
        )
        await self.db.create_session(session)

        # 更新标题
        self.sub_title = f"Session: {self.session_id[:8]}..."

        logger.info(f"Created new session: {self.session_id}")

    async def load_session(self, session_id: str) -> None:
        """
        加载指定会话

        Args:
            session_id: 会话 ID
        """
        # 检查会话是否存在
        session = await self.db.get_session(session_id)
        if not session:
            # 会话不存在，创建新会话
            self.notify(f"会话 {session_id} 不存在，创建新会话", severity="warning")
            await self.create_new_session()
            return

        self.session_id = session_id

        # 更新标题
        self.sub_title = f"Session: {session_id[:8]}... - {session.title}"

        # 加载历史消息
        messages = await self.db.get_messages(session_id)
        message_list = self.query_one("#messages", MessageList)

        for msg in messages:
            message_list.add_message(msg.role, msg.content)

        logger.info(f"Loaded session: {session_id} with {len(messages)} messages")

    async def on_unmount(self) -> None:
        """应用关闭时清理资源"""
        if self.db:
            await self.db.close()
        logger.info("Application closed")


def run_tui(session_id: Optional[str] = None, verbose: bool = False) -> None:
    """
    启动 TUI 应用

    Args:
        session_id: 会话 ID（可选）
        verbose: 是否显示详细日志
    """
    # 设置日志级别
    if verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.INFO)

    # 创建并运行应用
    app = AgentTUI(session_id=session_id, verbose=verbose)
    app.run()
```

**Step 5: 运行测试确认通过**

Run: `pytest tests/cli/test_app.py -v`
Expected: PASS (2 tests)

**Step 6: 提交**

```bash
git add src/cli/app.py src/cli/app.css tests/cli/test_app.py
git commit -m "feat(cli): implement base TUI application with Textual"
```

---

### Task 6.2.3: 手动测试 TUI 基础功能

**Step 1: 启动 TUI（Mock 模式）**

```bash
export USE_OLLAMA=false
agent --tui
```
Expected: TUI 界面启动，显示输入框和消息列表

**Step 2: 测试发送消息**

在 TUI 中输入 "你好" 并按 Enter
Expected: 显示用户消息和 Agent 响应

**Step 3: 测试清屏（Ctrl+K）**

按 Ctrl+K
Expected: 消息列表清空

**Step 4: 测试新会话（Ctrl+N）**

按 Ctrl+N
Expected: 创建新会话，标题更新

**Step 5: 测试退出（Ctrl+Q）**

按 Ctrl+Q
Expected: 应用正常退出

如果测试通过，继续下一阶段。

---

## Phase 6.3: 会话管理（预计2天）

### Task 6.3.1: 实现 SessionList 组件

**Files:**
- Create: `src/cli/widgets/session_list.py`
- Modify: `tests/cli/test_widgets.py`

**Step 1: 编写测试**

在 `tests/cli/test_widgets.py` 添加：
```python
@pytest.mark.asyncio
async def test_session_list_creation():
    """测试 SessionList 创建"""
    from src.cli.widgets.session_list import SessionList

    # Mock database
    class MockDB:
        async def get_all_sessions(self):
            return []

    session_list = SessionList(MockDB())
    assert session_list is not None
```

**Step 2: 运行测试确认失败**

Run: `pytest tests/cli/test_widgets.py::test_session_list_creation -v`
Expected: FAIL

**Step 3: 实现 src/cli/widgets/session_list.py**

```python
"""SessionList widget for selecting sessions"""
from textual.screen import ModalScreen
from textual.widgets import Button, Static, ListView, ListItem, Label
from textual.containers import Vertical, Horizontal
from textual.binding import Binding
from rich.text import Text
from datetime import datetime
from typing import List

from ...storage.models import Session


class SessionListItem(ListItem):
    """会话列表项"""

    def __init__(self, session: Session, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.session = session

    def compose(self):
        # 格式化时间
        now = datetime.now()
        delta = now - self.session.updated_at

        if delta.days > 0:
            time_str = f"{delta.days}天前"
        elif delta.seconds > 3600:
            time_str = f"{delta.seconds // 3600}小时前"
        elif delta.seconds > 60:
            time_str = f"{delta.seconds // 60}分钟前"
        else:
            time_str = "刚刚"

        # 创建显示文本
        text = Text()
        text.append("● ", style="bold green")
        text.append(f"{self.session.id[:12]}... ", style="cyan")
        text.append(f'"{self.session.title}" ', style="white")
        text.append(f"({time_str})", style="dim")

        yield Label(text)


class SessionList(ModalScreen):
    """
    会话列表选择器（模态对话框）
    """

    BINDINGS = [
        Binding("escape", "dismiss", "返回", show=True),
        Binding("n", "new_session", "新建会话", show=True),
    ]

    DEFAULT_CSS = """
    SessionList {
        align: center middle;
    }

    SessionList > Vertical {
        width: 80;
        height: auto;
        max-height: 80%;
        background: $panel;
        border: thick $primary;
    }

    SessionList .title {
        dock: top;
        height: 3;
        content-align: center middle;
        background: $primary;
        color: $text;
    }

    SessionList ListView {
        height: 1fr;
        margin: 1;
    }

    SessionList .buttons {
        dock: bottom;
        height: 3;
        align: center middle;
    }
    """

    def __init__(self, db, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.db = db
        self.sessions: List[Session] = []

    def compose(self):
        with Vertical():
            yield Static("选择会话", classes="title")
            yield ListView(id="session_list")
            with Horizontal(classes="buttons"):
                yield Button("新建会话 (N)", id="new_btn", variant="success")
                yield Button("返回 (Esc)", id="cancel_btn")

    async def on_mount(self):
        """加载会话列表"""
        # 获取所有会话（按更新时间排序）
        self.sessions = await self.db.get_all_sessions()

        # 填充列表
        list_view = self.query_one("#session_list", ListView)
        for session in self.sessions:
            list_view.append(SessionListItem(session))

        # 如果有会话，聚焦第一个
        if self.sessions:
            list_view.focus()

    async def on_list_view_selected(self, event: ListView.Selected):
        """选择会话"""
        item = event.item
        if isinstance(item, SessionListItem):
            # 返回选中的会话 ID
            self.dismiss(item.session.id)

    async def on_button_pressed(self, event: Button.Pressed):
        """按钮点击"""
        if event.button.id == "new_btn":
            await self.action_new_session()
        elif event.button.id == "cancel_btn":
            self.dismiss(None)

    async def action_new_session(self):
        """创建新会话"""
        self.dismiss("__new__")  # 特殊标记表示创建新会话
```

**Step 4: 运行测试确认通过**

Run: `pytest tests/cli/test_widgets.py::test_session_list_creation -v`
Expected: PASS

**Step 5: 提交**

```bash
git add src/cli/widgets/session_list.py tests/cli/test_widgets.py
git commit -m "feat(cli): implement SessionList widget for session selection"
```

---

### Task 6.3.2: 集成会话列表到 TUI

**Files:**
- Modify: `src/cli/app.py`
- Modify: `src/cli/widgets/__init__.py`

**Step 1: 更新 src/cli/widgets/__init__.py**

```python
"""TUI widgets"""
from .message_list import MessageList
from .session_list import SessionList

__all__ = ["MessageList", "SessionList"]
```

**Step 2: 在 AgentTUI 中添加会话列表功能**

修改 `src/cli/app.py`，添加以下内容：

```python
# 在 BINDINGS 中添加
Binding("ctrl+l", "list_sessions", "会话列表", show=True),
```

```python
# 添加方法
async def action_list_sessions(self) -> None:
    """显示会话列表（Ctrl+L）"""
    from .widgets import SessionList

    def handle_selection(session_id: Optional[str]) -> None:
        """处理会话选择"""
        if session_id == "__new__":
            # 创建新会话
            asyncio.create_task(self.create_new_session())
            self.query_one("#messages", MessageList).clear_messages()
        elif session_id:
            # 加载选中的会话
            asyncio.create_task(self.load_session(session_id))
            self.query_one("#messages", MessageList).clear_messages()

    # 显示会话列表
    await self.push_screen(SessionList(self.db), handle_selection)
```

**Step 3: 更新 Database 模型添加 get_all_sessions 方法**

修改 `src/storage/database.py`，添加：

```python
async def get_all_sessions(self) -> List[Session]:
    """
    获取所有会话（按更新时间倒序）

    Returns:
        会话列表
    """
    async with aiosqlite.connect(self.db_path) as db:
        async with db.execute(
            "SELECT id, title, created_at, updated_at FROM sessions "
            "ORDER BY updated_at DESC"
        ) as cursor:
            rows = await cursor.fetchall()

            sessions = []
            for row in rows:
                session = Session(
                    id=row[0],
                    title=row[1],
                    created_at=datetime.fromisoformat(row[2]),
                    updated_at=datetime.fromisoformat(row[3])
                )
                sessions.append(session)

            return sessions
```

**Step 4: 添加测试**

在 `tests/storage/test_database.py` 添加：

```python
@pytest.mark.asyncio
async def test_get_all_sessions(db: Database):
    """测试获取所有会话"""
    # 创建多个会话
    session1 = Session(
        id="session-1",
        title="Session 1",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    session2 = Session(
        id="session-2",
        title="Session 2",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )

    await db.create_session(session1)
    await asyncio.sleep(0.1)  # 确保时间差异
    await db.create_session(session2)

    # 获取所有会话
    sessions = await db.get_all_sessions()

    assert len(sessions) == 2
    assert sessions[0].id == "session-2"  # 最新的在前
    assert sessions[1].id == "session-1"
```

**Step 5: 运行测试**

Run: `pytest tests/storage/test_database.py::test_get_all_sessions -v`
Expected: PASS

**Step 6: 提交**

```bash
git add src/cli/app.py src/cli/widgets/__init__.py src/storage/database.py tests/storage/test_database.py
git commit -m "feat(cli): integrate SessionList into TUI with Ctrl+L binding"
```

---

### Task 6.3.3: 支持 --session 参数

**Step 1: 验证功能已实现**

在 Task 6.2.2 中，`AgentTUI.__init__` 已经接受 `session_id` 参数，`on_mount` 中会调用 `load_session`。

**Step 2: 测试 --session 参数**

```bash
# 先创建一个会话
export USE_OLLAMA=false
agent --tui
# 在 TUI 中发送消息，记录 session ID（从标题栏）

# 退出后重新加载该会话
agent --tui --session=session-xxxxx
```

Expected: 加载指定会话，显示历史消息

**Step 3: 如果测试通过，记录完成**

Phase 6.3 会话管理完成。

---

## Phase 6.4: 完善与测试（预计2天）

### Task 6.4.1: 添加启动检查

**Files:**
- Create: `src/cli/startup.py`
- Modify: `src/cli/app.py`

**Step 1: 实现启动检查模块**

Create `src/cli/startup.py`:
```python
"""Startup checks for CLI"""
import os
import logging
from pathlib import Path
import aiohttp

logger = logging.getLogger(__name__)


async def check_ollama_connection() -> bool:
    """
    检查 Ollama 连接

    Returns:
        True 如果连接成功，False 否则
    """
    ollama_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")

    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{ollama_url}/api/tags", timeout=5) as resp:
                return resp.status == 200
    except Exception as e:
        logger.debug(f"Ollama connection check failed: {e}")
        return False


async def startup_checks(verbose: bool = False) -> None:
    """
    启动前的系统检查

    Args:
        verbose: 是否显示详细信息

    Raises:
        RuntimeError: 如果关键检查失败
    """
    # 1. 检查数据目录
    data_dir = Path("data")
    if not data_dir.exists():
        if verbose:
            print("📁 创建数据目录...")
        data_dir.mkdir(parents=True, exist_ok=True)

    # 2. 检查 .env 文件
    env_file = Path(".env")
    if not env_file.exists():
        if verbose:
            print("⚠️  .env 文件不存在，使用默认配置")

    # 3. 检查 Ollama（如果配置了）
    use_ollama = os.getenv("USE_OLLAMA", "false").lower() == "true"
    if use_ollama:
        if verbose:
            print("🔍 检查 Ollama 连接...")

        if not await check_ollama_connection():
            ollama_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
            raise RuntimeError(
                f"❌ 无法连接到 Ollama 服务 ({ollama_url})\n"
                f"💡 提示：\n"
                f"  1. 运行 'ollama serve' 启动服务\n"
                f"  2. 或在 .env 中设置 USE_OLLAMA=false 使用 Mock 模式"
            )

        if verbose:
            print("✅ Ollama 连接正常")

    if verbose:
        print("✅ 启动检查完成")
```

**Step 2: 在 TUI 启动时调用检查**

修改 `src/cli/app.py` 中的 `run_tui` 函数：

```python
def run_tui(session_id: Optional[str] = None, verbose: bool = False) -> None:
    """
    启动 TUI 应用

    Args:
        session_id: 会话 ID（可选）
        verbose: 是否显示详细日志
    """
    import asyncio
    from .startup import startup_checks

    # 设置日志级别
    if verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.INFO)

    # 启动检查
    try:
        asyncio.run(startup_checks(verbose))
    except RuntimeError as e:
        print(str(e))
        return

    # 创建并运行应用
    app = AgentTUI(session_id=session_id, verbose=verbose)
    app.run()
```

**Step 3: 测试启动检查**

```bash
# 测试 Ollama 未启动的情况
export USE_OLLAMA=true
agent --tui
```

Expected: 显示友好的错误提示

**Step 4: 提交**

```bash
git add src/cli/startup.py src/cli/app.py
git commit -m "feat(cli): add startup checks for Ollama and data directory"
```

---

### Task 6.4.2: 添加集成测试

**Files:**
- Create: `tests/cli/test_integration.py`

**Step 1: 编写集成测试**

```python
"""Integration tests for CLI"""
import pytest
import asyncio
from pathlib import Path
from datetime import datetime

from src.cli.core_init import initialize_database, initialize_executor
from src.storage.models import Session


@pytest.mark.asyncio
async def test_cli_web_session_sharing(tmp_path):
    """
    测试 CLI 和 Web 会话共享

    验证：
    1. CLI 创建会话和消息
    2. 通过数据库查询可以读取
    """
    # 使用临时数据库
    db_path = tmp_path / "test.db"

    # CLI 侧：创建会话和消息
    db = await initialize_database(db_path)
    executor = await initialize_executor(db, verbose=False)

    session_id = "test-shared-session"
    session = Session(
        id=session_id,
        title="测试共享会话",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 发送消息
    result = await executor.execute("测试消息", session_id)
    assert result["response"] is not None

    # Web 侧：读取同一会话
    messages = await db.get_messages(session_id)
    assert len(messages) >= 2  # 至少有用户消息和 Agent 响应

    await db.close()


@pytest.mark.asyncio
async def test_quick_query_creates_session(tmp_path, monkeypatch):
    """测试快速查询创建会话"""
    from src.cli.quick import run_quick_query

    db_path = tmp_path / "test.db"
    monkeypatch.setenv("DB_PATH", str(db_path))

    # 执行快速查询
    result = await run_quick_query("测试", None, False)

    # 验证会话已创建
    db = await initialize_database(db_path)
    sessions = await db.get_all_sessions()

    assert len(sessions) >= 1
    assert sessions[0].title.startswith("测试")

    await db.close()
```

**Step 2: 运行集成测试**

Run: `pytest tests/cli/test_integration.py -v`
Expected: PASS

**Step 3: 提交**

```bash
git add tests/cli/test_integration.py
git commit -m "test(cli): add integration tests for CLI and session sharing"
```

---

### Task 6.4.3: 更新文档

**Files:**
- Modify: `README.md`
- Create: `docs/tui.md`

**Step 1: 更新 README.md**

在 "功能特性" 部分添加：

```markdown
### Phase 6: TUI 终端界面 ✅
- 命令行快速查询模式
- 交互式 TUI 界面（基于 Textual）
- 会话管理（创建、切换、列表）
- 与 Web 完全共享会话数据
```

在 "快速开始" 部分添加：

```markdown
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
```

**Step 2: 创建 docs/tui.md**

```markdown
# TUI 终端界面使用指南

General Agent 提供了基于 Textual 的终端用户界面（TUI），支持命令行快速查询和交互式对话。

---

## 安装

```bash
pip install -e ".[cli]"
```

---

## 使用模式

### 1. 命令行模式（快速查询）

适用于快速问答，执行后立即返回结果并退出。

```bash
agent "今天天气如何？"
agent "帮我总结这段文字：..."
```

**特点：**
- 快速响应
- 自动创建临时会话
- 适合脚本调用

### 2. TUI 模式（交互式界面）

适用于多轮对话和长期会话。

```bash
agent --tui
```

**特点：**
- 持续对话
- 会话管理
- 历史记录
- 快捷键支持

---

## 界面说明

```
┌─────────────────────────────────────────────────────┐
│ General Agent - Session: session-abc123       [Help]│ <- 标题栏
├─────────────────────────────────────────────────────┤
│ 🧑 User: 今天天气如何？                              │
│                                                     │
│ 🤖 Agent: 我无法实时获取天气信息...                  │
│                                                     │ <- 消息区（可滚动）
│ ...                                                 │
├─────────────────────────────────────────────────────┤
│ > 输入消息... _                                      │ <- 输入框
├─────────────────────────────────────────────────────┤
│ Enter=发送 Ctrl+N=新会话 Ctrl+L=列表 Ctrl+Q=退出     │ <- 快捷键提示
└─────────────────────────────────────────────────────┘
```

---

## 快捷键

| 快捷键 | 功能 | 说明 |
|--------|------|------|
| `Enter` | 发送消息 | 输入框中按 Enter 发送 |
| `Ctrl+N` | 新建会话 | 创建新会话并清空屏幕 |
| `Ctrl+L` | 会话列表 | 显示所有会话，选择切换 |
| `Ctrl+K` | 清屏 | 清空当前消息列表 |
| `Ctrl+Q` | 退出 | 关闭 TUI 应用 |
| `Esc` | 关闭弹窗 | 关闭会话列表等弹窗 |

---

## 会话管理

### 创建新会话

**方法 1：启动时自动创建**
```bash
agent --tui
```

**方法 2：在 TUI 中创建**
按 `Ctrl+N`

### 切换会话

**按 `Ctrl+L` 打开会话列表：**

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

使用方向键选择，按 `Enter` 确认。

### 加载指定会话

```bash
agent --tui --session=session-abc123
```

---

## 与 Web 界面共享

TUI 和 Web 界面完全共享会话数据：

- **共享数据库** - 使用同一 SQLite 文件
- **会话互通** - TUI 创建的会话在 Web 端可见
- **消息同步** - 所有消息实时保存
- **无缝切换** - 可以在两个界面间自由切换

**示例：**
1. 在 TUI 中创建会话并发送消息
2. 打开 Web 界面 (http://localhost:8000)
3. 可以看到相同的会话和消息

---

## 配置

TUI 使用与 Web 相同的配置：

### .env 配置

```bash
# LLM 配置
USE_OLLAMA=true
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=llama3.2:latest
OLLAMA_TIMEOUT=120.0

# MCP 配置
MCP_ENABLED=true

# 日志级别
LOG_LEVEL=INFO
```

### 启用详细日志

```bash
agent --tui --verbose
```

---

## 故障排除

### 无法连接到 Ollama

**错误：**
```
❌ 无法连接到 Ollama 服务 (http://localhost:11434)
```

**解决方法：**
1. 启动 Ollama：`ollama serve`
2. 或使用 Mock 模式：在 `.env` 中设置 `USE_OLLAMA=false`

### 数据库锁定

如果 TUI 和 Web 同时运行，SQLite 可能出现短暂锁定。系统会自动重试（最多 3 次）。

### 终端大小过小

TUI 需要至少 80x24 的终端大小。如果终端过小，调整窗口大小后重启。

---

## 高级用法

### 在脚本中使用

```bash
#!/bin/bash

# 快速查询模式，适合脚本
RESULT=$(agent "总结这个文件的内容" --session=my-session)
echo "Agent 回复: $RESULT"
```

### 持续会话

```bash
# 创建命名会话
SESSION_ID="work-$(date +%Y%m%d)"
agent --tui --session=$SESSION_ID
```

### 集成到工作流

```bash
# .zshrc 或 .bashrc
alias ask="agent"
alias chat="agent --tui"

# 使用
ask "今天天气"
chat  # 进入交互式界面
```

---

## 性能优化

### 使用更快的模型

```bash
# .env
OLLAMA_MODEL=llama3.2:1b  # 更小更快的模型
```

### 减少超时时间

```bash
# .env
OLLAMA_TIMEOUT=60.0  # 60秒超时
```

---

## 反馈与支持

遇到问题？

1. 查看日志：`agent --tui --verbose`
2. 检查配置：确保 `.env` 正确
3. 提交 Issue：[GitHub Issues](https://github.com/yourusername/general-agent/issues)

---

**文档更新时间：** 2026-03-05
```

**Step 3: 提交文档更新**

```bash
git add README.md docs/tui.md
git commit -m "docs(cli): add TUI user guide and update README"
```

---

### Task 6.4.4: 最终验收测试

**Step 1: 运行所有测试**

```bash
pytest tests/cli/ -v --cov=src/cli --cov-report=html
```

Expected: 所有测试通过，覆盖率 ≥ 80%

**Step 2: 手动功能验收**

按照以下清单逐项测试：

- [ ] **命令行模式**
  - [ ] `agent "测试查询"` 正常返回
  - [ ] `agent --help` 显示帮助
  - [ ] `agent --version` 显示版本
  - [ ] Ollama 未启动时显示友好错误

- [ ] **TUI 模式**
  - [ ] `agent --tui` 正常启动
  - [ ] 发送消息正常响应
  - [ ] 长消息自动换行
  - [ ] "正在思考"动画正常

- [ ] **会话管理**
  - [ ] Ctrl+N 创建新会话
  - [ ] Ctrl+L 显示会话列表
  - [ ] 选择会话正常切换
  - [ ] `agent --tui --session=xxx` 加载指定会话

- [ ] **快捷键**
  - [ ] Enter 发送消息
  - [ ] Ctrl+K 清屏
  - [ ] Ctrl+Q 退出
  - [ ] Esc 关闭弹窗

- [ ] **会话共享**
  - [ ] TUI 创建会话 → Web 可见
  - [ ] Web 创建会话 → TUI 可加载
  - [ ] 消息完全同步

- [ ] **错误处理**
  - [ ] 网络超时友好提示
  - [ ] 无效 session_id 自动处理

**Step 3: 记录验收结果**

所有测试通过后，Phase 6 完成。

---

### Task 6.4.5: 最终提交和标签

**Step 1: 检查代码质量**

```bash
# 代码检查
ruff check src/cli/
mypy src/cli/

# 测试覆盖率
pytest tests/cli/ --cov=src/cli --cov-report=term
```

Expected: 无错误，覆盖率 ≥ 80%

**Step 2: 最终提交**

```bash
git add -A
git commit -m "feat(phase6): complete TUI implementation

Phase 6 完成：
- 双模式 CLI（快速查询 + 交互式 TUI）
- 基于 Textual 的 TUI 界面
- 会话管理（创建、切换、列表）
- 与 Web 完全共享会话数据
- 完整测试覆盖（80%+）
- 用户文档

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
"
```

**Step 3: 打标签**

```bash
git tag -a v0.6.0 -m "Release v0.6.0 - TUI Terminal Interface

Features:
- Command-line quick query mode
- Interactive TUI with Textual
- Session management
- Full session sharing with Web
- Comprehensive documentation

Phase 6 complete!"

git push origin main --tags
```

---

## 总结

Phase 6 实施计划包含：

**Phase 6.1: 基础框架（2天）**
- Task 6.1.1: 更新依赖
- Task 6.1.2: 创建模块结构
- Task 6.1.3: 核心组件初始化
- Task 6.1.4: 命令行入口
- Task 6.1.5: 快速查询模式
- Task 6.1.6: 手动测试

**Phase 6.2: TUI 核心（3天）**
- Task 6.2.1: MessageList 组件
- Task 6.2.2: TUI 应用框架
- Task 6.2.3: 手动测试

**Phase 6.3: 会话管理（2天）**
- Task 6.3.1: SessionList 组件
- Task 6.3.2: 集成会话列表
- Task 6.3.3: --session 参数支持

**Phase 6.4: 完善与测试（2天）**
- Task 6.4.1: 启动检查
- Task 6.4.2: 集成测试
- Task 6.4.3: 文档更新
- Task 6.4.4: 最终验收
- Task 6.4.5: 提交和标签

**预计总工期：** 8-10 天
**测试覆盖率目标：** ≥ 80%
**交付物：** 完整的 TUI 功能、测试、文档

---

**计划创建时间：** 2026-03-05
**下一步：** 选择执行方式（Subagent-Driven 或 Parallel Session）
