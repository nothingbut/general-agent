"""AlertManager 测试"""

import pytest
from unittest.mock import AsyncMock, Mock
from datetime import datetime
from src.workflow.performance.alerts import AlertManager, AlertConfig, Alert


def test_alert_config_defaults() -> None:
    """测试 AlertConfig 默认值"""
    config = AlertConfig()
    assert config.failure_rate_threshold == 0.05
    assert config.p95_latency_threshold == 2.0
    assert config.p99_latency_threshold == 5.0
    assert config.memory_threshold_mb == 500.0
    assert config.cpu_threshold_percent == 80.0
    assert config.db_query_time_threshold == 1.0
    assert "high_failure_rate" in config.priority_mapping


def test_alert_config_custom_values() -> None:
    """测试 AlertConfig 自定义值"""
    config = AlertConfig(
        failure_rate_threshold=0.1,
        p95_latency_threshold=3.0,
        memory_threshold_mb=1000.0,
    )
    assert config.failure_rate_threshold == 0.1
    assert config.p95_latency_threshold == 3.0
    assert config.memory_threshold_mb == 1000.0


def test_alert_initialization() -> None:
    """测试 Alert 初始化"""
    alert = Alert(
        alert_type="high_failure_rate",
        severity="high",
        message="失败率过高",
        workflow_id="wf-test",
        metrics={"failure_rate": 0.1},
    )
    assert alert.alert_type == "high_failure_rate"
    assert alert.severity == "high"
    assert alert.message == "失败率过高"
    assert alert.workflow_id == "wf-test"
    assert alert.metrics["failure_rate"] == 0.1
    assert isinstance(alert.timestamp, datetime)


def test_alert_manager_initialization() -> None:
    """测试 AlertManager 初始化"""
    config = AlertConfig()
    manager = AlertManager(config)
    assert manager.config == config
    assert manager.notifier is None
    assert len(manager.active_alerts) == 0


def test_alert_manager_with_notifier() -> None:
    """测试 AlertManager 带通知器初始化"""
    config = AlertConfig()
    mock_notifier = object()
    manager = AlertManager(config, notifier=mock_notifier)
    assert manager.config == config
    assert manager.notifier is mock_notifier


@pytest.mark.asyncio
async def test_check_high_failure_rate() -> None:
    """测试高失败率告警"""
    from src.workflow.performance.collector import WorkflowMetrics

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
        db_avg_query_time=0.0,
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 1
    assert alerts[0].alert_type == "high_failure_rate"
    assert alerts[0].severity == "high"
    assert "10.0%" in alerts[0].message or "0.1" in alerts[0].message


@pytest.mark.asyncio
async def test_check_high_p95_latency() -> None:
    """测试高 P95 延迟告警"""
    from src.workflow.performance.collector import WorkflowMetrics

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
        db_avg_query_time=0.0,
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 1
    assert alerts[0].alert_type == "high_p95_latency"
    assert alerts[0].severity == "medium"


@pytest.mark.asyncio
async def test_check_multiple_alerts() -> None:
    """测试多个告警同时触发"""
    from src.workflow.performance.collector import WorkflowMetrics

    config = AlertConfig(failure_rate_threshold=0.05, memory_threshold_mb=100.0)
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
        db_avg_query_time=0.0,
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 2
    alert_types = [a.alert_type for a in alerts]
    assert "high_failure_rate" in alert_types
    assert "memory_exhaustion" in alert_types


@pytest.mark.asyncio
async def test_alert_deduplication() -> None:
    """测试告警去重"""
    from src.workflow.performance.collector import WorkflowMetrics

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
        db_avg_query_time=0.0,
    )

    # 第一次检查
    alerts1 = await manager.check_alerts(metrics)
    assert len(alerts1) == 1

    # 第二次检查（应该去重）
    alerts2 = await manager.check_alerts(metrics)
    assert len(alerts2) == 0  # 相同告警不重复触发


@pytest.mark.asyncio
async def test_check_all_alert_types() -> None:
    """测试所有 6 种告警类型"""
    from src.workflow.performance.collector import WorkflowMetrics

    config = AlertConfig(
        failure_rate_threshold=0.05,
        p95_latency_threshold=2.0,
        p99_latency_threshold=5.0,
        memory_threshold_mb=100.0,
        cpu_threshold_percent=80.0,
        db_query_time_threshold=1.0,
    )
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-005",
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
        p95_task_duration=3.0,  # 触发 P95 延迟告警
        p99_task_duration=6.0,  # 触发 P99 延迟告警
        peak_memory_mb=150.0,  # 触发内存告警
        avg_cpu_percent=85.0,  # 触发 CPU 告警
        db_query_count=10,
        db_total_time=15.0,
        db_avg_query_time=1.5,  # 触发数据库告警
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 6
    alert_types = {a.alert_type for a in alerts}
    assert alert_types == {
        "high_failure_rate",
        "high_p95_latency",
        "high_p99_latency",
        "memory_exhaustion",
        "high_cpu_usage",
        "slow_database",
    }


@pytest.mark.asyncio
async def test_check_no_alerts() -> None:
    """测试正常指标不触发告警"""
    from src.workflow.performance.collector import WorkflowMetrics

    config = AlertConfig()
    manager = AlertManager(config)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-006",
        total_tasks=100,
        completed_tasks=100,
        failed_tasks=0,  # 0% 失败率
        cancelled_tasks=0,
        started_at=datetime.now(),
        completed_at=datetime.now(),
        total_duration=100.0,
        throughput=1.0,
        avg_task_duration=0.5,
        p50_task_duration=0.5,
        p95_task_duration=0.5,  # 低于阈值
        p99_task_duration=1.0,  # 低于阈值
        peak_memory_mb=100.0,  # 低于阈值
        avg_cpu_percent=50.0,  # 低于阈值
        db_query_count=10,
        db_total_time=5.0,
        db_avg_query_time=0.5,  # 低于阈值
    )

    alerts = await manager.check_alerts(metrics)

    assert len(alerts) == 0


@pytest.mark.asyncio
async def test_send_notification() -> None:
    """测试发送通知"""
    from src.workflow.performance.collector import WorkflowMetrics

    # 创建 mock notifier
    mock_notifier = Mock()
    mock_notifier.send_notification = AsyncMock()

    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config, notifier=mock_notifier)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-007",
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
        peak_memory_mb=100.0,
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0,
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
async def test_no_notification_without_notifier() -> None:
    """测试没有 notifier 时只记录日志"""
    from src.workflow.performance.collector import WorkflowMetrics

    config = AlertConfig(failure_rate_threshold=0.05)
    manager = AlertManager(config, notifier=None)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-008",
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
        db_avg_query_time=0.0,
    )

    # 应该正常工作，只是不发送通知
    alerts = await manager.check_alerts(metrics)
    assert len(alerts) == 1


@pytest.mark.asyncio
async def test_notification_priority_mapping() -> None:
    """测试不同告警类型的优先级映射"""
    from src.workflow.performance.collector import WorkflowMetrics

    # 创建 mock notifier
    mock_notifier = Mock()
    mock_notifier.send_notification = AsyncMock()

    config = AlertConfig(
        p95_latency_threshold=2.0,
        memory_threshold_mb=100.0,
    )
    manager = AlertManager(config, notifier=mock_notifier)

    metrics = WorkflowMetrics(
        workflow_id="wf-test-009",
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
        p95_task_duration=3.0,  # 触发 P95 告警 (medium)
        p99_task_duration=4.0,
        peak_memory_mb=150.0,  # 触发内存告警 (high)
        avg_cpu_percent=50.0,
        db_query_count=0,
        db_total_time=0.0,
        db_avg_query_time=0.0,
    )

    alerts = await manager.check_alerts(metrics)

    # 验证两个告警都被触发
    assert len(alerts) == 2
    assert mock_notifier.send_notification.call_count == 2

    # 获取所有调用
    calls = mock_notifier.send_notification.call_args_list

    # 验证优先级映射正确
    priorities = [call.kwargs["priority"] for call in calls]
    assert "medium" in priorities  # P95 延迟 -> medium
    assert "high" in priorities  # 内存 -> high
