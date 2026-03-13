"""Shared fixtures for MCP tests."""
import pytest
from pathlib import Path
from unittest.mock import AsyncMock
import yaml


@pytest.fixture
def temp_mcp_config(tmp_path):
    """Create temporary MCP config file."""
    config = {
        "servers": {
            "filesystem": {
                "command": "npx",
                "args": [
                    "-y",
                    "@modelcontextprotocol/server-filesystem",
                    str(tmp_path)
                ],
                "security": {
                    "allowed_directories": [str(tmp_path)],
                    "allowed_operations": ["read_file", "list_directory"],
                    "denied_operations": ["delete_file"]
                },
                "timeout": 30.0,
                "health_check_interval": 60
            }
        },
        "global": {
            "enabled": True,
            "audit_log_retention_days": 90,
            "confirmation_timeout": 300
        }
    }

    config_file = tmp_path / "mcp_config.yaml"
    config_file.write_text(yaml.dump(config))
    return config_file


@pytest.fixture
def mock_mcp_session():
    """Mock MCP ClientSession for unit tests."""
    from unittest.mock import Mock
    session = AsyncMock()

    # Mock list_tools to return proper MCP response format
    tools_result = Mock()
    tool1 = Mock()
    tool1.name = "read_file"
    tool1.description = "Read file contents"
    tool1.inputSchema = {
        "type": "object",
        "properties": {"path": {"type": "string"}}
    }

    tool2 = Mock()
    tool2.name = "write_file"
    tool2.description = "Write file contents"
    tool2.inputSchema = {
        "type": "object",
        "properties": {
            "path": {"type": "string"},
            "content": {"type": "string"}
        }
    }

    tools_result.tools = [tool1, tool2]
    session.list_tools = AsyncMock(return_value=tools_result)

    # Mock call_tool to return proper MCP response format
    call_result = Mock()
    content_block = Mock()
    content_block.type = "text"
    content_block.text = "test result"
    call_result.content = [content_block]
    session.call_tool = AsyncMock(return_value=call_result)

    return session


@pytest.fixture
def mock_mcp_connection(mock_mcp_session):
    """Mock MCPConnection wrapper for unit tests."""
    from contextlib import AsyncExitStack
    from src.mcp.connection_manager import MCPConnection

    mock_exit_stack = AsyncMock(spec=AsyncExitStack)
    return MCPConnection(
        session=mock_mcp_session,
        exit_stack=mock_exit_stack,
        server_name="filesystem"
    )
