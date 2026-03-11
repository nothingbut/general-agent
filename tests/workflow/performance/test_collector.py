"""测试指标收集器"""

from datetime import datetime
from src.workflow.performance.collector import (
    WorkflowMetrics,
    TaskMetrics,
    MetricsCollector,
)


class TestWorkflowMetrics:
    """测试工作流指标"""

    def test_create_workflow_metrics(self) -> None:
        """测试创建工作流指标"""
        metrics = WorkflowMetrics(
            workflow_id="wf-1",
            total_tasks=10,
            completed_tasks=5,
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
        assert metrics.workflow_id == "wf-1"
        assert metrics.total_tasks == 10
        assert metrics.completed_tasks == 5


class TestTaskMetrics:
    """测试任务指标"""

    def test_create_task_metrics(self) -> None:
        """测试创建任务指标"""
        metrics = TaskMetrics(
            task_id="task-1",
            task_name="Test Task",
            tool_name="llm:chat",
            workflow_id="wf-1",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=1.5,
            status="completed",
            retry_count=0,
            memory_used=104857600,
            cpu_time=0.5,
        )
        assert metrics.task_id == "task-1"
        assert metrics.task_name == "Test Task"
        assert metrics.tool_name == "llm:chat"
        assert metrics.workflow_id == "wf-1"
        assert metrics.duration == 1.5
        assert metrics.retry_count == 0


class TestMetricsCollector:
    """测试指标收集器"""

    def test_start_workflow(self) -> None:
        """测试开始工作流"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", total_tasks=10)

        metrics = collector.get_workflow_metrics("wf-1")
        assert metrics is not None
        assert metrics.workflow_id == "wf-1"
        assert metrics.total_tasks == 10
        assert metrics.completed_tasks == 0
        assert metrics.started_at is not None
        assert metrics.completed_at is None

    def test_record_task(self) -> None:
        """测试记录任务"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", total_tasks=5)

        task_metric = TaskMetrics(
            task_id="task-1",
            task_name="Test Task",
            tool_name="llm:chat",
            workflow_id="wf-1",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=1.5,
            status="completed",
            retry_count=0,
            memory_used=104857600,
            cpu_time=0.5,
        )
        collector.record_task(task_metric)

        task_metrics = collector.get_task_metrics("wf-1")
        assert len(task_metrics) == 1
        assert task_metrics[0].task_id == "task-1"

    def test_complete_workflow(self) -> None:
        """测试完成工作流（验证统计计算）"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", total_tasks=10)

        # 记录多个任务以测试统计计算
        durations = [1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 10.0]
        for i, duration in enumerate(durations):
            task_metric = TaskMetrics(
                task_id=f"task-{i}",
                task_name=f"Test Task {i}",
                tool_name="llm:chat",
                workflow_id="wf-1",
                started_at=datetime.now(),
                completed_at=datetime.now(),
                duration=duration,
                status="completed",
                retry_count=0,
                memory_used=(100 + i * 10) * 1024 * 1024,  # 转换为字节
                cpu_time=0.5,
            )
            collector.record_task(task_metric)

        collector.complete_workflow("wf-1")

        metrics = collector.get_workflow_metrics("wf-1")
        assert metrics is not None
        assert metrics.completed_at is not None
        assert metrics.completed_tasks == 10
        assert metrics.total_duration > 0
        assert metrics.throughput > 0

        # 验证平均值
        assert metrics.avg_task_duration == sum(durations) / len(durations)

        # 验证分位数（P50 应该是中位数）
        # 对于 [1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 10.0]
        # 中位数是第5和第6个元素的平均值: (3.0 + 3.5) / 2 = 3.25
        assert metrics.p50_task_duration == 3.25  # 中位数
        assert metrics.p95_task_duration > metrics.p50_task_duration
        assert metrics.p99_task_duration > metrics.p95_task_duration

        # 验证内存峰值（最后一个任务：190 MB）
        assert metrics.peak_memory_mb == 190.0

    def test_to_dict_methods(self) -> None:
        """测试所有类的 to_dict() 方法"""
        # 测试 WorkflowMetrics.to_dict()
        workflow_metrics = WorkflowMetrics(
            workflow_id="wf-1",
            total_tasks=10,
            completed_tasks=5,
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
        wf_dict = workflow_metrics.to_dict()
        assert isinstance(wf_dict, dict)
        assert wf_dict["workflow_id"] == "wf-1"
        assert wf_dict["total_tasks"] == 10

        # 测试 TaskMetrics.to_dict()
        task_metrics = TaskMetrics(
            task_id="task-1",
            task_name="Test Task",
            tool_name="llm:chat",
            workflow_id="wf-1",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=1.5,
            status="completed",
            retry_count=0,
            memory_used=104857600,
            cpu_time=0.5,
        )
        task_dict = task_metrics.to_dict()
        assert isinstance(task_dict, dict)
        assert task_dict["task_id"] == "task-1"
        assert task_dict["task_name"] == "Test Task"
        assert task_dict["tool_name"] == "llm:chat"
        assert task_dict["duration"] == 1.5
        assert task_dict["retry_count"] == 0
        assert task_dict["memory_used"] == 104857600
        assert task_dict["cpu_time"] == 0.5
