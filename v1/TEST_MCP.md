# 测试 File MCP

## 快速测试

### 1. 检查 MCP 配置
```bash
cat ~/.config/general-agent/mcp-servers.json
```

如果不存在，创建：
```bash
mkdir -p ~/.config/general-agent
cat > ~/.config/general-agent/mcp-servers.json << 'EOF'
{
  "mcpServers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"],
      "env": {}
    }
  }
}
EOF
```

### 2. 测试 MCP 连接
```python
import asyncio
from src.mcp.connection import MCPConnectionManager

async def test():
    manager = MCPConnectionManager()
    await manager.connect("filesystem")
    tools = await manager.list_tools("filesystem")
    print(f"可用工具: {[t['name'] for t in tools]}")
    await manager.disconnect("filesystem")

asyncio.run(test())
```

### 3. 测试文件操作
```python
import asyncio
from src.mcp.connection import MCPConnectionManager

async def test():
    manager = MCPConnectionManager()
    await manager.connect("filesystem")

    # 读取文件
    result = await manager.call_tool(
        "filesystem",
        "read_file",
        {"path": "/tmp/test.txt"}
    )
    print(result)

    await manager.disconnect("filesystem")

asyncio.run(test())
```

### 4. 在 Agent 中使用
启动 agent 后，尝试：
- "帮我读取 /tmp/test.txt 文件"
- "列出 /tmp 目录的文件"
- "创建一个测试文件"

## 注意
- 确保有 Node.js/npx
- MCP 服务器会限制访问路径（如 /tmp）
