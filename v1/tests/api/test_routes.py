"""Tests for FastAPI routes"""
import pytest
from httpx import AsyncClient, ASGITransport
from pathlib import Path
import tempfile
from datetime import datetime

from src.main import app, get_db, get_executor
from src.storage.database import Database
from src.storage.models import Session
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


async def test_health_check(client: AsyncClient) -> None:
    """Test health check endpoint"""
    response = await client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert "timestamp" in data


async def test_chat_endpoint(client: AsyncClient, test_db: Database) -> None:
    """Test chat endpoint with existing session"""
    # Create a session first
    session = Session(
        id="test-session-123",
        title="Test Session",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    await test_db.create_session(session)

    # Send chat request
    response = await client.post(
        "/api/chat",
        json={
            "message": "Hello, how are you?",
            "session_id": "test-session-123"
        }
    )

    assert response.status_code == 200
    data = response.json()
    assert "response" in data
    assert data["session_id"] == "test-session-123"
    assert "plan_type" in data
    assert isinstance(data["response"], str)
    assert len(data["response"]) > 0


async def test_chat_without_session_id(client: AsyncClient) -> None:
    """Test chat endpoint auto-creates session when session_id not provided"""
    response = await client.post(
        "/api/chat",
        json={
            "message": "Hello without session"
        }
    )

    assert response.status_code == 200
    data = response.json()
    assert "response" in data
    assert "session_id" in data
    assert data["session_id"] is not None
    assert len(data["session_id"]) > 0
    assert "plan_type" in data


async def test_chat_empty_message(client: AsyncClient) -> None:
    """Test chat endpoint rejects empty message"""
    response = await client.post(
        "/api/chat",
        json={
            "message": "",
            "session_id": "test-session"
        }
    )

    assert response.status_code == 422


async def test_chat_whitespace_message(client: AsyncClient) -> None:
    """Test chat endpoint rejects whitespace-only message"""
    response = await client.post(
        "/api/chat",
        json={
            "message": "   ",
            "session_id": "test-session"
        }
    )

    assert response.status_code == 422
    data = response.json()
    assert "detail" in data
