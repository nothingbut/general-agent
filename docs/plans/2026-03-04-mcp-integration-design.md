# MCP 集成设计文档

**日期：** 2026-03-04
**版本：** 1.0
**状态：** 已批准
**阶段：** Phase 3 - MCP Integration

---

## 1. 概述

### 1.1 目标

将 Model Context Protocol (MCP) 集成到 General Agent 系统，实现通用 MCP Client 架构，支持外部工具调用和资源访问。

**Phase 3 范围：**
- 实现通用 MCP Client 基础架构
- 优先支持 filesystem MCP 服务器
- 为未来扩展其他 MCP 服务器（github, sqlite 等）打好基础

### 1.2 设计原则

1. **通用架构，渐进实现** - 核心组件支持任何 MCP 服务器，Phase 3 只配置 filesystem
2. **集成而非侵入** - 复用现有 Router → Executor 流程，MCP 作为新的 execution type
3. **安全第一** - 三层权限控制 + 目录白名单 + 审计日志
4. **Hybrid 调用** - 支持显式语法和自然语言（未来）

### 1.3 关键特性

✅ **通用 MCP Client** - 支持任何标准 MCP 服务器
✅ **Lazy Singleton** - 首次调用时启动，跨会话共享
✅ **三层权限控制** - Allowed → Denied → Prompt
✅ **路径白名单** - 目录级访问控制
✅ **健康检查** - 自动检测和恢复崩溃的服务器
✅ **审计日志** - 所有操作可追溯
✅ **Hybrid 调用** - 显式语法 + 自然语言（Phase 4）

---

## 2. 整体架构

### 2.1 系统层次

```
┌─────────────────────────────────────────────────────────┐
│                    API Layer (FastAPI)                  │
│                POST /api/chat                           │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                 AgentExecutor                           │
│            (现有核心协调层)                              │
└────┬────────────────────┬──────────────────────────────┘
     │                    │
     │ (现有)             │ (新增)
     ▼                    ▼
┌─────────────┐    ┌──────────────────────────────────────┐
│ SkillExecutor│    │      MCP Integration Layer          │
│   (Phase 2) │    │                                      │
└─────────────┘    │  ┌────────────────────────────┐     │
                   │  │   MCPConnectionManager     │     │
                   │  │  (Lazy Singleton Pattern)  │     │
                   │  └──────────┬─────────────────┘     │
                   │             │                        │
                   │  ┌──────────▼─────────────────┐     │
                   │  │    MCPSecurityLayer        │     │
                   │  │  (Whitelist + Permissions) │     │
                   │  └──────────┬─────────────────┘     │
                   │             │                        │
                   │  ┌──────────▼─────────────────┐     │
                   │  │     MCPToolExecutor        │     │
                   │  │   (Tool Discovery & Call)  │     │
                   │  └──────────┬─────────────────┘     │
                   │             │                        │
                   └─────────────┼──────────────────────┘
                                 │
                      ┌──────────▼──────────────┐
                      │   mcp Python SDK        │
                      │  (Official Package)     │
                      └──────────┬──────────────┘
                                 │ stdio (JSON-RPC)
                      ┌──────────▼──────────────┐
                      │  MCP Server Process     │
                      │  (filesystem server)    │
                      └─────────────────────────┘
```

### 2.2 数据流程

#### 2.2.1 显式调用流程

```
用户输入: "@mcp:filesystem:read_file path='/Users/user/test.txt'"
    ↓
Router.route()
    │ 检测 @mcp: 前缀
    │ 解析: server=filesystem, tool=read_file, args={path: ...}
    ↓
ExecutionPlan(type="mcp", metadata={...})
    ↓
AgentExecutor.execute()
    ↓
MCPToolExecutor.call_tool()
    │
    ├→ SecurityLayer.check_permission()
    │   ├─ denied_operations? → 拒绝
    │   ├─ 路径在白名单内? → 检查
    │   ├─ allowed_operations? → 允许
    │   └─ 否则 → raise ConfirmationRequired
    │
    ├→ 记录审计日志
    │
    ├→ MCPConnectionManager.get_connection("filesystem")
    │   └─ 如果未启动 → 启动服务器进程
    │
    ├→ connection.call_tool("read_file", {path: ...})
    │   └─ mcp SDK → stdio → MCP Server
    │
    └→ 返回结果
```

#### 2.2.2 自然语言调用流程（未来扩展）

```
用户输入: "帮我读取 test.txt 的内容"
    ↓
Router.route()
    │ 检测关键词：读取、文件
    │ requires_tools = True
    ↓
ExecutionPlan(type="simple_query", requires_tools=True)
    ↓
AgentExecutor.execute()
    │
    ├→ 收集上下文（会话历史）
    │
    ├→ 发现可用工具
    │   MCPToolExecutor.discover_tools("filesystem")
    │   → [read_file, write_file, list_directory, ...]
    │
    ├→ 构造 LLM 请求（带工具定义）
    │   messages + tools=[{name: "filesystem:read_file", ...}]
    │
    ├→ LLM.chat() → 返回 tool_call
    │   {tool: "filesystem:read_file", args: {path: "/path/to/test.txt"}}
    │
    ├→ MCPToolExecutor.call_tool(...)
    │   （同显式调用流程）
    │
    └→ 将工具结果返回给 LLM → 生成最终回复
```

**注：** Phase 3 先实现显式调用，自然语言调用等 Ollama 支持 tool calling 后再加。

---

## 3. 核心组件设计

### 3.1 MCPConnectionManager (连接管理器)

**文件：** `src/mcp/connection_manager.py`

**职责：** 管理 MCP 服务器进程生命周期，实现 lazy singleton 模式

**核心接口：**

```python
class MCPConnectionManager:
    """MCP 服务器连接管理器 (Singleton)

    特性：
    - Lazy initialization: 首次调用时启动服务器
    - 跨会话共享：所有请求共用同一个服务器实例
    - 健康检查：定期 ping，失败时自动重启
    - 优雅关闭：应用退出时清理进程
    """

    def __init__(self, config_path: str):
        """初始化连接管理器

        Args:
            config_path: YAML 配置文件路径
        """
        self.config = self._load_config(config_path)
        self.connections: Dict[str, MCPConnection] = {}
        self._locks: Dict[str, asyncio.Lock] = {}
        self._restart_count: Dict[str, int] = {}
        self._disabled_servers: Set[str] = set()

    async def get_connection(self, server_name: str) -> MCPConnection:
        """获取或创建服务器连接（线程安全）

        首次调用时启动服务器进程，后续调用复用连接。
        使用 asyncio.Lock 确保并发安全。

        Args:
            server_name: 服务器名称（如 "filesystem"）

        Returns:
            MCPConnection 实例

        Raises:
            MCPServerStartupError: 服务器启动失败
            MCPServerDisabledError: 服务器已被禁用
        """
        if server_name in self._disabled_servers:
            raise MCPServerDisabledError(server_name)

        if server_name not in self.connections:
            async with self._get_lock(server_name):
                if server_name not in self.connections:
                    self.connections[server_name] = await self._start_server(server_name)

        return self.connections[server_name]

    async def _start_server(self, server_name: str) -> MCPConnection:
        """启动 MCP 服务器进程

        使用 mcp SDK 的 stdio transport 启动服务器子进程。

        Args:
            server_name: 服务器名称

        Returns:
            MCPConnection 实例

        Raises:
            MCPServerStartupError: 启动失败
        """
        # 使用 mcp SDK 创建连接
        # connection = await MCPConnection.create(
        #     command=config.command,
        #     args=config.args,
        #     env=config.env
        # )
        pass

    async def health_check(self, server_name: str) -> bool:
        """检查服务器健康状态

        定期调用（60秒间隔），检测服务器是否响应。

        Args:
            server_name: 服务器名称

        Returns:
            True if healthy, False otherwise
        """
        pass

    async def restart_server(self, server_name: str):
        """重启失败的服务器

        最多重启 3 次，超过限制则禁用服务器。

        Args:
            server_name: 服务器名称
        """
        pass

    async def shutdown_all(self):
        """关闭所有服务器连接

        应用退出时调用，清理所有子进程。
        """
        pass

    def _get_lock(self, server_name: str) -> asyncio.Lock:
        """获取服务器专用锁（懒创建）"""
        if server_name not in self._locks:
            self._locks[server_name] = asyncio.Lock()
        return self._locks[server_name]

    def _load_config(self, config_path: str) -> MCPConfig:
        """加载 YAML 配置文件"""
        pass
```

**关键特性：**
- **Lazy Singleton**: 首次调用 `get_connection()` 时才启动进程
- **并发安全**: 使用 `asyncio.Lock` 防止重复启动
- **健康检查**: 后台任务定期 ping，检测崩溃
- **自动重启**: 崩溃时最多重启 3 次
- **优雅关闭**: 应用退出时清理子进程

---

### 3.2 MCPSecurityLayer (安全层)

**文件：** `src/mcp/security.py`

**职责：** 实现三层权限控制 + 目录白名单

**数据模型：**

```python
@dataclass(frozen=True)
class SecurityConfig:
    """安全配置（不可变）

    Attributes:
        allowed_directories: 允许访问的目录列表
        allowed_operations: 直接允许的操作列表
        denied_operations: 直接拒绝的操作列表
    """
    allowed_directories: List[str]
    allowed_operations: List[str]
    denied_operations: List[str]
```

**核心接口：**

```python
class MCPSecurityLayer:
    """MCP 安全层

    权限检查流程：
    1. 检查操作是否在 denied_operations → 拒绝
    2. 检查操作是否在 allowed_operations → 允许
    3. 否则 → 需要用户确认（fallback）

    Filesystem 特殊检查：
    - 路径必须在 allowed_directories 内
    - 使用 pathlib.resolve() 防止 .. 绕过
    """

    def __init__(self, config: SecurityConfig):
        """初始化安全层

        Args:
            config: 安全配置实例
        """
        self.config = config
        self.resolved_dirs = [Path(d).resolve() for d in config.allowed_directories]

    async def check_permission(
        self,
        server_name: str,
        tool_name: str,
        arguments: dict
    ) -> Tuple[Optional[bool], Optional[str]]:
        """检查权限

        三层权限逻辑：
        1. denied_operations → (False, reason)
        2. allowed_operations + 路径检查 → (True, None)
        3. 未定义 → (None, None) 需要确认

        Args:
            server_name: 服务器名称
            tool_name: 工具名称（如 "filesystem:read_file"）
            arguments: 工具参数

        Returns:
            (allowed, reason_or_none)
            - (True, None): 直接允许
            - (False, "reason"): 拒绝，附带原因
            - (None, None): 需要用户确认
        """
        operation = tool_name.split(":")[-1]

        # 1. 检查 denied list
        if operation in self.config.denied_operations:
            return False, f"Operation '{operation}' is denied by security policy"

        # 2. Filesystem 特殊检查：路径白名单
        if server_name == "filesystem":
            if not self._check_path_allowed(arguments):
                return False, "Path outside allowed directories"

        # 3. 检查 allowed list
        if operation in self.config.allowed_operations:
            return True, None

        # 4. 未定义 → 需要确认
        return None, None

    def _check_path_allowed(self, arguments: dict) -> bool:
        """检查路径是否在白名单内

        防护措施：
        - resolve() 解析符号链接和 .. 路径
        - is_relative_to() 检查是否在白名单目录内

        Args:
            arguments: 工具参数，可能包含 "path" 或 "directory"

        Returns:
            True if path is allowed, False otherwise
        """
        path_arg = arguments.get("path") or arguments.get("directory")
        if not path_arg:
            return True  # 没有路径参数，放行

        try:
            resolved = Path(path_arg).resolve()
            return any(
                resolved.is_relative_to(allowed_dir)
                for allowed_dir in self.resolved_dirs
            )
        except (ValueError, OSError):
            # 路径解析失败，拒绝
            return False
```

**关键特性：**
- **三层权限**: Allowed → Denied → Prompt (未定义)
- **路径白名单**: 使用 `pathlib.resolve()` 防止路径遍历攻击
- **不可变配置**: 使用 `frozen=True` 防止运行时修改
- **明确原因**: 拒绝时返回清晰的错误信息

---

### 3.3 MCPToolExecutor (工具执行器)

**文件：** `src/mcp/tool_executor.py`

**职责：** 工具发现、调用、结果处理

**核心接口：**

```python
class MCPToolExecutor:
    """MCP 工具执行器

    功能：
    - 工具发现：从服务器获取可用工具列表
    - 工具调用：通过 SDK 调用工具
    - 安全检查：调用前进行权限验证
    - 审计日志：记录所有操作
    """

    def __init__(
        self,
        manager: MCPConnectionManager,
        security: MCPSecurityLayer,
        db: Database
    ):
        """初始化工具执行器

        Args:
            manager: 连接管理器实例
            security: 安全层实例
            db: 数据库实例（用于审计日志）
        """
        self.manager = manager
        self.security = security
        self.db = db
        self._tool_cache: Dict[str, List[dict]] = {}

    async def discover_tools(self, server_name: str) -> List[dict]:
        """发现服务器提供的工具

        首次调用时从服务器获取，后续调用使用缓存。

        Args:
            server_name: 服务器名称

        Returns:
            工具列表，每个工具包含 name, description, parameters
        """
        if server_name not in self._tool_cache:
            connection = await self.manager.get_connection(server_name)
            tools = await connection.list_tools()
            self._tool_cache[server_name] = tools
        return self._tool_cache[server_name]

    async def call_tool(
        self,
        server_name: str,
        tool_name: str,
        arguments: dict,
        session_id: str
    ) -> dict:
        """调用 MCP 工具

        执行流程：
        1. 安全检查（三层权限 + 路径白名单）
        2. 如需确认，抛出 ConfirmationRequired
        3. 记录审计日志
        4. 调用工具
        5. 返回结果

        Args:
            server_name: 服务器名称
            tool_name: 工具名称
            arguments: 工具参数
            session_id: 会话 ID（用于审计）

        Returns:
            工具执行结果

        Raises:
            PermissionDeniedError: 操作被拒绝
            ConfirmationRequired: 需要用户确认
            MCPToolError: 工具执行失败
        """
        # 1. 安全检查
        allowed, reason = await self.security.check_permission(
            server_name, tool_name, arguments
        )

        if allowed is False:
            raise PermissionDeniedError(tool_name, reason)

        if allowed is None:
            # 需要用户确认
            raise ConfirmationRequired(
                tool_name=tool_name,
                arguments=arguments,
                prompt=f"Allow {tool_name} with args {arguments}?"
            )

        # 2. 记录审计日志
        await self._log_operation(session_id, server_name, tool_name, arguments)

        # 3. 调用工具
        try:
            connection = await self.manager.get_connection(server_name)
            result = await connection.call_tool(tool_name, arguments)
            return result
        except Exception as e:
            # 包装错误，添加上下文
            raise MCPToolError(f"Tool '{tool_name}' failed: {e}") from e

    async def _log_operation(self, session_id, server_name, tool_name, arguments):
        """记录操作到审计日志

        Args:
            session_id: 会话 ID
            server_name: 服务器名称
            tool_name: 工具名称
            arguments: 工具参数
        """
        await self.db.log_mcp_operation(
            session_id=session_id,
            server=server_name,
            tool=tool_name,
            arguments=arguments,
            timestamp=datetime.now()
        )
```

**关键特性：**
- **工具发现**: 缓存工具列表，避免重复查询
- **安全优先**: 调用前强制安全检查
- **审计日志**: 记录所有操作，包含时间戳和会话 ID
- **错误包装**: 添加上下文信息，便于调试

---

## 4. 配置格式

### 4.1 MCP 配置文件

**路径：** `config/mcp_config.yaml`

```yaml
# MCP 服务器配置
servers:
  # Filesystem 服务器（Phase 3 实现）
  filesystem:
    # 服务器启动命令
    command: npx
    args:
      - "-y"
      - "@modelcontextprotocol/server-filesystem"
      - "/tmp"  # MCP server 的 root 参数（我们的白名单会进一步限制）

    # 安全配置
    security:
      # 允许访问的目录白名单
      allowed_directories:
        - /Users/shichang/Documents
        - /Users/shichang/Downloads
        - /Users/shichang/Workspace

      # 直接允许的操作（无需确认）
      allowed_operations:
        - read_file
        - list_directory
        - get_file_info

      # 明确拒绝的操作
      denied_operations:
        - delete_file
        - delete_directory

      # 未列出的操作需要用户确认：
      # - write_file
      # - create_directory
      # - move_file

    # 可选：环境变量
    env: {}

    # 可选：超时配置（秒）
    timeout: 30.0

    # 可选：健康检查间隔（秒）
    health_check_interval: 60

  # 未来扩展示例（Phase 4+）
  # github:
  #   command: npx
  #   args: ["-y", "@modelcontextprotocol/server-github"]
  #   env:
  #     GITHUB_TOKEN: ${GITHUB_TOKEN}
  #   security:
  #     allowed_operations: [search_repositories, get_file_contents]
  #     denied_operations: [delete_repository]

# 全局配置
global:
  # 是否启用 MCP（环境变量可覆盖）
  enabled: true

  # 审计日志保留天数
  audit_log_retention_days: 90

  # 确认提示超时（秒）
  confirmation_timeout: 300
```

### 4.2 环境变量

```bash
# 禁用 MCP（用于测试）
MCP_ENABLED=false

# 添加额外的允许目录（逗号分隔）
MCP_FILESYSTEM_EXTRA_DIRS=/tmp/workspace,/opt/data

# GitHub token（未来）
GITHUB_TOKEN=ghp_xxxxx
```

**优先级：** 环境变量 > YAML 配置 > 默认值

---

## 5. Router 和 Executor 集成

### 5.1 SimpleRouter 扩展

**文件：** `src/core/router.py`

**新增功能：**

```python
class SimpleRouter:
    # 现有: SKILL_PATTERN = re.compile(r'^[@/](\S+)')

    # 新增: MCP 显式调用模式
    MCP_PATTERN = re.compile(r'^@mcp:(\w+):(\w+)')
    # 匹配: @mcp:filesystem:read_file

    # 新增: 参数解析模式
    ARG_PATTERN = re.compile(r'(\w+)=["\']([^"\']+)["\']')
    # 匹配: path='/path/to/file' name="value"

    def route(self, user_input: str, context: Context) -> ExecutionPlan:
        """路由用户输入

        优先级：
        1. MCP 显式调用
        2. Skill 调用
        3. 关键词检测（可能需要工具）
        4. 简单查询
        """

        # 1. 检查 MCP 显式调用
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

        # 2. 检查 Skill 调用（现有逻辑）
        skill_match = self.SKILL_PATTERN.match(user_input)
        if skill_match:
            # ... 现有 skill 逻辑
            pass

        # 3. 检查是否需要工具（关键词检测）
        if self._might_need_tools(user_input):
            return ExecutionPlan(
                type="simple_query",
                requires_llm=True,
                requires_tools=True  # LLM 决定是否使用
            )

        # 4. 默认简单查询
        return ExecutionPlan(type="simple_query", requires_llm=True)

    def _parse_arguments(self, user_input: str) -> dict:
        """解析参数

        支持格式：
        - key='value'
        - key="value"

        Args:
            user_input: 用户输入字符串

        Returns:
            参数字典
        """
        args = {}
        for match in self.ARG_PATTERN.finditer(user_input):
            key, value = match.groups()
            args[key] = value
        return args

    def _might_need_tools(self, user_input: str) -> bool:
        """简单关键词检测（Phase 3 可选，Phase 4 增强）

        检测用户输入是否可能需要使用工具。

        Args:
            user_input: 用户输入

        Returns:
            True if likely needs tools, False otherwise
        """
        keywords = ["读取", "read", "写入", "write", "列出", "list", "文件", "file"]
        return any(kw in user_input.lower() for kw in keywords)
```

### 5.2 AgentExecutor 扩展

**文件：** `src/core/executor.py`

**新增功能：**

```python
class AgentExecutor:
    def __init__(
        self,
        db: Database,
        router: SimpleRouter,
        llm_client: LLMClient,
        skill_registry: Optional[SkillRegistry] = None,
        skill_executor: Optional[SkillExecutor] = None,
        mcp_executor: Optional[MCPToolExecutor] = None  # 新增
    ):
        """初始化执行器

        Args:
            db: 数据库实例
            router: 路由器实例
            llm_client: LLM 客户端实例
            skill_registry: 技能注册表实例（可选）
            skill_executor: 技能执行器实例（可选）
            mcp_executor: MCP 工具执行器实例（可选）
        """
        self.db = db
        self.router = router
        self.llm_client = llm_client
        self.skill_registry = skill_registry
        self.skill_executor = skill_executor
        self.mcp_executor = mcp_executor  # 新增

    async def execute(self, user_input: str, session_id: str) -> Dict[str, Any]:
        """执行用户请求

        新增 MCP 类型处理。
        """
        # ... 现有验证逻辑

        # 路由
        plan = self.router.route(user_input, context)

        # 处理 MCP 类型
        if plan.type == "mcp":
            return await self._execute_mcp(plan, session_id)

        # ... 现有 skill、simple_query 处理

    async def _execute_mcp(
        self,
        plan: ExecutionPlan,
        session_id: str
    ) -> Dict[str, Any]:
        """执行 MCP 工具调用

        处理三种情况：
        1. 成功 → 返回结果
        2. 需要确认 → 返回确认提示
        3. 拒绝 → 返回错误信息

        Args:
            plan: 执行计划
            session_id: 会话 ID

        Returns:
            响应字典
        """
        if not self.mcp_executor:
            return {
                "response": "MCP is not configured",
                "type": "error"
            }

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
            # 需要用户确认
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
            # 权限拒绝
            return {
                "response": f"Permission denied: {e.reason}",
                "type": "error"
            }

        except MCPToolError as e:
            # 工具执行错误
            return {
                "response": f"Tool execution failed: {e}",
                "type": "error"
            }
```

### 5.3 main.py 初始化

**文件：** `src/main.py`

**新增功能：**

```python
# 新增导入
from .mcp.connection_manager import MCPConnectionManager
from .mcp.security import MCPSecurityLayer
from .mcp.tool_executor import MCPToolExecutor
from .mcp.config import load_mcp_config

@asynccontextmanager
async def lifespan(app: FastAPI):
    """应用生命周期管理"""
    # ... 现有数据库初始化

    # 初始化 MCP（如果启用）
    mcp_enabled = os.getenv("MCP_ENABLED", "true").lower() == "true"
    mcp_executor = None

    if mcp_enabled:
        try:
            # 加载配置
            config_path = "config/mcp_config.yaml"
            mcp_config = load_mcp_config(config_path)

            # 创建组件
            mcp_manager = MCPConnectionManager(config_path)
            mcp_security = MCPSecurityLayer(mcp_config.security)
            mcp_executor = MCPToolExecutor(mcp_manager, mcp_security, db)

            logger.info("MCP integration initialized")

        except Exception as e:
            logger.error(f"Failed to initialize MCP: {e}")
            logger.warning("Continuing without MCP support")

    # 创建 executor（传入 mcp_executor）
    executor = AgentExecutor(
        db=db,
        router=router,
        llm_client=llm_client,
        skill_registry=skill_registry,
        skill_executor=skill_executor,
        mcp_executor=mcp_executor  # 新增
    )

    # ... 现有逻辑

    yield

    # 关闭 MCP 连接
    if mcp_executor:
        await mcp_executor.manager.shutdown_all()
        logger.info("MCP connections closed")
```

---

## 6. 错误处理

### 6.1 异常层次

**文件：** `src/mcp/exceptions.py`

```python
class MCPError(Exception):
    """MCP 相关错误的基类"""
    pass

class MCPConnectionError(MCPError):
    """服务器连接错误"""
    pass

class MCPServerStartupError(MCPConnectionError):
    """服务器启动失败"""
    def __init__(self, server_name: str, reason: str):
        self.server_name = server_name
        self.reason = reason
        super().__init__(f"Failed to start MCP server '{server_name}': {reason}")

class MCPServerCrashError(MCPConnectionError):
    """服务器进程崩溃"""
    pass

class MCPServerDisabledError(MCPConnectionError):
    """服务器已被禁用"""
    pass

class MCPToolError(MCPError):
    """工具调用错误"""
    pass

class MCPToolNotFoundError(MCPToolError):
    """工具不存在"""
    pass

class MCPSecurityError(MCPError):
    """安全相关错误的基类"""
    pass

class PermissionDeniedError(MCPSecurityError):
    """操作被安全策略拒绝"""
    def __init__(self, operation: str, reason: str):
        self.operation = operation
        self.reason = reason
        super().__init__(f"Permission denied for '{operation}': {reason}")

class PathNotAllowedError(MCPSecurityError):
    """路径不在白名单内"""
    pass

class ConfirmationRequired(MCPError):
    """需要用户确认的操作"""
    def __init__(self, tool_name: str, arguments: dict, prompt: str):
        self.tool_name = tool_name
        self.arguments = arguments
        self.prompt = prompt
        super().__init__(prompt)
```

### 6.2 错误处理策略

**分层处理：**

1. **MCPConnectionManager 层**
   - 服务器启动失败 → 记录日志，返回友好错误消息
   - 服务器崩溃 → 自动重启（最多3次），失败后禁用该服务器
   - 通信超时 → 重试一次，失败则报错

2. **MCPSecurityLayer 层**
   - 路径违规 → 立即拒绝，返回明确原因
   - 权限拒绝 → 返回策略说明
   - 需要确认 → 抛出 ConfirmationRequired

3. **MCPToolExecutor 层**
   - 工具不存在 → 返回可用工具列表
   - 参数错误 → 返回参数说明
   - 执行失败 → 包装原始错误，添加上下文

4. **AgentExecutor 层**
   - 所有 MCP 错误 → 转换为用户友好的响应
   - ConfirmationRequired → 返回确认提示给前端
   - 记录所有错误到日志

### 6.3 降级策略

```python
class MCPConnectionManager:
    async def get_connection(self, server_name: str) -> MCPConnection:
        """获取连接，失败时优雅降级"""
        try:
            # 正常获取连接
            return await self._get_or_create_connection(server_name)

        except MCPServerStartupError as e:
            logger.error(f"MCP server startup failed: {e}")
            # 禁用该服务器
            self._mark_server_disabled(server_name)
            raise

        except MCPServerCrashError as e:
            logger.error(f"MCP server crashed: {e}")

            # 自动重启（最多3次）
            if self._restart_count[server_name] < 3:
                logger.info(f"Attempting to restart {server_name}...")
                self._restart_count[server_name] += 1
                return await self._restart_server(server_name)
            else:
                # 重启失败，禁用服务器
                logger.error(f"Max restart attempts reached for {server_name}")
                self._mark_server_disabled(server_name)
                raise
```

---

## 7. 测试策略

### 7.1 测试覆盖（目标：80%+）

**单元测试（50+ tests）：**

```python
# tests/mcp/test_connection_manager.py (15 tests)
- test_lazy_initialization()
- test_singleton_pattern()
- test_concurrent_access_thread_safety()
- test_server_startup_success()
- test_server_startup_failure()
- test_health_check()
- test_auto_restart_on_crash()
- test_max_restart_limit()
- test_graceful_shutdown()
- test_config_loading()
- test_invalid_config_handling()
- test_environment_variable_override()
- test_connection_caching()
- test_multiple_servers()
- test_server_disabled_after_failures()

# tests/mcp/test_security_layer.py (12 tests)
- test_allowed_operation_passes()
- test_denied_operation_fails()
- test_undefined_operation_requires_confirmation()
- test_path_whitelist_enforcement()
- test_path_traversal_prevention()
- test_symlink_resolution()
- test_relative_path_handling()
- test_empty_whitelist_denies_all()
- test_multiple_whitelisted_directories()
- test_config_validation()
- test_operation_name_parsing()
- test_different_server_policies()

# tests/mcp/test_tool_executor.py (13 tests)
- test_tool_discovery()
- test_tool_discovery_caching()
- test_tool_call_success()
- test_tool_call_with_security_check()
- test_tool_call_permission_denied()
- test_tool_call_requires_confirmation()
- test_tool_not_found_error()
- test_invalid_arguments_error()
- test_audit_logging()
- test_multiple_servers_tool_namespace()
- test_tool_call_timeout()
- test_concurrent_tool_calls()
- test_error_wrapping()

# tests/mcp/test_router_integration.py (8 tests)
- test_explicit_mcp_syntax_parsing()
- test_mcp_argument_extraction()
- test_skill_vs_mcp_precedence()
- test_invalid_mcp_syntax()
- test_keyword_detection()
- test_natural_language_fallback()
- test_server_name_validation()
- test_tool_name_validation()

# tests/mcp/test_executor_integration.py (10 tests)
- test_mcp_execution_plan_handling()
- test_confirmation_required_response()
- test_permission_denied_response()
- test_tool_success_response()
- test_tool_error_handling()
- test_session_context_preservation()
- test_audit_log_creation()
- test_mcp_disabled_fallback()
- test_server_unavailable_handling()
- test_end_to_end_file_read()
```

**集成测试（20+ tests）：**

```python
# tests/mcp/test_integration.py
- test_full_read_file_flow()
- test_full_write_file_flow_with_confirmation()
- test_security_rejection_flow()
- test_server_crash_recovery()
- test_multiple_concurrent_operations()
- test_session_isolation()
- test_audit_log_persistence()
- test_config_reload()
- test_health_check_recovery()
- test_graceful_shutdown_cleanup()
# ... 等
```

**Mock 策略：**

- **单元测试**：Mock MCP SDK、subprocess、文件系统
- **集成测试**：使用真实 MCP filesystem server（隔离临时目录）
- **E2E 测试**：真实环境，真实文件操作（Phase 4+）

### 7.2 测试工具

```python
# tests/mcp/conftest.py
@pytest.fixture
def mock_mcp_connection():
    """Mock MCP connection for unit tests"""
    conn = AsyncMock(spec=MCPConnection)
    conn.call_tool = AsyncMock(return_value={"content": "test"})
    conn.list_tools = AsyncMock(return_value=[
        {"name": "read_file", "description": "..."},
        {"name": "write_file", "description": "..."}
    ])
    return conn

@pytest.fixture
def temp_mcp_config(tmp_path):
    """临时 MCP 配置文件"""
    config = {
        "servers": {
            "filesystem": {
                "command": "npx",
                "args": ["-y", "@modelcontextprotocol/server-filesystem", str(tmp_path)],
                "security": {
                    "allowed_directories": [str(tmp_path)],
                    "allowed_operations": ["read_file"],
                    "denied_operations": ["delete_file"]
                }
            }
        }
    }
    config_file = tmp_path / "mcp_config.yaml"
    config_file.write_text(yaml.dump(config))
    return config_file

@pytest.fixture
async def mcp_test_environment(tmp_path, temp_mcp_config):
    """完整的 MCP 测试环境"""
    # 创建测试文件
    test_file = tmp_path / "test.txt"
    test_file.write_text("test content")

    # 初始化组件
    manager = MCPConnectionManager(str(temp_mcp_config))
    security = MCPSecurityLayer(...)
    executor = MCPToolExecutor(manager, security, mock_db)

    yield {
        "tmp_path": tmp_path,
        "manager": manager,
        "security": security,
        "executor": executor,
        "test_file": test_file
    }

    # 清理
    await manager.shutdown_all()
```

---

## 8. 实现计划

### Phase 3 分为 3 个子阶段：

#### **Phase 3.1: 基础设施（2天）**

**目标：** 搭建 MCP 基础架构

**任务：**
1. 安装 mcp Python SDK 依赖
2. 实现 MCPConnectionManager（基础版）
   - 配置加载（YAML）
   - 服务器启动逻辑
   - 连接缓存
3. 创建配置文件结构
4. 编写单元测试（15+ tests）
   - 配置加载测试
   - 连接管理测试
   - Mock SDK 测试

**交付物：**
- `src/mcp/connection_manager.py`
- `src/mcp/config.py`
- `config/mcp_config.yaml`
- `tests/mcp/test_connection_manager.py`
- `tests/mcp/conftest.py`

---

#### **Phase 3.2: 安全与执行（2天）**

**目标：** 实现安全层和工具执行

**任务：**
1. 实现 MCPSecurityLayer
   - 三层权限逻辑
   - 路径白名单检查
   - 防路径遍历
2. 实现 MCPToolExecutor
   - 工具发现和缓存
   - 工具调用流程
   - 审计日志
3. 异常类定义
4. 编写单元测试（25+ tests）
   - 安全层测试（12 tests）
   - 工具执行器测试（13 tests）

**交付物：**
- `src/mcp/security.py`
- `src/mcp/tool_executor.py`
- `src/mcp/exceptions.py`
- `tests/mcp/test_security_layer.py`
- `tests/mcp/test_tool_executor.py`

---

#### **Phase 3.3: 集成与打磨（1-2天）**

**目标：** 集成到现有系统，端到端测试

**任务：**
1. 扩展 SimpleRouter
   - MCP 显式调用解析
   - 参数提取
   - 关键词检测（可选）
2. 扩展 AgentExecutor
   - MCP 执行计划处理
   - 确认机制（API）
   - 错误响应格式
3. 更新 main.py 初始化逻辑
4. 数据库扩展（审计日志表）
5. 编写集成测试（18+ tests）
6. 端到端测试
7. 更新文档

**交付物：**
- `src/core/router.py`（更新）
- `src/core/executor.py`（更新）
- `src/main.py`（更新）
- `src/storage/database.py`（审计日志）
- `tests/mcp/test_router_integration.py`
- `tests/mcp/test_executor_integration.py`
- `tests/mcp/test_integration.py`
- `docs/mcp.md`（使用文档）
- `README.md`（更新）

---

### 总估时：5-6天

**依赖项：**
- mcp Python SDK（需安装）
- Node.js + npx（运行 MCP 服务器）
- @modelcontextprotocol/server-filesystem（npm 包）

**成功标准：**
- ✅ 所有单元测试通过（50+ tests）
- ✅ 集成测试通过（20+ tests）
- ✅ 测试覆盖率 ≥ 80%
- ✅ 可以成功读取白名单目录内的文件
- ✅ 路径白名单有效阻止未授权访问
- ✅ 三层权限机制正常工作
- ✅ 审计日志正确记录
- ✅ 服务器崩溃时能自动恢复

---

## 9. 数据库扩展

### 9.1 审计日志表

**文件：** `src/storage/database.py`

```sql
-- MCP 操作审计日志
CREATE TABLE mcp_audit_logs (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    server_name TEXT NOT NULL,
    tool_name TEXT NOT NULL,
    arguments JSON NOT NULL,
    result JSON,
    status TEXT NOT NULL,  -- 'success' | 'denied' | 'failed' | 'confirmed'
    error_message TEXT,
    timestamp TIMESTAMP NOT NULL,
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

-- 索引优化
CREATE INDEX idx_mcp_logs_session ON mcp_audit_logs(session_id);
CREATE INDEX idx_mcp_logs_timestamp ON mcp_audit_logs(timestamp);
CREATE INDEX idx_mcp_logs_server ON mcp_audit_logs(server_name);
```

**清理策略：**
- 默认保留 90 天
- 可配置：`global.audit_log_retention_days`
- 定时任务：每天凌晨清理过期日志

---

## 10. 文件结构

```
general-agent/
├── config/
│   └── mcp_config.yaml              # MCP 配置文件（新增）
├── src/
│   ├── mcp/                          # MCP 模块（新增）
│   │   ├── __init__.py
│   │   ├── connection_manager.py    # 连接管理器
│   │   ├── security.py              # 安全层
│   │   ├── tool_executor.py         # 工具执行器
│   │   ├── config.py                # 配置加载
│   │   └── exceptions.py            # 异常定义
│   ├── core/
│   │   ├── router.py                # 扩展：MCP 路由
│   │   └── executor.py              # 扩展：MCP 执行
│   ├── storage/
│   │   └── database.py              # 扩展：审计日志表
│   └── main.py                      # 扩展：MCP 初始化
├── tests/
│   └── mcp/                          # MCP 测试（新增）
│       ├── __init__.py
│       ├── conftest.py              # 测试工具
│       ├── test_connection_manager.py
│       ├── test_security_layer.py
│       ├── test_tool_executor.py
│       ├── test_router_integration.py
│       ├── test_executor_integration.py
│       └── test_integration.py
├── docs/
│   ├── mcp.md                       # MCP 使用文档（新增）
│   └── plans/
│       └── 2026-03-04-mcp-integration-design.md  # 本文档
└── pyproject.toml                   # 添加 mcp 依赖
```

---

## 11. 依赖更新

### pyproject.toml

```toml
[project]
dependencies = [
    "fastapi>=0.109.0",
    "uvicorn[standard]>=0.27.0",
    "jinja2>=3.1.3",
    "aiosqlite>=0.19.0",
    "pydantic>=2.6.0",
    "python-multipart>=0.0.9",
    "pyyaml>=6.0",
    "aiohttp>=3.9.0",
    "mcp>=0.9.0",  # 新增：MCP Python SDK
]
```

### 外部依赖

```bash
# Node.js + npx（运行 MCP 服务器）
# macOS:
brew install node

# Ubuntu:
sudo apt install nodejs npm

# MCP filesystem server（自动通过 npx 安装）
# npx -y @modelcontextprotocol/server-filesystem
```

---

## 12. 使用示例

### 12.1 显式调用

```bash
# 启动应用
uvicorn src.main:app --reload

# 读取文件
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "@mcp:filesystem:read_file path=\"/Users/shichang/Documents/test.txt\"",
    "session_id": "test"
  }'

# 列出目录
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "@mcp:filesystem:list_directory path=\"/Users/shichang/Documents\"",
    "session_id": "test"
  }'

# 写入文件（需要确认）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "@mcp:filesystem:write_file path=\"/Users/shichang/Documents/new.txt\" content=\"Hello World\"",
    "session_id": "test"
  }'
# 响应：{"type": "confirmation_required", "prompt": "Allow write_file?", ...}

# 尝试删除（被拒绝）
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "@mcp:filesystem:delete_file path=\"/Users/shichang/Documents/test.txt\"",
    "session_id": "test"
  }'
# 响应：{"type": "error", "response": "Permission denied: Operation 'delete_file' is denied"}
```

### 12.2 配置示例

```yaml
# config/mcp_config.yaml
servers:
  filesystem:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"]
    security:
      allowed_directories:
        - /Users/shichang/Documents
        - /Users/shichang/Downloads
      allowed_operations:
        - read_file
        - list_directory
      denied_operations:
        - delete_file
        - delete_directory
```

---

## 13. 风险与缓解

### 13.1 技术风险

**风险：** MCP SDK API 变化
**缓解：** 锁定版本号，定期跟进官方更新

**风险：** MCP 服务器进程崩溃
**缓解：** 健康检查 + 自动重启（最多3次）

**风险：** 路径遍历攻击
**缓解：** `pathlib.resolve()` + 白名单检查

**风险：** 确认机制被绕过
**缓解：** 强制安全检查，无法跳过

### 13.2 性能风险

**风险：** 服务器启动慢（首次调用 1-2s 延迟）
**缓解：** Lazy singleton，首次后无延迟

**风险：** 并发调用冲突
**缓解：** asyncio.Lock 保证线程安全

### 13.3 用户体验风险

**风险：** 确认提示太频繁，影响体验
**缓解：** 允许用户配置 `allowed_operations`，常用操作无需确认

**风险：** 错误信息不友好
**缓解：** 所有错误返回明确原因和建议

---

## 14. 未来扩展

### 14.1 Phase 4: 自然语言调用

**目标：** 用户说"读取 test.txt"，系统自动调用 MCP 工具

**要求：**
- Ollama 支持 tool calling（函数调用）
- LLM 决策工具使用
- 工具描述传递给 LLM

**实现：**
```python
# AgentExecutor.execute()
if plan.requires_tools:
    # 发现可用工具
    tools = await self.mcp_executor.discover_all_tools()

    # 构造 LLM 请求
    messages = context.get_messages()
    response = await self.llm_client.chat(messages, tools=tools)

    # 处理 tool_call
    if response.has_tool_call():
        result = await self.mcp_executor.call_tool(...)
        # 将结果返回给 LLM
```

### 14.2 Phase 4+: 更多 MCP 服务器

**GitHub:**
```yaml
servers:
  github:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-github"]
    env:
      GITHUB_TOKEN: ${GITHUB_TOKEN}
    security:
      allowed_operations: [search_repositories, get_file_contents]
      denied_operations: [delete_repository]
```

**SQLite:**
```yaml
servers:
  sqlite:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-sqlite", "data/agent.db"]
    security:
      allowed_operations: [read_query, list_tables]
      denied_operations: [write_query, drop_table]
```

### 14.3 Phase 5: 高级功能

- **批量操作**: 一次调用处理多个文件
- **流式响应**: 大文件分块返回
- **缓存优化**: 缓存文件内容，减少重复读取
- **权限继承**: 基于会话的临时权限提升

---

## 15. 总结

本设计文档描述了一个**通用 MCP Client 架构**，核心特性：

✅ **通用设计** - 支持任何标准 MCP 服务器
✅ **安全优先** - 三层权限 + 路径白名单 + 审计日志
✅ **渐进实现** - Phase 3 专注 filesystem，未来轻松扩展
✅ **Lazy Singleton** - 高效资源管理
✅ **健壮容错** - 健康检查、自动重启、降级策略
✅ **测试完善** - 80%+ 覆盖率，50+ 单元测试

**Phase 3 目标：**
- 5-6 天完成
- Filesystem MCP 服务器完全可用
- 为后续 GitHub、SQLite 等扩展打好基础

---

**文档版本：** 1.0
**最后更新：** 2026-03-04
**下一步：** 执行 Phase 3.1 实现计划
