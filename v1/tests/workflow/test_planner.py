"""测试任务规划器"""
import pytest
import json
from unittest.mock import AsyncMock, MagicMock
from src.workflow.planner import WorkflowPlanner, PlanningError
from src.workflow.orchestrator import ToolOrchestrator
from src.workflow.models import Workflow, WorkflowStatus, TaskStatus


class TestWorkflowPlanner:
    """测试工作流规划器"""

    def setup_method(self):
        """测试前准备"""
        # Mock LLM client
        self.llm_mock = AsyncMock()

        # Mock orchestrator
        self.orchestrator_mock = MagicMock(spec=ToolOrchestrator)
        self.orchestrator_mock.list_all_tools.return_value = {
            "mcp": [
                {"name": "mcp:filesystem:read", "description": "读取文件", "type": "mcp"}
            ],
            "skill": [
                {"name": "skill:summarize", "description": "总结文本", "type": "skill"}
            ],
            "rag": [
                {"name": "rag:search", "description": "搜索知识库", "type": "rag"}
            ],
            "llm": [
                {"name": "llm:chat", "description": "LLM 对话", "type": "llm"}
            ]
        }

        # Create planner
        self.planner = WorkflowPlanner(
            llm_client=self.llm_mock,
            orchestrator=self.orchestrator_mock
        )

    @pytest.mark.asyncio
    async def test_plan_basic(self):
        """测试基本规划功能"""
        # Mock LLM response
        mock_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Read file",
                    "tool": "mcp:filesystem:read",
                    "params": {"path": "/test.txt"},
                    "dependencies": []
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        # Execute planning
        workflow = await self.planner.plan(
            goal="Read a test file",
            session_id="session-1"
        )

        # Assertions
        assert workflow is not None
        assert isinstance(workflow, Workflow)
        assert workflow.session_id == "session-1"
        assert workflow.goal == "Read a test file"
        assert len(workflow.tasks) == 1
        assert workflow.tasks[0].id == "task-1"
        assert workflow.tasks[0].name == "Read file"
        assert workflow.status == WorkflowStatus.PENDING

    @pytest.mark.asyncio
    async def test_plan_with_dependencies(self):
        """测试带依赖关系的规划"""
        mock_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Read file",
                    "tool": "mcp:filesystem:read",
                    "params": {"path": "/test.txt"},
                    "dependencies": []
                },
                {
                    "id": "task-2",
                    "name": "Summarize",
                    "tool": "skill:summarize",
                    "params": {"text": "${task-1.output}"},
                    "dependencies": ["task-1"]
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        workflow = await self.planner.plan(
            goal="Read and summarize file",
            session_id="session-1"
        )

        assert len(workflow.tasks) == 2
        assert workflow.tasks[1].dependencies == ["task-1"]

    @pytest.mark.asyncio
    async def test_plan_with_approval(self):
        """测试需要审批的任务"""
        mock_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Delete file",
                    "tool": "mcp:filesystem:delete",
                    "params": {"path": "/test.txt"},
                    "dependencies": [],
                    "requires_approval": True,
                    "approval_reason": "Dangerous operation"
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        workflow = await self.planner.plan(
            goal="Delete test file",
            session_id="session-1"
        )

        assert workflow.tasks[0].requires_approval is True
        assert workflow.tasks[0].approval_reason == "Dangerous operation"

    @pytest.mark.asyncio
    async def test_plan_with_context(self):
        """测试带额外上下文的规划"""
        mock_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Test task",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": []
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        context = {"user_preference": "detailed output"}

        workflow = await self.planner.plan(
            goal="Test goal",
            session_id="session-1",
            context=context
        )

        # Verify LLM was called with context in prompt
        call_args = self.llm_mock.chat.call_args[0][0]
        prompt = call_args[1]["content"]
        assert "user_preference" in prompt

    @pytest.mark.asyncio
    async def test_parse_plan_response_valid_json(self):
        """测试解析有效的 JSON 响应"""
        response = json.dumps({
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Test",
                    "tool": "llm:chat",
                    "params": {}
                }
            ]
        })

        plan_data = self.planner._parse_plan_response(response)
        assert "tasks" in plan_data
        assert len(plan_data["tasks"]) == 1

    @pytest.mark.asyncio
    async def test_parse_plan_response_with_explanation(self):
        """测试解析带解释文字的响应"""
        response = """Here is the plan:
{
  "tasks": [
    {
      "id": "task-1",
      "name": "Test",
      "tool": "llm:chat",
      "params": {}
    }
  ]
}
Hope this helps!"""

        plan_data = self.planner._parse_plan_response(response)
        assert "tasks" in plan_data
        assert len(plan_data["tasks"]) == 1

    def test_parse_plan_response_invalid_json(self):
        """测试解析无效 JSON"""
        response = "This is not a JSON response"

        with pytest.raises(PlanningError, match="does not contain valid JSON"):
            self.planner._parse_plan_response(response)

    def test_parse_plan_response_missing_tasks(self):
        """测试缺少 tasks 字段"""
        response = json.dumps({"other": "data"})

        with pytest.raises(PlanningError, match="must contain 'tasks' field"):
            self.planner._parse_plan_response(response)

    def test_validate_plan_too_many_tasks(self):
        """测试任务数量超限"""
        self.planner.max_tasks = 3

        plan_data = {
            "tasks": [
                {"id": f"task-{i}", "name": f"Task {i}", "tool": "llm:chat", "params": {}}
                for i in range(5)
            ]
        }

        with pytest.raises(PlanningError, match="Too many tasks"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_empty_tasks(self):
        """测试空任务列表"""
        plan_data = {"tasks": []}

        with pytest.raises(PlanningError, match="at least one task"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_missing_required_field(self):
        """测试缺少必需字段"""
        plan_data = {
            "tasks": [
                {"id": "task-1", "name": "Test"}  # 缺少 tool 和 params
            ]
        }

        with pytest.raises(PlanningError, match="missing required field"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_duplicate_task_id(self):
        """测试重复的任务 ID"""
        plan_data = {
            "tasks": [
                {"id": "task-1", "name": "Task 1", "tool": "llm:chat", "params": {}},
                {"id": "task-1", "name": "Task 2", "tool": "llm:chat", "params": {}}
            ]
        }

        with pytest.raises(PlanningError, match="Duplicate task ID"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_invalid_tool_format(self):
        """测试无效的工具名称格式"""
        plan_data = {
            "tasks": [
                {"id": "task-1", "name": "Test", "tool": "invalid", "params": {}}
            ]
        }

        with pytest.raises(PlanningError, match="Invalid tool name format"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_nonexistent_dependency(self):
        """测试不存在的依赖"""
        plan_data = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Test",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": ["nonexistent"]
                }
            ]
        }

        with pytest.raises(PlanningError, match="depends on non-existent task"):
            self.planner._validate_plan(plan_data)

    def test_validate_plan_circular_dependency(self):
        """测试循环依赖"""
        plan_data = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "T1",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": ["task-2"]
                },
                {
                    "id": "task-2",
                    "name": "T2",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": ["task-1"]
                }
            ]
        }

        with pytest.raises(PlanningError, match="Circular dependency"):
            self.planner._validate_plan(plan_data)

    def test_detect_circular_dependencies_simple_cycle(self):
        """测试简单循环检测"""
        tasks = [
            {"id": "task-1", "dependencies": ["task-2"]},
            {"id": "task-2", "dependencies": ["task-1"]}
        ]

        with pytest.raises(PlanningError, match="Circular dependency"):
            self.planner._detect_circular_dependencies(tasks)

    def test_detect_circular_dependencies_complex_cycle(self):
        """测试复杂循环检测"""
        tasks = [
            {"id": "task-1", "dependencies": ["task-2"]},
            {"id": "task-2", "dependencies": ["task-3"]},
            {"id": "task-3", "dependencies": ["task-1"]}
        ]

        with pytest.raises(PlanningError, match="Circular dependency"):
            self.planner._detect_circular_dependencies(tasks)

    def test_detect_circular_dependencies_no_cycle(self):
        """测试无循环的情况"""
        tasks = [
            {"id": "task-1", "dependencies": []},
            {"id": "task-2", "dependencies": ["task-1"]},
            {"id": "task-3", "dependencies": ["task-2"]}
        ]

        # Should not raise exception
        self.planner._detect_circular_dependencies(tasks)

    def test_build_workflow(self):
        """测试构建 Workflow 对象"""
        plan_data = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Test task",
                    "tool": "llm:chat",
                    "params": {"key": "value"},
                    "dependencies": [],
                    "requires_approval": False
                }
            ]
        }

        workflow = self.planner._build_workflow(
            plan_data=plan_data,
            session_id="session-1",
            goal="Test goal"
        )

        assert workflow.session_id == "session-1"
        assert workflow.goal == "Test goal"
        assert len(workflow.tasks) == 1
        assert workflow.tasks[0].id == "task-1"
        assert workflow.tasks[0].status == TaskStatus.PENDING
        assert workflow.status == WorkflowStatus.PENDING

    @pytest.mark.asyncio
    async def test_refine_plan(self):
        """测试优化计划"""
        # 创建原始工作流
        mock_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Original task",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": []
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        original_workflow = await self.planner.plan(
            goal="Original goal",
            session_id="session-1"
        )

        # Mock 优化后的计划
        refined_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "Refined task",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": []
                },
                {
                    "id": "task-2",
                    "name": "Additional task",
                    "tool": "rag:search",
                    "params": {},
                    "dependencies": ["task-1"]
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(refined_plan)

        # 执行优化
        refined_workflow = await self.planner.refine_plan(
            workflow=original_workflow,
            feedback="Add a search task"
        )

        # 验证
        assert refined_workflow.id == original_workflow.id  # ID 保持不变
        assert refined_workflow.session_id == original_workflow.session_id
        assert len(refined_workflow.tasks) == 2
        assert refined_workflow.tasks[1].name == "Additional task"

    @pytest.mark.asyncio
    async def test_plan_with_validation_disabled(self):
        """测试禁用验证的规划"""
        self.planner.enable_validation = False

        # 提供无效的计划（循环依赖）
        invalid_plan = {
            "tasks": [
                {
                    "id": "task-1",
                    "name": "T1",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": ["task-2"]
                },
                {
                    "id": "task-2",
                    "name": "T2",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": ["task-1"]
                }
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(invalid_plan)

        # 应该不抛出异常（因为验证被禁用）
        # 但构建 Workflow 时会在 __post_init__ 中验证
        with pytest.raises(PlanningError):
            await self.planner.plan(goal="Test", session_id="session-1")

    def test_estimate_execution_time(self):
        """测试执行时间估算"""
        plan_data = {
            "tasks": [
                {"id": "task-1", "name": "MCP", "tool": "mcp:test", "params": {}},
                {"id": "task-2", "name": "Skill", "tool": "skill:test", "params": {}},
                {"id": "task-3", "name": "RAG", "tool": "rag:test", "params": {}},
                {"id": "task-4", "name": "LLM", "tool": "llm:test", "params": {}}
            ]
        }

        workflow = self.planner._build_workflow(
            plan_data=plan_data,
            session_id="session-1",
            goal="Test"
        )

        estimate = self.planner.estimate_execution_time(workflow)

        assert "estimated_seconds" in estimate
        assert "estimated_minutes" in estimate
        assert "task_count" in estimate
        assert estimate["task_count"] == 4
        assert estimate["estimated_seconds"] == 20.0  # 2+5+3+10
        assert estimate["estimated_minutes"] == pytest.approx(0.333, rel=0.01)

    @pytest.mark.asyncio
    async def test_plan_llm_call_error(self):
        """测试 LLM 调用失败"""
        self.llm_mock.chat.side_effect = Exception("LLM API error")

        with pytest.raises(PlanningError, match="Failed to plan workflow"):
            await self.planner.plan(
                goal="Test goal",
                session_id="session-1"
            )

    @pytest.mark.asyncio
    async def test_get_available_tools(self):
        """测试获取可用工具列表"""
        tools = await self.planner._get_available_tools()

        assert len(tools) == 4
        assert all("name" in tool for tool in tools)
        assert all("description" in tool for tool in tools)
        assert all("type" in tool for tool in tools)

    def test_build_planning_prompt(self):
        """测试构建规划 Prompt"""
        available_tools = [
            {"name": "mcp:test", "description": "Test tool", "type": "mcp"}
        ]

        prompt = self.planner._build_planning_prompt(
            goal="Test goal",
            available_tools=available_tools,
            context={"key": "value"}
        )

        assert "Test goal" in prompt
        assert "mcp:test" in prompt
        assert "Test tool" in prompt
        assert "key: value" in prompt
        assert "JSON" in prompt

    @pytest.mark.asyncio
    async def test_plan_multiple_tasks(self):
        """测试多任务规划"""
        mock_plan = {
            "tasks": [
                {
                    "id": f"task-{i}",
                    "name": f"Task {i}",
                    "tool": "llm:chat",
                    "params": {},
                    "dependencies": [f"task-{i-1}"] if i > 1 else []
                }
                for i in range(1, 6)
            ]
        }
        self.llm_mock.chat.return_value = json.dumps(mock_plan)

        workflow = await self.planner.plan(
            goal="Complex multi-task goal",
            session_id="session-1"
        )

        assert len(workflow.tasks) == 5
        # Verify dependency chain
        for i in range(1, 5):
            assert f"task-{i}" in workflow.tasks[i].dependencies
