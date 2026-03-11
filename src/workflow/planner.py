"""任务规划器 - 使用 LLM 将用户目标分解为可执行的任务计划"""
import json
import logging
import uuid
from typing import Dict, Any, List, Optional
from datetime import datetime

from .models import Workflow, Task, TaskStatus, WorkflowStatus
from .orchestrator import ToolOrchestrator

logger = logging.getLogger(__name__)


class PlanningError(Exception):
    """规划错误"""
    pass


class WorkflowPlanner:
    """工作流规划器 - 将用户目标转换为结构化的执行计划"""

    def __init__(
        self,
        llm_client,
        orchestrator: ToolOrchestrator,
        max_tasks: int = 20,
        enable_validation: bool = True
    ):
        """初始化规划器

        Args:
            llm_client: LLM 客户端
            orchestrator: 工具编排器（用于获取可用工具列表）
            max_tasks: 最大任务数限制
            enable_validation: 是否启用计划验证
        """
        self.llm_client = llm_client
        self.orchestrator = orchestrator
        self.max_tasks = max_tasks
        self.enable_validation = enable_validation

    async def plan(
        self,
        goal: str,
        session_id: str,
        context: Optional[Dict[str, Any]] = None
    ) -> Workflow:
        """生成执行计划

        Args:
            goal: 用户目标描述
            session_id: 会话ID
            context: 额外上下文信息（可选）

        Returns:
            Workflow 对象

        Raises:
            PlanningError: 规划失败
        """
        try:
            logger.info(f"Planning workflow for goal: {goal[:100]}...")

            # 1. 获取可用工具列表
            available_tools = await self._get_available_tools()

            # 2. 构建 LLM Prompt
            prompt = self._build_planning_prompt(goal, available_tools, context)

            # 3. 调用 LLM 生成计划
            raw_plan = await self._call_llm(prompt)

            # 4. 解析 LLM 响应
            plan_data = self._parse_plan_response(raw_plan)

            # 5. 验证计划
            if self.enable_validation:
                self._validate_plan(plan_data)

            # 6. 构建 Workflow 对象
            workflow = self._build_workflow(
                plan_data=plan_data,
                session_id=session_id,
                goal=goal
            )

            logger.info(f"Planning complete: {len(workflow.tasks)} tasks generated")
            return workflow

        except Exception as e:
            logger.error(f"Planning failed: {e}")
            raise PlanningError(f"Failed to plan workflow: {e}") from e

    async def _get_available_tools(self) -> List[Dict[str, Any]]:
        """获取可用工具列表

        Returns:
            工具列表（简化的描述）
        """
        all_tools = self.orchestrator.list_all_tools()

        # 简化工具描述（仅保留名称和描述）
        simplified = []
        for tool_type, tools in all_tools.items():
            for tool in tools:
                simplified.append({
                    "name": tool["name"],
                    "description": tool["description"],
                    "type": tool_type
                })

        return simplified

    def _build_planning_prompt(
        self,
        goal: str,
        available_tools: List[Dict[str, Any]],
        context: Optional[Dict[str, Any]] = None
    ) -> str:
        """构建规划 Prompt

        Args:
            goal: 用户目标
            available_tools: 可用工具列表
            context: 额外上下文

        Returns:
            Prompt 字符串
        """
        # 格式化工具列表
        tools_text = "\n".join([
            f"- {tool['name']}: {tool['description']}"
            for tool in available_tools
        ])

        # 构建 Prompt
        prompt = f"""你是一个任务规划专家。请将用户的目标分解为可执行的任务计划。

用户目标:
{goal}

可用工具:
{tools_text}

要求:
1. 分析用户目标，识别需要完成的子任务
2. 为每个子任务选择合适的工具
3. 确定任务之间的依赖关系（哪些任务必须先完成）
4. 标识需要用户审批的危险操作（如删除、修改文件）
5. 任务数量不超过 {self.max_tasks} 个

请以 JSON 格式返回计划，格式如下:
{{
  "tasks": [
    {{
      "id": "task-1",
      "name": "任务名称",
      "tool": "工具名称（从可用工具列表中选择）",
      "params": {{"参数名": "参数值"}},
      "dependencies": ["依赖的任务ID"],
      "requires_approval": false,
      "approval_reason": "审批原因（如果需要审批）"
    }}
  ]
}}

注意事项:
- task ID 使用 "task-1", "task-2" 等格式
- 参数值可以使用变量引用: "${{task-1.output}}" 表示引用 task-1 的输出
- 危险操作（删除、修改、发送消息等）必须设置 requires_approval=true
- 确保依赖关系正确，避免循环依赖

请直接返回 JSON，不要添加其他解释文字。
"""

        # 添加额外上下文
        if context:
            context_text = "\n".join([f"- {k}: {v}" for k, v in context.items()])
            prompt += f"\n额外上下文信息:\n{context_text}\n"

        return prompt

    async def _call_llm(self, prompt: str) -> str:
        """调用 LLM 生成计划

        Args:
            prompt: Prompt 文本

        Returns:
            LLM 响应文本
        """
        messages = [
            {
                "role": "system",
                "content": "你是一个专业的任务规划助手，擅长将复杂目标分解为可执行的任务序列。"
            },
            {
                "role": "user",
                "content": prompt
            }
        ]

        response = await self.llm_client.chat(messages)
        return response

    def _parse_plan_response(self, response: str) -> Dict[str, Any]:
        """解析 LLM 响应

        Args:
            response: LLM 响应文本

        Returns:
            解析后的计划数据

        Raises:
            PlanningError: 解析失败
        """
        try:
            # 尝试提取 JSON（LLM 可能返回带解释的文本）
            json_start = response.find("{")
            json_end = response.rfind("}") + 1

            if json_start == -1 or json_end == 0:
                raise PlanningError("Response does not contain valid JSON")

            json_text = response[json_start:json_end]
            plan_data = json.loads(json_text)

            # 验证基本结构
            if "tasks" not in plan_data:
                raise PlanningError("Plan must contain 'tasks' field")

            if not isinstance(plan_data["tasks"], list):
                raise PlanningError("'tasks' must be a list")

            return plan_data

        except json.JSONDecodeError as e:
            raise PlanningError(f"Failed to parse JSON response: {e}") from e

    def _validate_plan(self, plan_data: Dict[str, Any]) -> None:
        """验证计划的有效性

        Args:
            plan_data: 计划数据

        Raises:
            PlanningError: 验证失败
        """
        tasks = plan_data["tasks"]

        # 1. 验证任务数量
        if len(tasks) > self.max_tasks:
            raise PlanningError(
                f"Too many tasks: {len(tasks)} (max: {self.max_tasks})"
            )

        if len(tasks) == 0:
            raise PlanningError("Plan must contain at least one task")

        # 2. 验证每个任务的必需字段
        task_ids = set()
        for i, task in enumerate(tasks):
            # 检查必需字段
            required_fields = ["id", "name", "tool", "params"]
            for field in required_fields:
                if field not in task:
                    raise PlanningError(
                        f"Task {i} missing required field: {field}"
                    )

            # 检查 ID 唯一性
            task_id = task["id"]
            if task_id in task_ids:
                raise PlanningError(f"Duplicate task ID: {task_id}")
            task_ids.add(task_id)

            # 检查工具名称格式
            tool_name = task["tool"]
            if ":" not in tool_name:
                raise PlanningError(
                    f"Invalid tool name format in task {task_id}: {tool_name}"
                )

        # 3. 验证依赖关系
        for task in tasks:
            task_id = task["id"]
            dependencies = task.get("dependencies", [])

            for dep_id in dependencies:
                if dep_id not in task_ids:
                    raise PlanningError(
                        f"Task {task_id} depends on non-existent task {dep_id}"
                    )

        # 4. 检测循环依赖（简单检测）
        self._detect_circular_dependencies(tasks)

    def _detect_circular_dependencies(self, tasks: List[Dict[str, Any]]) -> None:
        """检测循环依赖

        Args:
            tasks: 任务列表

        Raises:
            PlanningError: 检测到循环依赖
        """
        # 构建邻接表
        graph = {task["id"]: task.get("dependencies", []) for task in tasks}

        # DFS 检测环
        visited = set()
        rec_stack = set()

        def has_cycle(node: str) -> bool:
            visited.add(node)
            rec_stack.add(node)

            for neighbor in graph.get(node, []):
                if neighbor not in visited:
                    if has_cycle(neighbor):
                        return True
                elif neighbor in rec_stack:
                    return True

            rec_stack.remove(node)
            return False

        for task_id in graph:
            if task_id not in visited:
                if has_cycle(task_id):
                    raise PlanningError(
                        f"Circular dependency detected involving task {task_id}"
                    )

    def _build_workflow(
        self,
        plan_data: Dict[str, Any],
        session_id: str,
        goal: str
    ) -> Workflow:
        """构建 Workflow 对象

        Args:
            plan_data: 计划数据
            session_id: 会话ID
            goal: 目标描述

        Returns:
            Workflow 对象
        """
        # 创建 Task 对象列表
        tasks = []
        for task_data in plan_data["tasks"]:
            task = Task(
                id=task_data["id"],
                name=task_data["name"],
                tool=task_data["tool"],
                params=task_data["params"],
                dependencies=task_data.get("dependencies", []),
                requires_approval=task_data.get("requires_approval", False),
                approval_reason=task_data.get("approval_reason"),
                status=TaskStatus.PENDING
            )
            tasks.append(task)

        # 创建 Workflow 对象
        workflow = Workflow(
            id=str(uuid.uuid4()),
            session_id=session_id,
            goal=goal,
            tasks=tasks,
            status=WorkflowStatus.PENDING,
            created_at=datetime.now()
        )

        return workflow

    async def refine_plan(
        self,
        workflow: Workflow,
        feedback: str
    ) -> Workflow:
        """根据反馈优化计划

        Args:
            workflow: 原始工作流
            feedback: 用户反馈

        Returns:
            优化后的工作流

        Raises:
            PlanningError: 优化失败
        """
        try:
            # 获取可用工具
            available_tools = await self._get_available_tools()

            # 构建优化 Prompt
            prompt = self._build_refinement_prompt(
                workflow=workflow,
                feedback=feedback,
                available_tools=available_tools
            )

            # 调用 LLM
            raw_plan = await self._call_llm(prompt)

            # 解析和验证
            plan_data = self._parse_plan_response(raw_plan)
            if self.enable_validation:
                self._validate_plan(plan_data)

            # 构建新的 Workflow（保留原始 ID 和会话）
            refined_workflow = self._build_workflow(
                plan_data=plan_data,
                session_id=workflow.session_id,
                goal=workflow.goal
            )

            # 保留原始 workflow ID
            refined_workflow.id = workflow.id

            logger.info(f"Plan refined: {len(refined_workflow.tasks)} tasks")
            return refined_workflow

        except Exception as e:
            logger.error(f"Plan refinement failed: {e}")
            raise PlanningError(f"Failed to refine plan: {e}") from e

    def _build_refinement_prompt(
        self,
        workflow: Workflow,
        feedback: str,
        available_tools: List[Dict[str, Any]]
    ) -> str:
        """构建优化 Prompt

        Args:
            workflow: 原始工作流
            feedback: 用户反馈
            available_tools: 可用工具列表

        Returns:
            Prompt 字符串
        """
        # 格式化原始计划
        original_plan = workflow.get_plan_dict()
        original_plan_json = json.dumps(original_plan, indent=2, ensure_ascii=False)

        # 格式化工具列表
        tools_text = "\n".join([
            f"- {tool['name']}: {tool['description']}"
            for tool in available_tools
        ])

        prompt = f"""请根据用户反馈优化以下任务计划。

原始目标:
{workflow.goal}

原始计划:
{original_plan_json}

用户反馈:
{feedback}

可用工具:
{tools_text}

请根据反馈调整计划，保持 JSON 格式不变。
- 可以添加、删除或修改任务
- 确保依赖关系正确
- 任务数量不超过 {self.max_tasks} 个

请直接返回优化后的 JSON，格式与原始计划相同。
"""

        return prompt

    def estimate_execution_time(self, workflow: Workflow) -> Dict[str, Any]:
        """估算工作流执行时间（简单估算）

        Args:
            workflow: 工作流

        Returns:
            估算信息字典
        """
        # 简单的启发式估算
        task_times = {
            "mcp": 2.0,   # MCP 工具平均 2 秒
            "skill": 5.0,  # Skills 平均 5 秒
            "rag": 3.0,    # RAG 平均 3 秒
            "llm": 10.0    # LLM 平均 10 秒
        }

        total_time = 0.0
        for task in workflow.tasks:
            tool_type = task.tool.split(":")[0]
            total_time += task_times.get(tool_type, 5.0)

        # 考虑依赖关系（简化：假设完全串行）
        return {
            "estimated_seconds": total_time,
            "estimated_minutes": total_time / 60,
            "task_count": len(workflow.tasks),
            "note": "This is a rough estimate. Actual time may vary."
        }
