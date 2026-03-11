# MCP Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Integrate Model Context Protocol (MCP) to enable file system operations via the filesystem MCP server with security controls.

**Architecture:** Generic MCP Client with lazy singleton connection manager, three-tier security layer (allowed/denied/prompt), and tool executor. Extends existing Router → Executor pattern with new "mcp" execution type.

**Tech Stack:** mcp Python SDK, aiohttp, asyncio, pathlib, PyYAML

**Design Doc:** `docs/plans/2026-03-04-mcp-integration-design.md`

---

## Phase 3.1: Infrastructure (Tasks 1-8)

### Task 1: Install MCP SDK Dependency

**Files:**
- Modify: `pyproject.toml`

**Step 1: Add mcp dependency**

```bash
# Open pyproject.toml and add to dependencies array
```

```toml
dependencies = [
    "fastapi>=0.109.0",
    "uvicorn[standard]>=0.27.0",
    "jinja2>=3.1.3",
    "aiosqlite>=0.19.0",
    "pydantic>=2.6.0",
    "python-multipart>=0.0.9",
    "pyyaml>=6.0",
    "aiohttp>=3.9.0",
    "mcp>=0.9.0",
]
```

**Step 2: Install dependencies**

Run: `uv pip install -e .`
Expected: Successfully installs mcp package

**Step 3: Verify installation**

Run: `python -c "import mcp; print(mcp.__version__)"`
Expected: Prints version number (e.g., "0.9.0")

**Step 4: Commit**

```bash
git add pyproject.toml
git commit -m "deps: add mcp SDK for MCP integration"
```

---

### Task 2: Create MCP Module Structure

**Files:**
- Create: `src/mcp/__init__.py`
- Create: `src/mcp/exceptions.py`
- Create: `tests/mcp/__init__.py`
- Create: `tests/mcp/conftest.py`

**Step 1: Create mcp module directory**

Run: `mkdir -p src/mcp tests/mcp`

**Step 2: Create __init__.py files**

```python
# src/mcp/__init__.py
"""MCP (Model Context Protocol) integration module."""

__all__ = [
    "MCPConnectionManager",
    "MCPSecurityLayer",
    "MCPToolExecutor",
    "MCPConfig",
]
```

```python
# tests/mcp/__init__.py
"""Tests for MCP integration."""
```

**Step 3: Create exceptions module**

```python
# src/mcp/exceptions.py
"""MCP exception hierarchy."""


class MCPError(Exception):
    """Base class for all MCP errors."""
    pass


class MCPConnectionError(MCPError):
    """Server connection errors."""
    pass


class MCPServerStartupError(MCPConnectionError):
    """Server failed to start."""

    def __init__(self, server_name: str, reason: str):
        self.server_name = server_name
        self.reason = reason
        super().__init__(f"Failed to start MCP server '{server_name}': {reason}")


class MCPServerCrashError(MCPConnectionError):
    """Server process crashed."""
    pass


class MCPServerDisabledError(MCPConnectionError):
    """Server has been disabled after failures."""

    def __init__(self, server_name: str):
        self.server_name = server_name
        super().__init__(f"MCP server '{server_name}' is disabled")


class MCPToolError(MCPError):
    """Tool execution errors."""
    pass


class MCPToolNotFoundError(MCPToolError):
    """Requested tool does not exist."""
    pass


class MCPSecurityError(MCPError):
    """Security-related errors."""
    pass


class PermissionDeniedError(MCPSecurityError):
    """Operation denied by security policy."""

    def __init__(self, operation: str, reason: str):
        self.operation = operation
        self.reason = reason
        super().__init__(f"Permission denied for '{operation}': {reason}")


class PathNotAllowedError(MCPSecurityError):
    """Path not in whitelist."""
    pass


class ConfirmationRequired(MCPError):
    """Operation requires user confirmation."""

    def __init__(self, tool_name: str, arguments: dict, prompt: str):
        self.tool_name = tool_name
        self.arguments = arguments
        self.prompt = prompt
        super().__init__(prompt)
```

**Step 4: Create test fixtures**

```python
# tests/mcp/conftest.py
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
def mock_mcp_connection():
    """Mock MCP connection for unit tests."""
    conn = AsyncMock()
    conn.call_tool = AsyncMock(return_value={"content": "test result"})
    conn.list_tools = AsyncMock(return_value=[
        {
            "name": "read_file",
            "description": "Read file contents",
            "inputSchema": {
                "type": "object",
                "properties": {
                    "path": {"type": "string"}
                }
            }
        },
        {
            "name": "write_file",
            "description": "Write file contents",
            "inputSchema": {
                "type": "object",
                "properties": {
                    "path": {"type": "string"},
                    "content": {"type": "string"}
                }
            }
        }
    ])
    return conn
```

**Step 5: Commit**

```bash
git add src/mcp/ tests/mcp/
git commit -m "feat(mcp): add module structure and exceptions"
```

---

### Task 3: Create MCP Config Module

**Files:**
- Create: `src/mcp/config.py`
- Create: `tests/mcp/test_config.py`

**Step 1: Write test for config loading**

```python
# tests/mcp/test_config.py
"""Tests for MCP configuration."""
import pytest
from pathlib import Path
import yaml
from src.mcp.config import MCPConfig, ServerConfig, SecurityConfig, load_mcp_config


def test_load_valid_config(temp_mcp_config):
    """Test loading valid YAML config."""
    config = load_mcp_config(str(temp_mcp_config))

    assert isinstance(config, MCPConfig)
    assert "filesystem" in config.servers
    assert config.global_config["enabled"] is True


def test_server_config_validation(temp_mcp_config):
    """Test server config structure."""
    config = load_mcp_config(str(temp_mcp_config))
    server = config.servers["filesystem"]

    assert isinstance(server, ServerConfig)
    assert server.command == "npx"
    assert len(server.args) > 0
    assert server.timeout == 30.0


def test_security_config_validation(temp_mcp_config):
    """Test security config structure."""
    config = load_mcp_config(str(temp_mcp_config))
    security = config.servers["filesystem"].security

    assert isinstance(security, SecurityConfig)
    assert len(security.allowed_directories) > 0
    assert "read_file" in security.allowed_operations
    assert "delete_file" in security.denied_operations


def test_missing_config_file():
    """Test error handling for missing config."""
    with pytest.raises(FileNotFoundError):
        load_mcp_config("/nonexistent/config.yaml")


def test_invalid_yaml_format(tmp_path):
    """Test error handling for invalid YAML."""
    bad_config = tmp_path / "bad_config.yaml"
    bad_config.write_text("invalid: yaml: content: [")

    with pytest.raises(yaml.YAMLError):
        load_mcp_config(str(bad_config))
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/mcp/test_config.py -v`
Expected: FAIL - module 'src.mcp.config' not found

**Step 3: Implement config module**

```python
# src/mcp/config.py
"""MCP configuration loading and validation."""
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Any
import yaml


@dataclass(frozen=True)
class SecurityConfig:
    """Security configuration for an MCP server.

    Attributes:
        allowed_directories: Whitelist of accessible directories
        allowed_operations: Operations that don't need confirmation
        denied_operations: Operations that are always blocked
    """
    allowed_directories: List[str]
    allowed_operations: List[str]
    denied_operations: List[str]


@dataclass(frozen=True)
class ServerConfig:
    """Configuration for a single MCP server.

    Attributes:
        command: Command to start the server (e.g., "npx")
        args: Command arguments
        security: Security configuration
        env: Environment variables
        timeout: Operation timeout in seconds
        health_check_interval: Health check interval in seconds
    """
    command: str
    args: List[str]
    security: SecurityConfig
    env: Dict[str, str]
    timeout: float
    health_check_interval: int


@dataclass(frozen=True)
class MCPConfig:
    """Complete MCP configuration.

    Attributes:
        servers: Map of server name to configuration
        global_config: Global settings
    """
    servers: Dict[str, ServerConfig]
    global_config: Dict[str, Any]


def load_mcp_config(config_path: str) -> MCPConfig:
    """Load MCP configuration from YAML file.

    Args:
        config_path: Path to YAML config file

    Returns:
        MCPConfig instance

    Raises:
        FileNotFoundError: Config file doesn't exist
        yaml.YAMLError: Invalid YAML format
        ValueError: Invalid config structure
    """
    path = Path(config_path)
    if not path.exists():
        raise FileNotFoundError(f"Config file not found: {config_path}")

    with open(path) as f:
        raw_config = yaml.safe_load(f)

    if not isinstance(raw_config, dict):
        raise ValueError("Config must be a YAML object")

    # Parse servers
    servers = {}
    for name, server_data in raw_config.get("servers", {}).items():
        security_data = server_data.get("security", {})
        security = SecurityConfig(
            allowed_directories=security_data.get("allowed_directories", []),
            allowed_operations=security_data.get("allowed_operations", []),
            denied_operations=security_data.get("denied_operations", [])
        )

        servers[name] = ServerConfig(
            command=server_data["command"],
            args=server_data.get("args", []),
            security=security,
            env=server_data.get("env", {}),
            timeout=server_data.get("timeout", 30.0),
            health_check_interval=server_data.get("health_check_interval", 60)
        )

    return MCPConfig(
        servers=servers,
        global_config=raw_config.get("global", {})
    )
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/mcp/test_config.py -v`
Expected: 5 passed

**Step 5: Commit**

```bash
git add src/mcp/config.py tests/mcp/test_config.py
git commit -m "feat(mcp): add config loading with validation"
```

---

### Task 4: Create Sample MCP Config File

**Files:**
- Create: `config/mcp_config.yaml`
- Create: `config/.gitignore`

**Step 1: Create config directory**

Run: `mkdir -p config`

**Step 2: Create sample config**

```yaml
# config/mcp_config.yaml
# MCP Server Configuration

servers:
  # Filesystem MCP server
  filesystem:
    # Server startup command
    command: npx
    args:
      - "-y"
      - "@modelcontextprotocol/server-filesystem"
      - "/tmp"  # MCP server root (our whitelist restricts further)

    # Security configuration
    security:
      # Allowed directories (whitelist)
      allowed_directories:
        - /Users/shichang/Documents
        - /Users/shichang/Downloads
        - /Users/shichang/Workspace

      # Operations allowed without confirmation
      allowed_operations:
        - read_file
        - list_directory
        - get_file_info

      # Operations always denied
      denied_operations:
        - delete_file
        - delete_directory

      # Operations not listed require user confirmation:
      # - write_file
      # - create_directory
      # - move_file

    # Optional settings
    env: {}
    timeout: 30.0
    health_check_interval: 60

  # Future servers (Phase 4+)
  # github:
  #   command: npx
  #   args: ["-y", "@modelcontextprotocol/server-github"]
  #   env:
  #     GITHUB_TOKEN: ${GITHUB_TOKEN}
  #   security:
  #     allowed_operations: [search_repositories, get_file_contents]
  #     denied_operations: [delete_repository]

# Global configuration
global:
  # Enable/disable MCP (can override with MCP_ENABLED env var)
  enabled: true

  # Audit log retention
  audit_log_retention_days: 90

  # Confirmation timeout
  confirmation_timeout: 300
```

**Step 3: Create gitignore for local overrides**

```
# config/.gitignore
# Allow local config overrides
mcp_config.local.yaml
```

**Step 4: Test config loading**

Run: `python -c "from src.mcp.config import load_mcp_config; c = load_mcp_config('config/mcp_config.yaml'); print(f'Loaded {len(c.servers)} servers')"`
Expected: "Loaded 1 servers"

**Step 5: Commit**

```bash
git add config/
git commit -m "feat(mcp): add sample MCP configuration"
```

---

### Task 5: Implement MCPConnectionManager (Part 1 - Structure)

**Files:**
- Create: `src/mcp/connection_manager.py`
- Create: `tests/mcp/test_connection_manager.py`

**Step 1: Write test for connection manager initialization**

```python
# tests/mcp/test_connection_manager.py
"""Tests for MCP connection manager."""
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
    conn1 = await manager.get_connection("filesystem")
    assert conn1 is mock_mcp_connection
    assert len(manager.connections) == 1

    # Second call should reuse connection
    conn2 = await manager.get_connection("filesystem")
    assert conn2 is conn1
    assert len(manager.connections) == 1


@pytest.mark.asyncio
async def test_disabled_server_raises_error(temp_mcp_config):
    """Test accessing disabled server raises error."""
    manager = MCPConnectionManager(str(temp_mcp_config))
    manager._disabled_servers.add("filesystem")

    with pytest.raises(MCPServerDisabledError) as exc_info:
        await manager.get_connection("filesystem")

    assert "filesystem" in str(exc_info.value)
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/mcp/test_connection_manager.py -v`
Expected: FAIL - module not found

**Step 3: Implement connection manager structure**

```python
# src/mcp/connection_manager.py
"""MCP connection manager - handles server lifecycle."""
import asyncio
import logging
from typing import Dict, Set, Any
from pathlib import Path

from .config import load_mcp_config, MCPConfig, ServerConfig
from .exceptions import (
    MCPServerStartupError,
    MCPServerDisabledError,
    MCPServerCrashError
)

logger = logging.getLogger(__name__)


class MCPConnectionManager:
    """Manages MCP server connections with lazy initialization.

    Features:
    - Lazy singleton: starts servers on first access
    - Connection caching: reuses connections across requests
    - Health checking: detects and recovers from crashes
    - Auto-restart: attempts recovery up to 3 times
    - Graceful shutdown: cleans up on app exit

    Attributes:
        config: MCP configuration
        connections: Active server connections
        _locks: Per-server locks for thread safety
        _restart_count: Track restart attempts
        _disabled_servers: Set of disabled server names
    """

    def __init__(self, config_path: str):
        """Initialize connection manager.

        Args:
            config_path: Path to MCP YAML config file
        """
        self.config = load_mcp_config(config_path)
        self.connections: Dict[str, Any] = {}
        self._locks: Dict[str, asyncio.Lock] = {}
        self._restart_count: Dict[str, int] = {}
        self._disabled_servers: Set[str] = set()

        logger.info(f"MCPConnectionManager initialized with {len(self.config.servers)} servers")

    async def get_connection(self, server_name: str) -> Any:
        """Get or create server connection (thread-safe).

        First call starts the server process, subsequent calls reuse
        the connection. Uses asyncio.Lock for concurrency safety.

        Args:
            server_name: Name of server (e.g., "filesystem")

        Returns:
            MCP connection object

        Raises:
            MCPServerDisabledError: Server is disabled
            MCPServerStartupError: Failed to start server
        """
        if server_name in self._disabled_servers:
            raise MCPServerDisabledError(server_name)

        if server_name not in self.config.servers:
            raise ValueError(f"Unknown MCP server: {server_name}")

        # Lazy creation with lock
        if server_name not in self.connections:
            lock = self._get_lock(server_name)
            async with lock:
                # Double-check after acquiring lock
                if server_name not in self.connections:
                    logger.info(f"Starting MCP server: {server_name}")
                    self.connections[server_name] = await self._start_server(server_name)

        return self.connections[server_name]

    async def _start_server(self, server_name: str) -> Any:
        """Start MCP server process.

        Args:
            server_name: Server name

        Returns:
            MCP connection

        Raises:
            MCPServerStartupError: Startup failed
        """
        # TODO: Implement using mcp SDK
        # For now, this is a placeholder
        raise NotImplementedError("Server startup not yet implemented")

    async def health_check(self, server_name: str) -> bool:
        """Check if server is healthy.

        Args:
            server_name: Server name

        Returns:
            True if healthy, False otherwise
        """
        # TODO: Implement health check
        return True

    async def restart_server(self, server_name: str):
        """Restart a crashed server.

        Args:
            server_name: Server name
        """
        # TODO: Implement restart logic
        pass

    async def shutdown_all(self):
        """Shutdown all server connections."""
        logger.info("Shutting down all MCP servers")

        for server_name, connection in self.connections.items():
            try:
                # TODO: Implement connection cleanup
                logger.info(f"Closed connection to {server_name}")
            except Exception as e:
                logger.error(f"Error closing {server_name}: {e}")

        self.connections.clear()

    def _get_lock(self, server_name: str) -> asyncio.Lock:
        """Get or create lock for server.

        Args:
            server_name: Server name

        Returns:
            asyncio.Lock for this server
        """
        if server_name not in self._locks:
            self._locks[server_name] = asyncio.Lock()
        return self._locks[server_name]

    def _mark_server_disabled(self, server_name: str):
        """Disable a server after too many failures.

        Args:
            server_name: Server name
        """
        self._disabled_servers.add(server_name)
        logger.warning(f"Server '{server_name}' has been disabled")
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/mcp/test_connection_manager.py::test_manager_initialization -v`
Run: `pytest tests/mcp/test_connection_manager.py::test_lazy_connection_creation -v`
Run: `pytest tests/mcp/test_connection_manager.py::test_disabled_server_raises_error -v`
Expected: 3 passed

**Step 5: Commit**

```bash
git add src/mcp/connection_manager.py tests/mcp/test_connection_manager.py
git commit -m "feat(mcp): add connection manager structure with lazy loading"
```

---

### Task 6: Test Connection Manager Concurrency

**Files:**
- Modify: `tests/mcp/test_connection_manager.py`

**Step 1: Write concurrency tests**

```python
# tests/mcp/test_connection_manager.py (append)

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
    connections = await asyncio.gather(*tasks)

    # All should get same connection, server started only once
    assert all(conn is mock_mcp_connection for conn in connections)
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
```

**Step 2: Run new tests**

Run: `pytest tests/mcp/test_connection_manager.py::test_concurrent_connection_requests -v`
Run: `pytest tests/mcp/test_connection_manager.py::test_multiple_servers_independent_locks -v`
Expected: 2 passed

**Step 3: Run all connection manager tests**

Run: `pytest tests/mcp/test_connection_manager.py -v`
Expected: 5 passed

**Step 4: Commit**

```bash
git add tests/mcp/test_connection_manager.py
git commit -m "test(mcp): add connection manager concurrency tests"
```

---

### Task 7: Add Configuration Validation Tests

**Files:**
- Modify: `tests/mcp/test_config.py`

**Step 1: Write validation tests**

```python
# tests/mcp/test_config.py (append)

def test_missing_required_fields(tmp_path):
    """Test config validation catches missing fields."""
    bad_config = tmp_path / "bad.yaml"
    bad_config.write_text("""
servers:
  filesystem:
    # Missing 'command' field
    args: []
""")

    with pytest.raises(KeyError):
        load_mcp_config(str(bad_config))


def test_environment_variable_expansion(tmp_path, monkeypatch):
    """Test env var expansion in config."""
    # Set test env var
    monkeypatch.setenv("TEST_TOKEN", "secret123")

    config_content = """
servers:
  test:
    command: echo
    args: []
    security:
      allowed_directories: []
      allowed_operations: []
      denied_operations: []
    env:
      TOKEN: ${TEST_TOKEN}
"""
    config_file = tmp_path / "config.yaml"
    config_file.write_text(config_content)

    config = load_mcp_config(str(config_file))
    # Note: Env var expansion would need to be implemented
    # For now, just test structure is valid
    assert "test" in config.servers


def test_default_values(tmp_path):
    """Test default values are applied."""
    minimal_config = tmp_path / "minimal.yaml"
    minimal_config.write_text("""
servers:
  test:
    command: echo
    args: []
    security:
      allowed_directories: []
      allowed_operations: []
      denied_operations: []
""")

    config = load_mcp_config(str(minimal_config))
    server = config.servers["test"]

    assert server.timeout == 30.0  # Default
    assert server.health_check_interval == 60  # Default
    assert server.env == {}  # Default empty dict
```

**Step 2: Run validation tests**

Run: `pytest tests/mcp/test_config.py::test_missing_required_fields -v`
Run: `pytest tests/mcp/test_config.py::test_environment_variable_expansion -v`
Run: `pytest tests/mcp/test_config.py::test_default_values -v`
Expected: 3 passed

**Step 3: Run all config tests**

Run: `pytest tests/mcp/test_config.py -v`
Expected: 8 passed

**Step 4: Commit**

```bash
git add tests/mcp/test_config.py
git commit -m "test(mcp): add config validation tests"
```

---

### Task 8: Phase 3.1 Summary and Documentation

**Files:**
- Create: `docs/mcp-phase-3.1-summary.md`

**Step 1: Write phase summary**

```markdown
# Phase 3.1 Summary: MCP Infrastructure

**Status:** ✅ Complete
**Date:** 2026-03-04
**Duration:** ~2 days

## Completed Tasks

1. ✅ Installed mcp SDK dependency
2. ✅ Created MCP module structure with exceptions
3. ✅ Implemented configuration loading with validation
4. ✅ Created sample MCP config file
5. ✅ Implemented MCPConnectionManager with lazy loading
6. ✅ Added connection manager concurrency tests
7. ✅ Added configuration validation tests

## Test Coverage

- Config module: 8 tests passing
- Connection manager: 5 tests passing
- **Total: 13 tests, 100% pass rate**

## Deliverables

**Code:**
- `src/mcp/__init__.py` - Module initialization
- `src/mcp/exceptions.py` - Exception hierarchy
- `src/mcp/config.py` - Configuration loading
- `src/mcp/connection_manager.py` - Connection manager (structure)

**Config:**
- `config/mcp_config.yaml` - Sample configuration

**Tests:**
- `tests/mcp/conftest.py` - Test fixtures
- `tests/mcp/test_config.py` - Config tests (8 tests)
- `tests/mcp/test_connection_manager.py` - Manager tests (5 tests)

## Known Limitations

- `_start_server()` is a placeholder (NotImplementedError)
- Health check and restart logic not yet implemented
- Will be completed in Phase 3.2

## Next Phase

**Phase 3.2:** Security & Execution
- Implement MCPSecurityLayer
- Implement MCPToolExecutor
- Complete connection manager with real MCP SDK integration
```

**Step 2: Commit documentation**

```bash
git add docs/mcp-phase-3.1-summary.md
git commit -m "docs(mcp): Phase 3.1 infrastructure complete"
```

**Step 3: Run all Phase 3.1 tests**

Run: `pytest tests/mcp/ -v --cov=src/mcp --cov-report=term-missing`
Expected: 13 passed, high coverage on config and manager structure

---

## Phase 3.2: Security & Execution (Tasks 9-16)

### Task 9: Implement MCPSecurityLayer (Part 1 - Structure)

**Files:**
- Create: `src/mcp/security.py`
- Create: `tests/mcp/test_security.py`

**Step 1: Write security layer initialization tests**

```python
# tests/mcp/test_security.py
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
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/mcp/test_security.py -v`
Expected: FAIL - module not found

**Step 3: Implement security layer structure**

```python
# src/mcp/security.py
"""MCP security layer - enforces access controls."""
import logging
from pathlib import Path
from typing import Tuple, Optional
from dataclasses import dataclass

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class SecurityConfig:
    """Security configuration (immutable).

    Attributes:
        allowed_directories: Whitelist of accessible directories
        allowed_operations: Operations allowed without confirmation
        denied_operations: Operations that are always blocked
    """
    allowed_directories: list[str]
    allowed_operations: list[str]
    denied_operations: list[str]


class MCPSecurityLayer:
    """MCP security layer with three-tier permissions.

    Permission flow:
    1. Check denied_operations → Reject
    2. Check filesystem paths → Validate whitelist
    3. Check allowed_operations → Allow
    4. Otherwise → Require confirmation

    Attributes:
        config: Security configuration
        resolved_dirs: Resolved absolute paths for whitelist
    """

    def __init__(self, config: SecurityConfig):
        """Initialize security layer.

        Args:
            config: Security configuration
        """
        self.config = config

        # Resolve all allowed directories to absolute paths
        self.resolved_dirs = []
        for dir_path in config.allowed_directories:
            try:
                resolved = Path(dir_path).resolve()
                self.resolved_dirs.append(resolved)
                logger.debug(f"Allowed directory: {resolved}")
            except (ValueError, OSError) as e:
                logger.warning(f"Failed to resolve directory {dir_path}: {e}")

    async def check_permission(
        self,
        server_name: str,
        tool_name: str,
        arguments: dict
    ) -> Tuple[Optional[bool], Optional[str]]:
        """Check if operation is allowed.

        Three-tier permission logic:
        1. denied_operations → (False, reason)
        2. allowed_operations + path check → (True, None)
        3. undefined → (None, None) requires confirmation

        Args:
            server_name: Server name (e.g., "filesystem")
            tool_name: Tool name (e.g., "filesystem:read_file")
            arguments: Tool arguments

        Returns:
            Tuple of (allowed, reason):
            - (True, None): Operation allowed
            - (False, "reason"): Operation denied with reason
            - (None, None): Operation requires user confirmation
        """
        # Extract operation name from tool name
        operation = tool_name.split(":")[-1]

        # 1. Check denied list
        if operation in self.config.denied_operations:
            reason = f"Operation '{operation}' is denied by security policy"
            logger.warning(f"Denied: {tool_name} - {reason}")
            return False, reason

        # 2. Filesystem-specific: check path whitelist
        if server_name == "filesystem":
            if not self._check_path_allowed(arguments):
                reason = "Path outside allowed directories"
                logger.warning(f"Denied: {tool_name} - {reason}")
                return False, reason

        # 3. Check allowed list
        if operation in self.config.allowed_operations:
            logger.debug(f"Allowed: {tool_name}")
            return True, None

        # 4. Undefined → requires confirmation
        logger.info(f"Confirmation required: {tool_name}")
        return None, None

    def _check_path_allowed(self, arguments: dict) -> bool:
        """Check if path is in allowed directories.

        Security measures:
        - Uses Path.resolve() to prevent .. traversal
        - Uses is_relative_to() for containment check
        - Handles symlinks properly

        Args:
            arguments: Tool arguments (may contain "path" or "directory")

        Returns:
            True if path is allowed, False otherwise
        """
        # Extract path from arguments
        path_arg = arguments.get("path") or arguments.get("directory")

        if not path_arg:
            # No path argument, allow
            return True

        try:
            # Resolve to absolute path (follows symlinks, removes ..)
            resolved_path = Path(path_arg).resolve()

            # Check if path is within any allowed directory
            for allowed_dir in self.resolved_dirs:
                if resolved_path.is_relative_to(allowed_dir):
                    logger.debug(f"Path {resolved_path} is within {allowed_dir}")
                    return True

            logger.warning(f"Path {resolved_path} not in whitelist")
            return False

        except (ValueError, OSError) as e:
            # Path resolution failed, deny for safety
            logger.error(f"Path resolution failed for {path_arg}: {e}")
            return False
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/mcp/test_security.py -v`
Expected: 2 passed

**Step 5: Commit**

```bash
git add src/mcp/security.py tests/mcp/test_security.py
git commit -m "feat(mcp): add security layer with three-tier permissions"
```

---

### Task 10: Test Security Layer Permissions

**Files:**
- Modify: `tests/mcp/test_security.py`

**Step 1: Write permission tests**

```python
# tests/mcp/test_security.py (append)

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
```

**Step 2: Run permission tests**

Run: `pytest tests/mcp/test_security.py::test_denied_operation_rejected -v`
Run: `pytest tests/mcp/test_security.py::test_allowed_operation_passes -v`
Run: `pytest tests/mcp/test_security.py::test_undefined_operation_requires_confirmation -v`
Expected: 3 passed

**Step 3: Commit**

```bash
git add tests/mcp/test_security.py
git commit -m "test(mcp): add security layer permission tests"
```

---

### Task 11: Test Path Whitelist Enforcement

**Files:**
- Modify: `tests/mcp/test_security.py`

**Step 1: Write path validation tests**

```python
# tests/mcp/test_security.py (append)

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
```

**Step 2: Run path validation tests**

Run: `pytest tests/mcp/test_security.py -k "path" -v`
Expected: 5 passed

**Step 3: Run all security tests**

Run: `pytest tests/mcp/test_security.py -v`
Expected: 10 passed

**Step 4: Commit**

```bash
git add tests/mcp/test_security.py
git commit -m "test(mcp): add path whitelist enforcement tests"
```

---

### Task 12: Implement MCPToolExecutor (Part 1 - Structure)

**Files:**
- Create: `src/mcp/tool_executor.py`
- Create: `tests/mcp/test_tool_executor.py`

**Step 1: Write tool executor initialization tests**

```python
# tests/mcp/test_tool_executor.py
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
async def test_tool_discovery(mock_mcp_connection, monkeypatch):
    """Test tool discovery from server."""
    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_connection)

    security = AsyncMock()
    db = AsyncMock()

    from src.mcp.tool_executor import MCPToolExecutor
    executor = MCPToolExecutor(manager, security, db)

    tools = await executor.discover_tools("filesystem")

    assert len(tools) == 2
    assert tools[0]["name"] == "read_file"
    assert tools[1]["name"] == "write_file"
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/mcp/test_tool_executor.py -v`
Expected: FAIL - module not found

**Step 3: Implement tool executor structure**

```python
# src/mcp/tool_executor.py
"""MCP tool executor - discovers and executes tools."""
import logging
from datetime import datetime
from typing import Dict, List, Any

from .connection_manager import MCPConnectionManager
from .security import MCPSecurityLayer
from .exceptions import (
    PermissionDeniedError,
    ConfirmationRequired,
    MCPToolError,
    MCPToolNotFoundError
)

logger = logging.getLogger(__name__)


class MCPToolExecutor:
    """MCP tool executor.

    Features:
    - Tool discovery with caching
    - Security-enforced tool calls
    - Audit logging
    - Error wrapping with context

    Attributes:
        manager: Connection manager
        security: Security layer
        db: Database for audit logs
        _tool_cache: Cached tool lists per server
    """

    def __init__(
        self,
        manager: MCPConnectionManager,
        security: MCPSecurityLayer,
        db: Any
    ):
        """Initialize tool executor.

        Args:
            manager: MCP connection manager
            security: Security layer
            db: Database instance
        """
        self.manager = manager
        self.security = security
        self.db = db
        self._tool_cache: Dict[str, List[dict]] = {}

    async def discover_tools(self, server_name: str) -> List[dict]:
        """Discover tools from MCP server.

        Results are cached to avoid repeated queries.

        Args:
            server_name: Server name (e.g., "filesystem")

        Returns:
            List of tool definitions with name, description, schema

        Raises:
            MCPConnectionError: Failed to connect to server
        """
        if server_name not in self._tool_cache:
            logger.info(f"Discovering tools from {server_name}")
            connection = await self.manager.get_connection(server_name)
            tools = await connection.list_tools()
            self._tool_cache[server_name] = tools
            logger.info(f"Discovered {len(tools)} tools from {server_name}")

        return self._tool_cache[server_name]

    async def call_tool(
        self,
        server_name: str,
        tool_name: str,
        arguments: dict,
        session_id: str
    ) -> dict:
        """Call MCP tool with security checks.

        Flow:
        1. Security check (three-tier permissions)
        2. Raise ConfirmationRequired if needed
        3. Log to audit trail
        4. Execute tool
        5. Return result

        Args:
            server_name: Server name
            tool_name: Tool name (e.g., "read_file")
            arguments: Tool arguments
            session_id: Session ID for audit

        Returns:
            Tool execution result

        Raises:
            PermissionDeniedError: Operation not allowed
            ConfirmationRequired: Needs user confirmation
            MCPToolError: Tool execution failed
        """
        full_tool_name = f"{server_name}:{tool_name}"

        # 1. Security check
        allowed, reason = await self.security.check_permission(
            server_name, full_tool_name, arguments
        )

        if allowed is False:
            raise PermissionDeniedError(tool_name, reason)

        if allowed is None:
            # Requires confirmation
            raise ConfirmationRequired(
                tool_name=full_tool_name,
                arguments=arguments,
                prompt=f"Allow {full_tool_name} with arguments {arguments}?"
            )

        # 2. Log operation (before execution)
        await self._log_operation(
            session_id, server_name, tool_name, arguments, "started"
        )

        # 3. Execute tool
        try:
            connection = await self.manager.get_connection(server_name)
            result = await connection.call_tool(tool_name, arguments)

            # Log success
            await self._log_operation(
                session_id, server_name, tool_name, arguments,
                "success", result
            )

            logger.info(f"Tool {full_tool_name} executed successfully")
            return result

        except Exception as e:
            # Log failure
            await self._log_operation(
                session_id, server_name, tool_name, arguments,
                "failed", error=str(e)
            )

            logger.error(f"Tool {full_tool_name} failed: {e}")
            raise MCPToolError(f"Tool '{full_tool_name}' failed: {e}") from e

    async def _log_operation(
        self,
        session_id: str,
        server_name: str,
        tool_name: str,
        arguments: dict,
        status: str,
        result: Any = None,
        error: str = None
    ):
        """Log tool operation to audit trail.

        Args:
            session_id: Session ID
            server_name: Server name
            tool_name: Tool name
            arguments: Tool arguments
            status: Operation status (started/success/failed)
            result: Tool result (if successful)
            error: Error message (if failed)
        """
        # TODO: Implement database logging in Phase 3.3
        logger.debug(
            f"Audit log: {session_id} - {server_name}:{tool_name} - {status}"
        )
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/mcp/test_tool_executor.py -v`
Expected: 2 passed

**Step 5: Commit**

```bash
git add src/mcp/tool_executor.py tests/mcp/test_tool_executor.py
git commit -m "feat(mcp): add tool executor with discovery and security"
```

---

### Task 13: Test Tool Executor Security Integration

**Files:**
- Modify: `tests/mcp/test_tool_executor.py`

**Step 1: Write security integration tests**

```python
# tests/mcp/test_tool_executor.py (append)

@pytest.mark.asyncio
async def test_call_tool_with_allowed_operation(mock_mcp_connection):
    """Test calling tool with allowed operation."""
    from src.mcp.tool_executor import MCPToolExecutor
    from src.mcp.security import MCPSecurityLayer, SecurityConfig

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_connection)

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

    assert result["content"] == "test result"
    mock_mcp_connection.call_tool.assert_called_once()


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
```

**Step 2: Run security integration tests**

Run: `pytest tests/mcp/test_tool_executor.py -k "security" -v`
Expected: 4 passed (new tests)

**Step 3: Run all tool executor tests**

Run: `pytest tests/mcp/test_tool_executor.py -v`
Expected: 6 passed

**Step 4: Commit**

```bash
git add tests/mcp/test_tool_executor.py
git commit -m "test(mcp): add tool executor security integration tests"
```

---

### Task 14: Test Tool Discovery Caching

**Files:**
- Modify: `tests/mcp/test_tool_executor.py`

**Step 1: Write caching tests**

```python
# tests/mcp/test_tool_executor.py (append)

@pytest.mark.asyncio
async def test_tool_discovery_caching(mock_mcp_connection):
    """Test tool discovery results are cached."""
    from src.mcp.tool_executor import MCPToolExecutor

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_connection)

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
    mock_mcp_connection.list_tools.assert_called_once()


@pytest.mark.asyncio
async def test_tool_discovery_different_servers(mock_mcp_connection):
    """Test different servers have separate caches."""
    from src.mcp.tool_executor import MCPToolExecutor

    manager = AsyncMock()
    manager.get_connection = AsyncMock(return_value=mock_mcp_connection)

    security = AsyncMock()
    db = AsyncMock()

    executor = MCPToolExecutor(manager, security, db)

    # Discover from two different servers
    await executor.discover_tools("filesystem")
    await executor.discover_tools("github")

    # Should call connection twice (once per server)
    assert manager.get_connection.call_count == 2
```

**Step 2: Run caching tests**

Run: `pytest tests/mcp/test_tool_executor.py -k "caching or different_servers" -v`
Expected: 2 passed

**Step 3: Run all tool executor tests**

Run: `pytest tests/mcp/test_tool_executor.py -v`
Expected: 8 passed

**Step 4: Commit**

```bash
git add tests/mcp/test_tool_executor.py
git commit -m "test(mcp): add tool discovery caching tests"
```

---

### Task 15: Phase 3.2 Integration Tests

**Files:**
- Create: `tests/mcp/test_phase_3_2_integration.py`

**Step 1: Write integration tests**

```python
# tests/mcp/test_phase_3_2_integration.py
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

    assert result["content"] == "test result"


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
```

**Step 2: Run integration tests**

Run: `pytest tests/mcp/test_phase_3_2_integration.py -v`
Expected: 4 passed

**Step 3: Run all Phase 3.2 tests**

Run: `pytest tests/mcp/ -v`
Expected: 25+ passed (config, connection, security, tool executor, integration)

**Step 4: Check test coverage**

Run: `pytest tests/mcp/ --cov=src/mcp --cov-report=term-missing`
Expected: 80%+ coverage

**Step 5: Commit**

```bash
git add tests/mcp/test_phase_3_2_integration.py
git commit -m "test(mcp): add Phase 3.2 integration tests"
```

---

### Task 16: Phase 3.2 Summary

**Files:**
- Create: `docs/mcp-phase-3.2-summary.md`

**Step 1: Write phase summary**

```markdown
# Phase 3.2 Summary: Security & Execution

**Status:** ✅ Complete
**Date:** 2026-03-04
**Duration:** ~2 days

## Completed Tasks

9. ✅ Implemented MCPSecurityLayer with three-tier permissions
10. ✅ Added security layer permission tests
11. ✅ Added path whitelist enforcement tests
12. ✅ Implemented MCPToolExecutor structure
13. ✅ Added tool executor security integration tests
14. ✅ Added tool discovery caching tests
15. ✅ Added Phase 3.2 integration tests
16. ✅ Phase 3.2 summary

## Test Coverage

- Security layer: 10 tests passing
- Tool executor: 8 tests passing
- Integration: 4 tests passing
- **Total: 22 tests (Phase 3.2), 35+ tests (cumulative)**
- **Coverage: 80%+ on all Phase 3.2 modules**

## Deliverables

**Code:**
- `src/mcp/security.py` - Three-tier security layer
- `src/mcp/tool_executor.py` - Tool discovery and execution

**Tests:**
- `tests/mcp/test_security.py` - Security tests (10 tests)
- `tests/mcp/test_tool_executor.py` - Executor tests (8 tests)
- `tests/mcp/test_phase_3_2_integration.py` - Integration (4 tests)

## Key Features Implemented

✅ **Three-Tier Permissions**
- Allowed operations (no confirmation)
- Denied operations (always blocked)
- Undefined operations (require confirmation)

✅ **Path Whitelist**
- Directory-level access control
- Path traversal prevention
- Symlink resolution

✅ **Tool Executor**
- Tool discovery with caching
- Security-enforced execution
- Audit logging (structure ready)

## Known Limitations

- Connection manager `_start_server()` still placeholder
- Audit logging to database not yet implemented
- Will be completed in Phase 3.3

## Next Phase

**Phase 3.3:** Integration & Polish
- Complete connection manager with real MCP SDK
- Extend Router for MCP syntax
- Extend AgentExecutor for MCP execution
- Add database audit logging
- End-to-end tests
```

**Step 2: Commit documentation**

```bash
git add docs/mcp-phase-3.2-summary.md
git commit -m "docs(mcp): Phase 3.2 security & execution complete"
```

---

## Phase 3.3: Integration & Polish (Tasks 17-27)

### Task 17: Extend SimpleRouter for MCP

**Files:**
- Modify: `src/core/router.py`
- Modify: `tests/core/test_router.py`

**Step 1: Write MCP routing tests**

```python
# tests/core/test_router.py (append)

def test_route_mcp_explicit_syntax():
    """Test routing MCP explicit syntax."""
    router = SimpleRouter()
    context = Context()

    plan = router.route("@mcp:filesystem:read_file path='/tmp/test.txt'", context)

    assert plan.type == "mcp"
    assert plan.requires_tools is True
    assert plan.metadata["server"] == "filesystem"
    assert plan.metadata["tool"] == "read_file"
    assert plan.metadata["arguments"]["path"] == "/tmp/test.txt"


def test_route_mcp_with_multiple_arguments():
    """Test MCP routing with multiple arguments."""
    router = SimpleRouter()
    context = Context()

    plan = router.route(
        '@mcp:filesystem:write_file path="/tmp/test.txt" content="Hello World"',
        context
    )

    assert plan.type == "mcp"
    assert plan.metadata["tool"] == "write_file"
    assert plan.metadata["arguments"]["path"] == "/tmp/test.txt"
    assert plan.metadata["arguments"]["content"] == "Hello World"


def test_route_mcp_takes_precedence_over_skill():
    """Test MCP syntax takes precedence over skill."""
    router = SimpleRouter()
    context = Context()

    # @mcp: should match before @skill:
    plan = router.route("@mcp:filesystem:read_file path='/tmp/test.txt'", context)

    assert plan.type == "mcp"


def test_route_invalid_mcp_syntax():
    """Test invalid MCP syntax falls back to simple query."""
    router = SimpleRouter()
    context = Context()

    # Missing colon between server and tool
    plan = router.route("@mcp:filesystem-read_file", context)

    # Should not match MCP pattern, fall back to simple query
    assert plan.type == "simple_query"
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/core/test_router.py -k "mcp" -v`
Expected: FAIL - MCP routing not implemented

**Step 3: Implement MCP routing in SimpleRouter**

```python
# src/core/router.py (modify)

import re
from dataclasses import dataclass
from typing import Any, Literal, Dict


@dataclass(frozen=True)
class ExecutionPlan:
    """Execution plan for processing user input.

    This is an immutable dataclass - all fields are frozen after creation.

    Attributes:
        type: Type of execution ('simple_query' | 'task' | 'skill' | 'mcp')
        requires_llm: Whether LLM processing is required
        requires_rag: Whether RAG (retrieval) is required
        requires_tools: Whether tool/function calling is required
        metadata: Optional metadata dictionary for additional context
    """

    type: Literal["simple_query", "task", "skill", "mcp"]
    requires_llm: bool
    requires_rag: bool = False
    requires_tools: bool = False
    metadata: dict[str, Any] | None = None


class SimpleRouter:
    """Simple router for user input.

    Routes user input to appropriate execution plans. Supports:
    - MCP tool detection (@mcp:server:tool syntax)
    - Skill detection (@skill or /skill syntax)
    - Parameter parsing (key='value' or key="value")
    - Simple query routing (default)

    Future versions will add:
    - Intent recognition (greeting, question, command)
    - RAG requirement detection
    """

    # Pattern to detect MCP explicit invocation: @mcp:server:tool
    MCP_PATTERN = re.compile(r'^@mcp:(\w+):(\w+)')

    # Pattern to detect skill invocation: @skill or /skill
    SKILL_PATTERN = re.compile(r'^[@/](\S+)')

    # Pattern to parse arguments: key='value' or key="value"
    ARG_PATTERN = re.compile(r'(\w+)=["\']([^"\']+)["\']')

    def route(self, user_input: str, context: Any) -> ExecutionPlan:
        """Route user input to appropriate execution plan.

        Priority order:
        1. MCP explicit syntax (@mcp:server:tool)
        2. Skill invocation (@skill or /skill)
        3. Simple query

        Args:
            user_input: Raw user input string
            context: Context manager instance

        Returns:
            ExecutionPlan with routing decision
        """
        # 1. Check MCP explicit invocation
        mcp_match = self.MCP_PATTERN.match(user_input)
        if mcp_match:
            server_name = mcp_match.group(1)
            tool_name = mcp_match.group(2)
            args = self._parse_arguments(user_input)

            return ExecutionPlan(
                type="mcp",
                requires_llm=False,
                requires_tools=True,
                metadata={
                    "server": server_name,
                    "tool": tool_name,
                    "arguments": args
                }
            )

        # 2. Check skill invocation (existing logic)
        skill_match = self.SKILL_PATTERN.match(user_input)
        if skill_match:
            skill_name = skill_match.group(1)
            args = self._parse_arguments(user_input)

            return ExecutionPlan(
                type="skill",
                requires_llm=False,
                metadata={
                    "skill_name": skill_name,
                    "arguments": args
                }
            )

        # 3. Default: simple query
        return ExecutionPlan(
            type="simple_query",
            requires_llm=True
        )

    def _parse_arguments(self, user_input: str) -> dict:
        """Parse key='value' arguments from input.

        Supports both single and double quotes.

        Args:
            user_input: Raw input string

        Returns:
            Dictionary of parsed arguments
        """
        args = {}
        for match in self.ARG_PATTERN.finditer(user_input):
            key, value = match.groups()
            args[key] = value
        return args
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/core/test_router.py -k "mcp" -v`
Expected: 4 passed

**Step 5: Run all router tests**

Run: `pytest tests/core/test_router.py -v`
Expected: All tests passing (including existing skill tests)

**Step 6: Commit**

```bash
git add src/core/router.py tests/core/test_router.py
git commit -m "feat(router): add MCP explicit syntax routing"
```

---

### Task 18: Extend AgentExecutor for MCP

**Files:**
- Modify: `src/core/executor.py`
- Modify: `tests/core/test_executor.py`

**Step 1: Write MCP execution tests**

```python
# tests/core/test_executor.py (append)

@pytest.mark.asyncio
async def test_execute_mcp_success(db):
    """Test executing MCP tool successfully."""
    from unittest.mock import AsyncMock
    from src.core.executor import AgentExecutor
    from src.core.router import SimpleRouter
    from src.core.llm_client import MockLLMClient

    router = SimpleRouter()
    llm_client = MockLLMClient()

    # Mock MCP executor
    mcp_executor = AsyncMock()
    mcp_executor.call_tool = AsyncMock(return_value={"content": "file contents"})

    executor = AgentExecutor(
        db=db,
        router=router,
        llm_client=llm_client,
        mcp_executor=mcp_executor
    )

    response = await executor.execute(
        "@mcp:filesystem:read_file path='/tmp/test.txt'",
        "session_1"
    )

    assert response["type"] == "mcp_result"
    assert "file contents" in response["response"]
    mcp_executor.call_tool.assert_called_once()


@pytest.mark.asyncio
async def test_execute_mcp_permission_denied(db):
    """Test MCP execution with permission denied."""
    from unittest.mock import AsyncMock
    from src.core.executor import AgentExecutor
    from src.core.router import SimpleRouter
    from src.core.llm_client import MockLLMClient
    from src.mcp.exceptions import PermissionDeniedError

    router = SimpleRouter()
    llm_client = MockLLMClient()

    # Mock MCP executor that raises permission error
    mcp_executor = AsyncMock()
    mcp_executor.call_tool = AsyncMock(
        side_effect=PermissionDeniedError("delete_file", "denied by policy")
    )

    executor = AgentExecutor(
        db=db,
        router=router,
        llm_client=llm_client,
        mcp_executor=mcp_executor
    )

    response = await executor.execute(
        "@mcp:filesystem:delete_file path='/tmp/test.txt'",
        "session_1"
    )

    assert response["type"] == "error"
    assert "Permission denied" in response["response"]


@pytest.mark.asyncio
async def test_execute_mcp_confirmation_required(db):
    """Test MCP execution requiring confirmation."""
    from unittest.mock import AsyncMock
    from src.core.executor import AgentExecutor
    from src.core.router import SimpleRouter
    from src.core.llm_client import MockLLMClient
    from src.mcp.exceptions import ConfirmationRequired

    router = SimpleRouter()
    llm_client = MockLLMClient()

    # Mock MCP executor that requires confirmation
    mcp_executor = AsyncMock()
    mcp_executor.call_tool = AsyncMock(
        side_effect=ConfirmationRequired(
            "filesystem:write_file",
            {"path": "/tmp/test.txt", "content": "test"},
            "Allow write_file?"
        )
    )

    executor = AgentExecutor(
        db=db,
        router=router,
        llm_client=llm_client,
        mcp_executor=mcp_executor
    )

    response = await executor.execute(
        '@mcp:filesystem:write_file path="/tmp/test.txt" content="test"',
        "session_1"
    )

    assert response["type"] == "confirmation_required"
    assert "Allow write_file?" in response["response"]
    assert "pending_operation" in response
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/core/test_executor.py -k "mcp" -v`
Expected: FAIL - MCP execution not implemented

**Step 3: Implement MCP execution in AgentExecutor**

```python
# src/core/executor.py (modify)

"""Agent执行器

该模块提供AgentExecutor类，用于协调所有组件（数据库、路由、LLM）
来处理用户请求。这是系统的核心编排层。
"""
from typing import Dict, Any, Optional
from .router import SimpleRouter, ExecutionPlan
from .llm_client import MockLLMClient
from .context import ContextManager
from ..storage.database import Database


class AgentExecutor:
    """Agent执行器 - 协调路由、上下文和LLM

    该类是系统的核心编排层，负责：
    1. 接收用户输入
    2. 管理会话上下文
    3. 路由到适当的执行计划
    4. 调用LLM处理请求
    5. 保存对话历史
    6. 执行技能（如果已配置）
    7. 执行MCP工具（如果已配置）

    Attributes:
        db: 数据库实例，用于持久化会话和消息
        router: 路由器实例，用于决策执行计划
        llm_client: LLM客户端实例，用于生成响应
        skill_registry: 技能注册表（可选）
        skill_executor: 技能执行器（可选）
        mcp_executor: MCP工具执行器（可选）
    """

    def __init__(
        self,
        db: Database,
        router: SimpleRouter,
        llm_client: MockLLMClient,
        skill_registry: Optional[Any] = None,
        skill_executor: Optional[Any] = None,
        mcp_executor: Optional[Any] = None
    ) -> None:
        """初始化执行器

        Args:
            db: 数据库实例
            router: 路由器实例
            llm_client: LLM客户端实例
            skill_registry: 技能注册表实例（可选）
            skill_executor: 技能执行器实例（可选）
            mcp_executor: MCP工具执行器实例（可选）
        """
        self.db = db
        self.router = router
        self.llm_client = llm_client
        self.skill_registry = skill_registry
        self.skill_executor = skill_executor
        self.mcp_executor = mcp_executor

    async def execute(self, user_input: str, session_id: str) -> Dict[str, Any]:
        """执行用户请求

        执行流程：
        1. 验证输入
        2. 获取会话上下文
        3. 路由决策
        4. 根据类型执行：
           - mcp: 调用MCP工具
           - skill: 执行技能
           - simple_query: LLM处理
        5. 保存结果
        6. 返回响应

        Args:
            user_input: 用户输入文本
            session_id: 会话ID

        Returns:
            包含响应和元数据的字典

        Raises:
            ValueError: 输入验证失败
        """
        # 验证输入
        if not user_input or not user_input.strip():
            return {
                "response": "Empty input",
                "type": "error"
            }

        # 获取或创建会话
        session = await self.db.get_or_create_session(session_id)

        # 构建上下文
        context = ContextManager(self.db)
        await context.load_session(session_id)

        # 路由决策
        plan = self.router.route(user_input, context)

        # 根据类型执行
        if plan.type == "mcp":
            response = await self._execute_mcp(plan, session_id)
        elif plan.type == "skill":
            response = await self._execute_skill(plan, session_id, context)
        else:
            response = await self._execute_simple_query(user_input, session_id, context)

        # 保存消息
        await self.db.save_message(
            session_id=session_id,
            role="user",
            content=user_input
        )
        await self.db.save_message(
            session_id=session_id,
            role="assistant",
            content=response.get("response", "")
        )

        return response

    async def _execute_mcp(
        self,
        plan: ExecutionPlan,
        session_id: str
    ) -> Dict[str, Any]:
        """执行MCP工具调用

        处理三种情况：
        1. 成功 → 返回工具结果
        2. 需要确认 → 返回确认提示
        3. 拒绝 → 返回错误信息

        Args:
            plan: 执行计划（包含server、tool、arguments）
            session_id: 会话ID

        Returns:
            响应字典
        """
        if not self.mcp_executor:
            return {
                "response": "MCP is not configured",
                "type": "error"
            }

        # Import exceptions here to avoid circular imports
        from ..mcp.exceptions import (
            PermissionDeniedError,
            ConfirmationRequired,
            MCPToolError
        )

        try:
            result = await self.mcp_executor.call_tool(
                server_name=plan.metadata["server"],
                tool_name=plan.metadata["tool"],
                arguments=plan.metadata["arguments"],
                session_id=session_id
            )

            return {
                "response": f"Tool executed successfully:\n{result}",
                "type": "mcp_result",
                "tool": plan.metadata["tool"],
                "result": result
            }

        except ConfirmationRequired as e:
            # Requires user confirmation
            return {
                "response": e.prompt,
                "type": "confirmation_required",
                "pending_operation": {
                    "server": plan.metadata["server"],
                    "tool": plan.metadata["tool"],
                    "arguments": plan.metadata["arguments"]
                }
            }

        except PermissionDeniedError as e:
            # Permission denied
            return {
                "response": f"Permission denied: {e.reason}",
                "type": "error"
            }

        except MCPToolError as e:
            # Tool execution error
            return {
                "response": f"Tool execution failed: {e}",
                "type": "error"
            }

    async def _execute_skill(
        self,
        plan: ExecutionPlan,
        session_id: str,
        context: ContextManager
    ) -> Dict[str, Any]:
        """执行技能（现有逻辑）"""
        # ... existing skill execution code ...
        if not self.skill_executor:
            return {
                "response": "Skills not configured",
                "type": "error"
            }

        skill_name = plan.metadata.get("skill_name")
        arguments = plan.metadata.get("arguments", {})

        try:
            result = await self.skill_executor.execute(
                skill_name=skill_name,
                arguments=arguments,
                context=context
            )
            return {
                "response": result,
                "type": "skill_result"
            }
        except Exception as e:
            return {
                "response": f"Skill execution failed: {e}",
                "type": "error"
            }

    async def _execute_simple_query(
        self,
        user_input: str,
        session_id: str,
        context: ContextManager
    ) -> Dict[str, Any]:
        """执行简单查询（现有逻辑）"""
        # ... existing simple query code ...
        messages = context.get_messages()
        messages.append({"role": "user", "content": user_input})

        response_text = await self.llm_client.chat(messages)

        return {
            "response": response_text,
            "type": "llm_response"
        }
```

**Step 4: Run tests to verify they pass**

Run: `pytest tests/core/test_executor.py -k "mcp" -v`
Expected: 3 passed

**Step 5: Run all executor tests**

Run: `pytest tests/core/test_executor.py -v`
Expected: All tests passing

**Step 6: Commit**

```bash
git add src/core/executor.py tests/core/test_executor.py
git commit -m "feat(executor): add MCP tool execution support"
```

---

### Task 19: Add Database Audit Logging

**Files:**
- Modify: `src/storage/database.py`
- Modify: `tests/storage/test_database.py`

**Step 1: Write audit log tests**

```python
# tests/storage/test_database.py (append)

@pytest.mark.asyncio
async def test_log_mcp_operation(db):
    """Test logging MCP operation to audit trail."""
    from datetime import datetime

    await db.log_mcp_operation(
        session_id="session_1",
        server="filesystem",
        tool="read_file",
        arguments={"path": "/tmp/test.txt"},
        status="success",
        result={"content": "test"},
        timestamp=datetime.now()
    )

    # Verify log was created
    logs = await db.get_mcp_audit_logs("session_1")
    assert len(logs) == 1
    assert logs[0]["server"] == "filesystem"
    assert logs[0]["tool"] == "read_file"
    assert logs[0]["status"] == "success"


@pytest.mark.asyncio
async def test_get_mcp_audit_logs_multiple(db):
    """Test retrieving multiple audit logs."""
    from datetime import datetime

    # Log multiple operations
    await db.log_mcp_operation(
        "session_1", "filesystem", "read_file",
        {"path": "/tmp/1.txt"}, "success",
        timestamp=datetime.now()
    )
    await db.log_mcp_operation(
        "session_1", "filesystem", "write_file",
        {"path": "/tmp/2.txt"}, "denied",
        timestamp=datetime.now()
    )

    logs = await db.get_mcp_audit_logs("session_1")
    assert len(logs) == 2


@pytest.mark.asyncio
async def test_mcp_audit_log_with_error(db):
    """Test logging failed operation with error."""
    from datetime import datetime

    await db.log_mcp_operation(
        session_id="session_1",
        server="filesystem",
        tool="read_file",
        arguments={"path": "/nonexistent.txt"},
        status="failed",
        error="File not found",
        timestamp=datetime.now()
    )

    logs = await db.get_mcp_audit_logs("session_1")
    assert logs[0]["status"] == "failed"
    assert logs[0]["error"] == "File not found"
```

**Step 2: Run tests to verify they fail**

Run: `pytest tests/storage/test_database.py -k "mcp_audit" -v`
Expected: FAIL - methods not implemented

**Step 3: Implement audit logging in database**

```python
# src/storage/database.py (add to Database class)

    async def log_mcp_operation(
        self,
        session_id: str,
        server: str,
        tool: str,
        arguments: dict,
        status: str,
        result: Any = None,
        error: str = None,
        timestamp: datetime = None
    ):
        """Log MCP operation to audit trail.

        Args:
            session_id: Session ID
            server: MCP server name
            tool: Tool name
            arguments: Tool arguments
            status: Operation status (success/denied/failed)
            result: Tool result (optional)
            error: Error message (optional)
            timestamp: Operation timestamp
        """
        if timestamp is None:
            timestamp = datetime.now()

        log_id = str(uuid.uuid4())

        await self.conn.execute(
            """
            INSERT INTO mcp_audit_logs
            (id, session_id, server_name, tool_name, arguments, result, status, error_message, timestamp)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                log_id,
                session_id,
                server,
                tool,
                json.dumps(arguments),
                json.dumps(result) if result else None,
                status,
                error,
                timestamp.isoformat()
            )
        )
        await self.conn.commit()

    async def get_mcp_audit_logs(
        self,
        session_id: str,
        limit: int = 100
    ) -> List[Dict[str, Any]]:
        """Get MCP audit logs for a session.

        Args:
            session_id: Session ID
            limit: Maximum number of logs to return

        Returns:
            List of audit log dictionaries
        """
        cursor = await self.conn.execute(
            """
            SELECT id, session_id, server_name, tool_name, arguments,
                   result, status, error_message, timestamp
            FROM mcp_audit_logs
            WHERE session_id = ?
            ORDER BY timestamp DESC
            LIMIT ?
            """,
            (session_id, limit)
        )
        rows = await cursor.fetchall()

        logs = []
        for row in rows:
            logs.append({
                "id": row[0],
                "session_id": row[1],
                "server": row[2],
                "tool": row[3],
                "arguments": json.loads(row[4]),
                "result": json.loads(row[5]) if row[5] else None,
                "status": row[6],
                "error": row[7],
                "timestamp": row[8]
            })

        return logs
```

**Step 4: Add database schema migration**

```python
# src/storage/database.py (add to _initialize_schema method)

    async def _initialize_schema(self):
        """Initialize database schema."""
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        """)

        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        # MCP audit logs table (NEW)
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS mcp_audit_logs (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                server_name TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                arguments JSON NOT NULL,
                result JSON,
                status TEXT NOT NULL,
                error_message TEXT,
                timestamp TIMESTAMP NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        # Indexes for performance
        await self.conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_mcp_logs_session
            ON mcp_audit_logs(session_id)
        """)

        await self.conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_mcp_logs_timestamp
            ON mcp_audit_logs(timestamp)
        """)

        await self.conn.commit()
```

**Step 5: Run tests to verify they pass**

Run: `pytest tests/storage/test_database.py -k "mcp_audit" -v`
Expected: 3 passed

**Step 6: Run all database tests**

Run: `pytest tests/storage/test_database.py -v`
Expected: All tests passing

**Step 7: Commit**

```bash
git add src/storage/database.py tests/storage/test_database.py
git commit -m "feat(database): add MCP audit logging"
```

---

### Task 20: Connect Tool Executor to Audit Logging

**Files:**
- Modify: `src/mcp/tool_executor.py`

**Step 1: Update _log_operation to use database**

```python
# src/mcp/tool_executor.py (replace _log_operation)

    async def _log_operation(
        self,
        session_id: str,
        server_name: str,
        tool_name: str,
        arguments: dict,
        status: str,
        result: Any = None,
        error: str = None
    ):
        """Log tool operation to audit trail."""
        from datetime import datetime

        await self.db.log_mcp_operation(
            session_id=session_id,
            server=server_name,
            tool=tool_name,
            arguments=arguments,
            status=status,
            result=result,
            error=error,
            timestamp=datetime.now()
        )
```

**Step 2: Update call_tool to log appropriately**

Ensure status values are: "success", "failed", "denied"

**Step 3: Test integration**

Run: `pytest tests/mcp/test_tool_executor.py -v`

**Step 4: Commit**

```bash
git add src/mcp/tool_executor.py
git commit -m "feat(mcp): connect tool executor to audit logging"
```

---

### Task 21: Initialize MCP in main.py

**Files:**
- Modify: `src/main.py`

**Step 1: Add MCP imports and initialization**

```python
# src/main.py (add imports)
from .mcp.connection_manager import MCPConnectionManager
from .mcp.security import MCPSecurityLayer
from .mcp.tool_executor import MCPToolExecutor
from .mcp.config import load_mcp_config
```

```python
# src/main.py (in lifespan function, after skill initialization)

    # Initialize MCP if enabled
    mcp_enabled = os.getenv("MCP_ENABLED", "true").lower() == "true"
    mcp_executor = None

    if mcp_enabled:
        try:
            config_path = "config/mcp_config.yaml"
            if Path(config_path).exists():
                mcp_config = load_mcp_config(config_path)

                mcp_manager = MCPConnectionManager(config_path)
                # Get security config for filesystem server
                fs_config = mcp_config.servers.get("filesystem")
                if fs_config:
                    mcp_security = MCPSecurityLayer(fs_config.security)
                    mcp_executor = MCPToolExecutor(mcp_manager, mcp_security, db)
                    logger.info("MCP integration initialized")
            else:
                logger.warning(f"MCP config not found at {config_path}")
        except Exception as e:
            logger.error(f"Failed to initialize MCP: {e}")
            logger.warning("Continuing without MCP support")

    # Create executor with MCP support
    executor = AgentExecutor(
        db=db,
        router=router_instance,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor,
        mcp_executor=mcp_executor
    )

    # ... rest of lifespan

    # Shutdown
    if mcp_executor:
        await mcp_executor.manager.shutdown_all()
        logger.info("MCP connections closed")
```

**Step 2: Test startup**

Run: `uvicorn src.main:app --reload`
Expected: "MCP integration initialized" in logs

**Step 3: Commit**

```bash
git add src/main.py
git commit -m "feat(main): initialize MCP on startup"
```

---

### Task 22: End-to-End Integration Tests

**Files:**
- Create: `tests/test_mcp_e2e.py`

**Step 1: Write E2E tests**

```python
# tests/test_mcp_e2e.py
"""End-to-end tests for MCP integration."""
import pytest
from httpx import AsyncClient
from src.main import app


@pytest.mark.asyncio
async def test_mcp_explicit_call_via_api():
    """Test MCP call through full API stack."""
    async with AsyncClient(app=app, base_url="http://test") as client:
        response = await client.post(
            "/api/chat",
            json={
                "message": "@mcp:filesystem:read_file path='/tmp/test.txt'",
                "session_id": "test_e2e"
            }
        )

        assert response.status_code == 200
        data = response.json()
        # Should either succeed or fail with security error
        assert data["type"] in ["mcp_result", "error"]


@pytest.mark.asyncio
async def test_mcp_denied_operation_via_api():
    """Test denied operation returns error."""
    async with AsyncClient(app=app, base_url="http://test") as client:
        response = await client.post(
            "/api/chat",
            json={
                "message": "@mcp:filesystem:delete_file path='/tmp/test.txt'",
                "session_id": "test_e2e"
            }
        )

        assert response.status_code == 200
        data = response.json()
        assert data["type"] == "error"
        assert "Permission denied" in data["response"]
```

**Step 2: Run E2E tests**

Run: `pytest tests/test_mcp_e2e.py -v`

**Step 3: Commit**

```bash
git add tests/test_mcp_e2e.py
git commit -m "test(mcp): add end-to-end integration tests"
```

---

### Task 23: Documentation - User Guide

**Files:**
- Create: `docs/mcp.md`

**Step 1: Write user guide**

```markdown
# MCP Integration User Guide

## Overview

MCP (Model Context Protocol) integration enables filesystem operations through a secure, permission-controlled interface.

## Quick Start

1. **Ensure Ollama/Node.js installed:**
   ```bash
   brew install node  # macOS
   ```

2. **Configure allowed directories:**
   Edit `config/mcp_config.yaml`:
   ```yaml
   servers:
     filesystem:
       security:
         allowed_directories:
           - /Users/yourname/Documents
   ```

3. **Start application:**
   ```bash
   uvicorn src.main:app --reload
   ```

## Usage

### Explicit Syntax

```bash
# Read file
@mcp:filesystem:read_file path='/path/to/file.txt'

# List directory
@mcp:filesystem:list_directory path='/path/to/dir'

# Write file (requires confirmation)
@mcp:filesystem:write_file path='/path/to/file.txt' content='Hello'
```

### Security Model

**Three-tier permissions:**
- **Allowed**: Executes immediately (read_file, list_directory)
- **Denied**: Rejected immediately (delete_file)
- **Undefined**: Requires confirmation (write_file)

**Path whitelist:**
- Only directories in `allowed_directories` are accessible
- Path traversal (`..`) is prevented

## Configuration

See `config/mcp_config.yaml` for full options.

## Troubleshooting

**MCP not initialized:**
- Check `config/mcp_config.yaml` exists
- Set `MCP_ENABLED=true` environment variable

**Permission denied:**
- Add directory to `allowed_directories`
- Check operation not in `denied_operations`
```

**Step 2: Commit**

```bash
git add docs/mcp.md
git commit -m "docs(mcp): add user guide"
```

---

### Task 24: Update README

**Files:**
- Modify: `README.md`

**Step 1: Add MCP section**

Add to README features section:

```markdown
## Features

- ✅ **Skill System** - Claude Code compatible skills
- ✅ **Ollama LLM** - Local model support
- ✅ **MCP Integration** - Filesystem operations via MCP
  - Three-tier security (allowed/denied/confirm)
  - Directory whitelist
  - Audit logging
```

Add to usage section:

```markdown
### MCP Tools

```bash
# Read file
curl -X POST http://localhost:8000/api/chat \
  -d '{"message": "@mcp:filesystem:read_file path=\"/path/to/file.txt\"", "session_id": "test"}'
```
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add MCP integration to README"
```

---

### Task 25: Run Full Test Suite

**Step 1: Run all tests with coverage**

Run: `pytest tests/ -v --cov=src --cov-report=term-missing`
Expected: 50+ tests passing, 80%+ coverage

**Step 2: Check specific modules**

Run: `pytest tests/mcp/ -v --cov=src/mcp`
Expected: 35+ MCP tests passing

**Step 3: Fix any failures**

Address any test failures found.

---

### Task 26: Phase 3 Complete Summary

**Files:**
- Create: `docs/mcp-phase-3-complete.md`

**Step 1: Write completion summary**

```markdown
# Phase 3: MCP Integration - COMPLETE ✅

**Date:** 2026-03-04
**Duration:** 5-6 days
**Status:** ✅ All tasks complete

## Summary

Successfully integrated Model Context Protocol with:
- Generic MCP Client architecture
- Filesystem MCP server support
- Three-tier security system
- Audit logging
- Full router/executor integration

## Test Results

- **Total tests:** 50+ passing
- **Coverage:** 80%+ on all MCP modules
- **Integration:** Full stack tested

## Key Deliverables

**Phase 3.1: Infrastructure**
- MCP SDK integration
- Configuration system
- Connection manager (lazy singleton)

**Phase 3.2: Security & Execution**
- Three-tier permissions (allowed/denied/confirm)
- Path whitelist with traversal prevention
- Tool executor with discovery caching

**Phase 3.3: Integration**
- Router extension for @mcp: syntax
- Executor MCP execution support
- Database audit logging
- Main.py initialization
- End-to-end tests
- Documentation

## Usage

```bash
# Start app
uvicorn src.main:app --reload

# Use MCP tools
@mcp:filesystem:read_file path='/tmp/test.txt'
```

## Next Steps

**Phase 4 (Future):**
- Natural language tool invocation
- Additional MCP servers (github, sqlite)
- Streaming responses
- Advanced confirmation UI
```

**Step 2: Commit**

```bash
git add docs/mcp-phase-3-complete.md
git commit -m "docs(mcp): Phase 3 complete summary"
```

---

### Task 27: Final Commit and Tag

**Step 1: Final test run**

Run: `pytest tests/ -v`
Expected: All tests passing

**Step 2: Create final commit**

```bash
git add -A
git commit -m "feat(mcp): Phase 3 complete - MCP integration

Complete MCP integration with:
- Generic MCP Client (supports any MCP server)
- Filesystem server with lazy singleton
- Three-tier security (allowed/denied/confirm)
- Path whitelist enforcement
- Audit logging to database
- Router @mcp: syntax support
- Full test coverage (80%+)
- Documentation

Phase 3.1: Infrastructure ✅
Phase 3.2: Security & Execution ✅
Phase 3.3: Integration & Polish ✅

Tests: 50+ passing
Coverage: 80%+

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

**Step 3: Create git tag**

```bash
git tag -a v0.3.0 -m "Phase 3: MCP Integration Complete"
```

**Step 4: Celebrate! 🎉**

Phase 3 MCP Integration is complete!

---

## Execution Options

Plan saved to: `docs/plans/2026-03-04-mcp-integration.md`

**Choose execution approach:**

**1. Subagent-Driven (recommended)** - Current session, task-by-task with review
**2. Parallel Session** - New session with superpowers:executing-plans

Which would you prefer?