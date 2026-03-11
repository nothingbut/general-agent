# Phase 7.3.4 性能优化设计文档

**创建时间:** 2026-03-08
**状态:** 已批准
**方案:** 渐进式优化（数据驱动）

---

## 1. 概述

### 1.1 目标

实现 Phase 7 workflow 模块的全面性能优化，包括：
- 建立完整的性能监控体系
- 创建自动化基准测试套件
- 识别并优化性能瓶颈
- 支持混合场景（快速任务 + 长时间任务）

### 1.2 设计原则

- **数据驱动:** 先监控测量，再针对性优化
- **最小侵入:** 通过钩子机制集成，不破坏现有架构
- **可观测性优先:** 完整的指标收集和可视化
- **综合优化:** 平衡吞吐量、延迟、资源利用率、可扩展性

---

## 2. 架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────┐
│                    WorkflowExecutor                      │
│              (现有执行器，添加监控钩子)                   │
└──────────────┬──────────────────────────────────────────┘
               │
               ├─→ PerformanceMonitor (性能监控器)
               │   ├─ MetricsCollector (指标收集)
               │   ├─ TraceRecorder (链路追踪)
               │   └─ AlertManager (告警管理)
               │
               ├─→ MetricsStorage (指标存储)
               │   ├─ In-Memory Buffer (内存缓冲)
               │   └─ SQLite Backend (持久化)
               │
               └─→ MonitoringDashboard (监控面板)
                   ├─ RealTimeView (实时监控 TUI)
                   └─ ReportGenerator (报告生成)
```

### 2.2 集成方式

**观察者模式集成：**
- WorkflowExecutor 触发执行事件
- PerformanceMonitor 订阅并记录指标
- 异步处理，最小化性能开销

**事件钩子：**
- `on_workflow_start(workflow_id, task_count)`
- `on_workflow_complete(workflow_id, result)`
- `on_task_start(task_id, task_info)`
- `on_task_complete(task_id, duration, result)`
- `on_task_retry(task_id, retry_count, reason)`

---

## 3. 核心组件设计

### 3.1 PerformanceMonitor (性能监控器)

**职责：**
- 收集工作流和任务执行指标
- 记录任务执行链路追踪
- 检测性能异常并触发告警

**接口：**
```python
class PerformanceMonitor:
    def __init__(self, storage: MetricsStorage):
        self.metrics_collector = MetricsCollector()
        self.trace_recorder = TraceRecorder()
        self.alert_manager = AlertManager()
        self.storage = storage

    async def on_workflow_start(self, workflow_id: str, task_count: int)
    async def on_workflow_complete(self, workflow_id: str, result: dict)
    async def on_task_start(self, task_id: str, task_info: dict)
    async def on_task_complete(self, task_id: str, duration: float, result: dict)
    async def on_task_retry(self, task_id: str, retry_count: int, reason: str)

    def get_current_metrics(self) -> dict
    def get_task_trace(self, task_id: str) -> TaskTrace
    def get_workflow_summary(self, workflow_id: str) -> WorkflowMetrics
```

### 3.2 MetricsCollector (指标收集器)

**工作流级别指标：**
```python
@dataclass
class WorkflowMetrics:
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
```

**任务级别指标：**
```python
@dataclass
class TaskMetrics:
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
    memory_used: Optional[int]  # 字节
    cpu_time: Optional[float]  # CPU 时间（秒）
```

### 3.3 TraceRecorder (链路追踪)

**追踪执行阶段：**
```python
@dataclass
class TaskTrace:
    task_id: str
    workflow_id: str
    total_duration: float
    spans: List[Span]

@dataclass
class Span:
    name: str  # 阶段名称
    started_at: datetime
    duration: float  # 秒
    metadata: dict
```

**追踪的执行阶段：**
1. **dependency_check** - 依赖检查耗时
2. **approval_wait** - 审批等待耗时（如果需要）
3. **param_resolve** - 参数解析耗时
4. **tool_execution** - 工具执行耗时
5. **result_save** - 结果保存耗时
6. **db_write** - 数据库写入耗时

**用途：**
- 精确定位每个任务的瓶颈阶段
- 识别慢查询、长等待等问题
- 生成火焰图和时间线图

### 3.4 MetricsStorage (指标存储)

**两层存储架构：**

**内存缓冲层：**
- 实时指标存储在内存中
- 支持快速查询（<1ms）
- 使用滑动窗口（保留最近 1 小时）

**持久化层：**
- 定期刷新到 SQLite
- 支持历史查询和对比分析
- 表结构：
  - `workflow_metrics` - 工作流指标
  - `task_metrics` - 任务指标
  - `task_traces` - 链路追踪
  - `system_metrics` - 系统资源指标

```python
class MetricsStorage:
    def __init__(self, db_path: str):
        self.memory_buffer = InMemoryBuffer(max_age=3600)
        self.database = Database(db_path)

    async def store_workflow_metrics(self, metrics: WorkflowMetrics)
    async def store_task_metrics(self, metrics: TaskMetrics)
    async def store_trace(self, trace: TaskTrace)

    def query_workflow_metrics(self, workflow_id: str) -> WorkflowMetrics
    def query_task_metrics(self, task_id: str) -> TaskMetrics
    def query_traces(self, workflow_id: str) -> List[TaskTrace]

    async def flush_to_disk(self)
```

### 3.5 MonitoringDashboard (监控面板)

**实时 TUI 面板（使用 Rich）：**

```
┌─ 工作流执行监控 ────────────────────────────────────┐
│ 工作流: wf-12345                                    │
│ 状态: RUNNING  |  进度: 45/100 (45%)               │
│ 执行时间: 00:02:34  |  预计剩余: 00:03:12          │
└────────────────────────────────────────────────────┘

┌─ 实时指标 ─────────────────────────────────────────┐
│ 吞吐量:     78 tasks/s    ↑ +5%                   │
│ 并发任务:   5 / 5         █████ 100%              │
│ 队列长度:   12            ███░░ 60%               │
│ CPU 使用:   45%           ████░ 45%               │
│ 内存使用:   128 MB        ███░░ 52%               │
└────────────────────────────────────────────────────┘

┌─ 延迟分布 ─────────────────────────────────────────┐
│ P50: 120ms  |  P95: 850ms  |  P99: 1.2s           │
│ 最小: 50ms  |  最大: 3.5s  |  平均: 240ms         │
└────────────────────────────────────────────────────┘

┌─ 任务状态 ─────────────────────────────────────────┐
│ ✓ 成功: 42  |  ✗ 失败: 1  |  🔄 重试: 2          │
│ ⏳ 运行: 5  |  ⏸ 等待: 50                        │
└────────────────────────────────────────────────────┘

┌─ 最近完成任务 ─────────────────────────────────────┐
│ 15:30:45  task-42  ✓  llm:chat        150ms       │
│ 15:30:44  task-41  ✓  mcp:file:read   80ms        │
│ 15:30:43  task-40  ✗  mcp:api:call    TIMEOUT     │
└────────────────────────────────────────────────────┘
```

**接口：**
```python
class MonitoringDashboard:
    def __init__(self, monitor: PerformanceMonitor):
        self.monitor = monitor
        self.console = Console()

    async def display_realtime(self, workflow_id: str):
        """显示实时监控（自动刷新）"""

    def display_summary(self, workflow_id: str):
        """显示执行摘要"""
```

### 3.6 ReportGenerator (报告生成器)

**支持的报告类型：**

1. **工作流报告** - 单个工作流的详细性能分析
2. **对比报告** - 多个工作流的性能对比
3. **基准测试报告** - 基准测试结果汇总
4. **瓶颈分析报告** - 识别性能瓶颈和优化建议

**输出格式：**
- Markdown (默认)
- JSON (数据导出)
- HTML (可视化报告)
- CSV (Excel 兼容)

```python
class ReportGenerator:
    def generate_workflow_report(
        self,
        workflow_id: str,
        format: str = "markdown"
    ) -> str

    def generate_comparison_report(
        self,
        workflow_ids: List[str]
    ) -> str

    def generate_benchmark_report(
        self,
        results: List[BenchmarkResult]
    ) -> str

    def export_metrics(
        self,
        workflow_id: str,
        format: str = "json"
    ) -> str
```

### 3.7 AlertManager (告警管理器)

**告警规则：**
- 任务失败率 > 5%
- P95 延迟 > 配置阈值
- 内存使用 > 80%
- 队列积压 > 100 任务
- 数据库响应时间 > 1s
- 并发度长期 100%（饱和）

```python
class AlertManager:
    def __init__(self, config: AlertConfig):
        self.config = config
        self.rules = self._load_rules()

    async def check_alerts(self, metrics: WorkflowMetrics) -> List[Alert]

    async def send_notification(self, alert: Alert):
        """发送告警通知（集成 notification.py）"""
```

---

## 4. 基准测试套件

### 4.1 测试场景矩阵

| 场景名称 | 任务数 | 任务类型 | 并发度 | 测试目标 |
|---------|--------|----------|--------|----------|
| small-fast | 10 | 快速 (0.1s) | 5 | 基线性能 |
| small-slow | 10 | 慢速 (5s) | 5 | 并发效率 |
| medium-mixed | 100 | 混合 (0.1-5s) | 5/10/20 | 自适应并发 |
| large-fast | 1000 | 快速 (0.1s) | 5/20/50 | 吞吐量极限 |
| large-slow | 1000 | 慢速 (10s) | 5 | 长时间稳定性 |
| complex-dag | 100 | 复杂依赖 | 5 | 调度开销 |

### 4.2 BenchmarkSuite 类

```python
class BenchmarkSuite:
    def __init__(
        self,
        executor: WorkflowExecutor,
        monitor: PerformanceMonitor
    ):
        self.executor = executor
        self.monitor = monitor
        self.scenarios = self._load_scenarios()

    async def run_benchmark(
        self,
        scenario_name: str,
        repetitions: int = 3
    ) -> BenchmarkResult

    async def run_all_benchmarks(self) -> List[BenchmarkResult]

    def generate_report(
        self,
        results: List[BenchmarkResult],
        output_format: str = "markdown"
    ) -> str
```

### 4.3 基准测试结果

```python
@dataclass
class BenchmarkResult:
    scenario_name: str
    task_count: int
    max_parallel: int
    repetitions: int

    # 性能指标
    total_duration: float
    throughput: float  # 任务/秒
    avg_task_duration: float
    p50_task_duration: float
    p95_task_duration: float
    p99_task_duration: float

    # 资源指标
    peak_memory_mb: float
    avg_cpu_percent: float
    db_query_count: int
    db_avg_query_time: float

    # 成功率
    success_rate: float
    retry_rate: float

    # 并发指标
    avg_concurrent_tasks: float
    max_concurrent_tasks: int
    queue_wait_time: float

    # 调度指标
    topological_sort_time: float
    dependency_check_time: float
```

---

## 5. 快速优化措施

### 5.1 批量数据库写入

**问题：**
- 当前每个任务执行 3-4 次独立的数据库写入
- 1000 任务 = 3000+ 数据库操作
- 每次写入 ~5-10ms = 总计 15-30 秒浪费在 I/O

**解决方案：**
```python
class BatchDatabaseWriter:
    def __init__(
        self,
        database,
        batch_size: int = 10,
        flush_interval: float = 0.1
    ):
        self.database = database
        self.batch_size = batch_size
        self.flush_interval = flush_interval
        self.pending_writes = []
        self._flush_task = None

    async def start(self):
        """启动定期刷新任务"""
        self._flush_task = asyncio.create_task(self._periodic_flush())

    async def queue_write(self, operation: DatabaseOperation):
        """将写操作加入队列"""
        self.pending_writes.append(operation)
        if len(self.pending_writes) >= self.batch_size:
            await self.flush()

    async def flush(self):
        """批量执行写操作"""
        if not self.pending_writes:
            return

        operations = self.pending_writes[:]
        self.pending_writes.clear()

        async with self.database.transaction():
            for op in operations:
                await op.execute()

    async def _periodic_flush(self):
        """定期刷新（防止数据丢失）"""
        while True:
            await asyncio.sleep(self.flush_interval)
            await self.flush()
```

**预期效果：**
- 减少 60-70% 的数据库 I/O
- 提升 20-30% 整体吞吐量

### 5.2 任务结果内存管理

**问题：**
- 大结果数据（如文件内容、RAG 检索结果）长期驻留内存
- 1000 任务 × 平均 100KB = 100MB 内存占用
- 可能导致 OOM 或频繁 GC

**解决方案：**
```python
class ResultManager:
    def __init__(
        self,
        max_size_mb: int = 100,
        spillover_dir: str = "/tmp/workflow-results"
    ):
        self.max_size_mb = max_size_mb
        self.spillover_dir = Path(spillover_dir)
        self.spillover_dir.mkdir(exist_ok=True)

        self.results = {}  # task_id -> result
        self.size_tracker = {}
        self.total_size = 0

    def store_result(self, task_id: str, result: Any):
        """存储结果，超过阈值则溢出到磁盘"""
        size = self._estimate_size(result)

        if size > 1_000_000:  # 1MB
            # 大结果直接存储到磁盘
            self._store_to_disk(task_id, result)
        else:
            self.results[task_id] = result
            self.size_tracker[task_id] = size
            self.total_size += size

            # 超过总大小限制，溢出最旧的结果
            if self.total_size > self.max_size_mb * 1_000_000:
                self._evict_oldest()

    def get_result(self, task_id: str) -> Any:
        """获取结果，自动从磁盘加载"""
        if task_id in self.results:
            return self.results[task_id]
        return self._load_from_disk(task_id)

    def _estimate_size(self, obj: Any) -> int:
        """估算对象大小（字节）"""
        import sys
        return sys.getsizeof(obj)

    def _store_to_disk(self, task_id: str, result: Any):
        """溢出到磁盘 - 使用 JSON 格式（安全）"""
        import json
        path = self.spillover_dir / f"{task_id}.json"
        try:
            with open(path, "w") as f:
                json.dump(result, f)
        except (TypeError, ValueError):
            # 如果对象不能 JSON 序列化，使用更简单的字符串表示
            with open(path, "w") as f:
                f.write(str(result))

    def _load_from_disk(self, task_id: str) -> Any:
        """从磁盘加载"""
        import json
        path = self.spillover_dir / f"{task_id}.json"
        with open(path, "r") as f:
            try:
                return json.load(f)
            except json.JSONDecodeError:
                # 如果不是 JSON，返回原始字符串
                f.seek(0)
                return f.read()

    def _evict_oldest(self):
        """驱逐最旧的结果到磁盘"""
        if not self.results:
            return
        oldest_id = next(iter(self.results))
        result = self.results.pop(oldest_id)
        self.total_size -= self.size_tracker.pop(oldest_id)
        self._store_to_disk(oldest_id, result)
```

**安全考虑：**
- 使用 JSON 序列化而非 pickle（避免代码注入风险）
- 对于不能 JSON 序列化的对象，使用字符串表示
- 所有文件操作在受控的临时目录中进行

**预期效果：**
- 降低 40-50% 的内存峰值
- 支持更大规模的工作流

---

## 6. 文件结构

```
src/workflow/
├── performance/
│   ├── __init__.py
│   ├── monitor.py           # PerformanceMonitor
│   ├── collector.py         # MetricsCollector
│   ├── tracer.py            # TraceRecorder
│   ├── storage.py           # MetricsStorage
│   ├── dashboard.py         # MonitoringDashboard
│   ├── reporter.py          # ReportGenerator
│   ├── alerts.py            # AlertManager
│   └── optimizations.py     # BatchWriter, ResultManager
│
├── benchmarks/
│   ├── __init__.py
│   ├── suite.py             # BenchmarkSuite
│   ├── scenarios.py         # 测试场景定义
│   └── reports.py           # 基准测试报告
│
tests/workflow/performance/
├── test_monitor.py
├── test_collector.py
├── test_tracer.py
├── test_storage.py
├── test_dashboard.py
├── test_reporter.py
├── test_alerts.py
├── test_optimizations.py
└── test_benchmarks.py

docs/workflow/
├── performance-monitoring.md
└── benchmarks.md

examples/
└── workflow_benchmark_demo.py
```

---

## 7. 实施计划

### Week 3 任务分解

**Task 7.3.4.1: 性能监控框架（2 天）**
- PerformanceMonitor
- MetricsCollector
- TraceRecorder
- MetricsStorage
- 集成到 WorkflowExecutor
- 15+ 单元测试

**Task 7.3.4.2: 监控面板和报告（1 天）**
- MonitoringDashboard (TUI)
- ReportGenerator
- AlertManager
- 8+ 单元测试

**Task 7.3.4.3: 基准测试套件（1 天）**
- BenchmarkSuite
- 6 种测试场景
- 报告生成
- 5+ 集成测试

**Task 7.3.4.4: 快速优化实施（1 天）**
- BatchDatabaseWriter
- ResultManager
- 性能对比测试
- 优化效果验证

**Task 7.3.4.5: 文档和演示（0.5 天）**
- 性能监控使用文档
- 基准测试报告
- 演示程序

---

## 8. 验收标准

### 8.1 功能完整性

- ✅ 性能监控框架完整实现
- ✅ 实时监控 TUI 可用
- ✅ 基准测试套件覆盖 6 种场景
- ✅ 生成详细性能报告
- ✅ 2 个快速优化措施实施

### 8.2 测试覆盖

- ✅ 单元测试覆盖率 ≥ 80%
- ✅ 所有测试通过
- ✅ 性能回归测试建立

### 8.3 性能提升

- ✅ 批量写入优化：减少 60%+ 数据库 I/O
- ✅ 内存优化：降低 40%+ 内存峰值
- ✅ 基准测试报告显示具体提升数据

### 8.4 文档完整性

- ✅ 性能监控使用文档
- ✅ 基准测试报告
- ✅ 优化效果对比
- ✅ 演示程序可运行

---

## 9. 后续优化方向

基于基准测试结果，可能的后续优化方向：

1. **自适应并发控制**
   - 根据任务类型动态调整并发度
   - 快速任务高并发，慢速任务低并发

2. **任务优先级队列**
   - 支持任务优先级设置
   - 高优先级任务优先执行

3. **连接池优化**
   - 数据库连接池
   - MCP 服务器连接池

4. **缓存机制**
   - 工具调用结果缓存
   - 参数解析结果缓存

5. **分布式执行**
   - 支持多进程执行器
   - 任务分发到多台机器

---

## 10. 风险和限制

### 10.1 监控开销

**风险:** 监控本身可能带来 5-10% 的性能开销

**缓解措施:**
- 异步指标写入
- 批量刷新到存储
- 可配置的监控级别（minimal/normal/detailed）

### 10.2 内存溢出风险

**风险:** ResultManager 的磁盘溢出可能影响性能

**缓解措施:**
- 智能阈值设置（默认 100MB）
- 异步磁盘 I/O
- 使用 JSON 序列化（安全且高效）

### 10.3 基准测试准确性

**风险:** 测试环境与实际场景可能存在差异

**缓解措施:**
- 多次重复测试取平均值
- 记录测试环境信息
- 提供自定义场景扩展机制

---

**设计批准:** ✅ 2026-03-08
**下一步:** 创建实施计划（PLAN.md）
