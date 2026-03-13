"""Tests for MCP tool executor."""
import pytest
from unittest.mock import AsyncMock
from src.mcp.tool_executor import MCPToolExecutor
from src.mcp.exceptions import PermissionDeniedError, ConfirmationRequired


@pytest.mark.asyncio
async def test_executor_initialization(mock_mcp_connection, monkeypatch):
    """Test tool executor can be initialized."""
    from src.mcp.connection_manager import MCPConnectionManager
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    # Mock manager
    manager = AsyncMock(spec=MCPConnectionManager)

    # Mock security
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)

    # Mock database
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    assert executor is not None
    assert executor.manager is manager
    assert executor.security is security


@pytest.mark.asyncio
async def test_tool_discovery(mock_mcp_session, monkeypatch):
    """Test tool discovery from server."""
    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_session)

    security = AsyncMock()
    db = AsyncMock()

    from src.mcp.tool_executor import MCPToolExecutor
    executor = MCPToolExecutor(manager, security, db)

    tools = await executor.discover_tools("filesystem")

    assert len(tools) == 2
    assert tools[0]["name"] == "read_file"
    assert tools[1]["name"] == "write_file"


@pytest.mark.asyncio
async def test_call_tool_with_allowed_operation(mock_mcp_session):
    """Test calling tool with allowed operation."""
    from src.mcp.tool_executor import MCPToolExecutor
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_session)

    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    result = await executor.call_tool(
        "filesystem",
        "read_file",
        {"path": "/tmp/test.txt"},
        "session_1"
    )

    # Result is now a list of content blocks
    assert len(result["content"]) == 1
    assert result["content"][0]["text"] == "test result"
    mock_mcp_session.call_tool.assert_called_once()


@pytest.mark.asyncio
async def test_call_tool_denied_operation():
    """Test calling denied operation raises error."""
    from src.mcp.tool_executor import MCPToolExecutor
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    manager = AsyncMock()
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=[],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    with pytest.raises(PermissionDeniedError) as exc_info:
        await executor.call_tool(
            "filesystem",
            "delete_file",
            {"path": "/tmp/test.txt"},
            "session_1"
        )

    assert "denied by security policy" in str(exc_info.value)


@pytest.mark.asyncio
async def test_call_tool_requires_confirmation():
    """Test undefined operation raises ConfirmationRequired."""
    from src.mcp.tool_executor import MCPToolExecutor
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    manager = AsyncMock()
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # write_file is neither allowed nor denied
    with pytest.raises(ConfirmationRequired) as exc_info:
        await executor.call_tool(
            "filesystem",
            "write_file",
            {"path": "/tmp/test.txt", "content": "test"},
            "session_1"
        )

    assert "Allow filesystem:write_file" in exc_info.value.prompt


@pytest.mark.asyncio
async def test_call_tool_path_outside_whitelist():
    """Test path outside whitelist is denied."""
    from src.mcp.tool_executor import MCPToolExecutor
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    manager = AsyncMock()
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    with pytest.raises(PermissionDeniedError) as exc_info:
        await executor.call_tool(
            "filesystem",
            "read_file",
            {"path": "/etc/passwd"},
            "session_1"
        )

    assert "outside allowed directories" in str(exc_info.value)


@pytest.mark.asyncio
async def test_tool_discovery_caching(mock_mcp_session):
    """Test tool discovery results are cached."""
    from src.mcp.tool_executor import MCPToolExecutor

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_session)

    security = AsyncMock()
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # First call
    tools1 = await executor.discover_tools("filesystem")

    # Second call
    tools2 = await executor.discover_tools("filesystem")

    # Should return cached results
    assert tools1 is tools2
    # Connection should only be called once
    manager.get_connection.assert_called_once()
    mock_mcp_session.list_tools.assert_called_once()


@pytest.mark.asyncio
async def test_tool_discovery_different_servers(mock_mcp_session):
    """Test different servers have separate caches."""
    from src.mcp.tool_executor import MCPToolExecutor

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_session)

    security = AsyncMock()
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # Discover from two different servers
    await executor.discover_tools("filesystem")
    await executor.discover_tools("github")

    # Should call connection twice (once per server)
    assert manager.get_connection.call_count == 2
