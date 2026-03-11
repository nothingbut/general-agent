"""Integration tests for Phase 3.2 components."""
import pytest
from unittest.mock import AsyncMock
from src.mcp.connection_manager import MCPConnectionManager
from src.mcp.security import MCPSecurityLayer, SecurityConfig
from src.mcp.tool_executor import MCPToolExecutor
from src.mcp.exceptions import PermissionDeniedError, ConfirmationRequired


@pytest.mark.asyncio
async def test_full_stack_allowed_operation(temp_mcp_config, mock_mcp_connection, monkeypatch):
    """Test full stack from manager to executor for allowed operation."""
    # Setup components
    manager = MCPConnectionManager(str(temp_mcp_config))

    # Mock server startup
    async def mock_start(server_name):
        return mock_mcp_connection
    monkeypatch.setattr(manager, "_start_server", mock_start)

    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # Execute tool
    result = await executor.call_tool(
        "filesystem",
        "read_file",
        {"path": "/tmp/test.txt"},
        "session_1"
    )

    # Result is now a list of content blocks
    assert len(result["content"]) == 1
    assert result["content"][0]["text"] == "test result"


@pytest.mark.asyncio
async def test_full_stack_denied_by_policy(temp_mcp_config, monkeypatch):
    """Test full stack with denied operation."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=[],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # Should fail without even connecting to server
    with pytest.raises(PermissionDeniedError):
        await executor.call_tool(
            "filesystem",
            "delete_file",
            {"path": "/tmp/test.txt"},
            "session_1"
        )


@pytest.mark.asyncio
async def test_full_stack_path_whitelist(temp_mcp_config, tmp_path, monkeypatch):
    """Test full stack with path whitelist enforcement."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    # Use tmp_path as allowed directory
    config = SecurityConfig(
        allowed_directories=[str(tmp_path)],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # Try to access path outside whitelist
    with pytest.raises(PermissionDeniedError) as exc_info:
        await executor.call_tool(
            "filesystem",
            "read_file",
            {"path": "/etc/passwd"},
            "session_1"
        )

    assert "outside allowed directories" in str(exc_info.value)


@pytest.mark.asyncio
async def test_tool_discovery_through_executor(temp_mcp_config, mock_mcp_connection, monkeypatch):
    """Test tool discovery flows through all components."""
    manager = MCPConnectionManager(str(temp_mcp_config))

    async def mock_start(server_name):
        return mock_mcp_connection
    monkeypatch.setattr(manager, "_start_server", mock_start)

    security = AsyncMock()
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    tools = await executor.discover_tools("filesystem")

    assert len(tools) == 2
    assert any(t["name"] == "read_file" for t in tools)
    assert any(t["name"] == "write_file" for t in tools)
