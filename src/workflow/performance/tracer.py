"""链路追踪器"""

import asyncio
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional


@dataclass
class Span:
    """执行阶段

    Attributes:
        name: 阶段名称（如 "tool_execution", "dependency_check"）
        started_at: 阶段开始时间
        duration: 执行时长（秒）
        metadata: 附加元数据（如工具名称、参数等）
    """

    name: str
    started_at: datetime
    duration: float
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "name": self.name,
            "started_at": self.started_at.isoformat(),
            "duration": self.duration,
            "metadata": self.metadata,
        }


@dataclass
class TaskTrace:
    """任务执行追踪

    Attributes:
        task_id: 任务ID
        workflow_id: 工作流ID
        total_duration: 总执行时长（秒）
        spans: 执行阶段列表
    """

    task_id: str
    workflow_id: str
    total_duration: float
    spans: List[Span] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "task_id": self.task_id,
            "workflow_id": self.workflow_id,
            "total_duration": self.total_duration,
            "spans": [span.to_dict() for span in self.spans],
        }


class TraceRecorder:
    """追踪记录器（线程安全，带内存管理）"""

    def __init__(self, max_traces: int = 1000) -> None:
        """初始化追踪记录器

        Args:
            max_traces: 最大保留追踪数量（LRU策略）
        """
        self.traces: Dict[str, TaskTrace] = {}
        self.active_spans: Dict[str, Dict[str, datetime]] = {}
        self.trace_start_times: Dict[str, datetime] = {}
        self.max_traces = max_traces
        self._trace_order: deque = deque(maxlen=max_traces)
        self._lock = asyncio.Lock()

    async def start_trace(self, task_id: str, workflow_id: str) -> None:
        """开始追踪任务（线程安全）"""
        async with self._lock:
            self.traces[task_id] = TaskTrace(
                task_id=task_id,
                workflow_id=workflow_id,
                total_duration=0.0,
                spans=[],
            )
            self.active_spans[task_id] = {}
            self.trace_start_times[task_id] = datetime.now()

    async def start_span(self, task_id: str, span_name: str) -> None:
        """开始一个执行阶段（线程安全）"""
        async with self._lock:
            if task_id not in self.active_spans:
                return

            self.active_spans[task_id][span_name] = datetime.now()

    async def end_span(
        self, task_id: str, span_name: str, metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """结束一个执行阶段（线程安全）"""
        async with self._lock:
            if (
                task_id not in self.active_spans
                or span_name not in self.active_spans[task_id]
            ):
                return

            start_time = self.active_spans[task_id].pop(span_name)
            end_time = datetime.now()
            duration = (end_time - start_time).total_seconds()

            span = Span(
                name=span_name,
                started_at=start_time,
                duration=duration,
                metadata=metadata or {},
            )

            if task_id in self.traces:
                self.traces[task_id].spans.append(span)

    async def end_trace(self, task_id: str) -> None:
        """结束追踪（线程安全，带LRU清理）"""
        async with self._lock:
            if task_id not in self.trace_start_times:
                return

            start_time = self.trace_start_times.pop(task_id)
            end_time = datetime.now()
            total_duration = (end_time - start_time).total_seconds()

            if task_id in self.traces:
                self.traces[task_id].total_duration = total_duration

            if task_id in self.active_spans:
                del self.active_spans[task_id]

            # LRU 清理
            self._trace_order.append(task_id)
            if len(self.traces) > self.max_traces:
                # 移除最旧的追踪
                oldest_id = self._trace_order[0]
                if oldest_id != task_id and oldest_id in self.traces:
                    del self.traces[oldest_id]

    def get_trace(self, task_id: str) -> Optional[TaskTrace]:
        """获取追踪数据（读操作不需要锁）"""
        return self.traces.get(task_id)

    def get_all_traces(self, workflow_id: str) -> List[TaskTrace]:
        """获取工作流的所有追踪（读操作不需要锁）"""
        return [
            trace for trace in self.traces.values() if trace.workflow_id == workflow_id
        ]

    async def clear_traces(self, workflow_id: Optional[str] = None) -> None:
        """清理追踪数据（线程安全）

        Args:
            workflow_id: 如果指定，只清理该工作流的追踪；否则清理所有
        """
        async with self._lock:
            if workflow_id:
                to_remove = [
                    tid
                    for tid, trace in self.traces.items()
                    if trace.workflow_id == workflow_id
                ]
                for tid in to_remove:
                    del self.traces[tid]
            else:
                self.traces.clear()
                self.active_spans.clear()
                self.trace_start_times.clear()
                self._trace_order.clear()
