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
