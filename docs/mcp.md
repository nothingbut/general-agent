# MCP Integration User Guide

## Overview

MCP (Model Context Protocol) integration enables filesystem operations through a secure, permission-controlled interface. The general-agent system integrates with MCP servers to provide safe, audited access to external tools and resources.

## Quick Start

### Prerequisites

1. **Node.js** (for MCP filesystem server):
   ```bash
   # macOS
   brew install node

   # Ubuntu/Debian
   sudo apt install nodejs npm
   ```

2. **Python dependencies** (already included in pyproject.toml):
   - `mcp>=0.9.0`

### Installation

1. **Install the project** (including MCP support):
   ```bash
   pip install -e .
   ```

2. **Configure allowed directories**:

   Edit `config/mcp_config.yaml`:
   ```yaml
   servers:
     filesystem:
       command: npx
       args:
         - "-y"
         - "@modelcontextprotocol/server-filesystem"
         - "/tmp"  # Root directory for filesystem server
       security:
         allowed_directories:
           - /Users/yourname/Documents
           - /Users/yourname/Downloads
         allowed_operations:
           - read_file
           - list_directory
         denied_operations:
           - delete_file
       timeout: 30.0
       health_check_interval: 60

   global:
     enabled: true
     audit_log_retention_days: 90
     confirmation_timeout: 300
   ```

3. **Enable MCP** (set environment variable):
   ```bash
   export MCP_ENABLED=true
   ```

4. **Start application**:
   ```bash
   uvicorn src.main:app --reload
   ```

## Usage

### Explicit Syntax

MCP tools are invoked using the explicit syntax: `@mcp:server:tool arguments`

#### Examples

```bash
# Read a file
@mcp:filesystem:read_file path='/path/to/file.txt'

# List directory contents
@mcp:filesystem:list_directory path='/path/to/dir'

# Write file (requires confirmation if not in allowed_operations)
@mcp:filesystem:write_file path='/path/to/file.txt' content='Hello World'

# Create directory
@mcp:filesystem:create_directory path='/path/to/newdir'
```

#### Parameter Format

Parameters use key='value' or key="value" format:
- Single or double quotes
- Multiple parameters space-separated
- Paths should be absolute

### API Integration

Use the `/api/chat` endpoint to call MCP tools:

```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "@mcp:filesystem:read_file path=\"/tmp/test.txt\"",
    "session_id": "my-session"
  }'
```

Response format:
```json
{
  "response": "Tool executed successfully:\n{'content': [{'type': 'text', 'text': 'file contents...'}]}",
  "session_id": "my-session",
  "plan_type": "mcp",
  "success": true
}
```

## Security Model

### Three-Tier Permission System

MCP operations are controlled by a three-tier security system:

1. **Allowed Operations** (immediate execution):
   - Defined in `allowed_operations` list
   - Examples: `read_file`, `list_directory`
   - Execute without user confirmation

2. **Denied Operations** (immediate rejection):
   - Defined in `denied_operations` list
   - Examples: `delete_file`, `move_file`
   - Always rejected with error message

3. **Undefined Operations** (confirmation required):
   - Not in either list
   - Examples: `write_file`, `create_directory`
   - Require user confirmation before execution
   - Confirmation timeout: 300 seconds (configurable)

### Path Whitelist

Only directories listed in `allowed_directories` are accessible:

- **Path validation**: All paths must be within allowed directories
- **Traversal prevention**: `..` components are blocked
- **Absolute paths**: Relative paths are resolved to absolute
- **Security error**: Operations outside whitelist are rejected

Example:
```yaml
security:
  allowed_directories:
    - /Users/alice/Documents
    - /home/alice/projects
```

Allowed:
- `/Users/alice/Documents/file.txt` ✅
- `/Users/alice/Documents/subdir/file.txt` ✅

Denied:
- `/Users/alice/Desktop/file.txt` ❌
- `/Users/alice/Documents/../Desktop/file.txt` ❌
- `/etc/passwd` ❌

### Audit Logging

All MCP operations are logged to the database:

- **Operation**: Server, tool, and arguments
- **Timestamp**: When executed
- **Session ID**: Associated conversation
- **Result**: Success/failure and response
- **User confirmation**: Whether confirmation was required

Audit logs are retained for 90 days (configurable via `audit_log_retention_days`).

## Configuration

### Full Configuration Reference

```yaml
servers:
  filesystem:
    # Command to start MCP server
    command: npx

    # Arguments passed to command
    args:
      - "-y"  # Auto-install npm packages
      - "@modelcontextprotocol/server-filesystem"
      - "/tmp"  # Root directory

    # Security settings
    security:
      # Whitelist of accessible directories
      allowed_directories:
        - /Users/yourname/Documents

      # Operations that execute immediately
      allowed_operations:
        - read_file
        - list_directory

      # Operations that are always denied
      denied_operations:
        - delete_file

    # Connection timeout (seconds)
    timeout: 30.0

    # Health check interval (seconds)
    health_check_interval: 60

global:
  # Enable/disable MCP globally
  enabled: true

  # Audit log retention (days)
  audit_log_retention_days: 90

  # Confirmation timeout (seconds)
  confirmation_timeout: 300
```

### Environment Variables

- `MCP_ENABLED`: Enable MCP integration (default: `false`)
  ```bash
  export MCP_ENABLED=true
  ```

## Troubleshooting

### MCP Not Initialized

**Symptoms:**
- "MCP工具系统未配置" error message
- MCP calls fail

**Solutions:**
1. Verify `config/mcp_config.yaml` exists
2. Set `MCP_ENABLED=true` environment variable
3. Check MCP config is valid YAML
4. Ensure `filesystem` server is configured

### Permission Denied Errors

**Symptoms:**
- "Permission denied" error message
- Operations blocked by security layer

**Solutions:**
1. Add target directory to `allowed_directories`
2. Remove operation from `denied_operations` if applicable
3. Add operation to `allowed_operations` to skip confirmation
4. Check path doesn't use `..` (path traversal)

### Connection Failures

**Symptoms:**
- Timeout errors
- "Failed to connect to MCP server"

**Solutions:**
1. Verify Node.js is installed: `node --version`
2. Check npm package is available: `npx @modelcontextprotocol/server-filesystem --version`
3. Increase `timeout` value in config
4. Check server logs in database audit table

### Tool Not Found

**Symptoms:**
- "Tool 'xyz' not found"
- Available tools list doesn't include expected tool

**Solutions:**
1. Verify tool name spelling matches MCP server tools
2. Check MCP server supports the tool
3. List available tools: use `list_tools` endpoint (if implemented)
4. Consult MCP server documentation

## Advanced Usage

### Adding New MCP Servers

To integrate additional MCP servers (e.g., GitHub, SQLite):

1. Add server to `config/mcp_config.yaml`:
   ```yaml
   servers:
     github:
       command: npx
       args:
         - "-y"
         - "@modelcontextprotocol/server-github"
       security:
         allowed_operations:
           - get_file_contents
         denied_operations:
           - delete_repository
   ```

2. Restart application to load new server

3. Use with explicit syntax:
   ```bash
   @mcp:github:get_file_contents owner='anthropics' repo='claude' path='README.md'
   ```

### Customizing Security Policies

Adjust security based on your use case:

**Development (permissive):**
```yaml
security:
  allowed_directories:
    - /Users/yourname
  allowed_operations:
    - read_file
    - write_file
    - list_directory
    - create_directory
  denied_operations: []
```

**Production (restrictive):**
```yaml
security:
  allowed_directories:
    - /var/app/data
  allowed_operations:
    - read_file
  denied_operations:
    - write_file
    - delete_file
    - create_directory
    - move_file
```

## Architecture

### Component Overview

```
┌─────────────────┐
│   User Input    │
│ @mcp:fs:read... │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  SimpleRouter   │  ← Detects @mcp: prefix
│  (route)        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ AgentExecutor   │  ← Routes to MCP executor
│ (_execute_mcp)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ MCPToolExecutor │  ← Validates & executes
│ (call_tool)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SecurityLayer   │  ← Checks permissions
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ConnectionManager│  ← Manages MCP connections
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   MCP Server    │  ← Executes tool
│  (filesystem)   │
└─────────────────┘
```

### Key Components

- **SimpleRouter**: Detects `@mcp:` syntax and creates execution plan
- **AgentExecutor**: Orchestrates MCP tool execution
- **MCPToolExecutor**: Validates arguments and calls tools
- **MCPSecurityLayer**: Enforces three-tier permissions and path whitelist
- **MCPConnectionManager**: Lazy-loads and manages MCP server connections
- **Database**: Stores audit logs for compliance

## Next Steps

- **Phase 4 (Future)**: Natural language tool invocation (no `@mcp:` prefix needed)
- **Additional Servers**: GitHub, SQLite, Web Search integrations
- **Streaming**: Real-time MCP operation progress
- **Advanced UI**: Interactive confirmation dialogs

## See Also

- [MCP Protocol Specification](https://modelcontextprotocol.io)
- [MCP Filesystem Server](https://github.com/modelcontextprotocol/servers)
- [API Documentation](./api.md)
- [Skills Documentation](./skills.md)
