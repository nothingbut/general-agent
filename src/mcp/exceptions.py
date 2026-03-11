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
