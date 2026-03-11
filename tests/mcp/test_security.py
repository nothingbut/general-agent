"""Tests for MCP security layer."""
import pytest
from pathlib import Path
from src.mcp.security import MCPSecurityLayer, SecurityConfig


def test_security_layer_initialization():
    """Test security layer can be initialized."""
    config = SecurityConfig(
        allowed_directories=["/tmp", "/home/user/docs"],
        allowed_operations=["read_file", "list_directory"],
        denied_operations=["delete_file"]
    )

    security = MCPSecurityLayer(config)

    assert security is not None
    assert len(security.config.allowed_directories) == 2
    assert len(security.resolved_dirs) == 2


def test_resolved_directories_are_absolute():
    """Test directory paths are resolved to absolute."""
    config = SecurityConfig(
        allowed_directories=[".", "../test"],
        allowed_operations=[],
        denied_operations=[]
    )

    security = MCPSecurityLayer(config)

    # All resolved paths should be absolute
    for path in security.resolved_dirs:
        assert path.is_absolute()


@pytest.mark.asyncio
async def test_denied_operation_rejected():
    """Test denied operations are rejected."""
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)

    allowed, reason = await security.check_permission(
        "filesystem",
        "delete_file",
        {"path": "/tmp/test.txt"}
    )

    assert allowed is False
    assert "denied by security policy" in reason


@pytest.mark.asyncio
async def test_allowed_operation_passes():
    """Test allowed operations pass without confirmation."""
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    allowed, reason = await security.check_permission(
        "filesystem",
        "read_file",
        {"path": "/tmp/test.txt"}
    )

    assert allowed is True
    assert reason is None


@pytest.mark.asyncio
async def test_undefined_operation_requires_confirmation():
    """Test undefined operations require confirmation."""
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["read_file"],
        denied_operations=["delete_file"]
    )
    security = MCPSecurityLayer(config)

    # write_file is neither allowed nor denied
    allowed, reason = await security.check_permission(
        "filesystem",
        "write_file",
        {"path": "/tmp/test.txt"}
    )

    assert allowed is None  # Requires confirmation
    assert reason is None


@pytest.mark.asyncio
async def test_path_in_whitelist_allowed(tmp_path):
    """Test path within allowed directory is permitted."""
    config = SecurityConfig(
        allowed_directories=[str(tmp_path)],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    test_file = tmp_path / "test.txt"

    allowed, reason = await security.check_permission(
        "filesystem",
        "read_file",
        {"path": str(test_file)}
    )

    assert allowed is True
    assert reason is None


@pytest.mark.asyncio
async def test_path_outside_whitelist_denied(tmp_path):
    """Test path outside allowed directory is denied."""
    allowed_dir = tmp_path / "allowed"
    allowed_dir.mkdir()

    config = SecurityConfig(
        allowed_directories=[str(allowed_dir)],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    # Try to access file outside allowed directory
    forbidden_file = tmp_path / "forbidden.txt"

    allowed, reason = await security.check_permission(
        "filesystem",
        "read_file",
        {"path": str(forbidden_file)}
    )

    assert allowed is False
    assert "outside allowed directories" in reason


@pytest.mark.asyncio
async def test_path_traversal_prevented(tmp_path):
    """Test .. path traversal is prevented."""
    allowed_dir = tmp_path / "allowed"
    allowed_dir.mkdir()

    forbidden_dir = tmp_path / "forbidden"
    forbidden_dir.mkdir()

    config = SecurityConfig(
        allowed_directories=[str(allowed_dir)],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    # Try to use .. to access parent
    traversal_path = allowed_dir / ".." / "forbidden" / "secret.txt"

    allowed, reason = await security.check_permission(
        "filesystem",
        "read_file",
        {"path": str(traversal_path)}
    )

    # Should be denied because resolved path is outside whitelist
    assert allowed is False


@pytest.mark.asyncio
async def test_symlink_resolution(tmp_path):
    """Test symlinks are resolved for security check."""
    allowed_dir = tmp_path / "allowed"
    allowed_dir.mkdir()

    forbidden_dir = tmp_path / "forbidden"
    forbidden_dir.mkdir()

    # Create symlink from allowed to forbidden
    symlink = allowed_dir / "link"
    try:
        symlink.symlink_to(forbidden_dir)
    except OSError:
        pytest.skip("Cannot create symlinks on this system")

    config = SecurityConfig(
        allowed_directories=[str(allowed_dir)],
        allowed_operations=["read_file"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    # Try to access through symlink
    symlink_path = symlink / "secret.txt"

    allowed, reason = await security.check_permission(
        "filesystem",
        "read_file",
        {"path": str(symlink_path)}
    )

    # Should be denied because resolved path points outside whitelist
    assert allowed is False


@pytest.mark.asyncio
async def test_no_path_argument_allowed():
    """Test operations without path argument are allowed."""
    config = SecurityConfig(
        allowed_directories=["/tmp"],
        allowed_operations=["list_tools"],
        denied_operations=[]
    )
    security = MCPSecurityLayer(config)

    # No path in arguments
    allowed, reason = await security.check_permission(
        "filesystem",
        "list_tools",
        {}
    )

    assert allowed is True
    assert reason is None
