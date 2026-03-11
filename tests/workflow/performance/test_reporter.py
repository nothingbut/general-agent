"""ReportGenerator 测试"""

import pytest
from datetime import datetime
from typing import AsyncGenerator
from src.workflow.performance.reporter import ReportGenerator
from src.workflow.performance.storage import MetricsStorage
from src.workflow.performance.collector import WorkflowMetrics, TaskMetrics


@pytest.fixture
async def storage() -> AsyncGenerator[MetricsStorage, None]:
    """创建测试用的存储"""
    storage = MetricsStorage(":memory:")
    await storage.initialize()
    yield storage
    await storage.close()


@pytest.mark.asyncio
async def test_reporter_initialization(storage: MetricsStorage) -> None:
    """测试 ReportGenerator 初始化"""
    reporter = ReportGenerator(storage)
    assert reporter.storage == storage


@pytest.mark.asyncio
async def test_generate_comparison_report_markdown(storage: MetricsStorage) -> None:
    """测试生成 Markdown 对比报告"""
    # 准备三个工作流的测试数据
    workflows = [
        WorkflowMetrics(
            workflow_id="wf-fast",
            total_tasks=100,
            completed_tasks=98,
            failed_tasks=2,
            cancelled_tasks=0,
            started_at=datetime(2026, 3, 8, 10, 0, 0),
            completed_at=datetime(2026, 3, 8, 10, 10, 0),
            total_duration=600.0,
            throughput=10.0,
            avg_task_duration=0.5,
            p50_task_duration=0.4,
            p95_task_duration=0.8,
            p99_task_duration=1.0,
            peak_memory_mb=100.0,
            avg_cpu_percent=40.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        ),
        WorkflowMetrics(
            workflow_id="wf-slow",
            total_tasks=100,
            completed_tasks=90,
            failed_tasks=10,
            cancelled_tasks=0,
            started_at=datetime(2026, 3, 8, 11, 0, 0),
            completed_at=datetime(2026, 3, 8, 11, 20, 0),
            total_duration=1200.0,
            throughput=5.0,
            avg_task_duration=1.0,
            p50_task_duration=0.8,
            p95_task_duration=2.5,
            p99_task_duration=3.0,
            peak_memory_mb=200.0,
            avg_cpu_percent=60.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        ),
        WorkflowMetrics(
            workflow_id="wf-medium",
            total_tasks=100,
            completed_tasks=95,
            failed_tasks=5,
            cancelled_tasks=0,
            started_at=datetime(2026, 3, 8, 12, 0, 0),
            completed_at=datetime(2026, 3, 8, 12, 12, 0),
            total_duration=720.0,
            throughput=8.0,
            avg_task_duration=0.7,
            p50_task_duration=0.6,
            p95_task_duration=1.5,
            p99_task_duration=2.0,
            peak_memory_mb=150.0,
            avg_cpu_percent=50.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        ),
    ]

    # 存储所有工作流指标
    for wf_metrics in workflows:
        await storage.store_workflow_metrics(wf_metrics)

    # 生成对比报告
    reporter = ReportGenerator(storage)
    report = await reporter.generate_comparison_report(
        ["wf-fast", "wf-slow", "wf-medium"], output_format="markdown"
    )

    # 验证报告内容
    assert "# 工作流对比报告" in report
    assert "wf-fast" in report
    assert "wf-slow" in report
    assert "wf-medium" in report

    # 验证表格列
    assert "工作流 ID" in report
    assert "任务数" in report
    assert "成功率" in report
    assert "吞吐量" in report
    assert "P95 延迟" in report

    # 验证数据
    assert "98.0%" in report  # wf-fast 成功率
    assert "90.0%" in report  # wf-slow 成功率
    assert "95.0%" in report  # wf-medium 成功率
    assert "10.0" in report  # wf-fast 吞吐量
    assert "5.0" in report  # wf-slow 吞吐量
    assert "800ms" in report or "0.8" in report  # wf-fast P95 延迟

    # 验证亮点部分
    assert "对比亮点" in report or "亮点" in report
    assert "最高吞吐量" in report or "吞吐量" in report
    assert "最低延迟" in report or "延迟" in report
    assert "最高成功率" in report or "成功率" in report


@pytest.mark.asyncio
async def test_generate_comparison_report_workflow_not_found(
    storage: MetricsStorage,
) -> None:
    """测试对比报告时工作流不存在"""
    # 只存储一个工作流
    metrics = WorkflowMetrics(
        workflow_id="wf-exists",
        total_tasks=100,
        completed_tasks=95,
        failed_tasks=5,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=10.0,
        avg_task_duration=0.5,
        p50_task_duration=0.4,
        p95_task_duration=0.8,
        p99_task_duration=1.0,
        peak_memory_mb=100.0,
        avg_cpu_percent=40.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0,
    )
    await storage.store_workflow_metrics(metrics)

    reporter = ReportGenerator(storage)

    # 尝试对比不存在的工作流
    with pytest.raises(ValueError, match="Workflow .* not found"):
        await reporter.generate_comparison_report(
            ["wf-exists", "wf-not-exists"], output_format="markdown"
        )


@pytest.mark.asyncio
async def test_generate_comparison_report_json(storage: MetricsStorage) -> None:
    """测试生成 JSON 对比报告"""
    import json

    # 准备两个工作流的测试数据
    workflows = [
        WorkflowMetrics(
            workflow_id="wf-001",
            total_tasks=100,
            completed_tasks=95,
            failed_tasks=5,
            cancelled_tasks=0,
            started_at=datetime(2026, 3, 8, 10, 0, 0),
            completed_at=datetime(2026, 3, 8, 10, 10, 0),
            total_duration=600.0,
            throughput=10.0,
            avg_task_duration=0.5,
            p50_task_duration=0.4,
            p95_task_duration=0.8,
            p99_task_duration=1.0,
            peak_memory_mb=100.0,
            avg_cpu_percent=40.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        ),
        WorkflowMetrics(
            workflow_id="wf-002",
            total_tasks=100,
            completed_tasks=90,
            failed_tasks=10,
            cancelled_tasks=0,
            started_at=datetime(2026, 3, 8, 11, 0, 0),
            completed_at=datetime(2026, 3, 8, 11, 20, 0),
            total_duration=1200.0,
            throughput=5.0,
            avg_task_duration=1.0,
            p50_task_duration=0.8,
            p95_task_duration=2.5,
            p99_task_duration=3.0,
            peak_memory_mb=200.0,
            avg_cpu_percent=60.0,
            db_query_count=0,
            db_total_time=0.0,
            db_avg_query_time=0.0,
        ),
    ]

    # 存储所有工作流指标
    for wf_metrics in workflows:
        await storage.store_workflow_metrics(wf_metrics)

    # 生成 JSON 对比报告
    reporter = ReportGenerator(storage)
    report = await reporter.generate_comparison_report(
        ["wf-001", "wf-002"], output_format="json"
    )

    # 解析并验证
    data = json.loads(report)
    assert "workflows" in data
    assert len(data["workflows"]) == 2
    assert data["workflows"][0]["workflow_id"] == "wf-001"
    assert data["workflows"][0]["total_tasks"] == 100
    assert abs(data["workflows"][0]["success_rate"] - 95.0) < 0.1
    assert data["workflows"][1]["workflow_id"] == "wf-002"
    assert abs(data["workflows"][1]["success_rate"] - 90.0) < 0.1

    # 验证亮点
    assert "highlights" in data
    assert "best_throughput" in data["highlights"]
    assert "best_latency" in data["highlights"]
    assert "best_success_rate" in data["highlights"]
    assert data["highlights"]["best_throughput"]["workflow_id"] == "wf-001"
    assert data["highlights"]["best_latency"]["workflow_id"] == "wf-001"
    assert data["highlights"]["best_success_rate"]["workflow_id"] == "wf-001"


@pytest.mark.asyncio
async def test_export_metrics_workflow_not_found(storage: MetricsStorage) -> None:
    """测试 export_metrics 工作流不存在"""
    reporter = ReportGenerator(storage)
    with pytest.raises(ValueError, match="Workflow .* not found"):
        await reporter.export_metrics("test-001")


@pytest.mark.asyncio
async def test_generate_markdown_report(storage: MetricsStorage) -> None:
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
        db_avg_query_time=0.0,
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
            retry_count=0,
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
            retry_count=0,
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
            retry_count=1,
        ),
    ]
    for task_metric in task_metrics:
        await storage.store_task_metrics(task_metric)

    # 生成报告
    reporter = ReportGenerator(storage)
    report = await reporter.generate_workflow_report(
        "wf-test-001", output_format="markdown"
    )

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
    assert "3.50s" in report or "3.5s" in report
    assert "失败任务" in report
    assert "failed_task" in report


@pytest.mark.asyncio
async def test_generate_report_workflow_not_found(storage: MetricsStorage) -> None:
    """测试工作流不存在时抛出 ValueError"""
    reporter = ReportGenerator(storage)
    with pytest.raises(ValueError, match="Workflow .* not found"):
        await reporter.generate_workflow_report("non-existent")


@pytest.mark.asyncio
async def test_generate_report_empty_tasks(storage: MetricsStorage) -> None:
    """测试空任务列表"""
    metrics = WorkflowMetrics(
        workflow_id="wf-empty",
        total_tasks=0,
        completed_tasks=0,
        failed_tasks=0,
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
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
    await storage.store_workflow_metrics(metrics)

    reporter = ReportGenerator(storage)
    report = await reporter.generate_workflow_report("wf-empty")

    # 验证可以处理空任务列表
    assert "# 工作流性能报告" in report
    assert "wf-empty" in report
    assert "总任务数" in report
    assert "0" in report
    # 空任务时不应该有慢任务和失败任务部分
    # 但应该有其他基础信息


@pytest.mark.asyncio
async def test_generate_report_db_not_initialized() -> None:
    """测试数据库未初始化时抛出 RuntimeError"""
    storage = MetricsStorage(":memory:")
    # 故意不调用 initialize()
    reporter = ReportGenerator(storage)
    with pytest.raises(RuntimeError, match="Database not initialized"):
        await reporter.generate_workflow_report("test-001")


@pytest.mark.asyncio
async def test_generate_json_report(storage: MetricsStorage) -> None:
    """测试生成 JSON 报告"""
    import json

    # 准备测试数据
    metrics = WorkflowMetrics(
        workflow_id="wf-test-002",
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
        db_avg_query_time=0.0,
    )
    await storage.store_workflow_metrics(metrics)

    # 添加任务指标
    task_metrics = [
        TaskMetrics(
            task_id="task-1",
            task_name="llm:summarize",
            tool_name="llm:summarize",
            workflow_id="wf-test-002",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=3.5,
            status="completed",
            retry_count=0,
        ),
        TaskMetrics(
            task_id="task-2",
            task_name="mcp:api:call",
            tool_name="mcp:api:call",
            workflow_id="wf-test-002",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=2.8,
            status="completed",
            retry_count=0,
        ),
        TaskMetrics(
            task_id="task-3",
            task_name="failed_task",
            tool_name="mcp:api:call",
            workflow_id="wf-test-002",
            started_at=datetime.now(),
            completed_at=datetime.now(),
            duration=0.5,
            status="failed",
            retry_count=1,
        ),
    ]
    for task_metric in task_metrics:
        await storage.store_task_metrics(task_metric)

    # 生成 JSON 报告
    reporter = ReportGenerator(storage)
    report = await reporter.generate_workflow_report(
        "wf-test-002", output_format="json"
    )

    # 解析并验证
    data = json.loads(report)
    assert data["workflow_id"] == "wf-test-002"
    assert data["summary"]["total_tasks"] == 100
    assert data["summary"]["completed_tasks"] == 95
    assert data["summary"]["failed_tasks"] == 3
    assert abs(data["summary"]["success_rate"] - 95.0) < 0.1
    assert "performance" in data
    assert data["performance"]["throughput"] == 18.5
    assert "slow_tasks" in data
    assert len(data["slow_tasks"]) > 0
    assert "failed_tasks" in data
    assert len(data["failed_tasks"]) > 0


@pytest.mark.asyncio
async def test_export_metrics(storage: MetricsStorage) -> None:
    """测试导出原始指标"""
    import json

    # 准备测试数据
    metrics = WorkflowMetrics(
        workflow_id="wf-test-003",
        total_tasks=50,
        completed_tasks=48,
        failed_tasks=2,
        cancelled_tasks=0,
        started_at=datetime(2026, 3, 8, 16, 0, 0),
        completed_at=datetime(2026, 3, 8, 16, 5, 0),
        total_duration=300.0,
        throughput=10.0,
        avg_task_duration=0.5,
        p50_task_duration=0.4,
        p95_task_duration=1.0,
        p99_task_duration=1.5,
        peak_memory_mb=64.0,
        avg_cpu_percent=30.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0,
    )
    await storage.store_workflow_metrics(metrics)

    # 添加任务指标
    task_metric = TaskMetrics(
        task_id="task-export-1",
        task_name="test_task",
        tool_name="test_tool",
        workflow_id="wf-test-003",
        started_at=datetime.now(),
        completed_at=datetime.now(),
        duration=0.5,
        status="completed",
        retry_count=0,
    )
    await storage.store_task_metrics(task_metric)

    # 导出指标
    reporter = ReportGenerator(storage)
    export = await reporter.export_metrics("wf-test-003", output_format="json")

    # 解析并验证
    data = json.loads(export)
    assert "workflow_metrics" in data
    assert "task_metrics" in data
    assert "traces" in data
    assert data["workflow_metrics"]["workflow_id"] == "wf-test-003"
    assert data["workflow_metrics"]["total_tasks"] == 50
    assert len(data["task_metrics"]) >= 1
    assert data["task_metrics"][0]["task_id"] == "task-export-1"
