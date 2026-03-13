"""测试性能监控器"""
import pytest
import tempfile
import os
from datetime import datetime
from src.workflow.performance.monitor import PerformanceMonitor
from src.workflow.performance.storage import MetricsStorage


@pytest.mark.asyncio
class TestPerformanceMonitor:
    """测试性能监控器"""

    async def test_initialization(self) -> None:
        """测试初始化"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

            monitor = PerformanceMonitor(storage)
            assert monitor.storage == storage
            assert monitor.metrics_collector is not None
            assert monitor.trace_recorder is not None

            await storage.close()

    async def test_workflow_lifecycle(self) -> None:
        """测试工作流生命周期"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

            monitor = PerformanceMonitor(storage)

            # 开始工作流
            await monitor.on_workflow_start("wf-1", 3)

            # 任务执行
            await monitor.on_task_start("task-1", {
                "task_name": "Test Task 1",
                "tool_name": "llm:chat",
                "workflow_id": "wf-1"
            })
            await monitor.on_task_complete("task-1", 1.5, {
                "task_name": "Test Task 1",
                "tool_name": "llm:chat",
                "status": "completed",
                "retry_count": 0
            })

            # 完成工作流
            await monitor.on_workflow_complete("wf-1", {
                "status": "completed"
            })

            # 查询指标
            metrics = monitor.get_current_metrics("wf-1")
            assert metrics is not None
            assert metrics.workflow_id == "wf-1"
            assert metrics.completed_tasks == 1

            await storage.close()

    async def test_task_trace(self) -> None:
        """测试任务追踪"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

            monitor = PerformanceMonitor(storage)

            await monitor.on_workflow_start("wf-1", 1)

            task_info = {
                "task_name": "Test",
                "tool_name": "llm:chat",
                "workflow_id": "wf-1"
            }
            await monitor.on_task_start("task-1", task_info)

            # 模拟执行阶段
            await monitor.start_span("task-1", "tool_execution")
            await monitor.end_span("task-1", "tool_execution", {"tool": "llm:chat"})

            await monitor.on_task_complete("task-1", 1.0, {
                "task_name": "Test",
                "tool_name": "llm:chat",
                "status": "completed",
                "retry_count": 0
            })

            # 获取追踪
            trace = monitor.get_task_trace("task-1")
            assert trace is not None
            assert len(trace.spans) == 1
            assert trace.spans[0].name == "tool_execution"

            await storage.close()
