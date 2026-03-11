"""测试执行器"""
import pytest
from src.core.executor import AgentExecutor
from src.core.router import SimpleRouter
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
    assert executor.db is db
    assert executor.router is router
    assert executor.llm_client is llm_client

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
    assert "session_id" in result
    assert result["session_id"] == "sess-1"
    assert "plan_type" in result
    assert result["plan_type"] == "simple_query"

    # 验证消息已保存
    ctx = ContextManager(db, "sess-1")
    history = await ctx.get_history()
    assert len(history) == 2  # user + assistant
    assert history[0].role == "user"
    assert history[0].content == "Hello"
    assert history[1].role == "assistant"

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
    result1 = await executor.execute("My name is Alice", session_id="sess-1")
    assert result1 is not None
    assert "response" in result1

    # 第二条消息（应该能访问上下文）
    result2 = await executor.execute("What's my name?", session_id="sess-1")
    assert result2 is not None
    assert "response" in result2

    # 验证历史消息数量
    ctx = ContextManager(db, "sess-1")
    history = await ctx.get_history()
    assert len(history) == 4  # 2 user + 2 assistant
    assert history[0].role == "user"
    assert history[0].content == "My name is Alice"
    assert history[1].role == "assistant"
    assert history[2].role == "user"
    assert history[2].content == "What's my name?"
    assert history[3].role == "assistant"

    await db.close()


@pytest.mark.asyncio
async def test_execute_with_empty_input(test_db_path):
    """测试空输入验证"""
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

    # 测试空字符串
    with pytest.raises(ValueError, match="User input cannot be empty"):
        await executor.execute("", session_id="sess-1")

    # 测试仅包含空格
    with pytest.raises(ValueError, match="User input cannot be empty"):
        await executor.execute("   ", session_id="sess-1")

    await db.close()


@pytest.mark.asyncio
async def test_execute_with_invalid_session(test_db_path):
    """测试无效会话ID验证"""
    db = Database(test_db_path)
    await db.initialize()

    router = SimpleRouter()
    llm_client = MockLLMClient()
    executor = AgentExecutor(db, router, llm_client)

    # 测试空会话ID
    with pytest.raises(ValueError, match="Session ID cannot be empty"):
        await executor.execute("Hello", session_id="")

    # 测试仅包含空格的会话ID
    with pytest.raises(ValueError, match="Session ID cannot be empty"):
        await executor.execute("Hello", session_id="   ")

    await db.close()
