"""测试工作流执行器的高级特性"""
import pytest
import asyncio
from unittest.mock import AsyncMock
from datetime import datetime

from src.workflow.executor import WorkflowExecutor
from src.workflow.models import (
    Workflow,
    Task,
    TaskStatus,
    WorkflowStatus,
    ToolResult
)
from src.workflow.orchestrator import ToolOrchestrator


class TestWorkflowExecutorAdvanced:
    """测试工作流执行器的高级特性"""

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

    def test_calculate_backoff(self):
        """测试智能退避策略"""
        executor = WorkflowExecutor(
            orchestrator=self.orchestrator_mock,
            database=self.db_mock,
            base_backoff=1.0,
            max_backoff=60.0
        )

        # 第 1 次重试: 1.0 * 2^0 = 1.0s (±25%)
        backoff1 = executor._calculate_backoff(1)
        assert 0.75 <= backoff1 <= 1.25

        # 第 2 次重试: 1.0 * 2^1 = 2.0s (±25%)
        backoff2 = executor._calculate_backoff(2)
        assert 1.5 <= backoff2 <= 2.5

        # 第 3 次重试: 1.0 * 2^2 = 4.0s (±25%)
        backoff3 = executor._calculate_backoff(3)
        assert 3.0 <= backoff3 <= 5.0

        # 验证指数增长
        assert backoff2 > backoff1
        assert backoff3 > backoff2

        # 第 10 次重试: 应该被限制在 max_backoff
        backoff10 = executor._calculate_backoff(10)
        assert backoff10 <= 60.0 * 1.25  # max + 25% jitter

    @pytest.mark.asyncio
    async def test_pause_resume(self):
        """测试暂停和恢复功能"""
        # Mock tool execution with delay
        call_count = 0

        async def mock_execute_tool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            await asyncio.sleep(0.1)
            return ToolResult(success=True, data={"result": f"result-{call_count}"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-2", name="T2", tool="llm:chat", params={}, dependencies=[]),
            Task(id="task-3", name="T3", tool="llm:chat", params={}, dependencies=[])
        ]

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 启动执行并在中途暂停
        async def pause_after_delay():
            await asyncio.sleep(0.15)  # 等待第一个任务完成
            self.executor.pause()
            await asyncio.sleep(0.2)  # 保持暂停一段时间
            self.executor.resume()

        # 同时运行执行和暂停操作
        execute_task = asyncio.create_task(self.executor.execute(workflow))
        pause_task = asyncio.create_task(pause_after_delay())

        await pause_task
        result = await execute_task

        # 验证所有任务最终完成
        assert result["status"] == "completed"
        assert len(result["completed_tasks"]) == 3

    @pytest.mark.asyncio
    async def test_stop_execution(self):
        """测试停止执行"""
        call_count = 0

        async def mock_execute_tool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            await asyncio.sleep(0.1)
            return ToolResult(success=True, data={"result": f"result-{call_count}"})

        self.orchestrator_mock.execute_tool.side_effect = mock_execute_tool

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

        # 启动执行并在中途停止
        async def stop_after_delay():
            await asyncio.sleep(0.15)  # 等待第一个任务完成
            self.executor.stop()

        execute_task = asyncio.create_task(self.executor.execute(workflow))
        stop_task = asyncio.create_task(stop_after_delay())

        await stop_task
        result = await execute_task

        # 验证工作流被取消，且不是所有任务都完成
        assert result["status"] == "cancelled"
        assert len(result["completed_tasks"]) < 3

    @pytest.mark.asyncio
    async def test_resume_workflow(self):
        """测试断点恢复"""
        # Mock tool execution
        self.orchestrator_mock.execute_tool.return_value = ToolResult(
            success=True,
            data={"result": "ok"}
        )

        # Mock database to return partially completed workflow
        self.db_mock.get_workflow.return_value = {
            "id": "wf-1",
            "session_id": "session-1",
            "goal": "Test goal",
            "status": "running",
            "plan": {
                "tasks": [
                    {
                        "id": "task-1",
                        "name": "T1",
                        "tool": "llm:chat",
                        "params": {},
                        "dependencies": [],
                        "requires_approval": False,
                        "max_retries": 3
                    },
                    {
                        "id": "task-2",
                        "name": "T2",
                        "tool": "llm:chat",
                        "params": {},
                        "dependencies": ["task-1"],
                        "requires_approval": False,
                        "max_retries": 3
                    },
                    {
                        "id": "task-3",
                        "name": "T3",
                        "tool": "llm:chat",
                        "params": {},
                        "dependencies": ["task-2"],
                        "requires_approval": False,
                        "max_retries": 3
                    }
                ]
            },
            "created_at": datetime.now(),
            "started_at": datetime.now()
        }

        # Mock task executions: task-1 and task-2 already completed
        self.db_mock.get_workflow_executions.return_value = [
            {
                "task_id": "task-1",
                "status": "success",
                "result": {"result": "result-1"}
            },
            {
                "task_id": "task-2",
                "status": "success",
                "result": {"result": "result-2"}
            }
        ]

        # Resume workflow
        result = await self.executor.resume_workflow("wf-1")

        # 验证结果
        assert result["status"] == "completed"
        # 只有 task-3 应该被执行（task-1 和 task-2 已完成）
        assert self.orchestrator_mock.execute_tool.call_count == 1

    @pytest.mark.asyncio
    async def test_resume_workflow_not_found(self):
        """测试恢复不存在的工作流"""
        self.db_mock.get_workflow.return_value = None

        with pytest.raises(ValueError, match="Workflow not found"):
            await self.executor.resume_workflow("non-existent")

    @pytest.mark.asyncio
    async def test_resume_completed_workflow(self):
        """测试恢复已完成的工作流"""
        self.db_mock.get_workflow.return_value = {
            "id": "wf-1",
            "status": "completed"
        }

        with pytest.raises(ValueError, match="Cannot resume"):
            await self.executor.resume_workflow("wf-1")

    @pytest.mark.asyncio
    async def test_topological_sort_with_completed_tasks(self):
        """测试拓扑排序时跳过已完成的任务"""
        tasks = [
            Task(id="task-1", name="T1", tool="llm:chat", params={}, dependencies=[], status=TaskStatus.SUCCESS),
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

        # task-1 已完成，应该被跳过
        # 只有 task-2 和 task-3 应该在结果中
        assert len(batches) == 1
        assert len(batches[0]) == 2
        task_ids = {t.id for t in batches[0]}
        assert task_ids == {"task-2", "task-3"}
