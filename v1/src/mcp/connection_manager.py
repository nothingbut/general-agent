"""MCP connection manager - handles server lifecycle."""
import asyncio
import logging
from typing import Dict, Set, Any, Optional
from contextlib import AsyncExitStack
from dataclasses import dataclass

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

from .config import load_mcp_config, MCPConfig, ServerConfig
from .exceptions import (
    MCPServerStartupError,
    MCPServerDisabledError,
    MCPServerCrashError
)

logger = logging.getLogger(__name__)


@dataclass
class MCPConnection:
    """Wrapper for MCP server connection.

    Attributes:
        session: The active ClientSession
        exit_stack: AsyncExitStack managing contexts
        server_name: Name of the server
    """
    session: ClientSession
    exit_stack: AsyncExitStack
    server_name: str


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
        self.connections: Dict[str, MCPConnection] = {}
        self._locks: Dict[str, asyncio.Lock] = {}
        self._restart_count: Dict[str, int] = {}
        self._disabled_servers: Set[str] = set()

        logger.info(f"MCPConnectionManager initialized with {len(self.config.servers)} servers")

    async def get_connection(self, server_name: str) -> ClientSession:
        """Get or create server connection (thread-safe).

        First call starts the server process, subsequent calls reuse
        the connection. Uses asyncio.Lock for concurrency safety.

        Args:
            server_name: Name of server (e.g., "filesystem")

        Returns:
            MCP ClientSession object

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

        return self.connections[server_name].session

    async def _start_server(self, server_name: str) -> MCPConnection:
        """Start MCP server process.

        Args:
            server_name: Server name

        Returns:
            MCPConnection wrapper

        Raises:
            MCPServerStartupError: Startup failed
        """
        server_config = self.config.servers[server_name]

        try:
            # Create server parameters
            server_params = StdioServerParameters(
                command=server_config.command,
                args=server_config.args,
                env=server_config.env or {}
            )

            # Create exit stack to manage contexts
            exit_stack = AsyncExitStack()

            # Enter stdio_client context
            read, write = await exit_stack.enter_async_context(
                stdio_client(server_params)
            )

            # Enter ClientSession context
            session = await exit_stack.enter_async_context(
                ClientSession(read, write)
            )

            # Initialize the session
            await session.initialize()

            logger.info(f"Successfully started MCP server: {server_name}")

            return MCPConnection(
                session=session,
                exit_stack=exit_stack,
                server_name=server_name
            )

        except Exception as e:
            logger.error(f"Failed to start MCP server '{server_name}': {e}")
            raise MCPServerStartupError(server_name, str(e)) from e

    async def health_check(self, server_name: str) -> bool:
        """Check if server is healthy.

        Args:
            server_name: Server name

        Returns:
            True if healthy, False otherwise
        """
        if server_name not in self.connections:
            return False

        try:
            connection = self.connections[server_name]
            # Try to list tools as a health check ping
            await connection.session.list_tools()
            return True
        except Exception as e:
            logger.warning(f"Health check failed for '{server_name}': {e}")
            return False

    async def restart_server(self, server_name: str):
        """Restart a crashed server.

        Args:
            server_name: Server name

        Raises:
            MCPServerCrashError: Too many restart attempts
        """
        # Track restart attempts
        self._restart_count.setdefault(server_name, 0)
        self._restart_count[server_name] += 1

        if self._restart_count[server_name] > 3:
            self._mark_server_disabled(server_name)
            raise MCPServerCrashError(
                server_name,
                f"Server has crashed {self._restart_count[server_name]} times"
            )

        logger.info(f"Restarting MCP server: {server_name} (attempt {self._restart_count[server_name]})")

        # Close existing connection
        if server_name in self.connections:
            try:
                await self.connections[server_name].exit_stack.aclose()
            except Exception as e:
                logger.warning(f"Error closing connection during restart: {e}")
            del self.connections[server_name]

        # Start new connection
        try:
            self.connections[server_name] = await self._start_server(server_name)
            # Reset restart count on successful restart
            self._restart_count[server_name] = 0
        except Exception as e:
            logger.error(f"Failed to restart server '{server_name}': {e}")
            raise

    async def shutdown_all(self):
        """Shutdown all server connections."""
        logger.info("Shutting down all MCP servers")

        for server_name, connection in list(self.connections.items()):
            try:
                # Close the exit stack which will cleanup all contexts
                await connection.exit_stack.aclose()
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
