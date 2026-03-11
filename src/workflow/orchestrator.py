"""工具编排器 - 统一调用 MCP、Skills、RAG 和 LLM"""
import logging
import time
from typing import Dict, Any, List, Optional
from enum import Enum

from .models import ToolResult, ExecutionContext

logger = logging.getLogger(__name__)


class ToolType(Enum):
    """工具类型枚举"""
    MCP = "mcp"      # MCP 工具调用
    SKILL = "skill"  # Skills 技能执行
    RAG = "rag"      # RAG 知识库操作
    LLM = "llm"      # LLM 直接调用

    @classmethod
    def from_tool_name(cls, tool_name: str) -> "ToolType":
        """从工具名称解析工具类型

        Args:
            tool_name: 工具名称（格式：type:name 或 type:server:tool）

        Returns:
            ToolType 枚举值

        Raises:
            ValueError: 如果工具名称格式无效
        """
        if ":" not in tool_name:
            raise ValueError(f"Invalid tool name format: {tool_name}")

        tool_type = tool_name.split(":")[0]
        try:
            return cls(tool_type)
        except ValueError:
            raise ValueError(f"Unknown tool type: {tool_type}")


class ToolRegistry:
    """工具注册表 - 存储可用工具的元信息"""

    def __init__(self):
        """初始化工具注册表"""
        self._tools: Dict[str, Dict[str, Any]] = {}

    def register_tool(
        self,
        tool_name: str,
        tool_type: ToolType,
        description: str,
        params_schema: Optional[Dict[str, Any]] = None,
        metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """注册工具

        Args:
            tool_name: 工具完整名称（如 mcp:filesystem:read_file）
            tool_type: 工具类型
            description: 工具描述
            params_schema: 参数模式（可选）
            metadata: 额外元数据（可选）
        """
        self._tools[tool_name] = {
            "name": tool_name,
            "type": tool_type.value,
            "description": description,
            "params_schema": params_schema or {},
            "metadata": metadata or {}
        }
        logger.debug(f"Registered tool: {tool_name}")

    def get_tool(self, tool_name: str) -> Optional[Dict[str, Any]]:
        """获取工具信息

        Args:
            tool_name: 工具名称

        Returns:
            工具信息字典，如果不存在返回 None
        """
        return self._tools.get(tool_name)

    def list_tools(self, tool_type: Optional[ToolType] = None) -> List[Dict[str, Any]]:
        """列出所有工具

        Args:
            tool_type: 工具类型过滤（可选）

        Returns:
            工具信息列表
        """
        if tool_type is None:
            return list(self._tools.values())

        return [
            tool for tool in self._tools.values()
            if tool["type"] == tool_type.value
        ]

    def has_tool(self, tool_name: str) -> bool:
        """检查工具是否存在

        Args:
            tool_name: 工具名称

        Returns:
            是否存在
        """
        return tool_name in self._tools


class ToolOrchestrator:
    """工具编排器 - 统一调用各种工具"""

    def __init__(
        self,
        mcp_executor=None,
        skill_executor=None,
        skill_registry=None,
        rag_engine=None,
        llm_client=None
    ):
        """初始化工具编排器

        Args:
            mcp_executor: MCP 工具执行器（可选）
            skill_executor: Skills 执行器（可选）
            skill_registry: Skills 注册表（可选）
            rag_engine: RAG 引擎（可选）
            llm_client: LLM 客户端（可选）
        """
        self.mcp_executor = mcp_executor
        self.skill_executor = skill_executor
        self.skill_registry = skill_registry
        self.rag_engine = rag_engine
        self.llm_client = llm_client

        # 工具注册表
        self.registry = ToolRegistry()

        # 初始化工具注册
        self._initialize_registry()

    def _initialize_registry(self) -> None:
        """初始化工具注册表"""
        # 这里可以在启动时自动发现和注册工具
        # 当前为占位符，实际注册在 discover_tools 中完成
        pass

    async def discover_tools(self) -> Dict[str, List[Dict[str, Any]]]:
        """发现所有可用工具

        Returns:
            按类型分组的工具列表
        """
        discovered = {
            "mcp": [],
            "skill": [],
            "rag": [],
            "llm": []
        }

        # 1. 发现 MCP 工具
        if self.mcp_executor:
            try:
                # 假设有一个配置的 MCP 服务器列表
                # 这里需要从配置中获取服务器名称
                # 暂时使用占位符
                logger.info("MCP tools discovery skipped (需要服务器配置)")
            except Exception as e:
                logger.error(f"Failed to discover MCP tools: {e}")

        # 2. 发现 Skills
        if self.skill_registry:
            try:
                skills = self.skill_registry.list_all()
                for skill in skills:
                    tool_name = f"skill:{skill.full_name}"
                    self.registry.register_tool(
                        tool_name=tool_name,
                        tool_type=ToolType.SKILL,
                        description=skill.description or "No description",
                        params_schema={"parameters": [p.to_dict() for p in skill.parameters]},
                        metadata={"skill": skill.to_dict()}
                    )
                    discovered["skill"].append({
                        "name": tool_name,
                        "description": skill.description or "No description"
                    })
                logger.info(f"Discovered {len(skills)} skills")
            except Exception as e:
                logger.error(f"Failed to discover skills: {e}")

        # 3. 注册 RAG 方法
        if self.rag_engine:
            rag_methods = [
                ("rag:search", "搜索知识库"),
                ("rag:index", "索引文档"),
                ("rag:query", "完整查询")
            ]
            for tool_name, description in rag_methods:
                self.registry.register_tool(
                    tool_name=tool_name,
                    tool_type=ToolType.RAG,
                    description=description
                )
                discovered["rag"].append({
                    "name": tool_name,
                    "description": description
                })

        # 4. 注册 LLM 方法
        if self.llm_client:
            llm_methods = [
                ("llm:chat", "LLM 对话生成"),
                ("llm:analyze", "LLM 分析任务")
            ]
            for tool_name, description in llm_methods:
                self.registry.register_tool(
                    tool_name=tool_name,
                    tool_type=ToolType.LLM,
                    description=description
                )
                discovered["llm"].append({
                    "name": tool_name,
                    "description": description
                })

        return discovered

    async def execute_tool(
        self,
        tool_name: str,
        params: Dict[str, Any],
        context: ExecutionContext,
        session_id: Optional[str] = None
    ) -> ToolResult:
        """统一工具调用接口

        Args:
            tool_name: 工具名称（格式：type:name 或 type:server:tool）
            params: 工具参数
            context: 执行上下文
            session_id: 会话ID（用于 MCP 审计）

        Returns:
            ToolResult 工具执行结果

        Raises:
            ValueError: 工具名称格式无效或工具类型未知
            RuntimeError: 工具执行器未初始化
        """
        start_time = time.time()

        try:
            # 解析工具类型
            tool_type = ToolType.from_tool_name(tool_name)

            # 根据类型调用相应的执行器
            if tool_type == ToolType.MCP:
                result = await self._execute_mcp_tool(tool_name, params, session_id or context.session_id)
            elif tool_type == ToolType.SKILL:
                result = await self._execute_skill(tool_name, params)
            elif tool_type == ToolType.RAG:
                result = await self._execute_rag_method(tool_name, params)
            elif tool_type == ToolType.LLM:
                result = await self._execute_llm_method(tool_name, params, context)
            else:
                raise ValueError(f"Unknown tool type: {tool_type}")

            execution_time = time.time() - start_time
            result.execution_time = execution_time

            logger.info(f"Tool {tool_name} executed in {execution_time:.2f}s")
            return result

        except Exception as e:
            execution_time = time.time() - start_time
            logger.error(f"Tool {tool_name} failed: {e}")
            return ToolResult(
                success=False,
                error=str(e),
                execution_time=execution_time
            )

    async def _execute_mcp_tool(
        self,
        tool_name: str,
        params: Dict[str, Any],
        session_id: str
    ) -> ToolResult:
        """执行 MCP 工具

        Args:
            tool_name: 工具名称（格式：mcp:server:tool）
            params: 工具参数
            session_id: 会话ID

        Returns:
            ToolResult
        """
        if not self.mcp_executor:
            raise RuntimeError("MCP executor not initialized")

        # 解析工具名称：mcp:server:tool
        parts = tool_name.split(":")
        if len(parts) != 3:
            raise ValueError(f"Invalid MCP tool name format: {tool_name}. Expected mcp:server:tool")

        server_name = parts[1]
        actual_tool_name = parts[2]

        # 调用 MCP 工具
        result = await self.mcp_executor.call_tool(
            server_name=server_name,
            tool_name=actual_tool_name,
            arguments=params,
            session_id=session_id
        )

        return ToolResult(
            success=True,
            data=result,
            metadata={"tool_type": "mcp", "server": server_name}
        )

    async def _execute_skill(
        self,
        tool_name: str,
        params: Dict[str, Any]
    ) -> ToolResult:
        """执行 Skill

        Args:
            tool_name: 工具名称（格式：skill:name 或 skill:namespace:name）
            params: 技能参数

        Returns:
            ToolResult
        """
        if not self.skill_executor or not self.skill_registry:
            raise RuntimeError("Skill executor or registry not initialized")

        # 解析技能名称：skill:name 或 skill:namespace:name
        skill_name = ":".join(tool_name.split(":")[1:])

        # 从注册表获取技能定义
        skill = self.skill_registry.get(skill_name)

        # 执行技能
        execution_result = await self.skill_executor.execute(skill, params)

        return ToolResult(
            success=execution_result.success,
            data=execution_result.output if execution_result.success else None,
            error=execution_result.error,
            metadata=execution_result.metadata
        )

    async def _execute_rag_method(
        self,
        tool_name: str,
        params: Dict[str, Any]
    ) -> ToolResult:
        """执行 RAG 方法

        Args:
            tool_name: 工具名称（格式：rag:method）
            params: 方法参数

        Returns:
            ToolResult
        """
        if not self.rag_engine:
            raise RuntimeError("RAG engine not initialized")

        # 解析方法名称
        method = tool_name.split(":")[1]

        # 根据方法调用相应的 RAG 功能
        if method == "search" or method == "retrieve":
            result = await self.rag_engine.retrieve(
                query=params.get("query", ""),
                top_k=params.get("top_k"),
                filters=params.get("filters")
            )
            return ToolResult(
                success=True,
                data=[r.to_dict() for r in result] if hasattr(result[0] if result else None, 'to_dict') else result,
                metadata={"method": method}
            )

        elif method == "query":
            result = await self.rag_engine.query(
                query=params.get("query", ""),
                top_k=params.get("top_k"),
                filters=params.get("filters"),
                max_context_tokens=params.get("max_context_tokens"),
                include_metadata=params.get("include_metadata", False)
            )
            return ToolResult(
                success=True,
                data=result,
                metadata={"method": method}
            )

        elif method == "index":
            result = await self.rag_engine.index_documents(
                path=params.get("path", ""),
                recursive=params.get("recursive"),
                show_progress=params.get("show_progress", False)
            )
            return ToolResult(
                success=True,
                data=result,
                metadata={"method": method}
            )

        else:
            raise ValueError(f"Unknown RAG method: {method}")

    async def _execute_llm_method(
        self,
        tool_name: str,
        params: Dict[str, Any],
        context: ExecutionContext
    ) -> ToolResult:
        """执行 LLM 方法

        Args:
            tool_name: 工具名称（格式：llm:method）
            params: 方法参数
            context: 执行上下文

        Returns:
            ToolResult
        """
        if not self.llm_client:
            raise RuntimeError("LLM client not initialized")

        # 解析方法名称
        method = tool_name.split(":")[1]

        # 根据方法调用 LLM
        if method == "chat":
            # 直接聊天
            messages = params.get("messages", [])
            if not messages:
                raise ValueError("Missing required parameter: messages")

            response = await self.llm_client.chat(messages)
            return ToolResult(
                success=True,
                data=response,
                metadata={"method": method}
            )

        elif method == "analyze":
            # 分析任务（使用上下文信息）
            query = params.get("query", "")
            use_context = params.get("use_context", True)

            # 构建消息
            messages = []

            # 如果需要，添加上下文信息
            if use_context and context.metadata.get("rag_context"):
                context_text = context.metadata["rag_context"]
                messages.append({
                    "role": "system",
                    "content": f"参考以下上下文信息：\n\n{context_text}"
                })

            messages.append({
                "role": "user",
                "content": query
            })

            response = await self.llm_client.chat(messages)
            return ToolResult(
                success=True,
                data=response,
                metadata={"method": method}
            )

        else:
            raise ValueError(f"Unknown LLM method: {method}")

    def get_tool_info(self, tool_name: str) -> Optional[Dict[str, Any]]:
        """获取工具信息

        Args:
            tool_name: 工具名称

        Returns:
            工具信息字典，如果不存在返回 None
        """
        return self.registry.get_tool(tool_name)

    def list_all_tools(self) -> Dict[str, List[Dict[str, Any]]]:
        """列出所有可用工具（按类型分组）

        Returns:
            按类型分组的工具列表
        """
        return {
            "mcp": self.registry.list_tools(ToolType.MCP),
            "skill": self.registry.list_tools(ToolType.SKILL),
            "rag": self.registry.list_tools(ToolType.RAG),
            "llm": self.registry.list_tools(ToolType.LLM)
        }
