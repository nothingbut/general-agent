"""Workflow 数据模型定义"""
from dataclasses import dataclass, field, asdict
from datetime import datetime
from enum import Enum
from typing import List, Dict, Any, Optional
import uuid


class TaskStatus(Enum):
    """任务执行状态"""
    PENDING = "pending"  # 等待执行
    RUNNING = "running"  # 执行中
    SUCCESS = "success"  # 执行成功
    FAILED = "failed"    # 执行失败
    SKIPPED = "skipped"  # 已跳过
    CANCELLED = "cancelled"  # 已取消

    def __str__(self) -> str:
        return self.value


class WorkflowStatus(Enum):
    """工作流执行状态"""
    PENDING = "pending"  # 等待执行
    RUNNING = "running"  # 执行中
    WAITING_APPROVAL = "waiting_approval"  # 等待审批
    COMPLETED = "completed"  # 已完成
    FAILED = "failed"  # 执行失败
    CANCELLED = "cancelled"  # 已取消

    def __str__(self) -> str:
        return self.value


@dataclass
class Task:
    """任务模型 - 工作流中的单个任务"""
    id: str
    name: str
    tool: str  # 格式：mcp:server:tool, skill:name, rag:method, llm:method
    params: Dict[str, Any]
    dependencies: List[str] = field(default_factory=list)  # 依赖的任务ID列表
    requires_approval: bool = False  # 是否需要用户审批
    approval_reason: Optional[str] = None  # 审批原因说明
    status: TaskStatus = TaskStatus.PENDING
    result: Optional[Any] = None  # 执行结果
    error: Optional[str] = None  # 错误信息
    retry_count: int = 0  # 重试次数
    max_retries: int = 3  # 最大重试次数
    timeout: Optional[int] = None  # 超时时间（秒）

    def __post_init__(self):
        """验证任务配置"""
        if not self.id:
            raise ValueError("Task ID cannot be empty")
        if not self.name:
            raise ValueError("Task name cannot be empty")
        if not self.tool:
            raise ValueError("Task tool cannot be empty")

        # 验证工具格式
        if ":" not in self.tool:
            raise ValueError(f"Invalid tool format: {self.tool}. Expected format: type:name or type:server:tool")

        # 确保 status 是 TaskStatus 枚举
        if isinstance(self.status, str):
            self.status = TaskStatus(self.status)

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        data = asdict(self)
        data['status'] = self.status.value
        return data

    def is_ready(self, completed_tasks: set[str]) -> bool:
        """检查任务是否准备好执行（所有依赖已完成）"""
        if self.status != TaskStatus.PENDING:
            return False
        return all(dep_id in completed_tasks for dep_id in self.dependencies)

    def can_retry(self) -> bool:
        """检查任务是否可以重试"""
        return self.status == TaskStatus.FAILED and self.retry_count < self.max_retries

    def mark_running(self) -> None:
        """标记任务为运行中"""
        self.status = TaskStatus.RUNNING

    def mark_success(self, result: Any) -> None:
        """标记任务为成功"""
        self.status = TaskStatus.SUCCESS
        self.result = result
        self.error = None

    def mark_failed(self, error: str) -> None:
        """标记任务为失败"""
        self.status = TaskStatus.FAILED
        self.error = error

    def mark_skipped(self) -> None:
        """标记任务为跳过"""
        self.status = TaskStatus.SKIPPED


@dataclass
class Workflow:
    """工作流模型 - 包含多个任务的执行计划"""
    id: str
    session_id: str
    goal: str  # 工作流目标描述
    tasks: List[Task]
    status: WorkflowStatus = WorkflowStatus.PENDING
    current_task_id: Optional[str] = None  # 当前执行的任务ID
    created_at: Optional[datetime] = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    metadata: Optional[Dict[str, Any]] = None

    def __post_init__(self):
        """初始化和验证"""
        if not self.id:
            self.id = str(uuid.uuid4())
        if not self.created_at:
            self.created_at = datetime.now()

        # 确保 status 是 WorkflowStatus 枚举
        if isinstance(self.status, str):
            self.status = WorkflowStatus(self.status)

        # 验证任务依赖关系
        self._validate_dependencies()

    def _validate_dependencies(self) -> None:
        """验证任务依赖关系（检测循环依赖）"""
        task_ids = {task.id for task in self.tasks}

        # 检查所有依赖的任务是否存在
        for task in self.tasks:
            for dep_id in task.dependencies:
                if dep_id not in task_ids:
                    raise ValueError(f"Task {task.id} depends on non-existent task {dep_id}")

        # 检测循环依赖（简单 DFS）
        visited = set()
        rec_stack = set()

        def has_cycle(task_id: str) -> bool:
            visited.add(task_id)
            rec_stack.add(task_id)

            task = next((t for t in self.tasks if t.id == task_id), None)
            if task:
                for dep_id in task.dependencies:
                    if dep_id not in visited:
                        if has_cycle(dep_id):
                            return True
                    elif dep_id in rec_stack:
                        return True

            rec_stack.remove(task_id)
            return False

        for task in self.tasks:
            if task.id not in visited:
                if has_cycle(task.id):
                    raise ValueError(f"Circular dependency detected in workflow {self.id}")

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "id": self.id,
            "session_id": self.session_id,
            "goal": self.goal,
            "tasks": [task.to_dict() for task in self.tasks],
            "status": self.status.value,
            "current_task_id": self.current_task_id,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "started_at": self.started_at.isoformat() if self.started_at else None,
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "metadata": self.metadata
        }

    def get_plan_dict(self) -> Dict[str, Any]:
        """获取计划字典（用于数据库存储）"""
        return {
            "goal": self.goal,
            "tasks": [task.to_dict() for task in self.tasks]
        }

    def get_task_by_id(self, task_id: str) -> Optional[Task]:
        """根据ID获取任务"""
        return next((task for task in self.tasks if task.id == task_id), None)

    def get_ready_tasks(self) -> List[Task]:
        """获取所有准备好执行的任务（依赖已满足）"""
        completed_tasks = {
            task.id for task in self.tasks
            if task.status in (TaskStatus.SUCCESS, TaskStatus.SKIPPED)
        }
        return [
            task for task in self.tasks
            if task.is_ready(completed_tasks)
        ]

    def get_pending_approval_tasks(self) -> List[Task]:
        """获取所有需要审批的任务"""
        return [
            task for task in self.tasks
            if task.status == TaskStatus.PENDING and task.requires_approval
        ]

    def get_failed_tasks(self) -> List[Task]:
        """获取所有失败的任务"""
        return [task for task in self.tasks if task.status == TaskStatus.FAILED]

    def is_completed(self) -> bool:
        """检查工作流是否完成"""
        return all(
            task.status in (TaskStatus.SUCCESS, TaskStatus.SKIPPED, TaskStatus.CANCELLED)
            for task in self.tasks
        )

    def has_failed_tasks(self) -> bool:
        """检查是否有失败的任务"""
        return any(task.status == TaskStatus.FAILED for task in self.tasks)

    def get_progress(self) -> Dict[str, int]:
        """获取执行进度"""
        total = len(self.tasks)
        completed = sum(
            1 for task in self.tasks
            if task.status in (TaskStatus.SUCCESS, TaskStatus.SKIPPED)
        )
        failed = sum(1 for task in self.tasks if task.status == TaskStatus.FAILED)
        running = sum(1 for task in self.tasks if task.status == TaskStatus.RUNNING)

        return {
            "total": total,
            "completed": completed,
            "failed": failed,
            "running": running,
            "pending": total - completed - failed - running
        }


@dataclass
class ExecutionContext:
    """执行上下文 - 用于在任务间传递数据"""
    workflow_id: str
    session_id: str
    task_results: Dict[str, Any] = field(default_factory=dict)  # 任务ID -> 结果映射
    metadata: Dict[str, Any] = field(default_factory=dict)  # 额外的上下文数据

    def set_result(self, task_id: str, result: Any) -> None:
        """保存任务结果"""
        self.task_results[task_id] = result

    def get_result(self, task_id: str) -> Optional[Any]:
        """获取任务结果"""
        return self.task_results.get(task_id)

    def resolve_variable(self, variable: str) -> Any:
        """解析变量引用

        支持格式：
        - ${task-1.output}
        - ${task-1.output.field}
        - ${task-1.output.nested.field}
        """
        if not variable.startswith("${") or not variable.endswith("}"):
            return variable

        # 移除 ${ 和 }
        var_path = variable[2:-1]
        parts = var_path.split(".")

        if len(parts) < 2:
            raise ValueError(f"Invalid variable format: {variable}")

        task_id = parts[0]
        field_path = parts[1:]

        # 获取任务结果
        result = self.get_result(task_id)
        if result is None:
            raise ValueError(f"Task {task_id} result not found")

        # 遍历字段路径
        current = result
        for fld in field_path:
            if isinstance(current, dict):
                current = current.get(fld)
            elif hasattr(current, field):
                current = getattr(current, field)
            else:
                raise ValueError(f"Field {field} not found in {task_id} result")

            if current is None:
                break

        return current

    def resolve_params(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """解析参数中的变量引用"""
        resolved = {}
        for key, value in params.items():
            if isinstance(value, str):
                resolved[key] = self.resolve_variable(value)
            elif isinstance(value, dict):
                resolved[key] = self.resolve_params(value)
            elif isinstance(value, list):
                resolved[key] = [
                    self.resolve_variable(v) if isinstance(v, str) else v
                    for v in value
                ]
            else:
                resolved[key] = value
        return resolved


@dataclass
class ToolResult:
    """工具执行结果"""
    success: bool
    data: Optional[Any] = None
    error: Optional[str] = None
    execution_time: Optional[float] = None  # 执行时间（秒）
    metadata: Optional[Dict[str, Any]] = None

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "success": self.success,
            "data": self.data,
            "error": self.error,
            "execution_time": self.execution_time,
            "metadata": self.metadata
        }


@dataclass
class ApprovalRequest:
    """审批请求"""
    approval_id: str
    workflow_id: str
    task_id: str
    task_name: str
    tool_name: str
    params: Dict[str, Any]
    reason: Optional[str] = None
    created_at: Optional[datetime] = None

    def __post_init__(self):
        if not self.approval_id:
            self.approval_id = str(uuid.uuid4())
        if not self.created_at:
            self.created_at = datetime.now()

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "approval_id": self.approval_id,
            "workflow_id": self.workflow_id,
            "task_id": self.task_id,
            "task_name": self.task_name,
            "tool_name": self.tool_name,
            "params": self.params,
            "reason": self.reason,
            "created_at": self.created_at.isoformat() if self.created_at else None
        }


@dataclass
class ApprovalResult:
    """审批结果"""
    approval_id: str
    approved: bool  # True=批准, False=拒绝
    comment: Optional[str] = None
    responded_at: Optional[datetime] = None

    def __post_init__(self):
        if not self.responded_at:
            self.responded_at = datetime.now()

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "approval_id": self.approval_id,
            "approved": self.approved,
            "comment": self.comment,
            "responded_at": self.responded_at.isoformat() if self.responded_at else None
        }
