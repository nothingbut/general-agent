"""Tests for core component initialization"""
import pytest
from pathlib import Path
import tempfile
from unittest.mock import AsyncMock, MagicMock, patch

from src.cli.core_init import initialize_database, initialize_executor
from src.storage.database import Database
from src.core.executor import AgentExecutor


@pytest.mark.asyncio
async def test_initialize_database():
    """Test database initialization"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        db = await initialize_database(db_path)

        assert db is not None
        assert isinstance(db, Database)
        assert db_path.exists()

        await db.close()


@pytest.mark.asyncio
async def test_initialize_executor():
    """Test executor initialization"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"
        db = await initialize_database(db_path)

        executor = await initialize_executor(db, verbose=False)

        assert executor is not None
        assert isinstance(executor, AgentExecutor)
        assert executor.db is not None
        assert executor.router is not None
        assert executor.llm_client is not None

        await db.close()
