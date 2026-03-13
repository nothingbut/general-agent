"""MonitoringDashboard 测试"""

import pytest
from rich.console import Console
from src.workflow.performance.dashboard import MonitoringDashboard
from src.workflow.performance.monitor import PerformanceMonitor
from src.workflow.performance.storage import MetricsStorage


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
    try:
        dashboard.display_snapshot("wf-test-001")
        assert True
    except Exception as e:
        pytest.fail(f"display_snapshot raised {e}")


def test_build_layout(monitor):
    """测试构建布局"""
    from src.workflow.performance.collector import WorkflowMetrics
    from datetime import datetime
    from rich.layout import Layout

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
        db_avg_query_time=0.0,
    )

    dashboard = MonitoringDashboard(monitor)
    layout = dashboard._build_layout(metrics)

    # 验证返回的是 Layout 对象
    assert isinstance(layout, Layout)


def test_display_snapshot_workflow_not_found(monitor):
    """测试快照模式显示不存在的工作流"""
    dashboard = MonitoringDashboard(monitor)

    # 应该不抛出异常，只显示消息
    try:
        dashboard.display_snapshot("non-existent-workflow")
        assert True
    except Exception as e:
        pytest.fail(f"display_snapshot raised {e}")


@pytest.mark.asyncio
async def test_display_realtime(monitor):
    """测试实时监控模式"""
    import asyncio

    # 准备测试数据
    await monitor.on_workflow_start("wf-test-003", 100)

    dashboard = MonitoringDashboard(monitor)

    # 创建一个任务来停止实时监控
    async def stop_after_delay():
        await asyncio.sleep(1)  # 运行 1 秒后停止
        dashboard._stop_event.set()

    # 启动实时监控和停止任务
    stop_task = asyncio.create_task(stop_after_delay())

    try:
        await dashboard.display_realtime("wf-test-003", refresh_interval=0.2)
        assert True
    except Exception as e:
        pytest.fail(f"display_realtime raised {e}")
    finally:
        await stop_task


@pytest.mark.asyncio
async def test_display_realtime_workflow_completed(monitor):
    """测试实时监控模式 - 工作流完成后自动停止"""
    import asyncio

    # 准备测试数据
    await monitor.on_workflow_start("wf-test-004", 10)

    # 立即完成工作流
    result = {"status": "completed"}
    await monitor.on_workflow_complete("wf-test-004", result)

    dashboard = MonitoringDashboard(monitor)

    # 实时监控应该检测到工作流已完成并自动停止
    try:
        await asyncio.wait_for(
            dashboard.display_realtime("wf-test-004", refresh_interval=0.1), timeout=2.0
        )
        assert True
    except asyncio.TimeoutError:
        pytest.fail("display_realtime did not stop when workflow completed")


def test_build_empty_layout(monitor):
    """测试构建空布局"""
    from rich.panel import Panel

    dashboard = MonitoringDashboard(monitor)
    layout = dashboard._build_empty_layout()

    # 验证返回的是 Panel 对象
    assert isinstance(layout, Panel)


@pytest.mark.asyncio
async def test_display_summary(monitor):
    """测试显示执行摘要"""

    # 准备测试数据
    await monitor.on_workflow_start("wf-test-summary", 10)

    # 模拟任务执行
    for i in range(10):
        task_id = f"task-{i}"
        task_info = {"workflow_id": "wf-test-summary"}
        await monitor.on_task_start(task_id, task_info)

        result = {
            "task_name": f"task-{i}",
            "tool_name": "test_tool",
            "status": "completed" if i < 8 else "failed",
            "retry_count": 0,
        }
        await monitor.on_task_complete(task_id, 0.1 * (i + 1), result)

    # 完成工作流
    await monitor.on_workflow_complete("wf-test-summary", {"status": "completed"})

    # 创建 dashboard 并显示摘要
    dashboard = MonitoringDashboard(monitor)

    # 这个测试主要验证不抛出异常
    await dashboard.display_summary("wf-test-summary")
    assert True
