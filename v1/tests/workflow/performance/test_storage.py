"""测试指标存储"""
import pytest
import tempfile
import os
from datetime import datetime
from src.workflow.performance.storage import MetricsStorage, InMemoryBuffer
from src.workflow.performance.collector import WorkflowMetrics, TaskMetrics


class TestInMemoryBuffer:
    """测试内存缓冲"""

    def test_store_and_get(self) -> None:
        """测试存储和获取"""
        buffer = InMemoryBuffer(max_age=3600)

        data = {"key": "value"}
        buffer.store("test-1", data)

        result = buffer.get("test-1")
        assert result == data

    def test_get_missing_key(self) -> None:
        """测试获取不存在的键"""
        buffer = InMemoryBuffer()
        result = buffer.get("missing")
        assert result is None


@pytest.mark.asyncio
class TestMetricsStorage:
    """测试指标存储"""

    async def test_initialize(self) -> None:
        """测试初始化"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()
            await storage.close()

    async def test_store_workflow_metrics(self) -> None:
        """测试存储工作流指标"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

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
                db_avg_query_time=0.0
            )

            await storage.store_workflow_metrics(metrics)

            result = storage.query_workflow_metrics("wf-1")
            assert result is not None
            assert result.workflow_id == "wf-1"
            assert result.total_tasks == 10

            await storage.close()

    async def test_store_task_metrics(self) -> None:
        """测试存储任务指标"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

            metrics = TaskMetrics(
                task_id="task-1",
                task_name="Test",
                tool_name="llm:chat",
                workflow_id="wf-1",
                started_at=datetime.now(),
                completed_at=datetime.now(),
                duration=1.5,
                status="success",
                retry_count=0
            )

            await storage.store_task_metrics(metrics)

            result = storage.query_task_metrics("task-1")
            assert result is not None
            assert result.task_id == "task-1"

            await storage.close()
