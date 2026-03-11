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
    priority_mapping: Dict[str, str] = field(
        default_factory=lambda: {
            "high_failure_rate": "high",
            "high_p95_latency": "medium",
            "high_p99_latency": "high",
            "memory_exhaustion": "high",
            "high_cpu_usage": "medium",
            "slow_database": "medium",
        }
    )


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

    def __init__(self, config: AlertConfig, notifier: Optional[Any] = None) -> None:
        """初始化

        Args:
            config: 告警配置
            notifier: 通知管理器（可选）
        """
        self.config = config
        self.notifier = notifier
        self.active_alerts: Dict[str, List[Alert]] = {}

    async def check_alerts(self, metrics: WorkflowMetrics) -> List[Alert]:
        """检查工作流级别告警

        Args:
            metrics: 工作流指标

        Returns:
            触发的告警列表
        """
        alerts = []

        # 1. 检查失败率
        if metrics.total_tasks > 0:
            failure_rate = metrics.failed_tasks / metrics.total_tasks
            if failure_rate > self.config.failure_rate_threshold:
                if self._should_alert(metrics.workflow_id, "high_failure_rate"):
                    alerts.append(
                        Alert(
                            alert_type="high_failure_rate",
                            severity="high",
                            message=f"失败率过高: {failure_rate:.1%} (阈值: {self.config.failure_rate_threshold:.1%})",
                            workflow_id=metrics.workflow_id,
                            metrics={"failure_rate": failure_rate},
                        )
                    )

        # 2. 检查 P95 延迟
        if metrics.p95_task_duration > self.config.p95_latency_threshold:
            if self._should_alert(metrics.workflow_id, "high_p95_latency"):
                alerts.append(
                    Alert(
                        alert_type="high_p95_latency",
                        severity="medium",
                        message=f"P95 延迟过高: {metrics.p95_task_duration:.2f}s (阈值: {self.config.p95_latency_threshold:.2f}s)",
                        workflow_id=metrics.workflow_id,
                        metrics={"p95_latency": metrics.p95_task_duration},
                    )
                )

        # 3. 检查 P99 延迟
        if metrics.p99_task_duration > self.config.p99_latency_threshold:
            if self._should_alert(metrics.workflow_id, "high_p99_latency"):
                alerts.append(
                    Alert(
                        alert_type="high_p99_latency",
                        severity="high",
                        message=f"P99 延迟过高: {metrics.p99_task_duration:.2f}s (阈值: {self.config.p99_latency_threshold:.2f}s)",
                        workflow_id=metrics.workflow_id,
                        metrics={"p99_latency": metrics.p99_task_duration},
                    )
                )

        # 4. 检查内存使用
        if metrics.peak_memory_mb > self.config.memory_threshold_mb:
            if self._should_alert(metrics.workflow_id, "memory_exhaustion"):
                alerts.append(
                    Alert(
                        alert_type="memory_exhaustion",
                        severity="high",
                        message=f"内存使用过高: {metrics.peak_memory_mb:.1f}MB (阈值: {self.config.memory_threshold_mb:.1f}MB)",
                        workflow_id=metrics.workflow_id,
                        metrics={"peak_memory_mb": metrics.peak_memory_mb},
                    )
                )

        # 5. 检查 CPU 使用
        if metrics.avg_cpu_percent > self.config.cpu_threshold_percent:
            if self._should_alert(metrics.workflow_id, "high_cpu_usage"):
                alerts.append(
                    Alert(
                        alert_type="high_cpu_usage",
                        severity="medium",
                        message=f"CPU 使用率过高: {metrics.avg_cpu_percent:.1f}% (阈值: {self.config.cpu_threshold_percent:.1f}%)",
                        workflow_id=metrics.workflow_id,
                        metrics={"avg_cpu_percent": metrics.avg_cpu_percent},
                    )
                )

        # 6. 检查数据库性能
        if metrics.db_avg_query_time > self.config.db_query_time_threshold:
            if self._should_alert(metrics.workflow_id, "slow_database"):
                alerts.append(
                    Alert(
                        alert_type="slow_database",
                        severity="medium",
                        message=f"数据库查询慢: {metrics.db_avg_query_time:.2f}s (阈值: {self.config.db_query_time_threshold:.2f}s)",
                        workflow_id=metrics.workflow_id,
                        metrics={"db_avg_query_time": metrics.db_avg_query_time},
                    )
                )

        # 发送通知并记录活动告警
        for alert in alerts:
            await self._send_notification(alert)
            # 记录到活动告警
            if metrics.workflow_id not in self.active_alerts:
                self.active_alerts[metrics.workflow_id] = []
            self.active_alerts[metrics.workflow_id].append(alert)

        return alerts

    async def check_task_alert(self, task_metrics: TaskMetrics) -> List[Alert]:
        """检查任务级别告警（可选）

        Args:
            task_metrics: 任务指标

        Returns:
            触发的告警列表
        """
        raise NotImplementedError

    async def _send_notification(self, alert: Alert) -> None:
        """发送告警通知（内部方法）

        Args:
            alert: 告警对象
        """
        if not self.notifier:
            # 没有通知器，只记录日志
            logger.warning(f"Alert: {alert.alert_type} - {alert.message}")
            return

        # 映射告警类型到通知优先级
        priority = self.config.priority_mapping.get(alert.alert_type, "medium")

        # 发送通知
        await self.notifier.send_notification(
            title=f"⚠️ 性能告警: {alert.alert_type}",
            message=alert.message,
            priority=priority,
            channels=["terminal", "desktop"],
        )

    def _should_alert(self, workflow_id: str, alert_type: str) -> bool:
        """去重检查（内部方法）

        避免同一工作流的相同告警重复发送

        Args:
            workflow_id: 工作流 ID
            alert_type: 告警类型

        Returns:
            是否应该发送告警
        """
        if workflow_id not in self.active_alerts:
            return True

        # 检查是否已经有相同类型的告警
        for alert in self.active_alerts[workflow_id]:
            if alert.alert_type == alert_type:
                return False

        return True
