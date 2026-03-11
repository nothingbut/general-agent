"""Tests for MCP connection manager."""
import asyncio
import pytest
from src.mcp.connection_manager import MCPConnectionManager
from src.mcp.exceptions import MCPServerDisabledError


@pytest.mark.asyncio
async def test_manager_initialization(temp_mcp_config):
    """Test connection manager can be initialized."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    assert manager is not None
    assert len(manager.connections) == 0
    assert "filesystem" in manager.config.servers


@pytest.mark.asyncio
async def test_lazy_connection_creation(temp_mcp_config, mock_mcp_connection, monkeypatch):
    """Test connections are created lazily on first access."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    # Mock the _start_server method
    async def mock_start(server_name):
        return mock_mcp_connection

    monkeypatch.setattr(manager, "_start_server", mock_start)

    # First call should create connection
    session1 = await manager.get_connection("filesystem")
    assert session1 is mock_mcp_connection.session
    assert len(manager.connections) == 1

    # Second call should reuse connection
    session2 = await manager.get_connection("filesystem")
    assert session2 is session1
    assert len(manager.connections) == 1


@pytest.mark.asyncio
async def test_disabled_server_raises_error(temp_mcp_config):
    """Test accessing disabled server raises error."""
    manager = MCPConnectionManager(str(temp_mcp_config))
    manager._disabled_servers.add("filesystem")

    with pytest.raises(MCPServerDisabledError) as exc_info:
        await manager.get_connection("filesystem")

    assert "filesystem" in str(exc_info.value)


@pytest.mark.asyncio
async def test_concurrent_connection_requests(temp_mcp_config, mock_mcp_connection, monkeypatch):
    """Test multiple concurrent requests only start server once."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    start_count = 0

    async def mock_start(server_name):
        nonlocal start_count
        start_count += 1
        await asyncio.sleep(0.1)  # Simulate startup delay
        return mock_mcp_connection

    monkeypatch.setattr(manager, "_start_server", mock_start)

    # Launch 5 concurrent requests
    tasks = [manager.get_connection("filesystem") for _ in range(5)]
    sessions = await asyncio.gather(*tasks)

    # All should get same session, server started only once
    assert all(session is mock_mcp_connection.session for session in sessions)
    assert start_count == 1


@pytest.mark.asyncio
async def test_multiple_servers_independent_locks(temp_mcp_config, mock_mcp_connection, monkeypatch):
    """Test different servers use different locks."""
    # Add a second server to config
    temp_mcp_config.parent.joinpath("mcp_config.yaml").write_text("""
servers:
  filesystem:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"]
    security:
      allowed_directories: ["/tmp"]
      allowed_operations: []
      denied_operations: []
  github:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-github"]
    security:
      allowed_directories: []
      allowed_operations: []
      denied_operations: []
global:
  enabled: true
""")

    manager = MCPConnectionManager(str(temp_mcp_config))

    async def mock_start(server_name):
        return mock_mcp_connection

    monkeypatch.setattr(manager, "_start_server", mock_start)

    # Get locks for different servers
    lock1 = manager._get_lock("filesystem")
    lock2 = manager._get_lock("github")
    lock3 = manager._get_lock("filesystem")  # Same as lock1

    assert lock1 is not lock2  # Different servers have different locks
    assert lock1 is lock3  # Same server reuses lock
