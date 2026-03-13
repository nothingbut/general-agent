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
