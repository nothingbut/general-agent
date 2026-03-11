"""Agent执行器

该模块提供AgentExecutor类，用于协调所有组件（数据库、路由、LLM）
来处理用户请求。这是系统的核心编排层。
"""
from typing import Dict, Any, Optional
from .router import SimpleRouter
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
        3. 保存用户消息
        4. 路由决策
        5. 执行计划（调用LLM）
        6. 保存助手响应
        7. 返回结果

        Args:
            user_input: 用户输入字符串
            session_id: 会话ID

        Returns:
            执行结果字典，包含以下字段：
            - response: 助手的响应内容
            - session_id: 会话ID
            - plan_type: 执行计划类型（如 "simple_query"）

        Raises:
            ValueError: 如果user_input为空或session_id为空
        """
        # 1. 验证输入
        if not user_input or not user_input.strip():
            raise ValueError("User input cannot be empty")

        if not session_id or not session_id.strip():
            raise ValueError("Session ID cannot be empty")

        # 2. 获取上下文（ContextManager会验证session_id）
        ctx = ContextManager(self.db, session_id)

        # 3. 保存用户消息
        await ctx.add_message("user", user_input)

        # 4. 路由决策
        plan = self.router.route(user_input, context=ctx)

        # 5. 执行计划
        if plan.type == "mcp":
            # MCP tool execution (highest priority for explicit calls)
            response, success, error = await self._execute_mcp(plan, session_id)
        elif plan.type == "skill":
            response, success, error = await self._execute_skill(plan)
        elif plan.type == "simple_query":
            response = await self._execute_simple_query(ctx)
            success = True
            error = None
        else:
            # 未来：处理其他类型（task）
            response = "暂不支持该类型的请求"
            success = False
            error = "Unsupported plan type"

        # 6. 保存助手回复
        await ctx.add_message("assistant", response)

        # 7. 返回结果
        result = {
            "response": response,
            "session_id": session_id,
            "plan_type": plan.type,
            "success": success
        }

        if error:
            result["error"] = error

        return result

    async def _execute_simple_query(self, ctx: ContextManager) -> str:
        """执行简单问答

        从上下文获取历史消息，调用LLM生成响应。

        Args:
            ctx: 上下文管理器实例

        Returns:
            LLM生成的响应字符串
        """
        # 获取历史消息（格式化为LLM输入格式）
        messages = await ctx.format_for_llm(limit=10)

        # 调用LLM
        response = await self.llm_client.chat(messages)

        return response

    async def _execute_skill(self, plan: Any) -> tuple[str, bool, Optional[str]]:
        """执行技能

        Args:
            plan: ExecutionPlan with skill metadata

        Returns:
            Tuple of (response, success, error)
        """
        # Check if skill system is configured
        if not self.skill_registry or not self.skill_executor:
            return (
                "技能系统未配置。请在启动时初始化技能注册表和执行器。",
                False,
                "Skill system not configured"
            )

        # Extract metadata
        metadata = plan.metadata or {}
        skill_name = metadata.get("skill_name")
        parameters = metadata.get("parameters", {})

        if not skill_name:
            return (
                "技能名称缺失。请使用 @skill-name 或 /skill-name 格式。",
                False,
                "Missing skill name"
            )

        # Get skill from registry
        try:
            skill = self.skill_registry.get(skill_name)
        except Exception as e:
            error_msg = f"技能 '{skill_name}' 未找到。错误: {str(e)}"
            return (error_msg, False, f"Skill not found: {skill_name}")

        # Execute skill
        try:
            result = await self.skill_executor.execute(skill, parameters)

            if result.success:
                return (result.output, True, None)
            else:
                return (
                    f"技能执行失败: {result.error}",
                    False,
                    result.error
                )

        except Exception as e:
            error_msg = f"技能执行出错: {str(e)}"
            return (error_msg, False, str(e))

    async def _execute_mcp(
        self,
        plan: Any,
        session_id: str
    ) -> tuple[str, bool, Optional[str]]:
        """执行MCP工具调用

        处理三种情况：
        1. 成功 → 返回工具结果
        2. 需要确认 → 返回确认提示
        3. 拒绝 → 返回错误信息

        Args:
            plan: ExecutionPlan (包含server、tool、arguments)
            session_id: 会话ID

        Returns:
            Tuple of (response, success, error)
        """
        if not self.mcp_executor:
            return (
                "MCP工具系统未配置。请在启动时初始化MCP执行器。",
                False,
                "MCP executor not configured"
            )

        # Import exceptions here to avoid circular imports
        from ..mcp.exceptions import (
            PermissionDeniedError,
            ConfirmationRequired,
            MCPToolError
        )

        # Extract metadata
        metadata = plan.metadata or {}
        server_name = metadata.get("server")
        tool_name = metadata.get("tool")
        arguments = metadata.get("arguments", {})

        try:
            result = await self.mcp_executor.call_tool(
                server_name=server_name,
                tool_name=tool_name,
                arguments=arguments,
                session_id=session_id
            )

            response = f"Tool executed successfully:\n{result}"
            return (response, True, None)

        except ConfirmationRequired as e:
            # Requires user confirmation
            return (e.prompt, False, "confirmation_required")

        except PermissionDeniedError as e:
            # Permission denied
            error_msg = f"Permission denied: {e.reason}"
            return (error_msg, False, str(e))

        except MCPToolError as e:
            # Tool execution error
            error_msg = f"Tool execution failed: {str(e)}"
            return (error_msg, False, str(e))

        except Exception as e:
            # Unexpected error
            error_msg = f"MCP execution error: {str(e)}"
            return (error_msg, False, str(e))
