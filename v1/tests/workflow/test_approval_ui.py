"""TUI 审批界面测试"""
import pytest
from datetime import datetime
from unittest.mock import Mock, patch, AsyncMock
from io import StringIO

from rich.console import Console

from src.workflow.approval_ui import (
    ApprovalUI,
    create_approval_handler,
    default_approval_handler
)
from src.workflow.models import ApprovalRequest, ApprovalResult


class TestApprovalUI:
    """测试 ApprovalUI 类"""

    def test_initialization(self):
        """测试初始化"""
        ui = ApprovalUI()
        assert ui.console is not None
        assert not ui._running

    def test_initialization_with_console(self):
        """测试使用自定义 Console 初始化"""
        console = Console()
        ui = ApprovalUI(console=console)
        assert ui.console is console

    def test_format_params_simple(self):
        """测试格式化简单参数"""
        ui = ApprovalUI()
        params = {"name": "test", "value": 123}
        result = ui._format_params(params)
        assert "name: test" in result
        assert "value: 123" in result

    def test_format_params_long_value(self):
        """测试格式化长参数值"""
        ui = ApprovalUI()
        long_value = "x" * 100
        params = {"data": long_value}
        result = ui._format_params(params)
        # 应该被截断
        assert len(result.split(": ")[1]) <= 60
        assert "..." in result

    def test_format_params_empty(self):
        """测试格式化空参数"""
        ui = ApprovalUI()
        result = ui._format_params({})
        assert result == ""

    @pytest.mark.asyncio
    async def test_handle_approval_approved(self):
        """测试处理审批请求 - 批准"""
        # 创建请求
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试任务",
            tool_name="test:tool",
            params={"key": "value"},
            reason="测试原因"
        )

        # Mock Console 避免实际输出
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        # Mock 用户输入 - 批准
        with patch.object(ui, '_get_user_decision', return_value=(True, "测试批准")):
            result = await ui.handle_approval(request)

        assert result.approval_id == request.approval_id
        assert result.approved is True
        assert result.comment == "测试批准"
        assert result.responded_at is not None
        assert not ui._running  # 完成后应该停止

    @pytest.mark.asyncio
    async def test_handle_approval_rejected(self):
        """测试处理审批请求 - 拒绝"""
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试任务",
            tool_name="test:tool",
            params={}
        )

        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        # Mock 用户输入 - 拒绝
        with patch.object(ui, '_get_user_decision', return_value=(False, "不允许")):
            result = await ui.handle_approval(request)

        assert result.approved is False
        assert result.comment == "不允许"

    @pytest.mark.asyncio
    async def test_handle_approval_no_comment(self):
        """测试处理审批请求 - 无评论"""
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试任务",
            tool_name="test:tool",
            params={}
        )

        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        # Mock 用户输入 - 批准但无评论
        with patch.object(ui, '_get_user_decision', return_value=(True, None)):
            result = await ui.handle_approval(request)

        assert result.approved is True
        assert result.comment is None

    def test_display_approval_request(self):
        """测试显示审批请求"""
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试任务",
            tool_name="mcp:filesystem:read",
            params={"path": "/tmp/test.txt"},
            reason="需要读取文件"
        )

        # 使用 StringIO 捕获输出
        string_io = StringIO()
        console = Console(file=string_io)
        ui = ApprovalUI(console=console)

        ui._display_approval_request(request)

        output = string_io.getvalue()
        assert "审批请求" in output
        assert "测试任务" in output
        assert "mcp:filesystem:read" in output
        assert "需要读取文件" in output

    def test_display_result_approved(self):
        """测试显示批准结果"""
        result = ApprovalResult(
            approval_id="test-123",
            approved=True,
            comment="看起来不错"
        )

        string_io = StringIO()
        console = Console(file=string_io)
        ui = ApprovalUI(console=console)

        ui._display_result(result)

        output = string_io.getvalue()
        assert "已批准" in output or "✓" in output

    def test_display_result_rejected(self):
        """测试显示拒绝结果"""
        result = ApprovalResult(
            approval_id="test-123",
            approved=False,
            comment="有风险"
        )

        string_io = StringIO()
        console = Console(file=string_io)
        ui = ApprovalUI(console=console)

        ui._display_result(result)

        output = string_io.getvalue()
        assert "已拒绝" in output or "✗" in output

    def test_display_approval_history_empty(self):
        """测试显示空审批历史"""
        string_io = StringIO()
        console = Console(file=string_io)
        ui = ApprovalUI(console=console)

        ui.display_approval_history([])

        output = string_io.getvalue()
        assert "暂无审批历史" in output

    def test_display_approval_history_with_data(self):
        """测试显示审批历史（有数据）"""
        approvals = [
            {
                "created_at": datetime.now(),
                "task_name": "任务1",
                "tool_name": "test:tool1",
                "status": "approved",
                "user_comment": "批准"
            },
            {
                "created_at": datetime.now(),
                "task_name": "任务2",
                "tool_name": "test:tool2",
                "status": "rejected",
                "user_comment": "拒绝"
            },
            {
                "created_at": datetime.now(),
                "task_name": "任务3",
                "tool_name": "test:tool3",
                "status": "pending",
                "user_comment": None
            }
        ]

        string_io = StringIO()
        console = Console(file=string_io)
        ui = ApprovalUI(console=console)

        ui.display_approval_history(approvals)

        output = string_io.getvalue()
        assert "审批历史" in output
        assert "任务1" in output
        assert "任务2" in output
        assert "任务3" in output

    def test_get_user_decision_approve(self):
        """测试获取用户决策 - 批准"""
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        # Mock Prompt.ask
        with patch('src.workflow.approval_ui.Prompt.ask') as mock_ask:
            mock_ask.side_effect = ["Y", "测试批准"]
            approved, comment = ui._get_user_decision()

        assert approved is True
        assert comment == "测试批准"

    def test_get_user_decision_reject(self):
        """测试获取用户决策 - 拒绝"""
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        with patch('src.workflow.approval_ui.Prompt.ask') as mock_ask:
            mock_ask.side_effect = ["N", "不安全"]
            approved, comment = ui._get_user_decision()

        assert approved is False
        assert comment == "不安全"

    def test_get_user_decision_no_comment(self):
        """测试获取用户决策 - 无评论"""
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        with patch('src.workflow.approval_ui.Prompt.ask') as mock_ask:
            mock_ask.side_effect = ["Y", ""]  # 空评论
            approved, comment = ui._get_user_decision()

        assert approved is True
        assert comment is None

    def test_get_user_decision_with_help(self):
        """测试获取用户决策 - 先查看帮助"""
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        with patch('src.workflow.approval_ui.Prompt.ask') as mock_ask:
            # 先选择 I（帮助），再选择 Y（批准）
            mock_ask.side_effect = ["I", "Y", ""]
            approved, comment = ui._get_user_decision()

        assert approved is True


class TestApprovalHandlers:
    """测试审批处理函数"""

    @pytest.mark.asyncio
    async def test_create_approval_handler(self):
        """测试创建审批处理函数"""
        handler = await create_approval_handler()
        assert callable(handler)

        # 测试调用
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试",
            tool_name="test:tool",
            params={}
        )

        # Mock UI
        with patch('src.workflow.approval_ui.ApprovalUI.handle_approval') as mock_handle:
            mock_handle.return_value = ApprovalResult(
                approval_id=request.approval_id,
                approved=True
            )
            result = await handler(request)

        assert result.approved is True

    @pytest.mark.asyncio
    async def test_create_approval_handler_with_console(self):
        """测试使用自定义 Console 创建处理函数"""
        console = Console(file=StringIO())
        handler = await create_approval_handler(console=console)
        assert callable(handler)

    @pytest.mark.asyncio
    async def test_default_approval_handler(self):
        """测试默认审批处理函数"""
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="测试",
            tool_name="test:tool",
            params={}
        )

        # Mock 默认 UI
        with patch('src.workflow.approval_ui._default_ui.handle_approval') as mock_handle:
            mock_handle.return_value = ApprovalResult(
                approval_id=request.approval_id,
                approved=False,
                comment="测试拒绝"
            )
            result = await default_approval_handler(request)

        assert result.approved is False
        assert result.comment == "测试拒绝"


class TestIntegration:
    """集成测试"""

    @pytest.mark.asyncio
    async def test_full_approval_flow(self):
        """测试完整的审批流程"""
        # 创建审批管理器需要的请求
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="删除文件",
            tool_name="mcp:filesystem:delete",
            params={"paths": ["/tmp/test1.txt", "/tmp/test2.txt"]},
            reason="执行删除操作"
        )

        # 创建 UI（使用 StringIO 避免实际输出）
        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        # 模拟用户批准
        with patch.object(ui, '_get_user_decision', return_value=(True, "确认删除")):
            result = await ui.handle_approval(request)

        # 验证结果
        assert result.approval_id == request.approval_id
        assert result.approved is True
        assert result.comment == "确认删除"
        assert result.responded_at is not None

    @pytest.mark.asyncio
    async def test_approval_with_complex_params(self):
        """测试带复杂参数的审批"""
        request = ApprovalRequest(
            approval_id="test-123",
            workflow_id="workflow-456",
            task_id="task-1",
            task_name="复杂任务",
            tool_name="test:complex",
            params={
                "list": [1, 2, 3],
                "dict": {"key": "value"},
                "long_string": "x" * 100
            }
        )

        console = Console(file=StringIO())
        ui = ApprovalUI(console=console)

        with patch.object(ui, '_get_user_decision', return_value=(False, None)):
            result = await ui.handle_approval(request)

        assert result.approved is False
