"""Tests for startup checks"""
import pytest
import asyncio
import os
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch
import aiohttp

from src.cli.startup import check_ollama_connection, startup_checks


@pytest.mark.asyncio
async def test_check_ollama_connection_success():
    """Test successful Ollama connection check"""
    with patch('aiohttp.ClientSession.get') as mock_get:
        mock_get.return_value.__aenter__.return_value.status = 200

        result = await check_ollama_connection()

        assert result is True
        mock_get.assert_called_once()


@pytest.mark.asyncio
async def test_check_ollama_connection_non_200_status():
    """Test Ollama connection check with non-200 status"""
    with patch('aiohttp.ClientSession.get') as mock_get:
        mock_get.return_value.__aenter__.return_value.status = 404

        result = await check_ollama_connection()

        assert result is False


@pytest.mark.asyncio
async def test_check_ollama_connection_timeout():
    """Test Ollama connection check with timeout"""
    with patch('aiohttp.ClientSession.get', side_effect=asyncio.TimeoutError("Connection timeout")):
        result = await check_ollama_connection()

        assert result is False


@pytest.mark.asyncio
async def test_check_ollama_connection_client_error():
    """Test Ollama connection check with aiohttp.ClientError"""
    with patch('aiohttp.ClientSession.get', side_effect=aiohttp.ClientError("Connection refused")):
        result = await check_ollama_connection()

        assert result is False


@pytest.mark.asyncio
async def test_check_ollama_connection_os_error():
    """Test Ollama connection check with OSError"""
    with patch('aiohttp.ClientSession.get', side_effect=OSError("Network unreachable")):
        result = await check_ollama_connection()

        assert result is False


@pytest.mark.asyncio
async def test_check_ollama_connection_custom_url():
    """Test Ollama connection check with custom URL from environment"""
    custom_url = "http://custom-ollama:8080"
    with patch.dict(os.environ, {'OLLAMA_BASE_URL': custom_url}):
        with patch('aiohttp.ClientSession.get') as mock_get:
            mock_get.return_value.__aenter__.return_value.status = 200

            result = await check_ollama_connection()

            assert result is True
            # Verify custom URL was used
            call_args = mock_get.call_args
            assert custom_url in str(call_args)


@pytest.mark.asyncio
async def test_startup_checks_mock_mode(tmp_path, monkeypatch):
    """Test startup checks with USE_OLLAMA=false (Mock mode)"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        # Should complete without errors
        await startup_checks(verbose=False)

        # Verify data directory was created
        data_dir = tmp_path / "data"
        assert data_dir.exists()


@pytest.mark.asyncio
async def test_startup_checks_mock_mode_verbose(tmp_path, monkeypatch, capsys):
    """Test startup checks with verbose output in Mock mode"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        await startup_checks(verbose=True)

        captured = capsys.readouterr()
        assert "启动检查完成" in captured.out


@pytest.mark.asyncio
async def test_startup_checks_data_directory_creation(tmp_path, monkeypatch):
    """Test data directory is created if missing"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    data_dir = tmp_path / "data"
    assert not data_dir.exists()

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        await startup_checks(verbose=False)

        assert data_dir.exists()


@pytest.mark.asyncio
async def test_startup_checks_data_directory_already_exists(tmp_path, monkeypatch):
    """Test startup checks when data directory already exists"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    # Pre-create data directory
    data_dir = tmp_path / "data"
    data_dir.mkdir()

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        # Should not raise error
        await startup_checks(verbose=False)

        assert data_dir.exists()


@pytest.mark.asyncio
async def test_startup_checks_env_file_missing_warning(tmp_path, monkeypatch, capsys):
    """Test warning when .env file is missing"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        await startup_checks(verbose=True)

        captured = capsys.readouterr()
        assert ".env 文件不存在" in captured.out


@pytest.mark.asyncio
async def test_startup_checks_env_file_exists(tmp_path, monkeypatch, capsys):
    """Test no warning when .env file exists"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    # Create .env file
    env_file = tmp_path / ".env"
    env_file.write_text("USE_OLLAMA=false\n")

    with patch.dict(os.environ, {'USE_OLLAMA': 'false'}, clear=False):
        await startup_checks(verbose=True)

        captured = capsys.readouterr()
        # Should not contain .env warning
        assert ".env 文件不存在" not in captured.out


@pytest.mark.asyncio
async def test_startup_checks_ollama_mode_success(tmp_path, monkeypatch, capsys):
    """Test startup checks with USE_OLLAMA=true and successful connection"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'true'}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=True):
            await startup_checks(verbose=True)

            captured = capsys.readouterr()
            assert "Ollama 连接正常" in captured.out


@pytest.mark.asyncio
async def test_startup_checks_ollama_mode_failure(tmp_path, monkeypatch):
    """Test startup checks with USE_OLLAMA=true and failed connection"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'true'}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=False):
            with pytest.raises(RuntimeError) as exc_info:
                await startup_checks(verbose=False)

            error_msg = str(exc_info.value)
            assert "无法连接到 Ollama 服务" in error_msg
            assert "ollama serve" in error_msg
            assert "USE_OLLAMA=false" in error_msg


@pytest.mark.asyncio
async def test_startup_checks_ollama_mode_failure_verbose(tmp_path, monkeypatch, capsys):
    """Test startup checks with USE_OLLAMA=true, failed connection, and verbose output"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    with patch.dict(os.environ, {'USE_OLLAMA': 'true'}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=False):
            captured_before = capsys.readouterr()  # Clear buffer

            with pytest.raises(RuntimeError):
                await startup_checks(verbose=True)

            captured = capsys.readouterr()
            assert "检查 Ollama 连接" in captured.out


@pytest.mark.asyncio
async def test_startup_checks_ollama_mode_custom_url(tmp_path, monkeypatch):
    """Test startup checks with custom Ollama URL in error message"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    custom_url = "http://custom-host:9999"
    with patch.dict(os.environ, {'USE_OLLAMA': 'true', 'OLLAMA_BASE_URL': custom_url}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=False):
            with pytest.raises(RuntimeError) as exc_info:
                await startup_checks(verbose=False)

            error_msg = str(exc_info.value)
            assert custom_url in error_msg


@pytest.mark.asyncio
async def test_startup_checks_case_insensitive_use_ollama(tmp_path, monkeypatch):
    """Test that USE_OLLAMA is case-insensitive"""
    # Change to temp directory
    monkeypatch.chdir(tmp_path)

    # Test with "True" (capitalized)
    with patch.dict(os.environ, {'USE_OLLAMA': 'True'}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=True):
            await startup_checks(verbose=False)

    # Test with "TRUE" (uppercase)
    with patch.dict(os.environ, {'USE_OLLAMA': 'TRUE'}, clear=False):
        with patch('src.cli.startup.check_ollama_connection', return_value=True):
            await startup_checks(verbose=False)
