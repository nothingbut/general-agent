"""指标收集器实现"""

from dataclasses import dataclass, asdict
from datetime import datetime
from typing import Optional, List, Dict, Any
import statistics


# 常量定义
BYTES_PER_MB = 1024 * 1024  # 1 MB = 1024 * 1024 bytes


@dataclass
class WorkflowMetrics:
    """工作流级别指标"""

    workflow_id: str
    total_tasks: int
    completed_tasks: int
    failed_tasks: int
    cancelled_tasks: int
    started_at: datetime
    completed_at: Optional[datetime]
    total_duration: float
    throughput: float
    avg_task_duration: float
    p50_task_duration: float
    p95_task_duration: float
    p99_task_duration: float
    peak_memory_mb: float
    avg_cpu_percent: float
    db_query_count: int
    db_total_time: float
    db_avg_query_time: float

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        data = asdict(self)
        # 转换 datetime 对象为 ISO 格式字符串
        data["started_at"] = self.started_at.isoformat()
        data["completed_at"] = (
            self.completed_at.isoformat() if self.completed_at else None
        )
        return data


@dataclass
class TaskMetrics:
    """任务级别指标"""

    task_id: str
    task_name: str
    tool_name: str
    workflow_id: str
    started_at: datetime
    completed_at: Optional[datetime]
    duration: float
    status: str
    retry_count: int
    memory_used: Optional[int] = None
    cpu_time: Optional[float] = None

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "task_id": self.task_id,
            "task_name": self.task_name,
            "tool_name": self.tool_name,
            "workflow_id": self.workflow_id,
            "started_at": self.started_at.isoformat(),
            "completed_at": (
                self.completed_at.isoformat() if self.completed_at else None
            ),
            "duration": self.duration,
            "status": self.status,
            "retry_count": self.retry_count,
            "memory_used": self.memory_used,
            "cpu_time": self.cpu_time,
        }


class MetricsCollector:
    """指标收集器"""

    def __init__(self) -> None:
        """初始化收集器"""
        self._workflow_metrics: Dict[str, WorkflowMetrics] = {}
        self._task_metrics: Dict[str, List[TaskMetrics]] = {}

    def start_workflow(self, workflow_id: str, total_tasks: int) -> None:
        """开始工作流

        Args:
            workflow_id: 工作流ID
            total_tasks: 总任务数
        """
        metrics = WorkflowMetrics(
            workflow_id=workflow_id,
            total_tasks=total_tasks,
            completed_tasks=0,
            failed_tasks=0,
            cancelled_tasks=0,
            started_at=datetime.now(),
            completed_at=None,
            total_duration=0.0,
            throughput=0.0,
            avg_task_duration=0.0,
            p50_task_duration=0.0,
            p95_task_duration=0.0,
            p99_task_duration=0.0,
            peak_memory_mb=0.0,
            avg_cpu_percent=0.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        )
        self._workflow_metrics[workflow_id] = metrics
        self._task_metrics[workflow_id] = []

    def record_task(self, task_metric: TaskMetrics) -> None:
        """记录任务指标

        Args:
            task_metric: 任务指标
        """
        workflow_id = task_metric.workflow_id
        if workflow_id not in self._task_metrics:
            self._task_metrics[workflow_id] = []
        self._task_metrics[workflow_id].append(task_metric)

    def complete_workflow(self, workflow_id: str) -> None:
        """完成工作流（计算统计信息）

        Args:
            workflow_id: 工作流ID
        """
        if workflow_id not in self._workflow_metrics:
            raise ValueError(f"Workflow {workflow_id} not found")

        metrics = self._workflow_metrics[workflow_id]
        task_metrics = self._task_metrics.get(workflow_id, [])

        # 计算完成时间和总时长
        metrics.completed_at = datetime.now()
        metrics.total_duration = (
            metrics.completed_at - metrics.started_at
        ).total_seconds()

        # 更新完成的任务数
        metrics.completed_tasks = len(
            [t for t in task_metrics if t.status == "completed"]
        )
        metrics.failed_tasks = len([t for t in task_metrics if t.status == "failed"])
        metrics.cancelled_tasks = len(
            [t for t in task_metrics if t.status == "cancelled"]
        )

        # 计算吞吐量（任务/秒）
        if metrics.total_duration > 0:
            metrics.throughput = metrics.completed_tasks / metrics.total_duration

        if task_metrics:
            # 计算任务时长统计
            durations = [t.duration for t in task_metrics]
            metrics.avg_task_duration = sum(durations) / len(durations)

            # 计算分位数
            sorted_durations = sorted(durations)
            metrics.p50_task_duration = self._calculate_percentile(sorted_durations, 50)
            metrics.p95_task_duration = self._calculate_percentile(sorted_durations, 95)
            metrics.p99_task_duration = self._calculate_percentile(sorted_durations, 99)

            # 计算内存峰值（从字节转换为MB）
            memory_values = [
                t.memory_used for t in task_metrics if t.memory_used is not None
            ]
            if memory_values:
                metrics.peak_memory_mb = max(memory_values) / BYTES_PER_MB

            # 计算平均 CPU 时间
            cpu_values = [t.cpu_time for t in task_metrics if t.cpu_time is not None]
            if cpu_values:
                metrics.avg_cpu_percent = sum(cpu_values) / len(cpu_values)

            # 计算数据库查询统计（保留为0，因为TaskMetrics不再包含这些字段）
            metrics.db_query_count = 0
            metrics.db_total_time = 0.0
            metrics.db_avg_query_time = 0.0

    def get_workflow_metrics(self, workflow_id: str) -> Optional[WorkflowMetrics]:
        """获取工作流指标

        Args:
            workflow_id: 工作流ID

        Returns:
            工作流指标，如果不存在则返回 None
        """
        return self._workflow_metrics.get(workflow_id)

    def get_task_metrics(self, workflow_id: str) -> List[TaskMetrics]:
        """获取任务指标列表

        Args:
            workflow_id: 工作流ID

        Returns:
            任务指标列表
        """
        return self._task_metrics.get(workflow_id, [])

    @staticmethod
    def _calculate_percentile(sorted_values: List[float], percentile: int) -> float:
        """计算分位数

        Args:
            sorted_values: 已排序的数值列表
            percentile: 分位数（0-100）

        Returns:
            分位数值
        """
        if not sorted_values:
            return 0.0

        if percentile == 50:
            # P50 使用中位数
            return statistics.median(sorted_values)

        # 其他分位数使用线性插值
        index = (percentile / 100) * (len(sorted_values) - 1)
        lower_index = int(index)
        upper_index = lower_index + 1

        if upper_index >= len(sorted_values):
            return sorted_values[-1]

        # 线性插值
        lower_value = sorted_values[lower_index]
        upper_value = sorted_values[upper_index]
        fraction = index - lower_index

        return lower_value + (upper_value - lower_value) * fraction
