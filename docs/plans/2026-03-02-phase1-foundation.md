# Phase 1: 基础框架实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 建立General Agent的基础架构，实现最小可用的聊天功能

**Architecture:** 采用TDD方法构建核心模块（Agent Core + SQLite存储 + FastAPI + 简单Web界面），每个组件都有完整的测试覆盖

**Tech Stack:** Python 3.11+, FastAPI, SQLite, pytest, Jinja2

---

## Task 1: 项目初始化

**Files:**
- Create: `pyproject.toml`
- Create: `.gitignore`
- Create: `README.md`
- Create: `src/main.py`
- Create: `tests/conftest.py`

**Step 1: 创建 pyproject.toml**

```toml
[project]
name = "general-agent"
version = "0.1.0"
description = "通用Agent系统，兼容Skill协议、MCP和RAG"
requires-python = ">=3.11"
dependencies = [
    "fastapi>=0.109.0",
    "uvicorn[standard]>=0.27.0",
    "jinja2>=3.1.3",
    "aiosqlite>=0.19.0",
    "pydantic>=2.6.0",
    "python-multipart>=0.0.9",
]

[project.optional-dependencies]
dev = [
    "pytest>=8.0.0",
    "pytest-asyncio>=0.23.0",
    "pytest-cov>=4.1.0",
    "ruff>=0.2.0",
    "mypy>=1.8.0",
]

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.ruff]
line-length = 100
target-version = "py311"

[tool.pytest.ini_options]
asyncio_mode = "auto"
testpaths = ["tests"]
python_files = ["test_*.py"]
python_functions = ["test_*"]

[tool.mypy]
python_version = "3.11"
warn_return_any = true
warn_unused_configs = true
disallow_untyped_defs = true
```

**Step 2: 创建 .gitignore**

```gitignore
# Python
__pycache__/
*.py[cod]
*$py.class
*.so
.Python
venv/
ENV/
.venv
*.egg-info/

# IDE
.vscode/
.idea/
*.swp
*.swo

# Data
data/
*.db
*.sqlite

# Testing
.pytest_cache/
.coverage
htmlcov/

# OS
.DS_Store
Thumbs.db
```

**Step 3: 创建基础目录结构**

```bash
mkdir -p src/{core,storage,api,skills,mcp,rag}
mkdir -p tests/{core,storage,api,skills,mcp,rag}
mkdir -p templates static/{css,js}
mkdir -p skills/{personal,productivity,knowledge}
mkdir -p config data
touch src/__init__.py
touch src/core/__init__.py
touch src/storage/__init__.py
touch src/api/__init__.py
touch tests/__init__.py
```

**Step 4: 创建 README.md**

```markdown
# General Agent

通用Agent系统，支持：
- Skill协议（兼容Claude Code）
- MCP客户端（Model Context Protocol）
- RAG检索增强生成

## 快速开始

```bash
# 安装依赖
uv pip install -e ".[dev]"

# 运行测试
pytest

# 启动服务
uvicorn src.main:app --reload
```

## 架构

详见 `docs/plans/2026-03-02-general-agent-design.md`
```

**Step 5: 创建 tests/conftest.py**

```python
"""pytest配置和共享fixtures"""
import pytest
import asyncio
from pathlib import Path


@pytest.fixture(scope="session")
def event_loop():
    """创建事件循环"""
    loop = asyncio.get_event_loop_policy().new_event_loop()
    yield loop
    loop.close()


@pytest.fixture
def test_data_dir(tmp_path: Path) -> Path:
    """测试数据目录"""
    return tmp_path / "data"


@pytest.fixture
def test_db_path(test_data_dir: Path) -> Path:
    """测试数据库路径"""
    test_data_dir.mkdir(parents=True, exist_ok=True)
    return test_data_dir / "test.db"
```

**Step 6: 安装依赖**

```bash
uv pip install -e ".[dev]"
```

**Step 7: 验证环境**

```bash
pytest --version
python -c "import fastapi; print(fastapi.__version__)"
```

**Step 8: Commit**

```bash
git add .
git commit -m "chore: initialize project structure

- Add pyproject.toml with dependencies
- Create directory structure
- Add .gitignore and README
- Set up pytest configuration

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 2: SQLite存储层 - 基础架构

**Files:**
- Create: `src/storage/models.py`
- Create: `tests/storage/test_models.py`

**Step 1: 编写模型测试**

文件: `tests/storage/test_models.py`

```python
"""测试数据模型"""
import pytest
from datetime import datetime
from src.storage.models import Message, Session


def test_message_creation():
    """测试创建消息对象"""
    msg = Message(
        id="msg-1",
        session_id="sess-1",
        role="user",
        content="Hello",
        timestamp=datetime.now()
    )
    assert msg.id == "msg-1"
    assert msg.role == "user"
    assert msg.content == "Hello"


def test_session_creation():
    """测试创建会话对象"""
    session = Session(
        id="sess-1",
        title="Test Session",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    assert session.id == "sess-1"
    assert session.title == "Test Session"


def test_message_to_dict():
    """测试消息序列化"""
    msg = Message(
        id="msg-1",
        session_id="sess-1",
        role="assistant",
        content="Hi there",
        timestamp=datetime.now()
    )
    data = msg.to_dict()
    assert data["id"] == "msg-1"
    assert data["role"] == "assistant"
    assert "timestamp" in data
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/storage/test_models.py -v
```

Expected: FAIL with "ModuleNotFoundError: No module named 'src.storage.models'"

**Step 3: 实现数据模型**

文件: `src/storage/models.py`

```python
"""数据模型定义"""
from dataclasses import dataclass, asdict
from datetime import datetime
from typing import Optional


@dataclass
class Message:
    """消息模型"""
    id: str
    session_id: str
    role: str  # 'user' | 'assistant' | 'system'
    content: str
    timestamp: datetime
    metadata: Optional[dict] = None

    def to_dict(self) -> dict:
        """转换为字典"""
        data = asdict(self)
        data['timestamp'] = self.timestamp.isoformat()
        return data


@dataclass
class Session:
    """会话模型"""
    id: str
    title: str
    created_at: datetime
    updated_at: datetime
    metadata: Optional[dict] = None

    def to_dict(self) -> dict:
        """转换为字典"""
        data = asdict(self)
        data['created_at'] = self.created_at.isoformat()
        data['updated_at'] = self.updated_at.isoformat()
        return data
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/storage/test_models.py -v
```

Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/storage/models.py tests/storage/test_models.py
git commit -m "feat(storage): add data models

- Add Message and Session dataclasses
- Add serialization methods
- Add unit tests

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 3: SQLite存储层 - 数据库操作

**Files:**
- Create: `src/storage/database.py`
- Create: `tests/storage/test_database.py`

**Step 1: 编写数据库测试**

文件: `tests/storage/test_database.py`

```python
"""测试数据库操作"""
import pytest
from datetime import datetime
from pathlib import Path
from src.storage.database import Database
from src.storage.models import Message, Session


@pytest.mark.asyncio
async def test_database_init(test_db_path: Path):
    """测试数据库初始化"""
    db = Database(test_db_path)
    await db.initialize()
    assert test_db_path.exists()
    await db.close()


@pytest.mark.asyncio
async def test_create_session(test_db_path: Path):
    """测试创建会话"""
    db = Database(test_db_path)
    await db.initialize()

    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    retrieved = await db.get_session("sess-1")
    assert retrieved is not None
    assert retrieved.id == "sess-1"
    assert retrieved.title == "Test"

    await db.close()


@pytest.mark.asyncio
async def test_add_message(test_db_path: Path):
    """测试添加消息"""
    db = Database(test_db_path)
    await db.initialize()

    # 先创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 添加消息
    msg = Message(
        id="msg-1",
        session_id="sess-1",
        role="user",
        content="Hello",
        timestamp=datetime.now()
    )
    await db.add_message(msg)

    # 获取消息
    messages = await db.get_messages("sess-1")
    assert len(messages) == 1
    assert messages[0].content == "Hello"

    await db.close()


@pytest.mark.asyncio
async def test_get_recent_messages(test_db_path: Path):
    """测试获取最近消息"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    # 添加多条消息
    for i in range(5):
        msg = Message(
            id=f"msg-{i}",
            session_id="sess-1",
            role="user" if i % 2 == 0 else "assistant",
            content=f"Message {i}",
            timestamp=datetime.now()
        )
        await db.add_message(msg)

    # 获取最近3条
    recent = await db.get_recent_messages("sess-1", limit=3)
    assert len(recent) == 3

    await db.close()
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/storage/test_database.py -v
```

Expected: FAIL with "ModuleNotFoundError"

**Step 3: 实现数据库操作**

文件: `src/storage/database.py`

```python
"""SQLite数据库操作"""
import aiosqlite
import json
from pathlib import Path
from datetime import datetime
from typing import List, Optional
from .models import Message, Session


class Database:
    """SQLite数据库管理器"""

    def __init__(self, db_path: Path):
        self.db_path = db_path
        self.conn: Optional[aiosqlite.Connection] = None

    async def initialize(self) -> None:
        """初始化数据库和表结构"""
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        self.conn = await aiosqlite.connect(str(self.db_path))

        # 创建会话表
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                created_at TIMESTAMP NOT NULL,
                updated_at TIMESTAMP NOT NULL,
                metadata TEXT
            )
        """)

        # 创建消息表
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                timestamp TIMESTAMP NOT NULL,
                metadata TEXT,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        await self.conn.commit()

    async def close(self) -> None:
        """关闭数据库连接"""
        if self.conn:
            await self.conn.close()

    async def create_session(self, session: Session) -> None:
        """创建新会话"""
        await self.conn.execute(
            """
            INSERT INTO sessions (id, title, created_at, updated_at, metadata)
            VALUES (?, ?, ?, ?, ?)
            """,
            (
                session.id,
                session.title,
                session.created_at.isoformat(),
                session.updated_at.isoformat(),
                json.dumps(session.metadata) if session.metadata else None
            )
        )
        await self.conn.commit()

    async def get_session(self, session_id: str) -> Optional[Session]:
        """获取会话"""
        async with self.conn.execute(
            "SELECT * FROM sessions WHERE id = ?",
            (session_id,)
        ) as cursor:
            row = await cursor.fetchone()
            if not row:
                return None

            return Session(
                id=row[0],
                title=row[1],
                created_at=datetime.fromisoformat(row[2]),
                updated_at=datetime.fromisoformat(row[3]),
                metadata=json.loads(row[4]) if row[4] else None
            )

    async def add_message(self, message: Message) -> None:
        """添加消息"""
        await self.conn.execute(
            """
            INSERT INTO messages (id, session_id, role, content, timestamp, metadata)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                message.id,
                message.session_id,
                message.role,
                message.content,
                message.timestamp.isoformat(),
                json.dumps(message.metadata) if message.metadata else None
            )
        )
        await self.conn.commit()

    async def get_messages(self, session_id: str) -> List[Message]:
        """获取会话的所有消息"""
        async with self.conn.execute(
            "SELECT * FROM messages WHERE session_id = ? ORDER BY timestamp",
            (session_id,)
        ) as cursor:
            rows = await cursor.fetchall()
            return [
                Message(
                    id=row[0],
                    session_id=row[1],
                    role=row[2],
                    content=row[3],
                    timestamp=datetime.fromisoformat(row[4]),
                    metadata=json.loads(row[5]) if row[5] else None
                )
                for row in rows
            ]

    async def get_recent_messages(
        self,
        session_id: str,
        limit: int = 10
    ) -> List[Message]:
        """获取最近的N条消息"""
        async with self.conn.execute(
            """
            SELECT * FROM messages
            WHERE session_id = ?
            ORDER BY timestamp DESC
            LIMIT ?
            """,
            (session_id, limit)
        ) as cursor:
            rows = await cursor.fetchall()
            messages = [
                Message(
                    id=row[0],
                    session_id=row[1],
                    role=row[2],
                    content=row[3],
                    timestamp=datetime.fromisoformat(row[4]),
                    metadata=json.loads(row[5]) if row[5] else None
                )
                for row in rows
            ]
            # 反转顺序（最旧的在前）
            return list(reversed(messages))
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/storage/test_database.py -v
```

Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/storage/database.py tests/storage/test_database.py
git commit -m "feat(storage): add database operations

- Add Database class for SQLite operations
- Implement session and message CRUD
- Add async interface with aiosqlite
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 4: Context Manager - 上下文管理

**Files:**
- Create: `src/core/context.py`
- Create: `tests/core/test_context.py`

**Step 1: 编写上下文管理测试**

文件: `tests/core/test_context.py`

```python
"""测试上下文管理器"""
import pytest
from datetime import datetime
from src.core.context import ContextManager
from src.storage.database import Database
from src.storage.models import Session


@pytest.mark.asyncio
async def test_context_creation(test_db_path):
    """测试创建上下文管理器"""
    db = Database(test_db_path)
    await db.initialize()

    ctx = ContextManager(db, session_id="sess-1")
    assert ctx.session_id == "sess-1"

    await db.close()


@pytest.mark.asyncio
async def test_add_and_get_messages(test_db_path):
    """测试添加和获取消息"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    ctx = ContextManager(db, session_id="sess-1")

    # 添加消息
    await ctx.add_message("user", "Hello")
    await ctx.add_message("assistant", "Hi there")

    # 获取消息
    messages = await ctx.get_history()
    assert len(messages) == 2
    assert messages[0].role == "user"
    assert messages[1].role == "assistant"

    await db.close()


@pytest.mark.asyncio
async def test_get_recent_history(test_db_path):
    """测试获取最近历史"""
    db = Database(test_db_path)
    await db.initialize()

    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    ctx = ContextManager(db, session_id="sess-1")

    # 添加多条消息
    for i in range(5):
        await ctx.add_message("user", f"Message {i}")

    # 获取最近3条
    recent = await ctx.get_history(limit=3)
    assert len(recent) == 3

    await db.close()


@pytest.mark.asyncio
async def test_format_for_llm(test_db_path):
    """测试格式化为LLM输入"""
    db = Database(test_db_path)
    await db.initialize()

    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    ctx = ContextManager(db, session_id="sess-1")

    await ctx.add_message("user", "Hello")
    await ctx.add_message("assistant", "Hi")

    formatted = await ctx.format_for_llm()
    assert len(formatted) == 2
    assert formatted[0]["role"] == "user"
    assert formatted[0]["content"] == "Hello"

    await db.close()
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/core/test_context.py -v
```

Expected: FAIL

**Step 3: 实现上下文管理器**

文件: `src/core/context.py`

```python
"""上下文管理器"""
import uuid
from datetime import datetime
from typing import List, Dict, Any
from ..storage.database import Database
from ..storage.models import Message


class ContextManager:
    """管理会话上下文"""

    def __init__(self, db: Database, session_id: str):
        self.db = db
        self.session_id = session_id

    async def add_message(self, role: str, content: str, metadata: dict = None) -> Message:
        """添加消息到上下文"""
        message = Message(
            id=f"msg-{uuid.uuid4().hex[:8]}",
            session_id=self.session_id,
            role=role,
            content=content,
            timestamp=datetime.now(),
            metadata=metadata
        )
        await self.db.add_message(message)
        return message

    async def get_history(self, limit: int = None) -> List[Message]:
        """获取会话历史"""
        if limit:
            return await self.db.get_recent_messages(self.session_id, limit)
        return await self.db.get_messages(self.session_id)

    async def format_for_llm(self, limit: int = 10) -> List[Dict[str, Any]]:
        """格式化为LLM输入格式"""
        messages = await self.get_history(limit)
        return [
            {
                "role": msg.role,
                "content": msg.content
            }
            for msg in messages
        ]
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/core/test_context.py -v
```

Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/core/context.py tests/core/test_context.py
git commit -m "feat(core): add context manager

- Add ContextManager for session context
- Implement message history management
- Add LLM input formatting
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 5: Simple Router - 简单路由器

**Files:**
- Create: `src/core/router.py`
- Create: `tests/core/test_router.py`

**Step 1: 编写路由器测试**

文件: `tests/core/test_router.py`

```python
"""测试路由器"""
import pytest
from src.core.router import SimpleRouter, ExecutionPlan


def test_router_creation():
    """测试创建路由器"""
    router = SimpleRouter()
    assert router is not None


def test_simple_query_routing():
    """测试简单问答路由"""
    router = SimpleRouter()
    plan = router.route("What is Python?", context=None)

    assert isinstance(plan, ExecutionPlan)
    assert plan.type == "simple_query"
    assert plan.requires_llm is True


def test_greeting_routing():
    """测试问候语路由"""
    router = SimpleRouter()

    for greeting in ["hello", "hi", "hey"]:
        plan = router.route(greeting, context=None)
        assert plan.type == "simple_query"


def test_task_routing():
    """测试任务路由（未来扩展）"""
    router = SimpleRouter()
    plan = router.route("Remind me to call John at 3pm", context=None)

    # 当前版本：所有都是simple_query
    # 未来：应该识别为 task
    assert plan.type == "simple_query"
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/core/test_router.py -v
```

Expected: FAIL

**Step 3: 实现简单路由器**

文件: `src/core/router.py`

```python
"""智能路由器"""
from dataclasses import dataclass
from typing import Optional, Any


@dataclass
class ExecutionPlan:
    """执行计划"""
    type: str  # 'simple_query' | 'task' | 'skill' | 'mcp'
    requires_llm: bool
    requires_rag: bool = False
    requires_tools: bool = False
    metadata: dict = None


class SimpleRouter:
    """简单路由器（MVP版本）"""

    def route(self, user_input: str, context: Any) -> ExecutionPlan:
        """
        路由用户输入到执行计划

        当前版本：所有输入都路由到simple_query（直接LLM）
        未来版本：添加意图识别，路由到skill/mcp/rag
        """
        # MVP: 所有都是简单问答
        return ExecutionPlan(
            type="simple_query",
            requires_llm=True,
            requires_rag=False,
            requires_tools=False
        )
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/core/test_router.py -v
```

Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/core/router.py tests/core/test_router.py
git commit -m "feat(core): add simple router

- Add SimpleRouter for MVP
- Define ExecutionPlan dataclass
- Route all inputs to simple_query (for now)
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 6: Mock LLM Client - 模拟LLM客户端

**Files:**
- Create: `src/core/llm_client.py`
- Create: `tests/core/test_llm_client.py`

**Step 1: 编写LLM客户端测试**

文件: `tests/core/test_llm_client.py`

```python
"""测试LLM客户端"""
import pytest
from src.core.llm_client import MockLLMClient


@pytest.mark.asyncio
async def test_mock_llm_response():
    """测试模拟LLM响应"""
    client = MockLLMClient()

    messages = [
        {"role": "user", "content": "Hello"}
    ]

    response = await client.chat(messages)
    assert isinstance(response, str)
    assert len(response) > 0


@pytest.mark.asyncio
async def test_mock_llm_echo():
    """测试模拟LLM回声"""
    client = MockLLMClient()

    messages = [
        {"role": "user", "content": "Test message"}
    ]

    response = await client.chat(messages)
    # Mock LLM应该包含用户输入的关键信息
    assert "Test message" in response or "收到" in response
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/core/test_llm_client.py -v
```

Expected: FAIL

**Step 3: 实现模拟LLM客户端**

文件: `src/core/llm_client.py`

```python
"""LLM客户端"""
from typing import List, Dict, Any


class MockLLMClient:
    """
    模拟LLM客户端（用于测试和MVP）

    未来：替换为真实的OpenAI/Claude API调用
    """

    async def chat(self, messages: List[Dict[str, Any]]) -> str:
        """
        模拟聊天补全

        Args:
            messages: 消息列表，格式为 [{"role": "user", "content": "..."}]

        Returns:
            模拟的助手回复
        """
        if not messages:
            return "你好！有什么我可以帮助你的吗？"

        last_message = messages[-1]
        user_input = last_message.get("content", "")

        # 简单的模拟回复逻辑
        if not user_input:
            return "我没有收到你的消息，请再说一次？"

        # 回声模式：重复用户输入
        return f"我收到了你的消息：「{user_input}」。这是一个模拟响应，未来将连接真实的LLM。"


class LLMClient:
    """
    真实LLM客户端（未来实现）

    支持：
    - OpenAI API
    - Claude API
    - 本地模型（Ollama等）
    """
    pass
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/core/test_llm_client.py -v
```

Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add src/core/llm_client.py tests/core/test_llm_client.py
git commit -m "feat(core): add mock LLM client

- Add MockLLMClient for testing/MVP
- Simple echo-based responses
- Future: replace with real API calls
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 7: Agent Executor - 执行器

**Files:**
- Create: `src/core/executor.py`
- Create: `tests/core/test_executor.py`

**Step 1: 编写执行器测试**

文件: `tests/core/test_executor.py`

```python
"""测试执行器"""
import pytest
from src.core.executor import AgentExecutor
from src.core.router import SimpleRouter, ExecutionPlan
from src.core.llm_client import MockLLMClient
from src.core.context import ContextManager
from src.storage.database import Database
from src.storage.models import Session
from datetime import datetime


@pytest.mark.asyncio
async def test_executor_creation(test_db_path):
    """测试创建执行器"""
    db = Database(test_db_path)
    await db.initialize()

    router = SimpleRouter()
    llm_client = MockLLMClient()

    executor = AgentExecutor(db, router, llm_client)
    assert executor is not None

    await db.close()


@pytest.mark.asyncio
async def test_execute_simple_query(test_db_path):
    """测试执行简单问答"""
    db = Database(test_db_path)
    await db.initialize()

    # 创建会话
    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    router = SimpleRouter()
    llm_client = MockLLMClient()
    executor = AgentExecutor(db, router, llm_client)

    # 执行查询
    result = await executor.execute("Hello", session_id="sess-1")

    assert result is not None
    assert "response" in result
    assert isinstance(result["response"], str)

    # 验证消息已保存
    ctx = ContextManager(db, "sess-1")
    history = await ctx.get_history()
    assert len(history) == 2  # user + assistant

    await db.close()


@pytest.mark.asyncio
async def test_execute_with_context(test_db_path):
    """测试带上下文的执行"""
    db = Database(test_db_path)
    await db.initialize()

    session = Session(
        id="sess-1",
        title="Test",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await db.create_session(session)

    router = SimpleRouter()
    llm_client = MockLLMClient()
    executor = AgentExecutor(db, router, llm_client)

    # 第一条消息
    await executor.execute("My name is Alice", session_id="sess-1")

    # 第二条消息（应该能访问上下文）
    result = await executor.execute("What's my name?", session_id="sess-1")

    assert result is not None

    # 验证历史消息数量
    ctx = ContextManager(db, "sess-1")
    history = await ctx.get_history()
    assert len(history) == 4  # 2 user + 2 assistant

    await db.close()
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/core/test_executor.py -v
```

Expected: FAIL

**Step 3: 实现执行器**

文件: `src/core/executor.py`

```python
"""Agent执行器"""
from typing import Dict, Any
from .router import SimpleRouter, ExecutionPlan
from .llm_client import MockLLMClient
from .context import ContextManager
from ..storage.database import Database


class AgentExecutor:
    """Agent执行器 - 协调路由、上下文和LLM"""

    def __init__(
        self,
        db: Database,
        router: SimpleRouter,
        llm_client: MockLLMClient
    ):
        self.db = db
        self.router = router
        self.llm_client = llm_client

    async def execute(self, user_input: str, session_id: str) -> Dict[str, Any]:
        """
        执行用户请求

        Args:
            user_input: 用户输入
            session_id: 会话ID

        Returns:
            执行结果，包含response等字段
        """
        # 1. 获取上下文
        ctx = ContextManager(self.db, session_id)

        # 2. 保存用户消息
        await ctx.add_message("user", user_input)

        # 3. 路由决策
        plan = self.router.route(user_input, context=ctx)

        # 4. 执行计划
        if plan.type == "simple_query":
            response = await self._execute_simple_query(ctx)
        else:
            # 未来：处理其他类型（task、skill、mcp）
            response = "暂不支持该类型的请求"

        # 5. 保存助手回复
        await ctx.add_message("assistant", response)

        # 6. 返回结果
        return {
            "response": response,
            "session_id": session_id,
            "plan_type": plan.type
        }

    async def _execute_simple_query(self, ctx: ContextManager) -> str:
        """执行简单问答"""
        # 获取历史消息
        messages = await ctx.format_for_llm(limit=10)

        # 调用LLM
        response = await self.llm_client.chat(messages)

        return response
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/core/test_executor.py -v
```

Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/core/executor.py tests/core/test_executor.py
git commit -m "feat(core): add agent executor

- Add AgentExecutor to coordinate components
- Implement simple query execution
- Save messages to database
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 8: FastAPI应用 - 基础API

**Files:**
- Create: `src/api/routes.py`
- Create: `src/main.py`
- Create: `tests/api/test_routes.py`

**Step 1: 编写API测试**

文件: `tests/api/test_routes.py`

```python
"""测试API路由"""
import pytest
from httpx import AsyncClient
from src.main import app


@pytest.mark.asyncio
async def test_health_check():
    """测试健康检查"""
    async with AsyncClient(app=app, base_url="http://test") as client:
        response = await client.get("/health")
        assert response.status_code == 200
        assert response.json() == {"status": "ok"}


@pytest.mark.asyncio
async def test_chat_endpoint():
    """测试聊天接口"""
    async with AsyncClient(app=app, base_url="http://test") as client:
        response = await client.post(
            "/api/chat",
            json={
                "message": "Hello",
                "session_id": "test-session"
            }
        )
        assert response.status_code == 200
        data = response.json()
        assert "response" in data
        assert "session_id" in data


@pytest.mark.asyncio
async def test_chat_without_session_id():
    """测试不带session_id的聊天"""
    async with AsyncClient(app=app, base_url="http://test") as client:
        response = await client.post(
            "/api/chat",
            json={"message": "Hello"}
        )
        assert response.status_code == 200
        data = response.json()
        assert "session_id" in data
        # 应该自动生成session_id
        assert data["session_id"] is not None
```

**Step 2: 运行测试（预期失败）**

```bash
pytest tests/api/test_routes.py -v
```

Expected: FAIL

**Step 3: 实现API路由**

文件: `src/api/routes.py`

```python
"""API路由"""
import uuid
from datetime import datetime
from fastapi import APIRouter
from pydantic import BaseModel
from typing import Optional

from ..core.executor import AgentExecutor
from ..core.router import SimpleRouter
from ..core.llm_client import MockLLMClient
from ..storage.database import Database
from ..storage.models import Session


router = APIRouter()


class ChatRequest(BaseModel):
    """聊天请求"""
    message: str
    session_id: Optional[str] = None


class ChatResponse(BaseModel):
    """聊天响应"""
    response: str
    session_id: str
    plan_type: Optional[str] = None


# 全局实例（简化版，生产环境应使用依赖注入）
_db: Optional[Database] = None
_executor: Optional[AgentExecutor] = None


async def get_executor() -> AgentExecutor:
    """获取executor实例"""
    global _db, _executor

    if _executor is None:
        from pathlib import Path

        db_path = Path("data/agent.db")
        _db = Database(db_path)
        await _db.initialize()

        router = SimpleRouter()
        llm_client = MockLLMClient()
        _executor = AgentExecutor(_db, router, llm_client)

    return _executor


@router.get("/health")
async def health_check():
    """健康检查"""
    return {"status": "ok"}


@router.post("/api/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    """
    聊天接口

    Args:
        request: 包含message和可选的session_id

    Returns:
        助手回复和session_id
    """
    executor = await get_executor()

    # 生成或使用提供的session_id
    session_id = request.session_id or f"sess-{uuid.uuid4().hex[:8]}"

    # 如果是新会话，创建会话记录
    if not request.session_id:
        session = Session(
            id=session_id,
            title=request.message[:50],  # 使用前50个字符作为标题
            created_at=datetime.now(),
            updated_at=datetime.now()
        )
        await executor.db.create_session(session)

    # 执行请求
    result = await executor.execute(request.message, session_id)

    return ChatResponse(
        response=result["response"],
        session_id=result["session_id"],
        plan_type=result.get("plan_type")
    )
```

文件: `src/main.py`

```python
"""FastAPI应用入口"""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from .api.routes import router

app = FastAPI(
    title="General Agent",
    description="通用Agent系统 - 支持Skill、MCP、RAG",
    version="0.1.0"
)

# CORS中间件（开发环境）
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 注册路由
app.include_router(router)


@app.on_event("startup")
async def startup_event():
    """启动事件"""
    print("🚀 General Agent starting...")


@app.on_event("shutdown")
async def shutdown_event():
    """关闭事件"""
    print("👋 General Agent shutting down...")
```

**Step 4: 运行测试（预期通过）**

```bash
pytest tests/api/test_routes.py -v
```

Expected: PASS (3 tests)

**Step 5: 手动测试API**

```bash
# 启动服务
uvicorn src.main:app --reload

# 在另一个终端测试
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello"}'
```

**Step 6: Commit**

```bash
git add src/api/routes.py src/main.py tests/api/test_routes.py
git commit -m "feat(api): add FastAPI application

- Add chat API endpoint
- Add health check endpoint
- Auto-generate session_id if not provided
- Full test coverage

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 9: 简单Web界面

**Files:**
- Create: `templates/index.html`
- Create: `static/css/style.css`
- Create: `static/js/app.js`
- Modify: `src/main.py`

**Step 1: 创建HTML模板**

文件: `templates/index.html`

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>General Agent</title>
    <link rel="stylesheet" href="/static/css/style.css">
</head>
<body>
    <div class="container">
        <header>
            <h1>General Agent</h1>
            <p class="subtitle">通用AI助手 - MVP版本</p>
        </header>

        <main class="chat-container">
            <div id="messages" class="messages"></div>

            <div class="input-area">
                <textarea
                    id="input"
                    placeholder="输入消息..."
                    rows="2"
                ></textarea>
                <button id="send-btn" onclick="sendMessage()">发送</button>
            </div>
        </main>

        <footer>
            <p>状态: <span id="status">已连接</span></p>
        </footer>
    </div>

    <script src="/static/js/app.js"></script>
</body>
</html>
```

**Step 2: 创建CSS样式**

文件: `static/css/style.css`

```css
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    background: #f5f5f5;
    color: #333;
}

.container {
    max-width: 800px;
    margin: 0 auto;
    height: 100vh;
    display: flex;
    flex-direction: column;
    background: white;
}

header {
    padding: 20px;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    text-align: center;
}

header h1 {
    font-size: 24px;
    margin-bottom: 5px;
}

.subtitle {
    font-size: 14px;
    opacity: 0.9;
}

.chat-container {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

.messages {
    flex: 1;
    padding: 20px;
    overflow-y: auto;
}

.message {
    margin-bottom: 15px;
    padding: 12px 16px;
    border-radius: 12px;
    max-width: 70%;
    word-wrap: break-word;
}

.message.user {
    background: #667eea;
    color: white;
    margin-left: auto;
    text-align: right;
}

.message.assistant {
    background: #e9ecef;
    color: #333;
}

.input-area {
    padding: 20px;
    border-top: 1px solid #ddd;
    display: flex;
    gap: 10px;
}

#input {
    flex: 1;
    padding: 12px;
    border: 1px solid #ddd;
    border-radius: 8px;
    font-size: 14px;
    resize: none;
    font-family: inherit;
}

#input:focus {
    outline: none;
    border-color: #667eea;
}

#send-btn {
    padding: 12px 24px;
    background: #667eea;
    color: white;
    border: none;
    border-radius: 8px;
    cursor: pointer;
    font-size: 14px;
    font-weight: 500;
}

#send-btn:hover {
    background: #5568d3;
}

#send-btn:disabled {
    background: #ccc;
    cursor: not-allowed;
}

footer {
    padding: 10px 20px;
    border-top: 1px solid #ddd;
    font-size: 12px;
    color: #666;
}

#status {
    color: #28a745;
    font-weight: 500;
}
```

**Step 3: 创建JavaScript交互**

文件: `static/js/app.js`

```javascript
// 当前会话ID
let currentSessionId = null;

// 发送消息
async function sendMessage() {
    const input = document.getElementById('input');
    const message = input.value.trim();

    if (!message) return;

    // 显示用户消息
    appendMessage('user', message);

    // 清空输入框
    input.value = '';

    // 禁用发送按钮
    const sendBtn = document.getElementById('send-btn');
    sendBtn.disabled = true;
    sendBtn.textContent = '发送中...';

    try {
        // 发送请求
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                message: message,
                session_id: currentSessionId
            })
        });

        const data = await response.json();

        // 保存session_id
        if (!currentSessionId) {
            currentSessionId = data.session_id;
        }

        // 显示助手回复
        appendMessage('assistant', data.response);

    } catch (error) {
        console.error('Error:', error);
        appendMessage('assistant', '抱歉，发生了错误。请稍后再试。');
        updateStatus('错误', 'red');
    } finally {
        // 恢复发送按钮
        sendBtn.disabled = false;
        sendBtn.textContent = '发送';
    }
}

// 添加消息到界面
function appendMessage(role, content) {
    const messagesDiv = document.getElementById('messages');
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${role}`;
    messageDiv.textContent = content;
    messagesDiv.appendChild(messageDiv);

    // 滚动到底部
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// 更新状态
function updateStatus(text, color = '#28a745') {
    const status = document.getElementById('status');
    status.textContent = text;
    status.style.color = color;
}

// 监听Enter键发送
document.addEventListener('DOMContentLoaded', () => {
    const input = document.getElementById('input');

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });
});
```

**Step 4: 修改main.py添加静态文件和模板支持**

文件: `src/main.py`（追加）

```python
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from fastapi import Request

# ... 现有代码 ...

# 挂载静态文件
app.mount("/static", StaticFiles(directory="static"), name="static")

# 模板
templates = Jinja2Templates(directory="templates")


@app.get("/")
async def index(request: Request):
    """主页"""
    return templates.TemplateResponse("index.html", {"request": request})
```

**Step 5: 手动测试Web界面**

```bash
# 启动服务
uvicorn src.main:app --reload

# 打开浏览器访问
# http://localhost:8000
```

测试步骤：
1. 输入"Hello"并发送
2. 检查是否收到回复
3. 输入多条消息，验证上下文是否保持

**Step 6: Commit**

```bash
git add templates/ static/ src/main.py
git commit -m "feat(ui): add simple web interface

- Add HTML template with clean design
- Add CSS styling
- Add JavaScript for chat interaction
- Support Enter key to send
- Auto-scroll to latest message

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 10: 端到端测试

**Files:**
- Create: `tests/test_e2e.py`

**Step 1: 编写端到端测试**

文件: `tests/test_e2e.py`

```python
"""端到端测试"""
import pytest
from httpx import AsyncClient
from src.main import app


@pytest.mark.asyncio
async def test_full_conversation_flow():
    """测试完整对话流程"""
    async with AsyncClient(app=app, base_url="http://test") as client:
        # 第一条消息
        response1 = await client.post(
            "/api/chat",
            json={"message": "My name is Alice"}
        )
        assert response1.status_code == 200
        data1 = response1.json()
        session_id = data1["session_id"]

        # 第二条消息（使用相同session_id）
        response2 = await client.post(
            "/api/chat",
            json={
                "message": "What's my name?",
                "session_id": session_id
            }
        )
        assert response2.status_code == 200
        data2 = response2.json()
        assert data2["session_id"] == session_id

        # 第三条消息
        response3 = await client.post(
            "/api/chat",
            json={
                "message": "Thanks!",
                "session_id": session_id
            }
        )
        assert response3.status_code == 200


@pytest.mark.asyncio
async def test_multiple_sessions():
    """测试多个独立会话"""
    async with AsyncClient(app=app, base_url="http://test") as client:
        # 会话1
        response1 = await client.post(
            "/api/chat",
            json={"message": "Session 1"}
        )
        session1_id = response1.json()["session_id"]

        # 会话2
        response2 = await client.post(
            "/api/chat",
            json={"message": "Session 2"}
        )
        session2_id = response2.json()["session_id"]

        # 验证是不同的会话
        assert session1_id != session2_id
```

**Step 2: 运行端到端测试**

```bash
pytest tests/test_e2e.py -v
```

Expected: PASS (2 tests)

**Step 3: 运行所有测试**

```bash
pytest -v
```

Expected: 所有测试通过

**Step 4: 检查测试覆盖率**

```bash
pytest --cov=src --cov-report=html
```

Expected: 覆盖率 > 80%

**Step 5: Commit**

```bash
git add tests/test_e2e.py
git commit -m "test: add end-to-end tests

- Test full conversation flow
- Test multiple independent sessions
- Verify context persistence

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 11: 文档和README

**Files:**
- Modify: `README.md`
- Create: `docs/api.md`

**Step 1: 更新README**

文件: `README.md`

```markdown
# General Agent

通用Agent系统，支持：
- ✅ **Skill协议**（兼容Claude Code）
- ✅ **MCP客户端**（Model Context Protocol）
- ✅ **RAG**（检索增强生成）

## 功能特性

### Phase 1（当前版本）✅
- 基础聊天功能
- 会话上下文管理
- 简洁Web界面
- SQLite持久化存储

### 未来版本
- Skill System（Phase 2）
- MCP集成（Phase 3）
- RAG引擎（Phase 4）
- 高级功能（Phase 5-6）

## 快速开始

### 1. 安装依赖

```bash
# 使用uv（推荐）
uv pip install -e ".[dev]"

# 或使用pip
pip install -e ".[dev]"
```

### 2. 运行测试

```bash
pytest
```

### 3. 启动服务

```bash
uvicorn src.main:app --reload
```

访问 http://localhost:8000

### 4. API文档

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
│   ├── core/          # 核心模块
│   ├── storage/       # 存储层
│   ├── api/           # API路由
│   └── main.py        # 应用入口
├── tests/             # 测试
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

## 架构

详见：
- 设计文档: `docs/plans/2026-03-02-general-agent-design.md`
- 实现计划: `docs/plans/2026-03-02-phase1-foundation.md`

## 许可证

MIT
```

**Step 2: 创建API文档**

文件: `docs/api.md`

```markdown
# API文档

## 基础信息

- Base URL: `http://localhost:8000`
- Content-Type: `application/json`

## 端点

### 1. 健康检查

```
GET /health
```

响应：
```json
{
  "status": "ok"
}
```

### 2. 聊天接口

```
POST /api/chat
```

请求体：
```json
{
  "message": "用户输入（必需）",
  "session_id": "会话ID（可选）"
}
```

响应：
```json
{
  "response": "助手回复",
  "session_id": "会话ID",
  "plan_type": "simple_query"
}
```

**说明：**
- 如果不提供`session_id`，系统会自动生成一个新会话
- 相同`session_id`的请求会共享上下文
- `plan_type`表示执行计划类型（当前仅支持`simple_query`）

## 错误处理

所有错误响应格式：
```json
{
  "detail": "错误描述"
}
```

HTTP状态码：
- `200` - 成功
- `400` - 请求参数错误
- `500` - 服务器内部错误

## 示例

### Python

```python
import requests

response = requests.post(
    "http://localhost:8000/api/chat",
    json={
        "message": "Hello",
        "session_id": "my-session"
    }
)
print(response.json())
```

### JavaScript

```javascript
fetch('http://localhost:8000/api/chat', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        message: 'Hello',
        session_id: 'my-session'
    })
})
.then(res => res.json())
.then(data => console.log(data));
```

### cURL

```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "my-session"}'
```
```

**Step 3: Commit**

```bash
git add README.md docs/api.md
git commit -m "docs: update README and add API documentation

- Update README with Phase 1 features
- Add quick start guide
- Add API documentation
- Add development instructions

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 12: 最终验证和清理

**Step 1: 运行完整测试套件**

```bash
# 运行所有测试
pytest -v

# 检查覆盖率
pytest --cov=src --cov-report=term --cov-report=html

# 代码质量检查
ruff check src/ tests/
```

Expected: 所有检查通过，覆盖率 > 80%

**Step 2: 启动服务并手动测试**

```bash
uvicorn src.main:app --reload
```

测试清单：
- [ ] 访问 http://localhost:8000 看到Web界面
- [ ] 发送消息，收到回复
- [ ] 多轮对话，验证上下文保持
- [ ] 访问 http://localhost:8000/docs 查看API文档
- [ ] 使用curl测试API端点

**Step 3: 清理和整理**

```bash
# 删除临时文件
find . -name "*.pyc" -delete
find . -name "__pycache__" -delete

# 确保data目录在.gitignore中
echo "data/" >> .gitignore
```

**Step 4: 创建最终的汇总commit**

```bash
git add .
git commit -m "feat: complete Phase 1 - foundation

Phase 1 Implementation Complete ✅

Components:
- SQLite storage layer (models + database operations)
- Context manager (session history management)
- Simple router (MVP routing logic)
- Mock LLM client (for testing)
- Agent executor (core orchestration)
- FastAPI application (REST API)
- Simple web interface (HTML + CSS + JS)

Testing:
- Unit tests for all components
- Integration tests
- End-to-end tests
- Test coverage > 80%

Documentation:
- Updated README
- API documentation
- Architecture design document

Next Steps:
- Phase 2: Skill System
- Phase 3: MCP Integration
- Phase 4: RAG Engine

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 完成检查清单

Phase 1完成标准：

- [ ] 所有测试通过（单元 + 集成 + E2E）
- [ ] 测试覆盖率 ≥ 80%
- [ ] Web界面可用，支持基础聊天
- [ ] API文档完整
- [ ] 代码质量检查通过（ruff + mypy）
- [ ] 会话上下文正确保持
- [ ] SQLite数据持久化工作正常
- [ ] README和文档更新

---

## 后续Phase预览

### Phase 2: Skill System（预估1周）
- Skill加载器（支持.ignore）
- Skill解析器（YAML + Markdown）
- Skill执行器（prompt/script模式）
- 示例skills

### Phase 3: MCP集成（预估1周）
- MCP连接管理
- 工具发现和调用
- 资源访问
- 示例MCP服务器集成

### Phase 4: RAG Engine（预估2周）
- 文档索引器
- 向量数据库（Chroma）
- 混合检索器
- 知识库管理

---

**估计时间：** Phase 1总计约10-15小时（包含测试和文档）

**关键原则：**
- TDD严格执行（先写测试）
- 频繁commit（每个task完成后）
- DRY和YAGNI
- 完整的测试覆盖
