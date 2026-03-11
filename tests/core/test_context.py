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


@pytest.mark.asyncio
async def test_add_message_with_invalid_role(test_db_path):
    """测试无效角色会抛出ValueError"""
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

    with pytest.raises(ValueError, match="Invalid role"):
        await ctx.add_message("invalid_role", "Hello")

    await db.close()


@pytest.mark.asyncio
async def test_add_message_with_empty_content(test_db_path):
    """测试空内容会抛出ValueError"""
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

    with pytest.raises(ValueError, match="Content cannot be empty"):
        await ctx.add_message("user", "")

    with pytest.raises(ValueError, match="Content cannot be empty"):
        await ctx.add_message("user", "   ")

    await db.close()


@pytest.mark.asyncio
async def test_context_with_empty_session_id(test_db_path):
    """测试空会话ID会抛出ValueError"""
    db = Database(test_db_path)
    await db.initialize()

    with pytest.raises(ValueError, match="Session ID cannot be empty"):
        ContextManager(db, session_id="")

    with pytest.raises(ValueError, match="Session ID cannot be empty"):
        ContextManager(db, session_id="   ")

    await db.close()


@pytest.mark.asyncio
async def test_get_history_with_zero_limit(test_db_path):
    """测试limit=0返回空列表"""
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

    # 添加消息
    await ctx.add_message("user", "Message 1")
    await ctx.add_message("user", "Message 2")

    # limit=0 应该返回空列表
    messages = await ctx.get_history(limit=0)
    assert len(messages) == 0

    await db.close()
