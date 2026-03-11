"""Integration tests for CLI"""
import pytest
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
    try:
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

        # Verify message content
        assert messages[0].role == "user"
        assert messages[0].content == "测试消息"
        assert messages[1].role == "assistant"
        assert messages[1].content  # Verify non-empty
        assert len(messages[1].content) > 0
    finally:
        await db.close()


@pytest.mark.asyncio
async def test_quick_query_creates_session(tmp_path, monkeypatch):
    """测试快速查询创建会话"""
    from src.cli.quick import run_quick_query

    db_path = tmp_path / "test.db"

    # 修改 quick.py 中的 DB_PATH
    monkeypatch.setattr('src.cli.quick.DB_PATH', db_path)

    # 执行快速查询
    result = await run_quick_query("测试", None, False)

    # 验证返回了响应
    assert result is not None
    assert isinstance(result, str)
    assert len(result) > 0  # Verify non-empty response

    # 验证会话已创建
    db = await initialize_database(db_path)
    try:
        sessions = await db.get_all_sessions()

        assert len(sessions) >= 1
        assert sessions[0].title.startswith("测试")
    finally:
        await db.close()
