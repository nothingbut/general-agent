"""端到端测试"""
import pytest
from httpx import AsyncClient, ASGITransport
from pathlib import Path
import tempfile

from src.main import app, get_db, get_executor
from src.storage.database import Database
from src.core.executor import AgentExecutor
from src.core.router import SimpleRouter
from src.core.llm_client import MockLLMClient


@pytest.fixture
async def test_db() -> Database:
    """Create a test database"""
    with tempfile.NamedTemporaryFile(delete=False, suffix=".db") as tmp:
        db_path = Path(tmp.name)

    db = Database(db_path)
    await db.initialize()
    yield db
    await db.close()
    db_path.unlink()


@pytest.fixture
async def test_executor(test_db: Database) -> AgentExecutor:
    """Create a test executor"""
    router = SimpleRouter()
    llm_client = MockLLMClient()
    return AgentExecutor(test_db, router, llm_client)


@pytest.fixture
async def client(test_db: Database, test_executor: AgentExecutor) -> AsyncClient:
    """Create test client with dependency overrides"""
    from src.api import routes
    routes.set_dependencies(lambda: test_db, lambda: test_executor)

    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac

    routes.set_dependencies(get_db, get_executor)


@pytest.mark.asyncio
async def test_full_conversation_flow(client: AsyncClient):
    """测试完整对话流程"""
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
async def test_multiple_sessions(client: AsyncClient):
    """测试多个独立会话"""
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
