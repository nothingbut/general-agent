# 监控面板和报告 实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为性能监控框架添加可视化和报告能力（MonitoringDashboard、ReportGenerator、AlertManager）

**Architecture:** 3 个独立组件通过依赖注入获取服务。ReportGenerator 从 MetricsStorage 查询数据生成报告；AlertManager 检查指标并集成 notification.py 发送告警；MonitoringDashboard 使用 Rich TUI 显示实时监控面板。

**Tech Stack:** Rich (TUI)、asyncio、dataclasses、notification.py (已有)

---

## 实施顺序

按依赖关系实施：
1. **ReportGenerator** - 最独立，只依赖 MetricsStorage
2. **AlertManager** - 依赖 notification.py
3. **MonitoringDashboard** - 依赖 PerformanceMonitor

---

## Task 1: ReportGenerator - 基础结构和 Markdown 报告

**Files:**
- Create: `src/workflow/performance/reporter.py`
- Create: `tests/workflow/performance/test_reporter.py`
- Modify: `src/workflow/performance/__init__.py` (添加导出)

### Step 1.1: 写失败的测试 - 基础初始化

**File:** `tests/workflow/performance/test_reporter.py`

```python
"""ReportGenerator 测试"""
import pytest
from src.workflow.performance.reporter import ReportGenerator
from src.workflow.performance.storage import MetricsStorage
from src.workflow.performance.collector import WorkflowMetrics, TaskMetrics
from datetime import datetime


@pytest.fixture
async def storage():
    """创建测试用的存储"""
    storage = MetricsStorage(":memory:")
    await storage.initialize()
    yield storage
    await storage.close()


@pytest.mark.asyncio
async def test_reporter_initialization(storage):
    """测试 ReportGenerator 初始化"""
    reporter = ReportGenerator(storage)
    assert reporter.storage == storage
```

### Step 1.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_reporter.py::test_reporter_initialization -v
```

预期：FAIL - "No module named 'src.workflow.performance.reporter'"

### Step 1.3: 实现 ReportGenerator 基础结构

**File:** `src/workflow/performance/reporter.py`

```python
"""报告生成器"""
from typing import List, Dict, Any, Optional
from .storage import MetricsStorage
from .collector import WorkflowMetrics, TaskMetrics


class ReportGenerator:
    """报告生成器 - 生成 Markdown/JSON 格式的性能报告"""

    def __init__(self, storage: MetricsStorage):
        """初始化

        Args:
            storage: 指标存储实例
        """
        self.storage = storage

    def generate_workflow_report(
        self,
        workflow_id: str,
        format: str = "markdown"
    ) -> str:
        """生成单个工作流报告

        Args:
            workflow_id: 工作流 ID
            format: 输出格式（"markdown" 或 "json"）

        Returns:
            报告字符串
        """
        raise NotImplementedError

    def generate_comparison_report(
        self,
        workflow_ids: List[str],
        format: str = "markdown"
    ) -> str:
        """生成对比报告

        Args:
            workflow_ids: 工作流 ID 列表
            format: 输出格式

        Returns:
            对比报告字符串
        """
        raise NotImplementedError

    def export_metrics(
        self,
        workflow_id: str,
        format: str = "json"
    ) -> str:
        """导出原始指标

        Args:
            workflow_id: 工作流 ID
            format: 输出格式（目前只支持 "json"）

        Returns:
            指标数据字符串
        """
        raise NotImplementedError
```

### Step 1.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_reporter.py::test_reporter_initialization -v
```

预期：PASS

### Step 1.5: 提交基础结构

```bash
git add src/workflow/performance/reporter.py tests/workflow/performance/test_reporter.py
git commit -m "feat(performance): 添加 ReportGenerator 基础结构"
```

---

## Task 2: ReportGenerator - Markdown 报告生成

### Step 2.1: 写失败的测试 - Markdown 报告

**File:** `tests/workflow/performance/test_reporter.py` (追加)

```python
@pytest.mark.asyncio
async def test_generate_markdown_report(storage):
    """测试生成 Markdown 报告"""
    # 准备测试数据
    metrics = WorkflowMetrics(
        workflow_id="wf-test-001",
        total_tasks=100,
        completed_tasks=95,
        failed_tasks=3,
        cancelled_tasks=2,
        started_at=datetime(2026, 3, 8, 15, 30, 0),
        completed_at=datetime(2026, 3, 8, 15, 32, 34),
        total_duration=154.2,
        throughput=18.5,
        avg_task_duration=0.24,
        p50_task_duration=0.12,
        p95_task_duration=0.85,
        p99_task_duration=1.2,
        peak_memory_mb=128.0,
        avg_cpu_percent=45.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )
    await storage.store_workflow_metrics(metrics)

    # 添加任务指标
    task_metrics = [
        TaskMetrics(
            task_id="task-1",
            task_name="llm:summarize",
            tool_name="llm:summarize",
            workflow_id="wf-test-001",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=3.5,
            status="completed",
            retry_count=0
        ),
        TaskMetrics(
            task_id="task-2",
            task_name="mcp:api:call",
            tool_name="mcp:api:call",
            workflow_id="wf-test-001",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=2.8,
            status="completed",
            retry_count=0
        ),
        TaskMetrics(
            task_id="task-3",
            task_name="failed_task",
            tool_name="mcp:api:call",
            workflow_id="wf-test-001",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=0.5,
            status="failed",
            retry_count=1
        )
    ]
    for task_metric in task_metrics:
        await storage.store_task_metrics(task_metric)

    # 生成报告
    reporter = ReportGenerator(storage)
    report = reporter.generate_workflow_report("wf-test-001", format="markdown")

    # 验证报告内容
    assert "# 工作流性能报告" in report
    assert "wf-test-001" in report
    assert "总任务数" in report
    assert "100" in report
    assert "成功率" in report
    assert "95.0%" in report
    assert "吞吐量" in report
    assert "18.5" in report
    assert "P95 延迟" in report
    assert "850ms" in report or "0.85s" in report
    assert "峰值内存" in report
    assert "128" in report
    assert "慢任务" in report
    assert "llm:summarize" in report
    assert "3.5s" in report
    assert "失败任务" in report
    assert "failed_task" in report
```

### Step 2.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_markdown_report -v
```

预期：FAIL - "NotImplementedError"

### Step 2.3: 实现 Markdown 报告生成

**File:** `src/workflow/performance/reporter.py` (修改 `generate_workflow_report` 方法)

```python
import json
from datetime import datetime

class ReportGenerator:
    # ... 已有代码 ...

    def generate_workflow_report(
        self,
        workflow_id: str,
        format: str = "markdown"
    ) -> str:
        """生成单个工作流报告"""
        # 查询指标
        metrics = self.storage.query_workflow_metrics(workflow_id)
        if not metrics:
            raise ValueError(f"Workflow {workflow_id} not found")

        # 查询任务指标（通过 storage 的内部方法）
        task_metrics = self._get_task_metrics(workflow_id)

        # 计算派生指标
        derived = self._calculate_derived_metrics(metrics, task_metrics)

        if format == "markdown":
            return self._generate_markdown_report(metrics, derived)
        elif format == "json":
            return self._generate_json_report(metrics, derived)
        else:
            raise ValueError(f"Unsupported format: {format}")

    def _get_task_metrics(self, workflow_id: str) -> List[TaskMetrics]:
        """获取任务指标列表（从数据库查询）"""
        import sqlite3
        conn = sqlite3.connect(self.storage.db_path)
        cursor = conn.cursor()
        cursor.execute(
            "SELECT * FROM task_metrics WHERE workflow_id = ?",
            (workflow_id,)
        )
        rows = cursor.fetchall()
        conn.close()

        task_metrics = []
        for row in rows:
            task_metrics.append(TaskMetrics(
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
            ))
        return task_metrics

    def _calculate_derived_metrics(
        self,
        metrics: WorkflowMetrics,
        task_metrics: List[TaskMetrics]
    ) -> Dict[str, Any]:
        """计算派生指标"""
        success_rate = 0.0
        if metrics.total_tasks > 0:
            success_rate = metrics.completed_tasks / metrics.total_tasks

        # 慢任务 Top 5
        slow_tasks = sorted(
            task_metrics,
            key=lambda t: t.duration,
            reverse=True
        )[:5]

        # 失败任务
        failed_tasks = [t for t in task_metrics if t.status == "failed"]

        return {
            "success_rate": success_rate,
            "slow_tasks": slow_tasks,
            "failed_tasks": failed_tasks
        }

    def _generate_markdown_report(
        self,
        metrics: WorkflowMetrics,
        derived: Dict[str, Any]
    ) -> str:
        """生成 Markdown 格式报告"""
        # 格式化时间
        started = metrics.started_at.strftime("%Y-%m-%d %H:%M:%S")
        completed = metrics.completed_at.strftime("%Y-%m-%d %H:%M:%S") if metrics.completed_at else "N/A"

        # 格式化延迟
        def format_duration(seconds: float) -> str:
            if seconds < 1:
                return f"{int(seconds * 1000)}ms"
            else:
                return f"{seconds:.1f}s"

        report = f"""# 工作流性能报告

**工作流 ID:** {metrics.workflow_id}
**执行时间:** {started} - {completed}
**总时长:** {metrics.total_duration:.1f} 秒

## 执行概览

| 指标 | 数值 |
|------|------|
| 总任务数 | {metrics.total_tasks} |
| 完成任务 | {metrics.completed_tasks} |
| 失败任务 | {metrics.failed_tasks} |
| 取消任务 | {metrics.cancelled_tasks} |
| 成功率 | {derived['success_rate']:.1%} |

## 性能指标

| 指标 | 数值 |
|------|------|
| 吞吐量 | {metrics.throughput:.1f} tasks/s |
| 平均延迟 | {format_duration(metrics.avg_task_duration)} |
| P50 延迟 | {format_duration(metrics.p50_task_duration)} |
| P95 延迟 | {format_duration(metrics.p95_task_duration)} |
| P99 延迟 | {format_duration(metrics.p99_task_duration)} |

## 资源使用

- **峰值内存:** {metrics.peak_memory_mb:.1f} MB
- **平均 CPU:** {metrics.avg_cpu_percent:.1f}%
"""

        # 慢任务 Top 5
        if derived['slow_tasks']:
            report += "\n## 慢任务 Top 5\n\n"
            for i, task in enumerate(derived['slow_tasks'], 1):
                report += f"{i}. {task.task_id}: {task.task_name} - {format_duration(task.duration)}\n"

        # 失败任务
        if derived['failed_tasks']:
            report += "\n## 失败任务\n\n"
            for i, task in enumerate(derived['failed_tasks'], 1):
                report += f"{i}. {task.task_id}: {task.task_name} - {task.status.upper()}\n"

        return report

    def _generate_json_report(
        self,
        metrics: WorkflowMetrics,
        derived: Dict[str, Any]
    ) -> str:
        """生成 JSON 格式报告"""
        raise NotImplementedError  # Task 3 实现
```

### Step 2.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_markdown_report -v
```

预期：PASS

### Step 2.5: 提交 Markdown 报告功能

```bash
git add src/workflow/performance/reporter.py tests/workflow/performance/test_reporter.py
git commit -m "feat(performance): 实现 Markdown 报告生成"
```

---

## Task 3: ReportGenerator - JSON 报告和指标导出

### Step 3.1: 写失败的测试 - JSON 报告

**File:** `tests/workflow/performance/test_reporter.py` (追加)

```python
@pytest.mark.asyncio
async def test_generate_json_report(storage):
    """测试生成 JSON 报告"""
    # 准备测试数据（复用 Task 2 的数据）
    metrics = WorkflowMetrics(
        workflow_id="wf-test-002",
        total_tasks=50,
        completed_tasks=48,
        failed_tasks=2,
        cancelled_tasks=0,
        started_at=datetime(2026, 3, 8, 16, 0, 0),
        completed_at=datetime(2026, 3, 8, 16, 1, 30),
        total_duration=90.0,
        throughput=25.0,
        avg_task_duration=0.15,
        p50_task_duration=0.10,
        p95_task_duration=0.50,
        p99_task_duration=0.80,
        peak_memory_mb=64.0,
        avg_cpu_percent=30.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )
    await storage.store_workflow_metrics(metrics)

    # 生成 JSON 报告
    reporter = ReportGenerator(storage)
    report = reporter.generate_workflow_report("wf-test-002", format="json")

    # 解析并验证
    import json
    data = json.loads(report)
    assert data["workflow_id"] == "wf-test-002"
    assert data["summary"]["total_tasks"] == 50
    assert data["summary"]["completed_tasks"] == 48
    assert data["summary"]["success_rate"] == 0.96
    assert data["performance"]["throughput"] == 25.0
    assert data["performance"]["p95_latency"] == 0.50
    assert data["resources"]["peak_memory_mb"] == 64.0


@pytest.mark.asyncio
async def test_export_metrics(storage):
    """测试导出原始指标"""
    # 准备测试数据
    metrics = WorkflowMetrics(
        workflow_id="wf-test-003",
        total_tasks=10,
        completed_tasks=10,
        failed_tasks=0,
        cancelled_tasks=0,
        started_at=datetime(2026, 3, 8, 17, 0, 0),
        completed_at=datetime(2026, 3, 8, 17, 0, 10),
        total_duration=10.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=32.0,
        avg_cpu_percent=20.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )
    await storage.store_workflow_metrics(metrics)

    # 导出指标
    reporter = ReportGenerator(storage)
    export = reporter.export_metrics("wf-test-003", format="json")

    # 解析并验证
    import json
    data = json.loads(export)
    assert "workflow_metrics" in data
    assert data["workflow_metrics"]["workflow_id"] == "wf-test-003"
    assert "task_metrics" in data
    assert "traces" in data
```

### Step 3.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_json_report -v
pytest tests/workflow/performance/test_reporter.py::test_export_metrics -v
```

预期：FAIL - "NotImplementedError"

### Step 3.3: 实现 JSON 报告和指标导出

**File:** `src/workflow/performance/reporter.py` (修改方法)

```python
class ReportGenerator:
    # ... 已有代码 ...

    def _generate_json_report(
        self,
        metrics: WorkflowMetrics,
        derived: Dict[str, Any]
    ) -> str:
        """生成 JSON 格式报告"""
        report_data = {
            "workflow_id": metrics.workflow_id,
            "execution": {
                "started_at": metrics.started_at.isoformat(),
                "completed_at": metrics.completed_at.isoformat() if metrics.completed_at else None,
                "total_duration": metrics.total_duration
            },
            "summary": {
                "total_tasks": metrics.total_tasks,
                "completed_tasks": metrics.completed_tasks,
                "failed_tasks": metrics.failed_tasks,
                "cancelled_tasks": metrics.cancelled_tasks,
                "success_rate": derived['success_rate']
            },
            "performance": {
                "throughput": metrics.throughput,
                "avg_latency": metrics.avg_task_duration,
                "p50_latency": metrics.p50_task_duration,
                "p95_latency": metrics.p95_task_duration,
                "p99_latency": metrics.p99_task_duration
            },
            "resources": {
                "peak_memory_mb": metrics.peak_memory_mb,
                "avg_cpu_percent": metrics.avg_cpu_percent
            },
            "slow_tasks": [
                {
                    "task_id": task.task_id,
                    "name": task.task_name,
                    "duration": task.duration
                }
                for task in derived['slow_tasks']
            ],
            "failed_tasks": [
                {
                    "task_id": task.task_id,
                    "name": task.task_name,
                    "error": task.status
                }
                for task in derived['failed_tasks']
            ]
        }
        return json.dumps(report_data, indent=2)

    def export_metrics(
        self,
        workflow_id: str,
        format: str = "json"
    ) -> str:
        """导出原始指标"""
        if format != "json":
            raise ValueError(f"Unsupported format: {format}")

        # 查询所有指标
        workflow_metrics = self.storage.query_workflow_metrics(workflow_id)
        if not workflow_metrics:
            raise ValueError(f"Workflow {workflow_id} not found")

        task_metrics = self._get_task_metrics(workflow_id)
        traces = self._get_traces(workflow_id)

        export_data = {
            "workflow_metrics": workflow_metrics.to_dict(),
            "task_metrics": [tm.to_dict() for tm in task_metrics],
            "traces": [trace.to_dict() for trace in traces]
        }

        return json.dumps(export_data, indent=2)

    def _get_traces(self, workflow_id: str) -> List:
        """获取追踪数据"""
        import sqlite3
        from .tracer import TaskTrace, Span

        conn = sqlite3.connect(self.storage.db_path)
        cursor = conn.cursor()
        cursor.execute(
            "SELECT * FROM task_traces WHERE workflow_id = ?",
            (workflow_id,)
        )
        rows = cursor.fetchall()
        conn.close()

        traces = []
        for row in rows:
            spans_data = json.loads(row[3])
            spans = [
                Span(
                    name=s["name"],
                    started_at=datetime.fromisoformat(s["started_at"]),
                    duration=s["duration"],
                    metadata=s.get("metadata", {})
                )
                for s in spans_data
            ]
            traces.append(TaskTrace(
                task_id=row[0],
                workflow_id=row[1],
                total_duration=row[2],
                spans=spans
            ))
        return traces
```

### Step 3.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_json_report -v
pytest tests/workflow/performance/test_reporter.py::test_export_metrics -v
```

预期：PASS

### Step 3.5: 提交 JSON 报告和导出功能

```bash
git add src/workflow/performance/reporter.py tests/workflow/performance/test_reporter.py
git commit -m "feat(performance): 实现 JSON 报告和指标导出"
```

---

## Task 4: ReportGenerator - 对比报告

### Step 4.1: 写失败的测试 - 对比报告

**File:** `tests/workflow/performance/test_reporter.py` (追加)

```python
@pytest.mark.asyncio
async def test_generate_comparison_report(storage):
    """测试生成对比报告"""
    # 准备多个工作流数据
    workflows = [
        ("wf-compare-001", 100, 95, 18.5, 0.85),
        ("wf-compare-002", 150, 147, 22.3, 0.72),
        ("wf-compare-003", 200, 184, 15.8, 1.2)
    ]

    for wf_id, total, completed, throughput, p95 in workflows:
        metrics = WorkflowMetrics(
            workflow_id=wf_id,
            total_tasks=total,
            completed_tasks=completed,
            failed_tasks=total - completed,
            cancelled_tasks=0,
            started_at=datetime.now(),
            completed_at=datetime.now(),
            total_duration=100.0,
            throughput=throughput,
            avg_task_duration=0.2,
            p50_task_duration=0.1,
            p95_task_duration=p95,
            p99_task_duration=1.5,
            peak_memory_mb=100.0,
            avg_cpu_percent=40.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0
        )
        await storage.store_workflow_metrics(metrics)

    # 生成对比报告
    reporter = ReportGenerator(storage)
    report = reporter.generate_comparison_report(
        ["wf-compare-001", "wf-compare-002", "wf-compare-003"],
        format="markdown"
    )

    # 验证报告内容
    assert "# 工作流性能对比" in report
    assert "wf-compare-001" in report
    assert "wf-compare-002" in report
    assert "wf-compare-003" in report
    assert "95%" in report or "95.0%" in report
    assert "98%" in report or "98.0%" in report
    assert "18.5" in report
    assert "22.3" in report
    assert "最快吞吐量" in report
    assert "最低延迟" in report
    assert "最高成功率" in report
```

### Step 4.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_comparison_report -v
```

预期：FAIL - "NotImplementedError"

### Step 4.3: 实现对比报告

**File:** `src/workflow/performance/reporter.py` (修改方法)

```python
class ReportGenerator:
    # ... 已有代码 ...

    def generate_comparison_report(
        self,
        workflow_ids: List[str],
        format: str = "markdown"
    ) -> str:
        """生成对比报告"""
        # 查询所有工作流指标
        all_metrics = []
        for wf_id in workflow_ids:
            metrics = self.storage.query_workflow_metrics(wf_id)
            if metrics:
                all_metrics.append(metrics)

        if not all_metrics:
            raise ValueError("No workflows found")

        if format == "markdown":
            return self._generate_markdown_comparison(all_metrics)
        elif format == "json":
            return self._generate_json_comparison(all_metrics)
        else:
            raise ValueError(f"Unsupported format: {format}")

    def _generate_markdown_comparison(
        self,
        all_metrics: List[WorkflowMetrics]
    ) -> str:
        """生成 Markdown 格式对比报告"""
        report = "# 工作流性能对比\n\n"
        report += "## 概览\n\n"
        report += "| 工作流 | 任务数 | 成功率 | 吞吐量 | P95 延迟 |\n"
        report += "|--------|--------|--------|--------|----------|\n"

        for metrics in all_metrics:
            success_rate = 0.0
            if metrics.total_tasks > 0:
                success_rate = metrics.completed_tasks / metrics.total_tasks

            # 格式化延迟
            if metrics.p95_task_duration < 1:
                p95_str = f"{int(metrics.p95_task_duration * 1000)}ms"
            else:
                p95_str = f"{metrics.p95_task_duration:.1f}s"

            report += f"| {metrics.workflow_id} | {metrics.total_tasks} | {success_rate:.0%} | {metrics.throughput:.1f}/s | {p95_str} |\n"

        # 性能对比亮点
        report += "\n## 性能对比\n\n"

        # 最快吞吐量
        best_throughput = max(all_metrics, key=lambda m: m.throughput)
        report += f"- **最快吞吐量:** {best_throughput.workflow_id} ({best_throughput.throughput:.1f} tasks/s)\n"

        # 最低延迟
        best_latency = min(all_metrics, key=lambda m: m.p95_task_duration)
        if best_latency.p95_task_duration < 1:
            latency_str = f"{int(best_latency.p95_task_duration * 1000)}ms"
        else:
            latency_str = f"{best_latency.p95_task_duration:.1f}s"
        report += f"- **最低延迟:** {best_latency.workflow_id} (P95: {latency_str})\n"

        # 最高成功率
        best_success = max(
            all_metrics,
            key=lambda m: m.completed_tasks / m.total_tasks if m.total_tasks > 0 else 0
        )
        success_rate = best_success.completed_tasks / best_success.total_tasks if best_success.total_tasks > 0 else 0
        report += f"- **最高成功率:** {best_success.workflow_id} ({success_rate:.0%})\n"

        return report

    def _generate_json_comparison(
        self,
        all_metrics: List[WorkflowMetrics]
    ) -> str:
        """生成 JSON 格式对比报告"""
        comparison_data = {
            "workflows": []
        }

        for metrics in all_metrics:
            success_rate = 0.0
            if metrics.total_tasks > 0:
                success_rate = metrics.completed_tasks / metrics.total_tasks

            comparison_data["workflows"].append({
                "workflow_id": metrics.workflow_id,
                "total_tasks": metrics.total_tasks,
                "success_rate": success_rate,
                "throughput": metrics.throughput,
                "p95_latency": metrics.p95_task_duration
            })

        return json.dumps(comparison_data, indent=2)
```

### Step 4.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_reporter.py::test_generate_comparison_report -v
```

预期：PASS

### Step 4.5: 提交对比报告功能

```bash
git add src/workflow/performance/reporter.py tests/workflow/performance/test_reporter.py
git commit -m "feat(performance): 实现对比报告生成"
```

### Step 4.6: 更新 __init__.py 导出

**File:** `src/workflow/performance/__init__.py`

```python
"""性能监控模块"""
from .collector import MetricsCollector, WorkflowMetrics, TaskMetrics
from .tracer import TraceRecorder, TaskTrace, Span
from .storage import MetricsStorage, InMemoryBuffer
from .monitor import PerformanceMonitor
from .reporter import ReportGenerator

__all__ = [
    "MetricsCollector",
    "WorkflowMetrics",
    "TaskMetrics",
    "TraceRecorder",
    "TaskTrace",
    "Span",
    "MetricsStorage",
    "InMemoryBuffer",
    "PerformanceMonitor",
    "ReportGenerator",
]
```

```bash
git add src/workflow/performance/__init__.py
git commit -m "feat(performance): 导出 ReportGenerator"
```

---

## Task 5: AlertManager - 基础结构和配置

**Files:**
- Create: `src/workflow/performance/alerts.py`
- Create: `tests/workflow/performance/test_alerts.py`

### Step 5.1: 写失败的测试 - 基础初始化

**File:** `tests/workflow/performance/test_alerts.py`

```python
"""AlertManager 测试"""
import pytest
from src.workflow.performance.alerts import AlertManager, AlertConfig, Alert
from src.workflow.performance.collector import WorkflowMetrics
from datetime import datetime


def test_alert_config_defaults():
    """测试 AlertConfig 默认值"""
    config = AlertConfig()
    assert config.failure_rate_threshold == 0.05
    assert config.p95_latency_threshold == 2.0
    assert config.p99_latency_threshold == 5.0
    assert config.memory_threshold_mb == 500.0
    assert config.cpu_threshold_percent == 80.0
    assert config.db_query_time_threshold == 1.0
    assert "high_failure_rate" in config.priority_mapping


def test_alert_manager_initialization():
    """测试 AlertManager 初始化"""
    config = AlertConfig()
    manager = AlertManager(config)
    assert manager.config == config
    assert manager.notifier is None
    assert len(manager.active_alerts) == 0
```

### Step 5.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_alerts.py -v
```

预期：FAIL - "No module named 'src.workflow.performance.alerts'"

### Step 5.3: 实现 AlertManager 基础结构

**File:** `src/workflow/performance/alerts.py`

```python
"""告警管理器"""
from dataclasses import dataclass, field
from datetime import datetime
from typing import Dict, List, Any, Optional
import logging

from .collector import WorkflowMetrics, TaskMetrics

logger = logging.getLogger(__name__)


@dataclass
class AlertConfig:
    """告警配置"""

    # 失败率阈值
    failure_rate_threshold: float = 0.05  # 5%

    # 延迟阈值
    p95_latency_threshold: float = 2.0  # 2秒
    p99_latency_threshold: float = 5.0  # 5秒

    # 资源阈值
    memory_threshold_mb: float = 500.0  # 500MB
    cpu_threshold_percent: float = 80.0  # 80%

    # 数据库阈值
    db_query_time_threshold: float = 1.0  # 1秒

    # 告警优先级映射（告警类型 -> 通知优先级）
    priority_mapping: Dict[str, str] = field(default_factory=lambda: {
        "high_failure_rate": "high",
        "high_p95_latency": "medium",
        "high_p99_latency": "high",
        "memory_exhaustion": "high",
        "high_cpu_usage": "medium",
        "slow_database": "medium"
    })


@dataclass
class Alert:
    """告警"""

    alert_type: str  # 告警类型
    severity: str  # 严重程度: low/medium/high/critical
    message: str  # 告警消息
    workflow_id: str  # 工作流 ID
    metrics: Dict[str, Any]  # 相关指标
    timestamp: datetime = field(default_factory=datetime.now)


class AlertManager:
    """告警管理器"""

    def __init__(
        self,
        config: AlertConfig,
        notifier: Optional[Any] = None
    ):
        """初始化

        Args:
            config: 告警配置
            notifier: 通知管理器（可选）
        """
        self.config = config
        self.notifier = notifier
        self.active_alerts: Dict[str, List[Alert]] = {}

    async def check_alerts(
        self,
        metrics: WorkflowMetrics
    ) -> List[Alert]:
        """检查工作流级别告警

        Args:
            metrics: 工作流指标

        Returns:
            触发的告警列表
        """
        raise NotImplementedError

    async def check_task_alert(
        self,
        task_metrics: TaskMetrics
    ) -> List[Alert]:
        """检查任务级别告警（可选）

        Args:
            task_metrics: 任务指标

        Returns:
            触发的告警列表
        """
        raise NotImplementedError

    async def _send_notification(self, alert: Alert) -> None:
        """发送告警通知（内部方法）"""
        raise NotImplementedError

    def _should_alert(
        self,
        workflow_id: str,
        alert_type: str
    ) -> bool:
        """去重检查（内部方法）

        避免同一工作流的相同告警重复发送
        """
        if workflow_id not in self.active_alerts:
            return True

        # 检查是否已经有相同类型的告警
        for alert in self.active_alerts[workflow_id]:
            if alert.alert_type == alert_type:
                return False

        return True
```

### Step 5.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_alerts.py -v
```

预期：PASS

### Step 5.5: 提交基础结构

```bash
git add src/workflow/performance/alerts.py tests/workflow/performance/test_alerts.py
git commit -m "feat(performance): 添加 AlertManager 基础结构"
```

---

## Task 6: AlertManager - 告警检查逻辑

### Step 6.1: 写失败的测试 - 告警检查

**File:** `tests/workflow/performance/test_alerts.py` (追加)

```python
@pytest.mark.asyncio
async def test_check_high_failure_rate():
    """测试高失败率告警"""
    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-001",
        total_tasks=100,
        completed_tasks=90,
        failed_tasks=10,  # 10% 失败率
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 1
    assert alerts[0].alert_type == "high_failure_rate"
    assert alerts[0].severity == "high"
    assert "10.0%" in alerts[0].message or "0.1" in alerts[0].message


@pytest.mark.asyncio
async def test_check_high_p95_latency():
    """测试高 P95 延迟告警"""
    config = AlertConfig(p95_latency_threshold=2.0)
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-002",
        total_tasks=100,
        completed_tasks=100,
        failed_tasks=0,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=3.0,  # 超过阈值
        p99_task_duration=4.0,
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 1
    assert alerts[0].alert_type == "high_p95_latency"
    assert alerts[0].severity == "medium"


@pytest.mark.asyncio
async def test_check_multiple_alerts():
    """测试多个告警同时触发"""
    config = AlertConfig(
        failure_rate_threshold=0.05,
        memory_threshold_mb=100.0
    )
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-003",
        total_tasks=100,
        completed_tasks=90,
        failed_tasks=10,  # 触发失败率告警
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=150.0,  # 触发内存告警
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 2
    alert_types = [a.alert_type for a in alerts]
    assert "high_failure_rate" in alert_types
    assert "memory_exhaustion" in alert_types


@pytest.mark.asyncio
async def test_alert_deduplication():
    """测试告警去重"""
    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-004",
        total_tasks=100,
        completed_tasks=90,
        failed_tasks=10,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    # 第一次检查
    alerts1 = await manager.check_alerts(metrics)
    assert len(alerts1) == 1

    # 第二次检查（应该去重）
    alerts2 = await manager.check_alerts(metrics)
    assert len(alerts2) == 0  # 相同告警不重复触发
```

### Step 6.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_alerts.py::test_check_high_failure_rate -v
pytest tests/workflow/performance/test_alerts.py::test_check_high_p95_latency -v
pytest tests/workflow/performance/test_alerts.py::test_check_multiple_alerts -v
pytest tests/workflow/performance/test_alerts.py::test_alert_deduplication -v
```

预期：FAIL - "NotImplementedError"

### Step 6.3: 实现告警检查逻辑

**File:** `src/workflow/performance/alerts.py` (修改 `check_alerts` 方法)

```python
class AlertManager:
    # ... 已有代码 ...

    async def check_alerts(
        self,
        metrics: WorkflowMetrics
    ) -> List[Alert]:
        """检查工作流级别告警"""
        alerts = []

        # 1. 检查失败率
        if metrics.total_tasks > 0:
            failure_rate = metrics.failed_tasks / metrics.total_tasks
            if failure_rate > self.config.failure_rate_threshold:
                if self._should_alert(metrics.workflow_id, "high_failure_rate"):
                    alerts.append(Alert(
                        alert_type="high_failure_rate",
                        severity="high",
                        message=f"失败率过高: {failure_rate:.1%} (阈值: {self.config.failure_rate_threshold:.1%})",
                        workflow_id=metrics.workflow_id,
                        metrics={"failure_rate": failure_rate}
                    ))

        # 2. 检查 P95 延迟
        if metrics.p95_task_duration > self.config.p95_latency_threshold:
            if self._should_alert(metrics.workflow_id, "high_p95_latency"):
                alerts.append(Alert(
                    alert_type="high_p95_latency",
                    severity="medium",
                    message=f"P95 延迟过高: {metrics.p95_task_duration:.2f}s (阈值: {self.config.p95_latency_threshold:.2f}s)",
                    workflow_id=metrics.workflow_id,
                    metrics={"p95_latency": metrics.p95_task_duration}
                ))

        # 3. 检查 P99 延迟
        if metrics.p99_task_duration > self.config.p99_latency_threshold:
            if self._should_alert(metrics.workflow_id, "high_p99_latency"):
                alerts.append(Alert(
                    alert_type="high_p99_latency",
                    severity="high",
                    message=f"P99 延迟过高: {metrics.p99_task_duration:.2f}s (阈值: {self.config.p99_latency_threshold:.2f}s)",
                    workflow_id=metrics.workflow_id,
                    metrics={"p99_latency": metrics.p99_task_duration}
                ))

        # 4. 检查内存使用
        if metrics.peak_memory_mb > self.config.memory_threshold_mb:
            if self._should_alert(metrics.workflow_id, "memory_exhaustion"):
                alerts.append(Alert(
                    alert_type="memory_exhaustion",
                    severity="high",
                    message=f"内存使用过高: {metrics.peak_memory_mb:.1f}MB (阈值: {self.config.memory_threshold_mb:.1f}MB)",
                    workflow_id=metrics.workflow_id,
                    metrics={"peak_memory_mb": metrics.peak_memory_mb}
                ))

        # 5. 检查 CPU 使用
        if metrics.avg_cpu_percent > self.config.cpu_threshold_percent:
            if self._should_alert(metrics.workflow_id, "high_cpu_usage"):
                alerts.append(Alert(
                    alert_type="high_cpu_usage",
                    severity="medium",
                    message=f"CPU 使用率过高: {metrics.avg_cpu_percent:.1f}% (阈值: {self.config.cpu_threshold_percent:.1f}%)",
                    workflow_id=metrics.workflow_id,
                    metrics={"avg_cpu_percent": metrics.avg_cpu_percent}
                ))

        # 6. 检查数据库性能
        if metrics.db_avg_query_time > self.config.db_query_time_threshold:
            if self._should_alert(metrics.workflow_id, "slow_database"):
                alerts.append(Alert(
                    alert_type="slow_database",
                    severity="medium",
                    message=f"数据库查询慢: {metrics.db_avg_query_time:.2f}s (阈值: {self.config.db_query_time_threshold:.2f}s)",
                    workflow_id=metrics.workflow_id,
                    metrics={"db_avg_query_time": metrics.db_avg_query_time}
                ))

        # 发送通知
        for alert in alerts:
            await self._send_notification(alert)
            # 记录到活动告警
            if metrics.workflow_id not in self.active_alerts:
                self.active_alerts[metrics.workflow_id] = []
            self.active_alerts[metrics.workflow_id].append(alert)

        return alerts

    async def _send_notification(self, alert: Alert) -> None:
        """发送告警通知"""
        if not self.notifier:
            # 没有通知器，只记录日志
            logger.warning(f"Alert: {alert.alert_type} - {alert.message}")
            return

        # 后续 Task 7 集成 notification.py
        pass
```

### Step 6.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_alerts.py::test_check_high_failure_rate -v
pytest tests/workflow/performance/test_alerts.py::test_check_high_p95_latency -v
pytest tests/workflow/performance/test_alerts.py::test_check_multiple_alerts -v
pytest tests/workflow/performance/test_alerts.py::test_alert_deduplication -v
```

预期：PASS

### Step 6.5: 提交告警检查逻辑

```bash
git add src/workflow/performance/alerts.py tests/workflow/performance/test_alerts.py
git commit -m "feat(performance): 实现告警检查逻辑"
```

---

## Task 7: AlertManager - 集成 notification.py

### Step 7.1: 写失败的测试 - 通知集成

**File:** `tests/workflow/performance/test_alerts.py` (追加)

```python
from unittest.mock import AsyncMock, Mock


@pytest.mark.asyncio
async def test_send_notification():
    """测试发送通知"""
    # 创建 mock notifier
    mock_notifier = Mock()
    mock_notifier.send_notification = AsyncMock()

    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config, notifier=mock_notifier)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-005",
        total_tasks=100,
        completed_tasks=90,
        failed_tasks=10,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    alerts = await manager.check_alerts(metrics)

    # 验证通知被调用
    assert len(alerts) == 1
    mock_notifier.send_notification.assert_called_once()

    # 验证通知参数
    call_args = mock_notifier.send_notification.call_args
    assert "性能告警" in call_args.kwargs["title"]
    assert "high_failure_rate" in call_args.kwargs["title"]
    assert "失败率过高" in call_args.kwargs["message"]
    assert call_args.kwargs["priority"] == "high"
    assert "terminal" in call_args.kwargs["channels"]
    assert "desktop" in call_args.kwargs["channels"]


@pytest.mark.asyncio
async def test_no_notification_without_notifier():
    """测试没有 notifier 时只记录日志"""
    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config, notifier=None)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-006",
        total_tasks=100,
        completed_tasks=90,
        failed_tasks=10,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,
        p99_task_duration=0.5,
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    # 应该正常工作，只是不发送通知
    alerts = await manager.check_alerts(metrics)
    assert len(alerts) == 1
```

### Step 7.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_alerts.py::test_send_notification -v
pytest tests/workflow/performance/test_alerts.py::test_no_notification_without_notifier -v
```

预期：FAIL - 通知未被调用

### Step 7.3: 实现通知集成

**File:** `src/workflow/performance/alerts.py` (修改 `_send_notification` 方法)

```python
class AlertManager:
    # ... 已有代码 ...

    async def _send_notification(self, alert: Alert) -> None:
        """发送告警通知"""
        if not self.notifier:
            # 没有通知器，只记录日志
            logger.warning(f"Alert: {alert.alert_type} - {alert.message}")
            return

        # 映射告警类型到通知优先级
        priority = self.config.priority_mapping.get(
            alert.alert_type,
            "medium"
        )

        # 发送通知
        await self.notifier.send_notification(
            title=f"⚠️ 性能告警: {alert.alert_type}",
            message=alert.message,
            priority=priority,
            channels=["terminal", "desktop"]
        )
```

### Step 7.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_alerts.py::test_send_notification -v
pytest tests/workflow/performance/test_alerts.py::test_no_notification_without_notifier -v
```

预期：PASS

### Step 7.5: 提交通知集成

```bash
git add src/workflow/performance/alerts.py tests/workflow/performance/test_alerts.py
git commit -m "feat(performance): 集成 notification.py 发送告警通知"
```

### Step 7.6: 更新 __init__.py 导出

**File:** `src/workflow/performance/__init__.py`

```python
"""性能监控模块"""
from .collector import MetricsCollector, WorkflowMetrics, TaskMetrics
from .tracer import TraceRecorder, TaskTrace, Span
from .storage import MetricsStorage, InMemoryBuffer
from .monitor import PerformanceMonitor
from .reporter import ReportGenerator
from .alerts import AlertManager, AlertConfig, Alert

__all__ = [
    "MetricsCollector",
    "WorkflowMetrics",
    "TaskMetrics",
    "TraceRecorder",
    "TaskTrace",
    "Span",
    "MetricsStorage",
    "InMemoryBuffer",
    "PerformanceMonitor",
    "ReportGenerator",
    "AlertManager",
    "AlertConfig",
    "Alert",
]
```

```bash
git add src/workflow/performance/__init__.py
git commit -m "feat(performance): 导出 AlertManager"
```

---

## Task 8: MonitoringDashboard - 基础结构和快照模式

**Files:**
- Create: `src/workflow/performance/dashboard.py`
- Create: `tests/workflow/performance/test_dashboard.py`

### Step 8.1: 写失败的测试 - 基础初始化

**File:** `tests/workflow/performance/test_dashboard.py`

```python
"""MonitoringDashboard 测试"""
import pytest
from unittest.mock import Mock
from rich.console import Console
from src.workflow.performance.dashboard import MonitoringDashboard
from src.workflow.performance.monitor import PerformanceMonitor
from src.workflow.performance.storage import MetricsStorage
from src.workflow.performance.collector import WorkflowMetrics
from datetime import datetime


@pytest.fixture
async def monitor():
    """创建测试用的监控器"""
    storage = MetricsStorage(":memory:")
    await storage.initialize()
    monitor = PerformanceMonitor(storage)
    yield monitor
    await storage.close()


def test_dashboard_initialization(monitor):
    """测试 Dashboard 初始化"""
    dashboard = MonitoringDashboard(monitor)
    assert dashboard.monitor == monitor
    assert dashboard.console is not None


def test_dashboard_with_custom_console(monitor):
    """测试使用自定义 Console"""
    console = Console()
    dashboard = MonitoringDashboard(monitor, console=console)
    assert dashboard.console == console
```

### Step 8.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_dashboard.py::test_dashboard_initialization -v
pytest tests/workflow/performance/test_dashboard.py::test_dashboard_with_custom_console -v
```

预期：FAIL - "No module named 'src.workflow.performance.dashboard'"

### Step 8.3: 实现 Dashboard 基础结构

**File:** `src/workflow/performance/dashboard.py`

```python
"""监控面板 - Rich TUI"""
import asyncio
from typing import Optional
from rich.console import Console
from rich.layout import Layout
from rich.panel import Panel
from rich.table import Table
from rich.text import Text
from rich.live import Live

from .monitor import PerformanceMonitor
from .collector import WorkflowMetrics


class MonitoringDashboard:
    """监控面板（Rich TUI）"""

    def __init__(
        self,
        monitor: PerformanceMonitor,
        console: Optional[Console] = None
    ):
        """初始化

        Args:
            monitor: 性能监控器实例
            console: Rich Console（可选）
        """
        self.monitor = monitor
        self.console = console or Console()
        self._stop_event = asyncio.Event()

    async def display_realtime(
        self,
        workflow_id: str,
        refresh_interval: float = 1.0
    ) -> None:
        """实时监控模式（自动刷新）

        Args:
            workflow_id: 工作流 ID
            refresh_interval: 刷新间隔（秒）
        """
        raise NotImplementedError

    def display_snapshot(self, workflow_id: str) -> None:
        """快照模式（显示一次）

        Args:
            workflow_id: 工作流 ID
        """
        raise NotImplementedError

    def display_summary(self, workflow_id: str) -> None:
        """显示执行摘要

        Args:
            workflow_id: 工作流 ID
        """
        raise NotImplementedError

    def _build_layout(
        self,
        metrics: WorkflowMetrics
    ) -> Layout:
        """构建面板布局（内部方法）"""
        raise NotImplementedError
```

### Step 8.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_dashboard.py::test_dashboard_initialization -v
pytest tests/workflow/performance/test_dashboard.py::test_dashboard_with_custom_console -v
```

预期：PASS

### Step 8.5: 提交基础结构

```bash
git add src/workflow/performance/dashboard.py tests/workflow/performance/test_dashboard.py
git commit -m "feat(performance): 添加 MonitoringDashboard 基础结构"
```

---

## Task 9: MonitoringDashboard - 快照模式实现

### Step 9.1: 写失败的测试 - 快照显示

**File:** `tests/workflow/performance/test_dashboard.py` (追加)

```python
@pytest.mark.asyncio
async def test_display_snapshot(monitor):
    """测试快照模式显示"""
    # 准备测试数据
    await monitor.on_workflow_start("wf-test-001", 100)

    # 模拟完成
    result = {"status": "completed"}
    await monitor.on_workflow_complete("wf-test-001", result)

    # 创建 dashboard 并显示快照
    dashboard = MonitoringDashboard(monitor)

    # 这个测试主要验证不抛出异常
    # 实际输出需要手动验证
    try:
        dashboard.display_snapshot("wf-test-001")
        assert True
    except Exception as e:
        pytest.fail(f"display_snapshot raised {e}")


def test_build_layout(monitor):
    """测试构建布局"""
    metrics = WorkflowMetrics(
        workflow_id="wf-test-002",
        total_tasks=100,
        completed_tasks=45,
        failed_tasks=1,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=None,
        total_duration=154.2,
        throughput=18.5,
        avg_task_duration=0.24,
        p50_task_duration=0.12,
        p95_task_duration=0.85,
        p99_task_duration=1.2,
        peak_memory_mb=128.0,
        avg_cpu_percent=45.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0
    )

    dashboard = MonitoringDashboard(monitor)
    layout = dashboard._build_layout(metrics)

    # 验证返回的是 Layout 对象
    assert isinstance(layout, Layout)
```

### Step 9.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_dashboard.py::test_display_snapshot -v
pytest tests/workflow/performance/test_dashboard.py::test_build_layout -v
```

预期：FAIL - "NotImplementedError"

### Step 9.3: 实现快照模式和布局构建

**File:** `src/workflow/performance/dashboard.py` (修改方法)

```python
class MonitoringDashboard:
    # ... 已有代码 ...

    def display_snapshot(self, workflow_id: str) -> None:
        """快照模式（显示一次）"""
        metrics = self.monitor.get_current_metrics(workflow_id)
        if not metrics:
            self.console.print(f"[yellow]工作流 {workflow_id} 未找到[/yellow]")
            return

        layout = self._build_layout(metrics)
        self.console.print(layout)

    def _build_layout(self, metrics: WorkflowMetrics) -> Layout:
        """构建面板布局"""
        layout = Layout()

        # 创建 4 个面板
        overview_panel = self._build_overview_panel(metrics)
        performance_panel = self._build_performance_panel(metrics)
        status_panel = self._build_status_panel(metrics)

        # 组合布局
        layout.split_column(
            overview_panel,
            performance_panel,
            status_panel
        )

        return layout

    def _build_overview_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建概览面板"""
        # 计算进度
        progress = 0
        if metrics.total_tasks > 0:
            progress = (metrics.completed_tasks + metrics.failed_tasks) / metrics.total_tasks

        # 状态
        status = "RUNNING" if metrics.completed_at is None else "COMPLETED"
        status_color = "yellow" if status == "RUNNING" else "green"

        # 格式化时长
        duration_str = f"{int(metrics.total_duration // 60):02d}:{int(metrics.total_duration % 60):02d}"

        content = Text()
        content.append(f"状态: ", style="bold")
        content.append(f"{status}", style=f"bold {status_color}")
        content.append("  |  进度: ", style="bold")
        content.append(f"{metrics.completed_tasks + metrics.failed_tasks}/{metrics.total_tasks} ({progress:.0%})")
        content.append("\n")
        content.append(f"执行时间: ", style="bold")
        content.append(f"{duration_str}")
        content.append("  |  吞吐量: ", style="bold")
        content.append(f"{metrics.throughput:.1f} tasks/s")

        return Panel(
            content,
            title=f"工作流监控: {metrics.workflow_id[:12]}...",
            border_style="cyan"
        )

    def _build_performance_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建性能指标面板"""
        # 格式化延迟
        def format_duration(seconds: float) -> str:
            if seconds < 1:
                return f"{int(seconds * 1000)}ms"
            else:
                return f"{seconds:.1f}s"

        content = Text()
        content.append("平均延迟: ", style="bold")
        content.append(f"{format_duration(metrics.avg_task_duration):<10}")
        content.append("P95: ", style="bold")
        content.append(f"{format_duration(metrics.p95_task_duration):<10}")
        content.append("P99: ", style="bold")
        content.append(f"{format_duration(metrics.p99_task_duration)}")
        content.append("\n")
        content.append("CPU 使用: ", style="bold")
        content.append(f"{metrics.avg_cpu_percent:.0f}%{' ' * 6}")
        content.append("内存: ", style="bold")
        content.append(f"{metrics.peak_memory_mb:.0f} MB")

        return Panel(content, title="性能指标", border_style="green")

    def _build_status_panel(self, metrics: WorkflowMetrics) -> Panel:
        """构建任务状态面板"""
        running = metrics.total_tasks - metrics.completed_tasks - metrics.failed_tasks - metrics.cancelled_tasks

        content = Text()
        content.append("✓ 完成: ", style="green")
        content.append(f"{metrics.completed_tasks:<5}")
        content.append("✗ 失败: ", style="red")
        content.append(f"{metrics.failed_tasks:<5}")
        content.append("🔄 重试: ", style="yellow")
        content.append(f"0{' ' * 4}")  # TODO: 从 task_metrics 获取重试数
        content.append("⏳ 运行: ", style="cyan")
        content.append(f"{running}")

        return Panel(content, title="任务状态", border_style="blue")
```

### Step 9.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_dashboard.py::test_display_snapshot -v
pytest tests/workflow/performance/test_dashboard.py::test_build_layout -v
```

预期：PASS

### Step 9.5: 提交快照模式

```bash
git add src/workflow/performance/dashboard.py tests/workflow/performance/test_dashboard.py
git commit -m "feat(performance): 实现监控面板快照模式"
```

---

## Task 10: MonitoringDashboard - 实时监控模式

### Step 10.1: 写失败的测试 - 实时监控

**File:** `tests/workflow/performance/test_dashboard.py` (追加)

```python
@pytest.mark.asyncio
async def test_display_realtime(monitor):
    """测试实时监控模式"""
    # 准备测试数据
    await monitor.on_workflow_start("wf-test-003", 100)

    dashboard = MonitoringDashboard(monitor)

    # 创建一个任务来停止实时监控
    async def stop_after_delay():
        await asyncio.sleep(2)  # 运行 2 秒后停止
        dashboard._stop_event.set()

    # 启动实时监控和停止任务
    stop_task = asyncio.create_task(stop_after_delay())

    try:
        await dashboard.display_realtime("wf-test-003", refresh_interval=0.5)
        assert True
    except Exception as e:
        pytest.fail(f"display_realtime raised {e}")
    finally:
        await stop_task
```

### Step 10.2: 运行测试验证失败

```bash
pytest tests/workflow/performance/test_dashboard.py::test_display_realtime -v
```

预期：FAIL - "NotImplementedError"

### Step 10.3: 实现实时监控模式

**File:** `src/workflow/performance/dashboard.py` (修改方法)

```python
class MonitoringDashboard:
    # ... 已有代码 ...

    async def display_realtime(
        self,
        workflow_id: str,
        refresh_interval: float = 1.0
    ) -> None:
        """实时监控模式（自动刷新）"""
        self.console.print("[cyan]按 Ctrl+C 停止监控[/cyan]\n")

        try:
            with Live(
                self._build_empty_layout(),
                console=self.console,
                refresh_per_second=1
            ) as live:
                while not self._stop_event.is_set():
                    try:
                        # 获取最新指标
                        metrics = self.monitor.get_current_metrics(workflow_id)
                        if metrics:
                            # 更新布局
                            live.update(self._build_layout(metrics))

                            # 如果工作流已完成，停止监控
                            if metrics.completed_at is not None:
                                break

                        await asyncio.sleep(refresh_interval)
                    except KeyboardInterrupt:
                        break
        finally:
            self._stop_event.clear()

    def _build_empty_layout(self) -> Panel:
        """构建空布局（初始状态）"""
        content = Text("正在加载监控数据...", style="yellow")
        return Panel(content, title="监控面板", border_style="cyan")

    def display_summary(self, workflow_id: str) -> None:
        """显示执行摘要"""
        summary = self.monitor.get_workflow_summary(workflow_id)
        if not summary:
            self.console.print(f"[yellow]工作流 {workflow_id} 未找到[/yellow]")
            return

        # 创建摘要表格
        table = Table(title="工作流执行摘要", show_header=True)
        table.add_column("指标", style="cyan")
        table.add_column("数值", style="white")

        for key, value in summary.items():
            table.add_row(key, str(value))

        self.console.print(table)
```

### Step 10.4: 运行测试验证通过

```bash
pytest tests/workflow/performance/test_dashboard.py::test_display_realtime -v
```

预期：PASS

### Step 10.5: 提交实时监控模式

```bash
git add src/workflow/performance/dashboard.py tests/workflow/performance/test_dashboard.py
git commit -m "feat(performance): 实现监控面板实时监控模式"
```

### Step 10.6: 更新 __init__.py 导出

**File:** `src/workflow/performance/__init__.py`

```python
"""性能监控模块"""
from .collector import MetricsCollector, WorkflowMetrics, TaskMetrics
from .tracer import TraceRecorder, TaskTrace, Span
from .storage import MetricsStorage, InMemoryBuffer
from .monitor import PerformanceMonitor
from .reporter import ReportGenerator
from .alerts import AlertManager, AlertConfig, Alert
from .dashboard import MonitoringDashboard

__all__ = [
    "MetricsCollector",
    "WorkflowMetrics",
    "TaskMetrics",
    "TraceRecorder",
    "TaskTrace",
    "Span",
    "MetricsStorage",
    "InMemoryBuffer",
    "PerformanceMonitor",
    "ReportGenerator",
    "AlertManager",
    "AlertConfig",
    "Alert",
    "MonitoringDashboard",
]
```

```bash
git add src/workflow/performance/__init__.py
git commit -m "feat(performance): 导出 MonitoringDashboard"
```

---

## Task 11: 运行所有测试并验证覆盖率

### Step 11.1: 运行所有测试

```bash
pytest tests/workflow/performance/ -v
```

预期：所有测试通过

### Step 11.2: 检查覆盖率

```bash
pytest tests/workflow/performance/ --cov=src/workflow/performance --cov-report=term-missing
```

预期：覆盖率 ≥ 80%

### Step 11.3: 如果覆盖率不足，添加缺失测试

根据覆盖率报告识别未覆盖的代码，添加测试用例。

### Step 11.4: 提交最终测试

```bash
git add tests/workflow/performance/
git commit -m "test(performance): 完善测试覆盖率"
```

---

## Task 12: 更新进度文档

### Step 12.1: 更新任务进度

**File:** `.planning/phase7-week3-task-7.3.4-progress.md` (更新状态)

将 Task 7.3.4.2 状态从 "⏸️ 0%" 更新为 "✅ 100%"

### Step 12.2: 提交进度更新

```bash
git add .planning/phase7-week3-task-7.3.4-progress.md
git commit -m "docs: 更新 Task 7.3.4.2 进度为完成"
```

---

## 完成标准

- ✅ ReportGenerator 实现（Markdown + JSON）
- ✅ AlertManager 实现（6 种告警规则 + notification.py 集成）
- ✅ MonitoringDashboard 实现（实时 + 快照模式）
- ✅ 8+ 单元测试全部通过
- ✅ 测试覆盖率 ≥ 80%
- ✅ 所有组件导出到 __init__.py
- ✅ 进度文档已更新

---

## 注意事项

1. **TDD 流程严格执行**：每个功能先写测试，再实现
2. **频繁提交**：每完成一个小功能就提交
3. **类型注解**：所有函数都要有类型注解
4. **文档字符串**：所有公共方法都要有 docstring
5. **测试隔离**：使用 fixture 和 mock，避免测试间相互影响
6. **错误处理**：合理处理边界情况（如工作流未找到）

---

**预计总时间:** 12-16 小时（约 1 天）
