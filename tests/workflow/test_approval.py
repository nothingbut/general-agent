"""测试审批管理器"""
import pytest
import asyncio
from unittest.mock import AsyncMock, MagicMock
from datetime import datetime

from src.workflow.approval import ApprovalManager, ApprovalStatus
from src.workflow.models import (
    Task,
    Workflow,
    ApprovalRequest,
    ApprovalResult
)


class TestApprovalManager:
    """测试审批管理器"""

    def setup_method(self):
        """测试前准备"""
        # Mock database
        self.db_mock = AsyncMock()
        self.db_mock.create_approval.return_value = None
        self.db_mock.update_approval.return_value = None
        self.db_mock.get_workflow_approvals.return_value = []
        self.db_mock.get_all_approvals.return_value = []

        # Create manager
        self.manager = ApprovalManager(
            database=self.db_mock,
            default_timeout=5.0
        )

    @pytest.mark.asyncio
    async def test_auto_approve_mode(self):
        """测试自动批准模式"""
        # 启用自动批准
        manager = ApprovalManager(
            database=self.db_mock,
            enable_auto_approve=True
        )

        task = Task(
            id="task-1",
            name="Test task",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[],
            requires_approval=True
        )

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=[task]
        )

        # 请求审批
        result = await manager.request_approval(task, workflow)

        # 验证自动批准
        assert result.approved is True
        assert "自动批准" in result.comment

    @pytest.mark.asyncio
    async def test_request_approval_with_handler(self):
        """测试请求审批（有处理函数）"""
        # 注册处理函数
        async def mock_handler(request: ApprovalRequest) -> ApprovalResult:
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="测试批准",
                responded_at=datetime.now(
            )
            )

        self.manager.register_handler(mock_handler)

        task = Task(
            id="task-1",
            name="Test task",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[],
            requires_approval=True
        )

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=[task]
        )

        # 请求审批
        result = await self.manager.request_approval(task, workflow)

        # 验证
        assert result.approved is True
        assert result.comment == "测试批准"
        self.db_mock.create_approval.assert_called_once()
        self.db_mock.update_approval.assert_called_once()

    @pytest.mark.asyncio
    async def test_request_approval_timeout(self):
        """测试审批超时"""
        # 注册一个永远不响应的处理函数
        async def slow_handler(request: ApprovalRequest) -> ApprovalResult:
            await asyncio.sleep(10)
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="",
                responded_at=datetime.now(
            ))

        self.manager.register_handler(slow_handler)

        task = Task(
            id="task-1",
            name="Test task",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[],
            requires_approval=True
        )

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=[task]
        )

        # 请求审批（应该超时）
        with pytest.raises(TimeoutError):
            await self.manager.request_approval(task, workflow, timeout=0.5)

    @pytest.mark.asyncio
    async def test_request_approval_no_handler(self):
        """测试没有处理函数时默认拒绝"""
        task = Task(
            id="task-1",
            name="Test task",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[],
            requires_approval=True
        )

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=[task]
        )

        # 请求审批
        result = await self.manager.request_approval(task, workflow)

        # 验证默认拒绝
        assert result.approved is False
        assert "无可用的审批处理器" in result.comment

    @pytest.mark.asyncio
    async def test_multiple_handlers(self):
        """测试多个处理函数（取第一个响应）"""
        # 注册多个处理函数
        async def fast_handler(request: ApprovalRequest) -> ApprovalResult:
            await asyncio.sleep(0.1)
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="快速处理",
                responded_at=datetime.now(
            ))

        async def slow_handler(request: ApprovalRequest) -> ApprovalResult:
            await asyncio.sleep(1.0)
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=False,
                comment="慢速处理",
                responded_at=datetime.now(
            ))

        self.manager.register_handler(slow_handler)
        self.manager.register_handler(fast_handler)

        task = Task(
            id="task-1",
            name="Test task",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[],
            requires_approval=True
        )

        workflow = Workflow(
            id="wf-1",
            session_id="session-1",
            goal="Test",
            tasks=[task]
        )

        # 请求审批
        result = await self.manager.request_approval(task, workflow)

        # 验证返回快速处理函数的结果
        assert result.approved is True
        assert result.comment == "快速处理"

    @pytest.mark.asyncio
    async def test_get_pending_approvals(self):
        """测试获取待处理审批"""
        # 创建一些审批请求
        task1 = Task(id="task-1", name="T1", tool="mcp:test", params={}, dependencies=[])
        task2 = Task(id="task-2", name="T2", tool="mcp:test", params={}, dependencies=[])
        workflow = Workflow(id="wf-1", session_id="s-1", goal="Test", tasks=[task1, task2])

        # 使用自动批准模式，但先禁用它
        self.manager.enable_auto_approve = False

        # 注册一个不响应的处理函数
        async def pending_handler(request: ApprovalRequest) -> ApprovalResult:
            await asyncio.sleep(100)
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="",
                responded_at=datetime.now(
            ))

        self.manager.register_handler(pending_handler)

        # 创建两个待处理的审批请求
        task_results = []
        for task in [task1, task2]:
            task_result = asyncio.create_task(
                self.manager.request_approval(task, workflow, timeout=0.1)
            )
            task_results.append(task_result)

        # 等待一小段时间让请求进入待处理状态
        await asyncio.sleep(0.05)

        # 获取待处理审批
        pending = await self.manager.get_pending_approvals()
        assert len(pending) == 2

        # 取消任务
        for task_result in task_results:
            task_result.cancel()
            try:
                await task_result
            except asyncio.CancelledError:
                pass

    @pytest.mark.asyncio
    async def test_approve_and_reject(self):
        """测试批准和拒绝"""
        # 创建一个待处理的审批请求
        task = Task(id="task-1", name="T1", tool="mcp:test", params={}, dependencies=[])
        workflow = Workflow(id="wf-1", session_id="s-1", goal="Test", tasks=[task])

        # 使用自动批准模式
        self.manager.enable_auto_approve = True
        result = await self.manager.request_approval(task, workflow)

        # 获取审批 ID
        approval_id = list(self.manager._approval_results.keys())[0]

        # 重新添加到待处理列表（模拟待处理状态）
        request = ApprovalRequest(
            approval_id=approval_id,
            workflow_id=workflow.id,
            task_id=task.id,
            task_name=task.name,
            tool_name=task.tool,
            params=task.params,
            reason="test",
            created_at=datetime.now()
        )
        self.manager._pending_approvals[approval_id] = request

        # 测试批准
        result = await self.manager.approve(approval_id, "用户批准")
        assert result.approved is True
        assert "用户批准" in result.comment

        # 重新添加到待处理列表
        self.manager._pending_approvals[approval_id] = request

        # 测试拒绝
        result = await self.manager.reject(approval_id, "用户拒绝")
        assert result.approved is False
        assert "用户拒绝" in result.comment

    @pytest.mark.asyncio
    async def test_get_approval_history(self):
        """测试获取审批历史"""
        self.db_mock.get_workflow_approvals.return_value = [
            {
                "id": "approval-1",
                "workflow_id": "wf-1",
                "task_id": "task-1",
                "status": "approved",
                "user_comment": "批准",
                "created_at": "2026-03-07 10:00:00",
                "responded_at": "2026-03-07 10:01:00"
            }
        ]

        history = await self.manager.get_approval_history("wf-1")
        assert len(history) == 1
        assert history[0]["id"] == "approval-1"

    @pytest.mark.asyncio
    async def test_get_statistics(self):
        """测试获取统计信息"""
        self.db_mock.get_all_approvals.return_value = [
            {"id": "1", "decision": True},
            {"id": "2", "decision": True},
            {"id": "3", "decision": False},
        ]

        # 添加一个待处理的审批
        request = ApprovalRequest(
            approval_id="pending-1",
            workflow_id="wf-1",
            task_id="task-1",
            task_name="T1",
            tool_name="mcp:test",
            params={},
            reason="test",
            created_at=datetime.now()
        )
        self.manager._pending_approvals["pending-1"] = request

        stats = await self.manager.get_statistics()

        assert stats["total"] == 3
        assert stats["approved"] == 2
        assert stats["rejected"] == 1
        assert stats["pending"] == 1
        assert stats["approval_rate"] == 2/3

    def test_generate_approval_reason(self):
        """测试生成审批原因"""
        # MCP 删除操作
        task1 = Task(
            id="task-1",
            name="Delete file",
            tool="mcp:filesystem:delete",
            params={},
            dependencies=[]
        )
        reason1 = self.manager._generate_approval_reason(task1)
        assert "删除" in reason1

        # MCP 写入操作
        task2 = Task(
            id="task-2",
            name="Write file",
            tool="mcp:filesystem:write",
            params={},
            dependencies=[]
        )
        reason2 = self.manager._generate_approval_reason(task2)
        assert "写入" in reason2

        # 技能执行
        task3 = Task(
            id="task-3",
            name="Run skill",
            tool="skill:execute",
            params={},
            dependencies=[]
        )
        reason3 = self.manager._generate_approval_reason(task3)
        assert "执行" in reason3 or "技能" in reason3

    def test_register_unregister_handler(self):
        """测试注册和取消注册处理函数"""
        async def test_handler(request: ApprovalRequest) -> ApprovalResult:
            return ApprovalResult(
                approval_id=request.approval_id,
                approved=True,
                comment="",
                responded_at=datetime.now(
            ))

        # 注册
        self.manager.register_handler(test_handler)
        assert test_handler in self.manager._approval_handlers

        # 取消注册
        self.manager.unregister_handler(test_handler)
        assert test_handler not in self.manager._approval_handlers
