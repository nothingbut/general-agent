"""测试工作流执行器"""
import pytest
from unittest.mock import AsyncMock, MagicMock
from datetime import datetime

from src.workflow.executor import WorkflowExecutor, ExecutionError
from src.workflow.models import (
    Workflow,
    Task,
    TaskStatus,
    WorkflowStatus,
    ToolResult
)
from src.workflow.orchestrator import ToolOrchestrator


class TestWorkflowExecutor:
    """测试工作流执行器"""

    def setup_method(self):
        """测试前准备"""
        # Mock orchestrator
        self.orchestrator_mock = AsyncMock(spec=ToolOrchestrator)

        # Mock database
        self.db_mock = AsyncMock()
        self.db_mock.get_workflow.return_value = None
        self.db_mock.create_workflow.return_value = None
        self.db_mock.update_workflow_status.return_value = None
        self.db_mock.create_task_execution.return_value = None
        self.db_mock.get_workflow_executions.return_value = []

        # Create executor
        self.executor = WorkflowExecutor(
            orchestrator=self.orchestrator_mock,
            database=self.db_mock
        )

    def test_topological_sort_simple(self):
        """测试简单的拓扑排序"""
        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=["task-1"]),
            Task(id="task-3", name="T3", tool="llm:chat", params={}, dependencies=["task-1"])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        batches = self.executor._topological_sort(workflow)

        # 第一批只有 task-1
        assert len(batches) == 2
        assert len(batches[0]) == 1
        assert batches[0][0].id == "task-1"

        # 第二批有 task-2 和 task-3（可并行）
        assert len(batches[1]) == 2
        task_ids = {t.id for t in batches[1]}
        assert task_ids == {"task-2", "task-3"}

    def test_topological_sort_chain(self):
        """测试链式依赖"""
        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=["task-1"]),
            Task(id="task-3", name="T3", tool="llm:chat", params={}, dependencies=["task-2"])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        batches = self.executor._topological_sort(workflow)

        # 应该是 3 个批次，每批 1 个任务
        assert len(batches) == 3
        assert batches[0][0].id == "task-1"
        assert batches[1][0].id == "task-2"
        assert batches[2][0].id == "task-3"

    def test_topological_sort_diamond(self):
        """测试菱形依赖"""
        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=["task-1"]),
            Task(id="task-3", name="T3", tool="llm:chat", params={}, dependencies=["task-1"]),
            Task(id="task-4", name="T4", tool="llm:chat", params={}, dependencies=["task-2", "task-3"])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        batches = self.executor._topological_sort(workflow)

        # 第一批: task-1
        # 第二批: task-2, task-3 (并行)
        # 第三批: task-4
        assert len(batches) == 3
        assert batches[0][0].id == "task-1"
        assert len(batches[1]) == 2
        assert batches[2][0].id == "task-4"

    @pytest.mark.asyncio
    async def test_execute_single_task_success(self):
        """测试单任务执行成功"""
        # Mock tool execution
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=True,
            data={"result": "success"}
        )

        tasks = [
            Task(id="task-1", name="Test Task", tool="llm:chat", params={}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "completed"
        assert len(result["completed_tasks"]) == 1
        assert len(result["failed_tasks"]) == 0
        assert workflow.status == WorkflowStatus.COMPLETED

        # 验证工具被调用
        self.orchestrator_mock.execute_tool.assert_called_once()

    @pytest.mark.asyncio
    async def test_execute_single_task_failure(self):
        """测试单任务执行失败"""
        # Mock tool execution failure
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=False,
            error="Tool failed"
        )

        tasks = [
            Task(id="task-1", name="Test Task", tool="llm:chat", params={}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "failed"
        assert len(result["completed_tasks"]) == 0
        assert len(result["failed_tasks"]) == 1
        assert workflow.status == WorkflowStatus.FAILED

    @pytest.mark.asyncio
    async def test_execute_with_dependencies(self):
        """测试带依赖的任务执行"""
        # Mock tool execution
        call_count = 0

        async def mock_execute_tool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            return ToolResult(success=True, data={"result": f"result-{call_count}"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={"input": "${task-1.result}"}, dependencies=["task-1"])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "completed"
        assert len(result["completed_tasks"]) == 2
        assert self.orchestrator_mock.execute_tool.call_count == 2

    @pytest.mark.asyncio
    async def test_execute_with_retry(self):
        """测试任务重试"""
        # Mock tool execution: 前两次失败，第三次成功
        call_count = 0

        async def mock_execute_tool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count < 3:
                return ToolResult(success=False, error="Temporary error")
            return ToolResult(success=True, data={"result": "success"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

        tasks = [
            Task(id="task-1", name="Test", tool="llm:chat", params={}, dependencies=[], max_retries=3)
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "completed"
        assert call_count == 3  # 第一次 + 2 次重试

    @pytest.mark.asyncio
    async def test_execute_with_max_retries_exceeded(self):
        """测试超过最大重试次数"""
        # Mock tool execution: 一直失败
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=False,
            error="Persistent error"
        )

        tasks = [
            Task(id="task-1", name="Test", tool="llm:chat", params={}, dependencies=[], max_retries=2)
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "failed"
        # 1 次初始尝试 + 2 次重试 = 3 次调用
        assert self.orchestrator_mock.execute_tool.call_count == 3

    @pytest.mark.asyncio
    async def test_execute_parallel_tasks(self):
        """测试并行任务执行"""
        # Mock tool execution
        execution_order = []

        async def mock_execute_tool(tool_name, params, context, session_id):
            task_id = params.get("task_id", "unknown")
            execution_order.append(task_id)
            return ToolResult(success=True, data={"result": "ok"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

        # 创建 3 个无依赖的任务
        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={"task_id": "task-1"}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={"task_id": "task-2"}, dependencies=[]),
            Task(id="task-3", name="T3", tool="llm:chat", params={"task_id": "task-3"}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(workflow)

        # 验证结果
        assert result["status"] == "completed"
        assert len(result["completed_tasks"]) == 3
        # 3 个任务应该被执行（顺序可能不同）
        assert len(execution_order) == 3

    @pytest.mark.asyncio
    async def test_execute_with_approval_required(self):
        """测试需要审批的任务"""
        # Mock tool execution
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=True,
            data={"result": "ok"}
        )

        # Mock approval callback (批准)
        async def approval_callback(task, workflow):
            return True

        tasks = [
            Task(
                id="task-1",
                name="Dangerous task",
                tool="mcp:filesystem:delete",
                params={},
                dependencies=[],
                requires_approval=True
            )
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(
            workflow,
            on_approval_required=approval_callback
        )

        # 验证结果
        assert result["status"] == "completed"
        assert len(result["completed_tasks"]) == 1

    @pytest.mark.asyncio
    async def test_execute_with_approval_rejected(self):
        """测试审批被拒绝"""
        # Mock approval callback (拒绝)
        async def approval_callback(task, workflow):
            return False

        tasks = [
            Task(
                id="task-1",
                name="Dangerous task",
                tool="mcp:filesystem:delete",
                params={},
                dependencies=[],
                requires_approval=True
            )
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        result = await self.executor.execute(
            workflow,
            on_approval_required=approval_callback
        )

        # 验证结果
        # 审批拒绝被视为失败（因为任务未执行成功）
        assert result["status"] in ["failed", "cancelled"]
        assert len(result["completed_tasks"]) == 0

    @pytest.mark.asyncio
    async def test_execute_saves_workflow_state(self):
        """测试保存工作流状态"""
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=True,
            data={"result": "ok"}
        )

        tasks = [
            Task(id="task-1", name="Test", tool="llm:chat", params={}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        await self.executor.execute(workflow)

        # 验证数据库被调用
        assert self.db_mock.create_workflow.called or self.db_mock.update_workflow_status.called
        assert self.db_mock.create_task_execution.called

    @pytest.mark.asyncio
    async def test_get_execution_status(self):
        """测试获取执行状态"""
        # Mock database response
        self.db_mock.get_workflow.return_value = {
            "id": "wf-1",
            "status": "completed",
            "goal": "Test goal",
            "created_at": "2026-03-06T10:00:00",
            "completed_at": "2026-03-06T10:05:00"
        }

        self.db_mock.get_workflow_executions.return_value = [
            {
                "task_id": "task-1",
                "status": "success",
                "started_at": "2026-03-06T10:00:00"
            }
        ]

        status = await self.executor.get_execution_status("wf-1")

        assert status["workflow_id"] == "wf-1"
        assert status["status"] == "completed"
        assert len(status["task_executions"]) == 1

    @pytest.mark.asyncio
    async def test_execute_with_task_complete_callback(self):
        """测试任务完成回调"""
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=True,
            data={"result": "ok"}
        )

        completed_tasks = []

        async def task_complete_callback(task, result):
            completed_tasks.append(task.id)

        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        await self.executor.execute(workflow, on_task_complete=task_complete_callback)

        # 验证回调被调用
        assert len(completed_tasks) == 2

    @pytest.mark.asyncio
    async def test_execute_stops_on_failure(self):
        """测试遇到失败任务时停止执行"""
        call_count = 0

        async def mock_execute_tool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return ToolResult(success=False, error="First task failed")
            return ToolResult(success=True, data={"result": "ok"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[], max_retries=0),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=["task-1"])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 禁用重试
        executor = WorkflowExecutor(
            orchestrator=self.orchestrator_mock,
            database=self.db_mock,
            enable_retry=False
        )

        result = await executor.execute(workflow)

        # 验证结果
        assert result["status"] == "failed"
        assert len(result["failed_tasks"]) == 1
        # task-2 不应该被执行
        assert call_count == 1  # 只调用了第一个任务
