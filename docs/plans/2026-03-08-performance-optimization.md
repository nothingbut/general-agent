# Performance Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a comprehensive performance monitoring and optimization system for the workflow module with full observability, benchmarking, and targeted optimizations.

**Architecture:** Observer pattern for monitoring integration, two-tier storage (memory buffer + SQLite), Rich-based TUI dashboard, automated benchmark suite with 6 scenarios, and two quick-win optimizations (batch DB writes + result memory management).

**Tech Stack:** Python 3.11+, asyncio, Rich (TUI), SQLite, pytest, dataclasses

---

## Task 7.3.4.1: 实现性能监控框架

### Part 1: MetricsCollector (指标收集器)

**Files:**
- Create: `src/workflow/performance/__init__.py`
- Create: `src/workflow/performance/collector.py`
- Create: `tests/workflow/performance/__init__.py`
- Create: `tests/workflow/performance/test_collector.py`

**Step 1: Write the failing test for WorkflowMetrics**

Create `tests/workflow/performance/test_collector.py`:

```python
"""测试指标收集器"""
import pytest
from datetime import datetime
from src.workflow.performance.collector import (
    WorkflowMetrics,
    TaskMetrics,
    MetricsCollector
)


class TestWorkflowMetrics:
    """测试工作流指标"""

    def test_create_workflow_metrics(self):
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
            db_avg_query_time=0.0
        )
        assert metrics.workflow_id == "wf-1"
        assert metrics.total_tasks == 10
        assert metrics.completed_tasks == 5
```

**Step 2: Run test to verify it fails**

```bash
pytest tests/workflow/performance/test_collector.py::TestWorkflowMetrics::test_create_workflow_metrics -v
```

Expected: FAIL with "ModuleNotFoundError: No module named 'src.workflow.performance'"

**Step 3: Write minimal implementation**

Create `src/workflow/performance/__init__.py`:

```python
"""性能监控模块"""
from .collector import WorkflowMetrics, TaskMetrics, MetricsCollector
from .monitor import PerformanceMonitor

__all__ = [
    "WorkflowMetrics",
    "TaskMetrics",
    "MetricsCollector",
    "PerformanceMonitor"
]
```

Create `src/workflow/performance/collector.py`:

```python
"""指标收集器"""
from dataclasses import dataclass, field
from datetime import datetime
from typing import List, Optional, Dict, Any
import statistics


@dataclass
class WorkflowMetrics:
    """工作流级别指标"""
    workflow_id: str
    total_tasks: int
    completed_tasks: int
    failed_tasks: int
    cancelled_tasks: int

    # 时间指标
    started_at: datetime
    completed_at: Optional[datetime]
    total_duration: float

    # 性能指标
    throughput: float  # 任务/秒
    avg_task_duration: float
    p50_task_duration: float
    p95_task_duration: float
    p99_task_duration: float

    # 资源指标
    peak_memory_mb: float
    avg_cpu_percent: float

    # 数据库指标
    db_query_count: int
    db_total_time: float
    db_avg_query_time: float

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "workflow_id": self.workflow_id,
            "total_tasks": self.total_tasks,
            "completed_tasks": self.completed_tasks,
            "failed_tasks": self.failed_tasks,
            "cancelled_tasks": self.cancelled_tasks,
            "started_at": self.started_at.isoformat(),
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "total_duration": self.total_duration,
            "throughput": self.throughput,
            "avg_task_duration": self.avg_task_duration,
            "p50_task_duration": self.p50_task_duration,
            "p95_task_duration": self.p95_task_duration,
            "p99_task_duration": self.p99_task_duration,
            "peak_memory_mb": self.peak_memory_mb,
            "avg_cpu_percent": self.avg_cpu_percent,
            "db_query_count": self.db_query_count,
            "db_total_time": self.db_total_time,
            "db_avg_query_time": self.db_avg_query_time
        }


@dataclass
class TaskMetrics:
    """任务级别指标"""
    task_id: str
    task_name: str
    tool_name: str
    workflow_id: str

    # 时间指标
    started_at: datetime
    completed_at: Optional[datetime]
    duration: float

    # 状态指标
    status: str
    retry_count: int

    # 资源指标
    memory_used: Optional[int] = None  # 字节
    cpu_time: Optional[float] = None  # CPU 时间（秒）

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "task_id": self.task_id,
            "task_name": self.task_name,
            "tool_name": self.tool_name,
            "workflow_id": self.workflow_id,
            "started_at": self.started_at.isoformat(),
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "duration": self.duration,
            "status": self.status,
            "retry_count": self.retry_count,
            "memory_used": self.memory_used,
            "cpu_time": self.cpu_time
        }


class MetricsCollector:
    """指标收集器"""

    def __init__(self):
        self.workflow_metrics: Dict[str, WorkflowMetrics] = {}
        self.task_metrics: Dict[str, List[TaskMetrics]] = {}  # workflow_id -> tasks

    def start_workflow(self, workflow_id: str, total_tasks: int) -> None:
        """开始工作流"""
        self.workflow_metrics[workflow_id] = WorkflowMetrics(
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
            db_avg_query_time=0.0
        )
        self.task_metrics[workflow_id] = []

    def complete_workflow(self, workflow_id: str) -> None:
        """完成工作流"""
        if workflow_id not in self.workflow_metrics:
            return

        metrics = self.workflow_metrics[workflow_id]
        metrics.completed_at = datetime.now()
        metrics.total_duration = (
            metrics.completed_at - metrics.started_at
        ).total_seconds()

        # 计算吞吐量
        if metrics.total_duration > 0:
            metrics.throughput = metrics.completed_tasks / metrics.total_duration

        # 计算任务时长分布
        task_durations = [
            t.duration for t in self.task_metrics[workflow_id]
            if t.completed_at is not None
        ]
        if task_durations:
            metrics.avg_task_duration = statistics.mean(task_durations)
            metrics.p50_task_duration = statistics.median(task_durations)
            sorted_durations = sorted(task_durations)
            metrics.p95_task_duration = sorted_durations[int(len(sorted_durations) * 0.95)]
            metrics.p99_task_duration = sorted_durations[int(len(sorted_durations) * 0.99)]

    def record_task(self, task_metric: TaskMetrics) -> None:
        """记录任务指标"""
        workflow_id = task_metric.workflow_id
        if workflow_id not in self.task_metrics:
            self.task_metrics[workflow_id] = []

        self.task_metrics[workflow_id].append(task_metric)

        # 更新工作流统计
        if workflow_id in self.workflow_metrics:
            metrics = self.workflow_metrics[workflow_id]
            if task_metric.status == "success":
                metrics.completed_tasks += 1
            elif task_metric.status == "failed":
                metrics.failed_tasks += 1
            elif task_metric.status == "cancelled":
                metrics.cancelled_tasks += 1

    def get_workflow_metrics(self, workflow_id: str) -> Optional[WorkflowMetrics]:
        """获取工作流指标"""
        return self.workflow_metrics.get(workflow_id)

    def get_task_metrics(self, workflow_id: str) -> List[TaskMetrics]:
        """获取任务指标"""
        return self.task_metrics.get(workflow_id, [])
```

Create `tests/workflow/performance/__init__.py` (empty file):

```python
"""性能测试模块"""
```

**Step 4: Run test to verify it passes**

```bash
pytest tests/workflow/performance/test_collector.py::TestWorkflowMetrics::test_create_workflow_metrics -v
```

Expected: PASS

**Step 5: Add more tests for MetricsCollector**

Add to `tests/workflow/performance/test_collector.py`:

```python
class TestTaskMetrics:
    """测试任务指标"""

    def test_create_task_metrics(self):
        """测试创建任务指标"""
        metrics = TaskMetrics(
            task_id="task-1",
            task_name="Test Task",
            tool_name="llm:chat",
            workflow_id="wf-1",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=1.5,
            status="success",
            retry_count=0
        )
        assert metrics.task_id == "task-1"
        assert metrics.status == "success"
        assert metrics.duration == 1.5


class TestMetricsCollector:
    """测试指标收集器"""

    def test_start_workflow(self):
        """测试开始工作流"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", 10)

        metrics = collector.get_workflow_metrics("wf-1")
        assert metrics is not None
        assert metrics.workflow_id == "wf-1"
        assert metrics.total_tasks == 10
        assert metrics.completed_tasks == 0

    def test_record_task(self):
        """测试记录任务"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", 10)

        task_metric = TaskMetrics(
            task_id="task-1",
            task_name="Test",
            tool_name="llm:chat",
            workflow_id="wf-1",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=1.0,
            status="success",
            retry_count=0
        )
        collector.record_task(task_metric)

        metrics = collector.get_workflow_metrics("wf-1")
        assert metrics.completed_tasks == 1

        tasks = collector.get_task_metrics("wf-1")
        assert len(tasks) == 1
        assert tasks[0].task_id == "task-1"

    def test_complete_workflow(self):
        """测试完成工作流"""
        collector = MetricsCollector()
        collector.start_workflow("wf-1", 3)

        # 添加3个任务
        for i in range(3):
            task_metric = TaskMetrics(
                task_id=f"task-{i}",
                task_name=f"Test {i}",
                tool_name="llm:chat",
                workflow_id="wf-1",
                started_at=datetime.now(),
                completed_at=datetime.now(),
                duration=1.0 + i * 0.5,
                status="success",
                retry_count=0
            )
            collector.record_task(task_metric)

        collector.complete_workflow("wf-1")

        metrics = collector.get_workflow_metrics("wf-1")
        assert metrics.completed_at is not None
        assert metrics.total_duration > 0
        assert metrics.throughput > 0
        assert metrics.avg_task_duration > 0
```

**Step 6: Run all collector tests**

```bash
pytest tests/workflow/performance/test_collector.py -v
```

Expected: ALL PASS

**Step 7: Commit**

```bash
git add src/workflow/performance/ tests/workflow/performance/
git commit -m "feat(performance): 实现指标收集器 MetricsCollector"
```

---

### Part 2: TraceRecorder (链路追踪器)

**Files:**
- Create: `src/workflow/performance/tracer.py`
- Create: `tests/workflow/performance/test_tracer.py`

**Step 1: Write the failing test**

Create `tests/workflow/performance/test_tracer.py`:

```python
"""测试链路追踪器"""
import pytest
from datetime import datetime
from src.workflow.performance.tracer import Span, TaskTrace, TraceRecorder


class TestSpan:
    """测试执行阶段"""

    def test_create_span(self):
        """测试创建阶段"""
        span = Span(
            name="tool_execution",
            started_at=datetime.now(),
            duration=1.5,
            metadata={"tool": "llm:chat"}
        )
        assert span.name == "tool_execution"
        assert span.duration == 1.5
        assert span.metadata["tool"] == "llm:chat"


class TestTaskTrace:
    """测试任务追踪"""

    def test_create_task_trace(self):
        """测试创建任务追踪"""
        trace = TaskTrace(
            task_id="task-1",
            workflow_id="wf-1",
            total_duration=2.5,
            spans=[]
        )
        assert trace.task_id == "task-1"
        assert trace.total_duration == 2.5


class TestTraceRecorder:
    """测试追踪记录器"""

    def test_start_trace(self):
        """测试开始追踪"""
        recorder = TraceRecorder()
        recorder.start_trace("task-1", "wf-1")

        trace = recorder.get_trace("task-1")
        assert trace is not None
        assert trace.task_id == "task-1"

    def test_add_span(self):
        """测试添加阶段"""
        recorder = TraceRecorder()
        recorder.start_trace("task-1", "wf-1")

        recorder.start_span("task-1", "dependency_check")
        recorder.end_span("task-1", "dependency_check", {"count": 2})

        trace = recorder.get_trace("task-1")
        assert len(trace.spans) == 1
        assert trace.spans[0].name == "dependency_check"
        assert trace.spans[0].metadata["count"] == 2

    def test_end_trace(self):
        """测试结束追踪"""
        recorder = TraceRecorder()
        recorder.start_trace("task-1", "wf-1")
        recorder.start_span("task-1", "tool_execution")
        recorder.end_span("task-1", "tool_execution")
        recorder.end_trace("task-1")

        trace = recorder.get_trace("task-1")
        assert trace.total_duration > 0
```

**Step 2: Run test to verify it fails**

```bash
pytest tests/workflow/performance/test_tracer.py -v
```

Expected: FAIL with "ModuleNotFoundError"

**Step 3: Write implementation**

Create `src/workflow/performance/tracer.py`:

```python
"""链路追踪器"""
from dataclasses import dataclass, field
from datetime import datetime
from typing import Dict, List, Optional, Any


@dataclass
class Span:
    """执行阶段"""
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
            "metadata": self.metadata
        }


@dataclass
class TaskTrace:
    """任务执行追踪"""
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
            "spans": [span.to_dict() for span in self.spans]
        }


class TraceRecorder:
    """追踪记录器"""

    def __init__(self):
        self.traces: Dict[str, TaskTrace] = {}
        self.active_spans: Dict[str, Dict[str, datetime]] = {}  # task_id -> span_name -> start_time
        self.trace_start_times: Dict[str, datetime] = {}  # task_id -> start_time

    def start_trace(self, task_id: str, workflow_id: str) -> None:
        """开始追踪任务"""
        self.traces[task_id] = TaskTrace(
            task_id=task_id,
            workflow_id=workflow_id,
            total_duration=0.0,
            spans=[]
        )
        self.active_spans[task_id] = {}
        self.trace_start_times[task_id] = datetime.now()

    def start_span(self, task_id: str, span_name: str) -> None:
        """开始一个执行阶段"""
        if task_id not in self.active_spans:
            return

        self.active_spans[task_id][span_name] = datetime.now()

    def end_span(self, task_id: str, span_name: str, metadata: Optional[Dict[str, Any]] = None) -> None:
        """结束一个执行阶段"""
        if task_id not in self.active_spans or span_name not in self.active_spans[task_id]:
            return

        start_time = self.active_spans[task_id].pop(span_name)
        end_time = datetime.now()
        duration = (end_time - start_time).total_seconds()

        span = Span(
            name=span_name,
            started_at=start_time,
            duration=duration,
            metadata=metadata or {}
        )

        if task_id in self.traces:
            self.traces[task_id].spans.append(span)

    def end_trace(self, task_id: str) -> None:
        """结束追踪"""
        if task_id not in self.trace_start_times:
            return

        start_time = self.trace_start_times.pop(task_id)
        end_time = datetime.now()
        total_duration = (end_time - start_time).total_seconds()

        if task_id in self.traces:
            self.traces[task_id].total_duration = total_duration

        # 清理活动阶段
        if task_id in self.active_spans:
            del self.active_spans[task_id]

    def get_trace(self, task_id: str) -> Optional[TaskTrace]:
        """获取追踪数据"""
        return self.traces.get(task_id)

    def get_all_traces(self, workflow_id: str) -> List[TaskTrace]:
        """获取工作流的所有追踪"""
        return [
            trace for trace in self.traces.values()
            if trace.workflow_id == workflow_id
        ]
```

**Step 4: Run tests**

```bash
pytest tests/workflow/performance/test_tracer.py -v
```

Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/workflow/performance/tracer.py tests/workflow/performance/test_tracer.py
git commit -m "feat(performance): 实现链路追踪器 TraceRecorder"
```

---

### Part 3: MetricsStorage (指标存储)

**Files:**
- Create: `src/workflow/performance/storage.py`
- Create: `tests/workflow/performance/test_storage.py`

**Step 1: Write the failing test**

Create `tests/workflow/performance/test_storage.py`:

```python
"""测试指标存储"""
import pytest
import tempfile
import os
from datetime import datetime
from src.workflow.performance.storage import MetricsStorage, InMemoryBuffer
from src.workflow.performance.collector import WorkflowMetrics, TaskMetrics


class TestInMemoryBuffer:
    """测试内存缓冲"""

    def test_store_and_get(self):
        """测试存储和获取"""
        buffer = InMemoryBuffer(max_age=3600)

        data = {"key": "value"}
        buffer.store("test-1", data)

        result = buffer.get("test-1")
        assert result == data

    def test_eviction(self):
        """测试过期清理"""
        buffer = InMemoryBuffer(max_age=0)  # 立即过期

        data = {"key": "value"}
        buffer.store("test-1", data)

        import time
        time.sleep(0.1)
        buffer.cleanup()

        result = buffer.get("test-1")
        assert result is None


@pytest.mark.asyncio
class TestMetricsStorage:
    """测试指标存储"""

    async def test_store_workflow_metrics(self):
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

    async def test_store_task_metrics(self):
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
```

**Step 2: Run test to verify it fails**

```bash
pytest tests/workflow/performance/test_storage.py -v
```

Expected: FAIL

**Step 3: Write implementation**

Create `src/workflow/performance/storage.py`:

```python
"""指标存储"""
import aiosqlite
import json
from datetime import datetime, timedelta
from typing import Dict, Any, Optional, List
from .collector import WorkflowMetrics, TaskMetrics
from .tracer import TaskTrace


class InMemoryBuffer:
    """内存缓冲"""

    def __init__(self, max_age: int = 3600):
        """
        Args:
            max_age: 最大保存时间（秒）
        """
        self.max_age = max_age
        self.data: Dict[str, tuple[Any, datetime]] = {}

    def store(self, key: str, value: Any) -> None:
        """存储数据"""
        self.data[key] = (value, datetime.now())

    def get(self, key: str) -> Optional[Any]:
        """获取数据"""
        if key not in self.data:
            return None

        value, timestamp = self.data[key]
        age = (datetime.now() - timestamp).total_seconds()

        if age > self.max_age:
            del self.data[key]
            return None

        return value

    def cleanup(self) -> None:
        """清理过期数据"""
        now = datetime.now()
        expired_keys = [
            key for key, (_, timestamp) in self.data.items()
            if (now - timestamp).total_seconds() > self.max_age
        ]
        for key in expired_keys:
            del self.data[key]


class MetricsStorage:
    """指标存储"""

    def __init__(self, db_path: str):
        self.db_path = db_path
        self.memory_buffer = InMemoryBuffer(max_age=3600)
        self.db = None

    async def initialize(self) -> None:
        """初始化数据库"""
        self.db = await aiosqlite.connect(self.db_path)
        await self._create_tables()

    async def _create_tables(self) -> None:
        """创建表"""
        await self.db.execute("""
            CREATE TABLE IF NOT EXISTS workflow_metrics (
                workflow_id TEXT PRIMARY KEY,
                total_tasks INTEGER,
                completed_tasks INTEGER,
                failed_tasks INTEGER,
                cancelled_tasks INTEGER,
                started_at TEXT,
                completed_at TEXT,
                total_duration REAL,
                throughput REAL,
                avg_task_duration REAL,
                p50_task_duration REAL,
                p95_task_duration REAL,
                p99_task_duration REAL,
                peak_memory_mb REAL,
                avg_cpu_percent REAL,
                db_query_count INTEGER,
                db_total_time REAL,
                db_avg_query_time REAL
            )
        """)

        await self.db.execute("""
            CREATE TABLE IF NOT EXISTS task_metrics (
                task_id TEXT PRIMARY KEY,
                task_name TEXT,
                tool_name TEXT,
                workflow_id TEXT,
                started_at TEXT,
                completed_at TEXT,
                duration REAL,
                status TEXT,
                retry_count INTEGER,
                memory_used INTEGER,
                cpu_time REAL
            )
        """)

        await self.db.execute("""
            CREATE TABLE IF NOT EXISTS task_traces (
                task_id TEXT PRIMARY KEY,
                workflow_id TEXT,
                total_duration REAL,
                spans TEXT
            )
        """)

        await self.db.commit()

    async def store_workflow_metrics(self, metrics: WorkflowMetrics) -> None:
        """存储工作流指标"""
        # 先存到内存缓冲
        self.memory_buffer.store(f"workflow:{metrics.workflow_id}", metrics)

        # 异步写入数据库
        await self.db.execute("""
            INSERT OR REPLACE INTO workflow_metrics VALUES (
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
            )
        """, (
            metrics.workflow_id,
            metrics.total_tasks,
            metrics.completed_tasks,
            metrics.failed_tasks,
            metrics.cancelled_tasks,
            metrics.started_at.isoformat(),
            metrics.completed_at.isoformat() if metrics.completed_at else None,
            metrics.total_duration,
            metrics.throughput,
            metrics.avg_task_duration,
            metrics.p50_task_duration,
            metrics.p95_task_duration,
            metrics.p99_task_duration,
            metrics.peak_memory_mb,
            metrics.avg_cpu_percent,
            metrics.db_query_count,
            metrics.db_total_time,
            metrics.db_avg_query_time
        ))
        await self.db.commit()

    async def store_task_metrics(self, metrics: TaskMetrics) -> None:
        """存储任务指标"""
        # 先存到内存缓冲
        self.memory_buffer.store(f"task:{metrics.task_id}", metrics)

        # 异步写入数据库
        await self.db.execute("""
            INSERT OR REPLACE INTO task_metrics VALUES (
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
            )
        """, (
            metrics.task_id,
            metrics.task_name,
            metrics.tool_name,
            metrics.workflow_id,
            metrics.started_at.isoformat(),
            metrics.completed_at.isoformat() if metrics.completed_at else None,
            metrics.duration,
            metrics.status,
            metrics.retry_count,
            metrics.memory_used,
            metrics.cpu_time
        ))
        await self.db.commit()

    async def store_trace(self, trace: TaskTrace) -> None:
        """存储追踪数据"""
        self.memory_buffer.store(f"trace:{trace.task_id}", trace)

        spans_json = json.dumps([span.to_dict() for span in trace.spans])

        await self.db.execute("""
            INSERT OR REPLACE INTO task_traces VALUES (?, ?, ?, ?)
        """, (
            trace.task_id,
            trace.workflow_id,
            trace.total_duration,
            spans_json
        ))
        await self.db.commit()

    def query_workflow_metrics(self, workflow_id: str) -> Optional[WorkflowMetrics]:
        """查询工作流指标（先查内存，再查数据库）"""
        # 先查内存
        cached = self.memory_buffer.get(f"workflow:{workflow_id}")
        if cached:
            return cached

        # 查数据库（同步操作，用于快速查询）
        import sqlite3
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute(
            "SELECT * FROM workflow_metrics WHERE workflow_id = ?",
            (workflow_id,)
        )
        row = cursor.fetchone()
        conn.close()

        if not row:
            return None

        return WorkflowMetrics(
            workflow_id=row[0],
            total_tasks=row[1],
            completed_tasks=row[2],
            failed_tasks=row[3],
            cancelled_tasks=row[4],
            started_at=datetime.fromisoformat(row[5]),
            completed_at=datetime.fromisoformat(row[6]) if row[6] else None,
            total_duration=row[7],
            throughput=row[8],
            avg_task_duration=row[9],
            p50_task_duration=row[10],
            p95_task_duration=row[11],
            p99_task_duration=row[12],
            peak_memory_mb=row[13],
            avg_cpu_percent=row[14],
            db_query_count=row[15],
            db_total_time=row[16],
            db_avg_query_time=row[17]
        )

    def query_task_metrics(self, task_id: str) -> Optional[TaskMetrics]:
        """查询任务指标"""
        cached = self.memory_buffer.get(f"task:{task_id}")
        if cached:
            return cached

        import sqlite3
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute(
            "SELECT * FROM task_metrics WHERE task_id = ?",
            (task_id,)
        )
        row = cursor.fetchone()
        conn.close()

        if not row:
            return None

        return TaskMetrics(
            task_id=row[0],
            task_name=row[1],
            tool_name=row[2],
            workflow_id=row[3],
            started_at=datetime.fromisoformat(row[4]),
            completed_at=datetime.fromisoformat(row[5]) if row[5] else None,
            duration=row[6],
            status=row[7],
            retry_count=row[8],
            memory_used=row[9],
            cpu_time=row[10]
        )

    async def close(self) -> None:
        """关闭数据库连接"""
        if self.db:
            await self.db.close()
```

**Step 4: Run tests**

```bash
pytest tests/workflow/performance/test_storage.py -v
```

Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/workflow/performance/storage.py tests/workflow/performance/test_storage.py
git commit -m "feat(performance): 实现指标存储 MetricsStorage"
```

---

### Part 4: PerformanceMonitor (性能监控器)

**Files:**
- Create: `src/workflow/performance/monitor.py`
- Create: `tests/workflow/performance/test_monitor.py`

**Step 1: Write the failing test**

Create `tests/workflow/performance/test_monitor.py`:

```python
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

    async def test_workflow_lifecycle(self):
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
                "status": "success",
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

    async def test_task_trace(self):
        """测试任务追踪"""
        with tempfile.TemporaryDirectory() as tmpdir:
            db_path = os.path.join(tmpdir, "metrics.db")
            storage = MetricsStorage(db_path)
            await storage.initialize()

            monitor = PerformanceMonitor(storage)

            await monitor.on_workflow_start("wf-1", 1)

            # 带追踪的任务执行
            task_info = {
                "task_name": "Test",
                "tool_name": "llm:chat",
                "workflow_id": "wf-1"
            }
            await monitor.on_task_start("task-1", task_info)

            # 模拟执行阶段
            monitor.start_span("task-1", "tool_execution")
            monitor.end_span("task-1", "tool_execution", {"tool": "llm:chat"})

            await monitor.on_task_complete("task-1", 1.0, {
                "status": "success",
                "retry_count": 0
            })

            # 获取追踪
            trace = monitor.get_task_trace("task-1")
            assert trace is not None
            assert len(trace.spans) == 1
            assert trace.spans[0].name == "tool_execution"

            await storage.close()
```

**Step 2: Run test to verify it fails**

```bash
pytest tests/workflow/performance/test_monitor.py -v
```

Expected: FAIL

**Step 3: Write implementation**

Create `src/workflow/performance/monitor.py`:

```python
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

    def __init__(self, storage: MetricsStorage):
        self.storage = storage
        self.metrics_collector = MetricsCollector()
        self.trace_recorder = TraceRecorder()
        self.process = psutil.Process(os.getpid())

    async def on_workflow_start(self, workflow_id: str, task_count: int) -> None:
        """工作流开始"""
        self.metrics_collector.start_workflow(workflow_id, task_count)

    async def on_workflow_complete(self, workflow_id: str, result: Dict[str, Any]) -> None:
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
        self.trace_recorder.start_trace(task_id, workflow_id)

    async def on_task_complete(
        self,
        task_id: str,
        duration: float,
        result: Dict[str, Any]
    ) -> None:
        """任务完成"""
        # 结束追踪
        self.trace_recorder.end_trace(task_id)

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

    async def on_task_retry(self, task_id: str, retry_count: int, reason: str) -> None:
        """任务重试"""
        # 记录重试事件
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

    def start_span(self, task_id: str, span_name: str) -> None:
        """开始执行阶段"""
        self.trace_recorder.start_span(task_id, span_name)

    def end_span(self, task_id: str, span_name: str, metadata: Optional[Dict[str, Any]] = None) -> None:
        """结束执行阶段"""
        self.trace_recorder.end_span(task_id, span_name, metadata)
```

**Step 4: Run tests**

```bash
pytest tests/workflow/performance/test_monitor.py -v
```

Expected: ALL PASS

**Step 5: Update __init__.py**

Update `src/workflow/performance/__init__.py`:

```python
"""性能监控模块"""
from .collector import WorkflowMetrics, TaskMetrics, MetricsCollector
from .tracer import Span, TaskTrace, TraceRecorder
from .storage import MetricsStorage, InMemoryBuffer
from .monitor import PerformanceMonitor

__all__ = [
    "WorkflowMetrics",
    "TaskMetrics",
    "MetricsCollector",
    "Span",
    "TaskTrace",
    "TraceRecorder",
    "MetricsStorage",
    "InMemoryBuffer",
    "PerformanceMonitor"
]
```

**Step 6: Run all performance tests**

```bash
pytest tests/workflow/performance/ -v --cov=src/workflow/performance
```

Expected: ALL PASS with coverage ≥ 80%

**Step 7: Commit**

```bash
git add src/workflow/performance/ tests/workflow/performance/
git commit -m "feat(performance): 实现性能监控器 PerformanceMonitor (Task 7.3.4.1 完成)"
```

---

## Task 7.3.4.2: 实现监控面板和报告

### Part 1: MonitoringDashboard (监控面板)

**Files:**
- Create: `src/workflow/performance/dashboard.py`
- Create: `tests/workflow/performance/test_dashboard.py`

**Step 1: Write the failing test**

Create `tests/workflow/performance/test_dashboard.py`:

```python
"""测试监控面板"""
import pytest
from unittest.mock import Mock, MagicMock
from datetime import datetime
from src.workflow.performance.dashboard import MonitoringDashboard
from src.workflow.performance.collector import WorkflowMetrics


class TestMonitoringDashboard:
    """测试监控面板"""

    def test_create_dashboard(self):
        """测试创建面板"""
        monitor = Mock()
        dashboard = MonitoringDashboard(monitor)
        assert dashboard.monitor == monitor

    def test_display_summary(self):
        """测试显示摘要"""
        monitor = Mock()
        metrics = WorkflowMetrics(
            workflow_id="wf-1",
            total_tasks=10,
            completed_tasks=5,
            failed_tasks=1,
            cancelled_tasks=0,
            started_at=datetime.now(),
            completed_at=datetime.now(),
            total_duration=10.0,
            throughput=0.5,
            avg_task_duration=2.0,
            p50_task_duration=1.8,
            p95_task_duration=3.5,
            p99_task_duration=4.0,
            peak_memory_mb=128.0,
            avg_cpu_percent=45.0,
            db_query_count=20,
            db_total_time=0.5,
            db_avg_query_time=0.025
        )
        monitor.get_current_metrics.return_value = metrics

        dashboard = MonitoringDashboard(monitor)
        dashboard.display_summary("wf-1")

        monitor.get_current_metrics.assert_called_once_with("wf-1")
```

**Step 2: Run test to verify it fails**

```bash
pytest tests/workflow/performance/test_dashboard.py -v
```

Expected: FAIL

**Step 3: Write implementation**

Create `src/workflow/performance/dashboard.py`:

```python
"""监控面板"""
from rich.console import Console
from rich.table import Table
from rich.panel import Panel
from rich.text import Text
from rich.layout import Layout
from rich.live import Live
from typing import Optional
import asyncio


class MonitoringDashboard:
    """实时监控面板（TUI）"""

    def __init__(self, monitor):
        self.monitor = monitor
        self.console = Console()

    def display_summary(self, workflow_id: str) -> None:
        """显示执行摘要"""
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            self.console.print(f"[red]工作流 {workflow_id} 未找到[/red]")
            return

        # 创建摘要面板
        summary = self._create_summary_panel(metrics)
        self.console.print(summary)

        # 创建指标表格
        metrics_table = self._create_metrics_table(metrics)
        self.console.print(metrics_table)

    def _create_summary_panel(self, metrics) -> Panel:
        """创建摘要面板"""
        progress = (metrics.completed_tasks / metrics.total_tasks * 100) if metrics.total_tasks > 0 else 0

        content = Text()
        content.append(f"工作流: {metrics.workflow_id}\n", style="bold cyan")
        content.append(f"状态: ")
        if metrics.completed_at:
            content.append("已完成", style="bold green")
        else:
            content.append("运行中", style="bold yellow")
        content.append(f"  |  进度: {metrics.completed_tasks}/{metrics.total_tasks} ({progress:.1f}%)\n")

        if metrics.completed_at:
            duration = metrics.total_duration
            content.append(f"执行时间: {self._format_duration(duration)}\n")
        else:
            content.append(f"已运行: {self._format_duration(metrics.total_duration)}\n")

        return Panel(content, title="[bold]工作流执行监控[/bold]", border_style="blue")

    def _create_metrics_table(self, metrics) -> Table:
        """创建指标表格"""
        table = Table(title="实时指标", show_header=True)
        table.add_column("指标", style="cyan", width=25)
        table.add_column("值", style="green")

        table.add_row("吞吐量", f"{metrics.throughput:.2f} tasks/s")
        table.add_row("平均任务时长", f"{metrics.avg_task_duration:.2f}s")
        table.add_row("P50 延迟", f"{metrics.p50_task_duration:.2f}s")
        table.add_row("P95 延迟", f"{metrics.p95_task_duration:.2f}s")
        table.add_row("P99 延迟", f"{metrics.p99_task_duration:.2f}s")
        table.add_row("峰值内存", f"{metrics.peak_memory_mb:.2f} MB")
        table.add_row("平均 CPU", f"{metrics.avg_cpu_percent:.1f}%")
        table.add_row("数据库查询", f"{metrics.db_query_count} 次")
        table.add_row("数据库平均耗时", f"{metrics.db_avg_query_time * 1000:.2f} ms")

        # 任务状态
        table.add_row("", "")
        table.add_row("成功任务", f"{metrics.completed_tasks}")
        table.add_row("失败任务", f"{metrics.failed_tasks}")
        table.add_row("取消任务", f"{metrics.cancelled_tasks}")

        return table

    def _format_duration(self, seconds: float) -> str:
        """格式化时长"""
        hours = int(seconds // 3600)
        minutes = int((seconds % 3600) // 60)
        secs = int(seconds % 60)
        return f"{hours:02d}:{minutes:02d}:{secs:02d}"

    async def display_realtime(self, workflow_id: str, refresh_interval: float = 1.0) -> None:
        """显示实时监控（自动刷新）"""
        with Live(self._generate_layout(workflow_id), console=self.console, refresh_per_second=1) as live:
            while True:
                metrics = self.monitor.get_current_metrics(workflow_id)
                if not metrics:
                    break

                live.update(self._generate_layout(workflow_id))

                # 如果工作流完成，退出
                if metrics.completed_at:
                    break

                await asyncio.sleep(refresh_interval)

    def _generate_layout(self, workflow_id: str) -> Layout:
        """生成布局"""
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            return Layout(Panel("[red]工作流未找到[/red]"))

        layout = Layout()
        layout.split_column(
            Layout(self._create_summary_panel(metrics), size=5),
            Layout(self._create_metrics_table(metrics))
        )
        return layout
```

**Step 4: Run tests**

```bash
pytest tests/workflow/performance/test_dashboard.py -v
```

Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/workflow/performance/dashboard.py tests/workflow/performance/test_dashboard.py
git commit -m "feat(performance): 实现监控面板 MonitoringDashboard"
```

---

### Part 2: ReportGenerator (报告生成器)

**Files:**
- Create: `src/workflow/performance/reporter.py`
- Create: `tests/workflow/performance/test_reporter.py`

**Step 1-5: Similar TDD approach**

Create `src/workflow/performance/reporter.py`:

```python
"""报告生成器"""
from typing import List, Dict, Any
from datetime import datetime
import json


class ReportGenerator:
    """性能报告生成器"""

    def __init__(self, monitor):
        self.monitor = monitor

    def generate_workflow_report(
        self,
        workflow_id: str,
        format: str = "markdown"
    ) -> str:
        """生成工作流性能报告"""
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            return f"工作流 {workflow_id} 未找到"

        if format == "markdown":
            return self._generate_markdown_report(metrics)
        elif format == "json":
            return json.dumps(metrics.to_dict(), indent=2)
        else:
            raise ValueError(f"不支持的格式: {format}")

    def _generate_markdown_report(self, metrics) -> str:
        """生成 Markdown 报告"""
        report = f"""# 工作流性能报告

**工作流 ID:** {metrics.workflow_id}
**生成时间:** {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

---

## 执行概览

| 指标 | 值 |
|------|-----|
| 总任务数 | {metrics.total_tasks} |
| 成功任务 | {metrics.completed_tasks} |
| 失败任务 | {metrics.failed_tasks} |
| 取消任务 | {metrics.cancelled_tasks} |
| 执行时间 | {metrics.total_duration:.2f}s |
| 吞吐量 | {metrics.throughput:.2f} tasks/s |

---

## 性能指标

| 指标 | 值 |
|------|-----|
| 平均任务时长 | {metrics.avg_task_duration:.2f}s |
| P50 延迟 | {metrics.p50_task_duration:.2f}s |
| P95 延迟 | {metrics.p95_task_duration:.2f}s |
| P99 延迟 | {metrics.p99_task_duration:.2f}s |

---

## 资源使用

| 指标 | 值 |
|------|-----|
| 峰值内存 | {metrics.peak_memory_mb:.2f} MB |
| 平均 CPU | {metrics.avg_cpu_percent:.1f}% |

---

## 数据库性能

| 指标 | 值 |
|------|-----|
| 查询次数 | {metrics.db_query_count} |
| 总耗时 | {metrics.db_total_time:.2f}s |
| 平均查询时间 | {metrics.db_avg_query_time * 1000:.2f}ms |

---

**报告生成完成**
"""
        return report

    def generate_comparison_report(
        self,
        workflow_ids: List[str]
    ) -> str:
        """生成对比报告"""
        report = "# 工作流对比报告\n\n"
        report += "| 工作流ID | 任务数 | 吞吐量 | P95延迟 | 内存峰值 | 成功率 |\n"
        report += "|---------|--------|--------|---------|----------|--------|\n"

        for wf_id in workflow_ids:
            metrics = self.monitor.get_current_metrics(wf_id)
            if metrics:
                success_rate = (metrics.completed_tasks / metrics.total_tasks * 100) if metrics.total_tasks > 0 else 0
                report += f"| {wf_id} | {metrics.total_tasks} | {metrics.throughput:.2f} | "
                report += f"{metrics.p95_task_duration:.2f}s | {metrics.peak_memory_mb:.2f}MB | {success_rate:.1f}% |\n"

        return report

    def export_metrics(
        self,
        workflow_id: str,
        format: str = "json"
    ) -> str:
        """导出原始指标数据"""
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            return "{}"

        if format == "json":
            return json.dumps(metrics.to_dict(), indent=2)
        elif format == "csv":
            return self._to_csv(metrics)
        else:
            raise ValueError(f"不支持的格式: {format}")

    def _to_csv(self, metrics) -> str:
        """转换为 CSV"""
        headers = list(metrics.to_dict().keys())
        values = list(metrics.to_dict().values())
        return ",".join(str(h) for h in headers) + "\n" + ",".join(str(v) for v in values)
```

Create tests and follow TDD cycle...

**Step 6: Commit**

```bash
git add src/workflow/performance/reporter.py tests/workflow/performance/test_reporter.py
git commit -m "feat(performance): 实现报告生成器 ReportGenerator"
```

---

### Part 3: AlertManager (告警管理器)

**Files:**
- Create: `src/workflow/performance/alerts.py`
- Create: `tests/workflow/performance/test_alerts.py`

Create `src/workflow/performance/alerts.py`:

```python
"""告警管理器"""
from dataclasses import dataclass
from typing import List, Dict, Any, Optional
from datetime import datetime
from .collector import WorkflowMetrics


@dataclass
class Alert:
    """告警"""
    alert_id: str
    workflow_id: str
    level: str  # "info", "warning", "critical"
    message: str
    metric_name: str
    metric_value: float
    threshold: float
    created_at: datetime

    def to_dict(self) -> Dict[str, Any]:
        return {
            "alert_id": self.alert_id,
            "workflow_id": self.workflow_id,
            "level": self.level,
            "message": self.message,
            "metric_name": self.metric_name,
            "metric_value": self.metric_value,
            "threshold": self.threshold,
            "created_at": self.created_at.isoformat()
        }


@dataclass
class AlertConfig:
    """告警配置"""
    failure_rate_threshold: float = 0.05  # 5%
    p95_latency_threshold: float = 5.0  # 5秒
    memory_usage_threshold: float = 0.8  # 80%
    queue_backlog_threshold: int = 100
    db_response_threshold: float = 1.0  # 1秒


class AlertManager:
    """告警管理器"""

    def __init__(self, config: Optional[AlertConfig] = None):
        self.config = config or AlertConfig()
        self.alerts: List[Alert] = []

    async def check_alerts(self, metrics: WorkflowMetrics) -> List[Alert]:
        """检查告警"""
        alerts = []

        # 检查失败率
        if metrics.total_tasks > 0:
            failure_rate = metrics.failed_tasks / metrics.total_tasks
            if failure_rate > self.config.failure_rate_threshold:
                alert = Alert(
                    alert_id=f"alert-{len(self.alerts)}",
                    workflow_id=metrics.workflow_id,
                    level="warning",
                    message=f"任务失败率过高: {failure_rate * 100:.1f}%",
                    metric_name="failure_rate",
                    metric_value=failure_rate,
                    threshold=self.config.failure_rate_threshold,
                    created_at=datetime.now()
                )
                alerts.append(alert)

        # 检查 P95 延迟
        if metrics.p95_task_duration > self.config.p95_latency_threshold:
            alert = Alert(
                alert_id=f"alert-{len(self.alerts)}",
                workflow_id=metrics.workflow_id,
                level="warning",
                message=f"P95 延迟过高: {metrics.p95_task_duration:.2f}s",
                metric_name="p95_latency",
                metric_value=metrics.p95_task_duration,
                threshold=self.config.p95_latency_threshold,
                created_at=datetime.now()
            )
            alerts.append(alert)

        # 检查数据库响应时间
        if metrics.db_avg_query_time > self.config.db_response_threshold:
            alert = Alert(
                alert_id=f"alert-{len(self.alerts)}",
                workflow_id=metrics.workflow_id,
                level="warning",
                message=f"数据库响应时间过长: {metrics.db_avg_query_time:.2f}s",
                metric_name="db_response_time",
                metric_value=metrics.db_avg_query_time,
                threshold=self.config.db_response_threshold,
                created_at=datetime.now()
            )
            alerts.append(alert)

        self.alerts.extend(alerts)
        return alerts

    async def send_notification(self, alert: Alert) -> None:
        """发送告警通知（集成 notification.py）"""
        # TODO: 集成现有的通知系统
        pass

    def get_alerts(self, workflow_id: Optional[str] = None) -> List[Alert]:
        """获取告警"""
        if workflow_id:
            return [a for a in self.alerts if a.workflow_id == workflow_id]
        return self.alerts
```

Create tests and commit...

```bash
git add src/workflow/performance/alerts.py tests/workflow/performance/test_alerts.py
git commit -m "feat(performance): 实现告警管理器 AlertManager (Task 7.3.4.2 完成)"
```

---

## Task 7.3.4.3: 实现基准测试套件

**Files:**
- Create: `src/workflow/benchmarks/__init__.py`
- Create: `src/workflow/benchmarks/suite.py`
- Create: `src/workflow/benchmarks/scenarios.py`
- Create: `tests/workflow/test_benchmarks.py`

Due to length constraints, I'll provide a condensed version. Follow same TDD pattern:

1. Write test for BenchmarkSuite
2. Implement scenarios (small-fast, small-slow, medium-mixed, large-fast, large-slow, complex-dag)
3. Implement suite runner
4. Test report generation
5. Commit

```bash
git commit -m "feat(performance): 实现基准测试套件 (Task 7.3.4.3 完成)"
```

---

## Task 7.3.4.4: 实施快速优化措施

**Files:**
- Create: `src/workflow/performance/optimizations.py`
- Create: `tests/workflow/performance/test_optimizations.py`

Implement BatchDatabaseWriter and ResultManager following the design document.

```bash
git commit -m "feat(performance): 实施批量写入和内存优化 (Task 7.3.4.4 完成)"
```

---

## Task 7.3.4.5: 编写文档和演示程序

**Files:**
- Create: `docs/workflow/performance-monitoring.md`
- Create: `docs/workflow/benchmarks.md`
- Create: `examples/workflow_benchmark_demo.py`
- Create: `.planning/phase7-week3-complete-2026-03-08.md`

Document usage, write demo, and create completion report.

```bash
git commit -m "docs(performance): 完成性能优化文档和演示 (Task 7.3.4.5 完成)"
```

---

## Final Steps

**Run full test suite:**

```bash
pytest tests/workflow/performance/ -v --cov=src/workflow/performance --cov-report=html
```

Expected: 30+ tests passing, coverage ≥ 80%

**Final commit:**

```bash
git add .
git commit -m "feat(workflow): Phase 7.3.4 性能优化完成

- 性能监控框架（PerformanceMonitor）
- 实时监控 TUI 面板
- 基准测试套件（6 种场景）
- 批量数据库写入优化
- 任务结果内存管理优化
- 30+ 测试用例，覆盖率 80%+
"
```

---

## Verification Checklist

Before marking complete:

- [ ] All tests passing (30+ tests)
- [ ] Coverage ≥ 80%
- [ ] Monitoring dashboard works in TUI
- [ ] Benchmarks run successfully
- [ ] Reports generate correctly
- [ ] Optimizations show measurable improvement
- [ ] Documentation complete
- [ ] Demo program works

---

**Plan saved to:** `docs/plans/2026-03-08-performance-optimization.md`
**Next:** Choose execution approach (Subagent-Driven or Parallel Session)
