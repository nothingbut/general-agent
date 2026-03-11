# Task 7.3.4.2 监控面板和报告设计文档

**创建时间:** 2026-03-08
**状态:** 已批准
**任务:** Phase 7.3.4.2 - 实现监控面板和报告

---

## 1. 概述

### 1.1 目标

为性能监控框架（Task 7.3.4.1 已完成）添加可视化和报告能力：
- **MonitoringDashboard** - Rich TUI 实时监控面板
- **ReportGenerator** - 报告生成器（Markdown/JSON）
- **AlertManager** - 告警管理器（集成 notification.py）

### 1.2 设计原则

- **职责分离**：3 个独立组件，各司其职
- **依赖注入**：通过构造函数注入依赖，易于测试
- **复用已有能力**：利用 Rich TUI 经验（approval_ui.py）和通知系统（notification.py）
- **灵活性优先**：支持实时/快照模式、多种报告格式、可选通知

---

## 2. 整体架构

### 2.1 文件结构

```
src/workflow/performance/
├── monitor.py          # 已完成 - PerformanceMonitor
├── collector.py        # 已完成 - MetricsCollector
├── tracer.py          # 已完成 - TraceRecorder
├── storage.py         # 已完成 - MetricsStorage
├── dashboard.py       # 新增 - MonitoringDashboard
├── reporter.py        # 新增 - ReportGenerator
└── alerts.py          # 新增 - AlertManager

tests/workflow/performance/
├── test_dashboard.py  # 新增 - Dashboard 测试
├── test_reporter.py   # 新增 - Reporter 测试
└── test_alerts.py     # 新增 - AlertManager 测试
```

### 2.2 组件依赖关系

```
MonitoringDashboard
    ↓ 依赖
PerformanceMonitor ←─── ReportGenerator
    ↓                       ↓
MetricsStorage         AlertManager
    ↓                       ↓
Database            NotificationManager (已有)
```

### 2.3 设计方案

**选择：独立组件架构**

- 3 个独立的类，各自封装职责
- 通过依赖注入获取所需服务
- 用户可以根据需求选择使用哪些组件

**拒绝的方案：**
- ❌ 统一门面模式：违反单一职责原则，难以独立使用
- ❌ 混合方案：增加不必要的复杂度

---

## 3. MonitoringDashboard 设计

### 3.1 使用场景

**混合模式（已选择）：**
- **实时监控模式**：持续刷新，类似 `htop`，用于工作流执行期间
- **快照查询模式**：按需显示，用于事后分析

### 3.2 面板布局

```
┌─ 工作流监控: wf-12345 ──────────────────────────────┐
│ 状态: RUNNING  |  进度: 45/100 (45%)                │
│ 执行时间: 00:02:34  |  吞吐量: 18 tasks/s          │
└─────────────────────────────────────────────────────┘

┌─ 性能指标 ──────────────────────────────────────────┐
│ 平均延迟:   240ms    P95:  850ms    P99: 1.2s      │
│ CPU 使用:   45%      内存: 128 MB                   │
└─────────────────────────────────────────────────────┘

┌─ 任务状态 ──────────────────────────────────────────┐
│ ✓ 完成: 42  |  ✗ 失败: 1  |  🔄 重试: 2  |  ⏳ 运行: 5 │
└─────────────────────────────────────────────────────┘

┌─ 最近任务 ──────────────────────────────────────────┐
│ 15:30:45  task-42  ✓  llm:chat        150ms        │
│ 15:30:44  task-41  ✓  mcp:file:read    80ms        │
│ 15:30:43  task-40  ✗  mcp:api:call   TIMEOUT       │
└─────────────────────────────────────────────────────┘

按 Ctrl+C 停止监控
```

### 3.3 核心接口

```python
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
        # 使用 Rich Live 组件持续更新
        # 捕获 Ctrl+C 优雅退出

    def display_snapshot(self, workflow_id: str) -> None:
        """快照模式（显示一次）

        Args:
            workflow_id: 工作流 ID
        """
        # 获取当前指标并显示
        # 不自动刷新

    def display_summary(self, workflow_id: str) -> None:
        """显示执行摘要

        Args:
            workflow_id: 工作流 ID
        """
        # 显示完整的执行摘要

    def _build_layout(
        self,
        metrics: WorkflowMetrics
    ) -> Layout:
        """构建面板布局（内部方法）"""
        # 创建 Rich Layout 对象
        # 包含 4 个面板
```

### 3.4 技术实现

**Rich 组件选择：**
- **Live**：实时刷新容器
- **Panel**：面板边框
- **Table**：表格展示
- **Text**：带样式文本
- **Layout**：布局管理

**实时刷新机制：**
```python
from rich.live import Live

async def display_realtime(self, workflow_id: str, refresh_interval: float = 1.0):
    with Live(self._build_layout(metrics), console=self.console, refresh_per_second=1) as live:
        while not self._stop_event.is_set():
            try:
                # 获取最新指标
                metrics = self.monitor.get_current_metrics(workflow_id)
                if metrics:
                    # 更新布局
                    live.update(self._build_layout(metrics))

                await asyncio.sleep(refresh_interval)
            except KeyboardInterrupt:
                break
```

**优雅退出：**
- 捕获 `KeyboardInterrupt` (Ctrl+C)
- 设置 `_stop_event` 停止刷新循环
- 清理 Rich Live 资源

### 3.5 设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 监控模式 | 混合模式（实时 + 快照） | 提供最大灵活性 |
| TUI 库 | Rich | 已有经验（approval_ui.py），轻量级 |
| 刷新策略 | 可配置间隔（默认 1 秒） | 平衡实时性和性能 |
| 退出机制 | Ctrl+C 优雅退出 | 用户友好，清理资源 |

---

## 4. ReportGenerator 设计

### 4.1 支持格式

**核心格式（已选择）：**
- **Markdown** - 人类可读，适合文档和归档
- **JSON** - 机器可读，便于程序化处理和集成

**未包含（后续迭代）：**
- HTML - 需要模板引擎和图表库
- CSV - 使用场景较少，JSON 可轻松转换

### 4.2 报告类型

#### 4.2.1 单工作流报告

**Markdown 示例：**
```markdown
# 工作流性能报告

**工作流 ID:** wf-12345
**执行时间:** 2026-03-08 15:30:00 - 15:32:34
**总时长:** 154.2 秒

## 执行概览

| 指标 | 数值 |
|------|------|
| 总任务数 | 100 |
| 完成任务 | 95 |
| 失败任务 | 3 |
| 取消任务 | 2 |
| 成功率 | 95.0% |

## 性能指标

| 指标 | 数值 |
|------|------|
| 吞吐量 | 18.5 tasks/s |
| 平均延迟 | 240ms |
| P50 延迟 | 120ms |
| P95 延迟 | 850ms |
| P99 延迟 | 1.2s |

## 资源使用

- **峰值内存:** 128 MB
- **平均 CPU:** 45%

## 慢任务 Top 5

1. task-87: llm:summarize - 3.5s
2. task-42: mcp:api:call - 2.8s
3. task-13: llm:translate - 2.1s
4. task-78: mcp:file:write - 1.8s
5. task-23: llm:extract - 1.5s

## 失败任务

1. task-56: mcp:api:call - TIMEOUT
2. task-89: llm:chat - RATE_LIMIT
3. task-12: mcp:db:query - CONNECTION_ERROR
```

**JSON 结构：**
```json
{
  "workflow_id": "wf-12345",
  "execution": {
    "started_at": "2026-03-08T15:30:00",
    "completed_at": "2026-03-08T15:32:34",
    "total_duration": 154.2
  },
  "summary": {
    "total_tasks": 100,
    "completed_tasks": 95,
    "failed_tasks": 3,
    "cancelled_tasks": 2,
    "success_rate": 0.95
  },
  "performance": {
    "throughput": 18.5,
    "avg_latency": 0.24,
    "p50_latency": 0.12,
    "p95_latency": 0.85,
    "p99_latency": 1.2
  },
  "resources": {
    "peak_memory_mb": 128,
    "avg_cpu_percent": 45
  },
  "slow_tasks": [
    {"task_id": "task-87", "name": "llm:summarize", "duration": 3.5},
    {"task_id": "task-42", "name": "mcp:api:call", "duration": 2.8}
  ],
  "failed_tasks": [
    {"task_id": "task-56", "name": "mcp:api:call", "error": "TIMEOUT"}
  ]
}
```

#### 4.2.2 对比报告

用于比较多个工作流的性能：

```markdown
# 工作流性能对比

## 概览

| 工作流 | 任务数 | 成功率 | 吞吐量 | P95 延迟 |
|--------|--------|--------|--------|----------|
| wf-001 | 100 | 95% | 18.5/s | 850ms |
| wf-002 | 150 | 98% | 22.3/s | 720ms |
| wf-003 | 200 | 92% | 15.8/s | 1.2s |

## 性能对比

- **最快吞吐量:** wf-002 (22.3 tasks/s)
- **最低延迟:** wf-002 (P95: 720ms)
- **最高成功率:** wf-002 (98%)
```

#### 4.2.3 指标导出

原始指标数据导出（JSON 格式）：
```json
{
  "workflow_metrics": {...},
  "task_metrics": [...],
  "traces": [...]
}
```

### 4.3 核心接口

```python
class ReportGenerator:
    """报告生成器"""

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

    def _calculate_derived_metrics(
        self,
        metrics: WorkflowMetrics,
        task_metrics: List[TaskMetrics]
    ) -> Dict[str, Any]:
        """计算派生指标（内部方法）

        包括：
        - 成功率
        - 慢任务 Top N
        - 失败任务列表
        """
```

### 4.4 技术实现

**Markdown 生成：**
- 使用 f-string 模板
- 不引入额外依赖（如 Jinja2）
- 简单、可维护

**JSON 生成：**
- 使用 `json.dumps()` 标准库
- 处理 datetime 序列化

**派生指标计算：**
```python
def _calculate_derived_metrics(self, metrics, task_metrics):
    return {
        "success_rate": metrics.completed_tasks / metrics.total_tasks if metrics.total_tasks > 0 else 0,
        "slow_tasks": sorted(task_metrics, key=lambda t: t.duration, reverse=True)[:5],
        "failed_tasks": [t for t in task_metrics if t.status == "failed"]
    }
```

---

## 5. AlertManager 设计

### 5.1 告警规则

**6 种告警类型：**

| 告警类型 | 检查指标 | 默认阈值 | 严重程度 |
|---------|---------|---------|---------|
| high_failure_rate | 失败率 | > 5% | High |
| high_p95_latency | P95 延迟 | > 2s | Medium |
| high_p99_latency | P99 延迟 | > 5s | High |
| memory_exhaustion | 峰值内存 | > 500MB | High |
| high_cpu_usage | CPU 使用率 | > 80% | Medium |
| slow_database | 平均查询时间 | > 1s | Medium |

### 5.2 配置结构

```python
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
```

### 5.3 核心接口

```python
class AlertManager:
    """告警管理器"""

    def __init__(
        self,
        config: AlertConfig,
        notifier: Optional[NotificationManager] = None
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
        # 检查所有告警规则
        # 触发告警时发送通知

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

    async def _send_notification(self, alert: Alert) -> None:
        """发送告警通知（内部方法）"""
        # 集成 notification.py

    def _should_alert(
        self,
        workflow_id: str,
        alert_type: str
    ) -> bool:
        """去重检查（内部方法）

        避免同一工作流的相同告警重复发送
        """
```

### 5.4 告警检查逻辑

```python
async def check_alerts(self, metrics: WorkflowMetrics) -> List[Alert]:
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
            alerts.append(Alert(...))

    # 3. 检查 P99 延迟
    if metrics.p99_task_duration > self.config.p99_latency_threshold:
        if self._should_alert(metrics.workflow_id, "high_p99_latency"):
            alerts.append(Alert(...))

    # 4. 检查内存使用
    if metrics.peak_memory_mb > self.config.memory_threshold_mb:
        if self._should_alert(metrics.workflow_id, "memory_exhaustion"):
            alerts.append(Alert(...))

    # 5. 检查 CPU 使用
    if metrics.avg_cpu_percent > self.config.cpu_threshold_percent:
        if self._should_alert(metrics.workflow_id, "high_cpu_usage"):
            alerts.append(Alert(...))

    # 6. 检查数据库性能
    if metrics.db_avg_query_time > self.config.db_query_time_threshold:
        if self._should_alert(metrics.workflow_id, "slow_database"):
            alerts.append(Alert(...))

    # 发送通知
    for alert in alerts:
        await self._send_notification(alert)
        # 记录到活动告警
        if metrics.workflow_id not in self.active_alerts:
            self.active_alerts[metrics.workflow_id] = []
        self.active_alerts[metrics.workflow_id].append(alert)

    return alerts
```

### 5.5 集成 notification.py

```python
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

**使用示例：**
```python
from src.workflow.notification import NotificationManager
from src.workflow.performance import AlertManager, AlertConfig

# 创建通知管理器
notifier = NotificationManager()

# 创建告警管理器（集成通知）
alert_manager = AlertManager(
    config=AlertConfig(
        failure_rate_threshold=0.05,
        p95_latency_threshold=2.0
    ),
    notifier=notifier
)

# 检查告警（自动发送通知）
alerts = await alert_manager.check_alerts(metrics)
```

### 5.6 设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 通知集成 | 完全集成 notification.py | 复用已有能力，提供完整告警功能 |
| 阈值配置 | 通过 AlertConfig 可配置 | 灵活性，适应不同场景 |
| 去重机制 | 同一工作流的相同告警不重复 | 避免告警风暴 |
| 可选通知 | notifier 可为 None | 测试友好，默认只记录日志 |

---

## 6. 测试策略

### 6.1 测试覆盖

**单元测试（8+ 用例）：**

| 组件 | 测试用例数 | 覆盖内容 |
|------|-----------|---------|
| MonitoringDashboard | 3 | 初始化、快照显示、布局构建 |
| ReportGenerator | 3 | Markdown 报告、JSON 报告、对比报告 |
| AlertManager | 3 | 告警检查、通知发送、去重机制 |

**集成测试（2+ 用例）：**
- 完整监控流程：监控 → 报告 → 告警
- 真实工作流测试

### 6.2 测试要点

**MonitoringDashboard：**
- Mock `PerformanceMonitor.get_current_metrics()`
- 验证面板布局包含所有关键指标
- 测试 Ctrl+C 退出机制（使用 `asyncio.Event`）

**ReportGenerator：**
- Mock `MetricsStorage.query_workflow_metrics()`
- 验证 Markdown 格式正确（包含所有章节）
- 验证 JSON 可解析且包含所有字段
- 测试派生指标计算（成功率、慢任务）

**AlertManager：**
- Mock `NotificationManager.send_notification()`
- 验证所有告警规则触发条件
- 验证优先级映射正确
- 验证去重机制（相同告警不重复发送）

### 6.3 覆盖率目标

- **目标覆盖率:** ≥ 80%
- **关键路径:** 100% 覆盖（告警检查、报告生成）

---

## 7. 工作量估算

| 任务 | 预计时间 | 说明 |
|------|---------|------|
| dashboard.py | 4-5 小时 | Rich TUI 布局 + 实时刷新逻辑 |
| reporter.py | 3-4 小时 | 模板生成 + 数据聚合 |
| alerts.py | 2-3 小时 | 规则检查 + 通知集成 |
| test_dashboard.py | 1-1.5 小时 | 3 个测试用例 |
| test_reporter.py | 1-1.5 小时 | 3 个测试用例 |
| test_alerts.py | 1-1.5 小时 | 3 个测试用例 |
| **总计** | **约 1 天** | 12-16 小时 |

---

## 8. 实施顺序

按依赖关系实施：

1. **ReportGenerator** - 最独立，只依赖 MetricsStorage
2. **AlertManager** - 依赖 notification.py（已完成）
3. **MonitoringDashboard** - 依赖 PerformanceMonitor（已完成）
4. **集成测试** - 验证三个组件协同工作

---

## 9. 验收标准

### 9.1 功能完整性

- ✅ MonitoringDashboard 支持实时/快照模式
- ✅ ReportGenerator 支持 Markdown/JSON 格式
- ✅ AlertManager 集成 notification.py
- ✅ 所有 6 种告警规则实现

### 9.2 测试通过

- ✅ 8+ 单元测试全部通过
- ✅ 测试覆盖率 ≥ 80%
- ✅ 2+ 集成测试通过

### 9.3 代码质量

- ✅ 类型注解 100% 覆盖
- ✅ 文档字符串完整
- ✅ 符合项目编码规范

---

## 10. 风险和限制

### 10.1 实时刷新性能

**风险:** Rich Live 高频刷新可能影响性能

**缓解措施:**
- 默认刷新间隔 1 秒（可配置）
- 只在指标变化时重绘
- 异步获取指标，不阻塞刷新

### 10.2 告警风暴

**风险:** 频繁触发相同告警

**缓解措施:**
- 去重机制：同一工作流的相同告警不重复
- 可配置阈值，避免过于敏感
- 告警冷却期（可选，后续迭代）

### 10.3 格式扩展性

**风险:** 未来可能需要 HTML/CSV 格式

**缓解措施:**
- 模板化生成，易于扩展
- 预留 format 参数
- JSON 格式可轻松转换为其他格式

---

**设计批准:** ✅ 2026-03-08
**下一步:** 创建实施计划（PLAN.md）
