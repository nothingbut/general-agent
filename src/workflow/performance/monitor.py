"""性能监控器"""
import psutil
import os
from datetime import datetime
from typing import Dict, Any, Optional
from .collector import MetricsCollector, WorkflowMetrics, TaskMetrics
from .tracer import TraceRecorder, TaskTrace
from .storage import MetricsStorage


class PerformanceMonitor:
    """性能监控器 - 收集和分析执行指标"""

    def __init__(self, storage: MetricsStorage) -> None:
        """初始化监控器

        Args:
            storage: 指标存储实例
        """
        self.storage = storage
        self.metrics_collector = MetricsCollector()
        self.trace_recorder = TraceRecorder()
        self.process = psutil.Process(os.getpid())

    async def on_workflow_start(self, workflow_id: str, task_count: int) -> None:
        """工作流开始"""
        self.metrics_collector.start_workflow(workflow_id, task_count)

    async def on_workflow_complete(
        self,
        workflow_id: str,
        result: Dict[str, Any]
    ) -> None:
        """工作流完成"""
        self.metrics_collector.complete_workflow(workflow_id)

        # 获取资源指标
        metrics = self.metrics_collector.get_workflow_metrics(workflow_id)
        if metrics:
            # 获取内存和 CPU 使用
            memory_info = self.process.memory_info()
            metrics.peak_memory_mb = memory_info.rss / 1024 / 1024
            metrics.avg_cpu_percent = self.process.cpu_percent()

            # 保存到存储
            await self.storage.store_workflow_metrics(metrics)

    async def on_task_start(self, task_id: str, task_info: Dict[str, Any]) -> None:
        """任务开始"""
        workflow_id = task_info.get("workflow_id", "")
        await self.trace_recorder.start_trace(task_id, workflow_id)

    async def on_task_complete(
        self,
        task_id: str,
        duration: float,
        result: Dict[str, Any]
    ) -> None:
        """任务完成"""
        # 结束追踪
        await self.trace_recorder.end_trace(task_id)

        # 获取追踪并保存
        trace = self.trace_recorder.get_trace(task_id)
        if trace:
            await self.storage.store_trace(trace)

            # 创建任务指标
            task_metric = TaskMetrics(
                task_id=task_id,
                task_name=result.get("task_name", ""),
                tool_name=result.get("tool_name", ""),
                workflow_id=trace.workflow_id,
                started_at=datetime.now(),
                completed_at=datetime.now(),
                duration=duration,
                status=result.get("status", "unknown"),
                retry_count=result.get("retry_count", 0)
            )

            # 记录到收集器
            self.metrics_collector.record_task(task_metric)

            # 保存到存储
            await self.storage.store_task_metrics(task_metric)

    async def on_task_retry(
        self,
        task_id: str,
        retry_count: int,
        reason: str
    ) -> None:
        """任务重试"""
        pass

    def get_current_metrics(self, workflow_id: str) -> Optional[WorkflowMetrics]:
        """获取当前指标"""
        return self.metrics_collector.get_workflow_metrics(workflow_id)

    def get_task_trace(self, task_id: str) -> Optional[TaskTrace]:
        """获取任务追踪"""
        return self.trace_recorder.get_trace(task_id)

    def get_workflow_summary(self, workflow_id: str) -> Dict[str, Any]:
        """获取工作流摘要"""
        metrics = self.metrics_collector.get_workflow_metrics(workflow_id)
        if not metrics:
            return {}

        return metrics.to_dict()

    async def start_span(self, task_id: str, span_name: str) -> None:
        """开始执行阶段"""
        await self.trace_recorder.start_span(task_id, span_name)

    async def end_span(
        self,
        task_id: str,
        span_name: str,
        metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """结束执行阶段"""
        await self.trace_recorder.end_span(task_id, span_name, metadata)
