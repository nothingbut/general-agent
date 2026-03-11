"""Tests for quick query mode"""
import pytest
from pathlib import Path
import tempfile
from unittest.mock import AsyncMock, MagicMock, patch
import uuid

from src.cli.quick import run_quick_query


@pytest.mark.asyncio
async def test_run_quick_query_returns_string():
    """Test that quick query returns a string"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        # Mock DB_PATH
        with patch('src.cli.quick.DB_PATH', db_path):
            query = "What is the capital of France?"
            result = await run_quick_query(query, verbose=False)

            assert result is not None
            assert isinstance(result, str)
            assert len(result) > 0


@pytest.mark.asyncio
async def test_run_quick_query_with_session_id():
    """Test with specified session ID"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        # Mock DB_PATH
        with patch('src.cli.quick.DB_PATH', db_path):
            query = "Hello, how are you?"
            session_id = "test-session-123"
            result = await run_quick_query(query, session_id=session_id, verbose=False)

            assert result is not None
            assert isinstance(result, str)
            assert len(result) > 0


@pytest.mark.asyncio
async def test_run_quick_query_handles_connection_error():
    """Test handling of ConnectionError (Ollama service down)"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        with patch('src.cli.quick.DB_PATH', db_path):
            # Mock executor to raise ConnectionError
            with patch('src.cli.quick.initialize_executor') as mock_init_executor:
                mock_executor = AsyncMock()
                mock_executor.execute.side_effect = ConnectionError("Ollama service unavailable")
                mock_init_executor.return_value = mock_executor

                query = "Test query"
                result = await run_quick_query(query, verbose=False)

                assert isinstance(result, str)
                assert "connection" in result.lower() or "service" in result.lower()


@pytest.mark.asyncio
async def test_run_quick_query_handles_timeout_error():
    """Test handling of TimeoutError (slow model)"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        with patch('src.cli.quick.DB_PATH', db_path):
            # Mock executor to raise TimeoutError
            with patch('src.cli.quick.initialize_executor') as mock_init_executor:
                mock_executor = AsyncMock()
                mock_executor.execute.side_effect = TimeoutError("Request timed out")
                mock_init_executor.return_value = mock_executor

                query = "Test query"
                result = await run_quick_query(query, verbose=False)

                assert isinstance(result, str)
                assert "timeout" in result.lower() or "slow" in result.lower()


@pytest.mark.asyncio
async def test_run_quick_query_creates_temporary_session():
    """Test that temporary session is created when no session_id provided"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        with patch('src.cli.quick.DB_PATH', db_path):
            query = "Test query"

            # Patch uuid to verify temporary session ID format
            with patch('src.cli.quick.uuid.uuid4') as mock_uuid:
                mock_uuid.return_value = MagicMock()
                mock_uuid.return_value.__str__ = MagicMock(return_value="test-uuid-123")

                result = await run_quick_query(query, verbose=False)

                assert result is not None
                assert isinstance(result, str)


@pytest.mark.asyncio
async def test_run_quick_query_cleans_up_database():
    """Test that database connection is properly closed"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        with patch('src.cli.quick.DB_PATH', db_path):
            # Mock database to verify close is called
            with patch('src.cli.quick.initialize_database') as mock_init_db:
                mock_db = AsyncMock()
                mock_init_db.return_value = mock_db

                query = "Test query"
                await run_quick_query(query, verbose=False)

                # Verify close was called
                mock_db.close.assert_called_once()


@pytest.mark.asyncio
async def test_run_quick_query_empty_input():
    """Test handling of empty query input"""
    with tempfile.TemporaryDirectory() as tmpdir:
        db_path = Path(tmpdir) / "test.db"

        with patch('src.cli.quick.DB_PATH', db_path):
            query = ""
            result = await run_quick_query(query, verbose=False)

            # Should return error message
            assert isinstance(result, str)
            assert "empty" in result.lower() or "error" in result.lower()
