"""测试 workflow 数据模型"""
import pytest
from datetime import datetime
from src.workflow.models import (
    Task,
    TaskStatus,
    Workflow,
    WorkflowStatus,
    ExecutionContext,
    ToolResult,
    ApprovalRequest,
    ApprovalResult
)


class TestTaskStatus:
    """测试 TaskStatus 枚举"""

    def test_task_status_values(self):
        """测试状态枚举值"""
        assert TaskStatus.PENDING.value == "pending"
        assert TaskStatus.RUNNING.value == "running"
        assert TaskStatus.SUCCESS.value == "success"
        assert TaskStatus.FAILED.value == "failed"
        assert TaskStatus.SKIPPED.value == "skipped"
        assert TaskStatus.CANCELLED.value == "cancelled"

    def test_task_status_str(self):
        """测试状态字符串表示"""
        assert str(TaskStatus.PENDING) == "pending"
        assert str(TaskStatus.SUCCESS) == "success"


class TestWorkflowStatus:
    """测试 WorkflowStatus 枚举"""

    def test_workflow_status_values(self):
        """测试工作流状态枚举值"""
        assert WorkflowStatus.PENDING.value == "pending"
        assert WorkflowStatus.RUNNING.value == "running"
        assert WorkflowStatus.WAITING_APPROVAL.value == "waiting_approval"
        assert WorkflowStatus.COMPLETED.value == "completed"
        assert WorkflowStatus.FAILED.value == "failed"
        assert WorkflowStatus.CANCELLED.value == "cancelled"


class TestTask:
    """测试 Task 模型"""

    def test_create_basic_task(self):
        """测试创建基本任务"""
        task = Task(
            id="task-1",
            name="Test Task",
            tool="mcp:filesystem:read",
            params={"path": "/test"}
        )

        assert task.id == "task-1"
        assert task.name == "Test Task"
        assert task.tool == "mcp:filesystem:read"
        assert task.params == {"path": "/test"}
        assert task.status == TaskStatus.PENDING
        assert task.dependencies == []
        assert task.requires_approval is False
        assert task.retry_count == 0

    def test_task_with_dependencies(self):
        """测试带依赖的任务"""
        task = Task(
            id="task-2",
            name="Dependent Task",
            tool="skill:process",
            params={},
            dependencies=["task-1"]
        )

        assert task.dependencies == ["task-1"]

    def test_task_validation_empty_id(self):
        """测试空ID验证"""
        with pytest.raises(ValueError, match="Task ID cannot be empty"):
            Task(id="", name="Test", tool="mcp:test", params={})

    def test_task_validation_empty_name(self):
        """测试空名称验证"""
        with pytest.raises(ValueError, match="Task name cannot be empty"):
            Task(id="task-1", name="", tool="mcp:test", params={})

    def test_task_validation_invalid_tool_format(self):
        """测试无效工具格式"""
        with pytest.raises(ValueError, match="Invalid tool format"):
            Task(id="task-1", name="Test", tool="invalid", params={})

    def test_task_to_dict(self):
        """测试任务转换为字典"""
        task = Task(
            id="task-1",
            name="Test",
            tool="mcp:test",
            params={"key": "value"}
        )

        data = task.to_dict()
        assert data["id"] == "task-1"
        assert data["name"] == "Test"
        assert data["tool"] == "mcp:test"
        assert data["status"] == "pending"
        assert data["params"] == {"key": "value"}

    def test_task_is_ready_no_dependencies(self):
        """测试无依赖任务是否就绪"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})
        assert task.is_ready(set()) is True

    def test_task_is_ready_with_completed_dependencies(self):
        """测试依赖已完成的任务是否就绪"""
        task = Task(
            id="task-2",
            name="Test",
            tool="mcp:test",
            params={},
            dependencies=["task-1"]
        )
        assert task.is_ready({"task-1"}) is True

    def test_task_is_ready_with_pending_dependencies(self):
        """测试依赖未完成的任务不就绪"""
        task = Task(
            id="task-2",
            name="Test",
            tool="mcp:test",
            params={},
            dependencies=["task-1"]
        )
        assert task.is_ready(set()) is False

    def test_task_can_retry(self):
        """测试任务是否可以重试"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})

        # 失败且未达到最大重试次数
        task.mark_failed("Error")
        assert task.can_retry() is True

        # 达到最大重试次数
        task.retry_count = 3
        assert task.can_retry() is False

        # 成功的任务不能重试
        task2 = Task(id="task-2", name="Test", tool="mcp:test", params={})
        task2.mark_success({"result": "ok"})
        assert task2.can_retry() is False

    def test_task_mark_running(self):
        """测试标记任务为运行中"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})
        task.mark_running()
        assert task.status == TaskStatus.RUNNING

    def test_task_mark_success(self):
        """测试标记任务为成功"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})
        result = {"data": "test"}
        task.mark_success(result)

        assert task.status == TaskStatus.SUCCESS
        assert task.result == result
        assert task.error is None

    def test_task_mark_failed(self):
        """测试标记任务为失败"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})
        task.mark_failed("Test error")

        assert task.status == TaskStatus.FAILED
        assert task.error == "Test error"

    def test_task_mark_skipped(self):
        """测试标记任务为跳过"""
        task = Task(id="task-1", name="Test", tool="mcp:test", params={})
        task.mark_skipped()
        assert task.status == TaskStatus.SKIPPED


class TestWorkflow:
    """测试 Workflow 模型"""

    def test_create_basic_workflow(self):
        """测试创建基本工作流"""
        tasks = [
            Task(id="task-1", name="Task 1", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test workflow",
            tasks=tasks
        )

        assert workflow.id == "workflow-1"
        assert workflow.session_id == "session-1"
        assert workflow.goal == "Test workflow"
        assert len(workflow.tasks) == 1
        assert workflow.status == WorkflowStatus.PENDING
        assert workflow.created_at is not None

    def test_workflow_auto_generate_id(self):
        """测试工作流自动生成ID"""
        workflow = Workflow(
            id="",
            session_id="session-1",
            goal="Test",
            tasks=[]
        )

        assert workflow.id != ""
        assert len(workflow.id) > 0

    def test_workflow_validate_missing_dependency(self):
        """测试验证不存在的依赖"""
        tasks = [
            Task(
                id="task-1",
                name="Task 1",
                tool="mcp:test",
                params={},
                dependencies=["nonexistent"]
            )
        ]

        with pytest.raises(ValueError, match="depends on non-existent task"):
            Workflow(
                id="workflow-1",
                session_id="session-1",
                goal="Test",
                tasks=tasks
            )

    def test_workflow_detect_circular_dependency(self):
        """测试检测循环依赖"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}, dependencies=["task-2"]),
            Task(id="task-2", name="T2", tool="mcp:test", params={}, dependencies=["task-1"])
        ]

        with pytest.raises(ValueError, match="Circular dependency detected"):
            Workflow(
                id="workflow-1",
                session_id="session-1",
                goal="Test",
                tasks=tasks
            )

    def test_workflow_to_dict(self):
        """测试工作流转换为字典"""
        tasks = [
            Task(id="task-1", name="Task 1", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        data = workflow.to_dict()
        assert data["id"] == "workflow-1"
        assert data["session_id"] == "session-1"
        assert data["goal"] == "Test"
        assert data["status"] == "pending"
        assert len(data["tasks"]) == 1

    def test_workflow_get_plan_dict(self):
        """测试获取计划字典"""
        tasks = [
            Task(id="task-1", name="Task 1", tool="mcp:test", params={"key": "value"})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test goal",
            tasks=tasks
        )

        plan = workflow.get_plan_dict()
        assert plan["goal"] == "Test goal"
        assert len(plan["tasks"]) == 1
        assert plan["tasks"][0]["id"] == "task-1"

    def test_workflow_get_task_by_id(self):
        """测试根据ID获取任务"""
        tasks = [
            Task(id="task-1", name="Task 1", tool="mcp:test", params={}),
            Task(id="task-2", name="Task 2", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        task = workflow.get_task_by_id("task-1")
        assert task is not None
        assert task.id == "task-1"

        task = workflow.get_task_by_id("nonexistent")
        assert task is None

    def test_workflow_get_ready_tasks(self):
        """测试获取就绪任务"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}),
            Task(id="task-2", name="T2", tool="mcp:test", params={}, dependencies=["task-1"]),
            Task(id="task-3", name="T3", tool="mcp:test", params={}, dependencies=["task-2"])
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 初始状态：只有 task-1 就绪
        ready = workflow.get_ready_tasks()
        assert len(ready) == 1
        assert ready[0].id == "task-1"

        # task-1 完成后：task-2 就绪
        tasks[0].mark_success({"result": "ok"})
        ready = workflow.get_ready_tasks()
        assert len(ready) == 1
        assert ready[0].id == "task-2"

        # task-2 完成后：task-3 就绪
        tasks[1].mark_success({"result": "ok"})
        ready = workflow.get_ready_tasks()
        assert len(ready) == 1
        assert ready[0].id == "task-3"

    def test_workflow_get_failed_tasks(self):
        """测试获取失败任务"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}),
            Task(id="task-2", name="T2", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 标记 task-1 为失败
        tasks[0].mark_failed("Error")

        failed = workflow.get_failed_tasks()
        assert len(failed) == 1
        assert failed[0].id == "task-1"

    def test_workflow_is_completed(self):
        """测试检查工作流是否完成"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}),
            Task(id="task-2", name="T2", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 初始状态：未完成
        assert workflow.is_completed() is False

        # 一个成功，一个待处理：未完成
        tasks[0].mark_success({"result": "ok"})
        assert workflow.is_completed() is False

        # 全部成功：已完成
        tasks[1].mark_success({"result": "ok"})
        assert workflow.is_completed() is True

    def test_workflow_has_failed_tasks(self):
        """测试检查是否有失败任务"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}),
            Task(id="task-2", name="T2", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        assert workflow.has_failed_tasks() is False

        tasks[0].mark_failed("Error")
        assert workflow.has_failed_tasks() is True

    def test_workflow_get_progress(self):
        """测试获取执行进度"""
        tasks = [
            Task(id="task-1", name="T1", tool="mcp:test", params={}),
            Task(id="task-2", name="T2", tool="mcp:test", params={}),
            Task(id="task-3", name="T3", tool="mcp:test", params={})
        ]

        workflow = Workflow(
            id="workflow-1",
            session_id="session-1",
            goal="Test",
            tasks=tasks
        )

        # 初始状态
        progress = workflow.get_progress()
        assert progress["total"] == 3
        assert progress["completed"] == 0
        assert progress["failed"] == 0
        assert progress["running"] == 0
        assert progress["pending"] == 3

        # 一个成功，一个失败，一个运行中
        tasks[0].mark_success({"result": "ok"})
        tasks[1].mark_failed("Error")
        tasks[2].mark_running()

        progress = workflow.get_progress()
        assert progress["completed"] == 1
        assert progress["failed"] == 1
        assert progress["running"] == 1
        assert progress["pending"] == 0


class TestExecutionContext:
    """测试 ExecutionContext 模型"""

    def test_create_context(self):
        """测试创建执行上下文"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        assert ctx.workflow_id == "workflow-1"
        assert ctx.session_id == "session-1"
        assert ctx.task_results == {}
        assert ctx.metadata == {}

    def test_set_and_get_result(self):
        """测试设置和获取任务结果"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        result = {"data": "test"}
        ctx.set_result("task-1", result)

        retrieved = ctx.get_result("task-1")
        assert retrieved == result

        nonexistent = ctx.get_result("nonexistent")
        assert nonexistent is None

    def test_resolve_simple_variable(self):
        """测试解析简单变量"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        ctx.set_result("task-1", {"output": "test value"})

        resolved = ctx.resolve_variable("${task-1.output}")
        assert resolved == "test value"

    def test_resolve_nested_variable(self):
        """测试解析嵌套变量"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        ctx.set_result("task-1", {
            "output": {
                "nested": {
                    "field": "deep value"
                }
            }
        })

        resolved = ctx.resolve_variable("${task-1.output.nested.field}")
        assert resolved == "deep value"

    def test_resolve_non_variable_string(self):
        """测试解析非变量字符串"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        # 普通字符串直接返回
        resolved = ctx.resolve_variable("plain text")
        assert resolved == "plain text"

    def test_resolve_variable_task_not_found(self):
        """测试解析不存在的任务结果"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        with pytest.raises(ValueError, match="Task .* result not found"):
            ctx.resolve_variable("${nonexistent.output}")

    def test_resolve_variable_invalid_format(self):
        """测试无效的变量格式"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        with pytest.raises(ValueError, match="Invalid variable format"):
            ctx.resolve_variable("${invalid}")

    def test_resolve_params(self):
        """测试解析参数中的变量"""
        ctx = ExecutionContext(
            workflow_id="workflow-1",
            session_id="session-1"
        )

        ctx.set_result("task-1", {"output": "file.txt"})
        ctx.set_result("task-2", {"data": "content"})

        params = {
            "path": "${task-1.output}",
            "content": "${task-2.data}",
            "static": "value"
        }

        resolved = ctx.resolve_params(params)
        assert resolved["path"] == "file.txt"
        assert resolved["content"] == "content"
        assert resolved["static"] == "value"


class TestToolResult:
    """测试 ToolResult 模型"""

    def test_create_success_result(self):
        """测试创建成功结果"""
        result = ToolResult(
            success=True,
            data={"key": "value"},
            execution_time=1.5
        )

        assert result.success is True
        assert result.data == {"key": "value"}
        assert result.error is None
        assert result.execution_time == 1.5

    def test_create_error_result(self):
        """测试创建错误结果"""
        result = ToolResult(
            success=False,
            error="Tool failed"
        )

        assert result.success is False
        assert result.data is None
        assert result.error == "Tool failed"

    def test_tool_result_to_dict(self):
        """测试工具结果转换为字典"""
        result = ToolResult(
            success=True,
            data={"key": "value"},
            execution_time=2.0,
            metadata={"tool": "test"}
        )

        data = result.to_dict()
        assert data["success"] is True
        assert data["data"] == {"key": "value"}
        assert data["execution_time"] == 2.0
        assert data["metadata"] == {"tool": "test"}


class TestApprovalRequest:
    """测试 ApprovalRequest 模型"""

    def test_create_approval_request(self):
        """测试创建审批请求"""
        request = ApprovalRequest(
            approval_id="approval-1",
            workflow_id="workflow-1",
            task_id="task-1",
            task_name="Delete files",
            tool_name="mcp:filesystem:delete",
            params={"paths": ["/tmp/file.txt"]},
            reason="Dangerous operation"
        )

        assert request.approval_id == "approval-1"
        assert request.workflow_id == "workflow-1"
        assert request.task_id == "task-1"
        assert request.reason == "Dangerous operation"
        assert request.created_at is not None

    def test_approval_request_auto_generate_id(self):
        """测试审批请求自动生成ID"""
        request = ApprovalRequest(
            approval_id="",
            workflow_id="workflow-1",
            task_id="task-1",
            task_name="Test",
            tool_name="mcp:test",
            params={}
        )

        assert request.approval_id != ""
        assert len(request.approval_id) > 0

    def test_approval_request_to_dict(self):
        """测试审批请求转换为字典"""
        request = ApprovalRequest(
            approval_id="approval-1",
            workflow_id="workflow-1",
            task_id="task-1",
            task_name="Test",
            tool_name="mcp:test",
            params={"key": "value"}
        )

        data = request.to_dict()
        assert data["approval_id"] == "approval-1"
        assert data["workflow_id"] == "workflow-1"
        assert data["task_id"] == "task-1"
        assert data["params"] == {"key": "value"}


class TestApprovalResult:
    """测试 ApprovalResult 模型"""

    def test_create_approved_result(self):
        """测试创建批准结果"""
        result = ApprovalResult(
            approval_id="approval-1",
            approved=True,
            comment="Looks good"
        )

        assert result.approval_id == "approval-1"
        assert result.approved is True
        assert result.comment == "Looks good"
        assert result.responded_at is not None

    def test_create_rejected_result(self):
        """测试创建拒绝结果"""
        result = ApprovalResult(
            approval_id="approval-1",
            approved=False,
            comment="Too risky"
        )

        assert result.approved is False
        assert result.comment == "Too risky"

    def test_approval_result_to_dict(self):
        """测试审批结果转换为字典"""
        result = ApprovalResult(
            approval_id="approval-1",
            approved=True,
            comment="OK"
        )

        data = result.to_dict()
        assert data["approval_id"] == "approval-1"
        assert data["approved"] is True
        assert data["comment"] == "OK"
        assert data["responded_at"] is not None
