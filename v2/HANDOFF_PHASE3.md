# Phase 3 技术交接文档

**日期**: 2026-03-13
**版本**: v1.0
**目标**: 详细剖析 Python Workflow 系统，为 Rust 迁移提供技术参考

---

## 📚 目录

1. [Python Workflow 系统剖析](#python-workflow-系统剖析)
2. [Rust 技术选型](#rust-技术选型)
3. [数据库 Schema 设计](#数据库-schema-设计)
4. [API 设计](#api-设计)
5. [测试策略](#测试策略)
6. [迁移检查清单](#迁移检查清单)

---

## Python Workflow 系统剖析

### 1. 核心数据模型 (`src/workflow/models.py`)

#### 1.1 任务状态机

```python
class TaskStatus(Enum):
    PENDING = "pending"      # 初始状态
    RUNNING = "running"      # 执行中
    SUCCESS = "success"      # 执行成功
    FAILED = "failed"        # 执行失败
    SKIPPED = "skipped"      # 被跳过（条件不满足）
    CANCELLED = "cancelled"  # 被取消
```

**状态转换规则**:
```
PENDING → RUNNING → SUCCESS/FAILED
PENDING → SKIPPED (条件执行)
ANY → CANCELLED (用户取消)
FAILED → RUNNING (重试)
```

#### 1.2 Task 模型

```python
@dataclass
class Task:
    id: str                          # 唯一标识符
    name: str                        # 任务名称
    tool: str                        # 工具名称（格式见下文）
    params: Dict[str, Any]           # 工具参数
    dependencies: List[str]          # 依赖的任务ID列表
    requires_approval: bool          # 是否需要审批
    approval_reason: Optional[str]   # 审批原因
    status: TaskStatus               # 当前状态
    result: Optional[Any]            # 执行结果
    error: Optional[str]             # 错误信息
    retry_count: int                 # 当前重试次数
    max_retries: int = 3             # 最大重试次数
    timeout: Optional[int]           # 超时时间（秒）
```

**工具名称格式**:
- `mcp:server_name:tool_name` - MCP 工具调用
- `skill:skill_name` - Skills 技能执行
- `rag:method_name` - RAG 知识库操作
- `llm:method_name` - LLM 直接调用

**关键方法**:
- `is_ready(completed_tasks)` - 检查所有依赖是否完成
- `can_retry()` - 检查是否可以重试
- `mark_running()` / `mark_success()` / `mark_failed()` - 状态转换

#### 1.3 Workflow 模型

```python
@dataclass
class Workflow:
    id: str
    name: str
    description: str
    tasks: List[Task]                # 任务列表
    status: WorkflowStatus           # 工作流状态
    session_id: Optional[str]        # 关联的会话ID
    created_at: datetime
    started_at: Optional[datetime]
    completed_at: Optional[datetime]
    metadata: Dict[str, Any]         # 额外元数据
```

**工作流状态**:
```python
class WorkflowStatus(Enum):
    PENDING = "pending"
    RUNNING = "running"
    WAITING_APPROVAL = "waiting_approval"  # 等待审批
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"
```

---

### 2. 工作流编排器 (`src/workflow/orchestrator.py`)

#### 2.1 ToolOrchestrator - 工具路由

**职责**: 根据工具名称路由到对应的执行器

```python
class ToolOrchestrator:
    def __init__(
        self,
        mcp_executor,      # MCP 工具执行器
        skill_executor,    # Skills 执行器
        skill_registry,    # Skills 注册表
        rag_engine,        # RAG 引擎
        llm_client         # LLM 客户端
    ):
        self.registry = ToolRegistry()  # 工具注册表

    async def execute_tool(self, tool_name: str, params: Dict) -> ToolResult:
        """根据工具名称路由到对应执行器"""
        tool_type = ToolType.from_tool_name(tool_name)

        if tool_type == ToolType.MCP:
            # 解析：mcp:server:tool → server, tool
            _, server, tool = tool_name.split(":")
            return await self.mcp_executor.execute(server, tool, params)

        elif tool_type == ToolType.SKILL:
            # 解析：skill:name → name
            _, skill_name = tool_name.split(":")
            return await self.skill_executor.execute(skill_name, params)

        elif tool_type == ToolType.RAG:
            # 解析：rag:method → method
            _, method = tool_name.split(":")
            return await self.rag_engine.execute(method, params)

        elif tool_type == ToolType.LLM:
            # 解析：llm:method → method
            _, method = tool_name.split(":")
            return await self.llm_client.execute(method, params)
```

#### 2.2 ToolRegistry - 工具注册表

**职责**: 存储可用工具的元信息（用于验证和文档生成）

```python
class ToolRegistry:
    def register_tool(self, tool_name: str, tool_type: ToolType,
                     description: str, params_schema: Dict):
        """注册工具元信息"""

    def get_tool(self, tool_name: str) -> Optional[Dict]:
        """获取工具信息"""

    def list_tools(self, tool_type: Optional[ToolType]) -> List[Dict]:
        """列出所有工具（可按类型过滤）"""
```

**工具发现流程**:
1. 系统启动时，自动从 MCP 服务器、Skills 目录等发现工具
2. 注册到 ToolRegistry
3. 工作流规划时，可以查询可用工具
4. 执行时，验证工具是否存在

---

### 3. 工作流执行器 (`src/workflow/executor.py`)

#### 3.1 WorkflowExecutor - 核心调度逻辑

**职责**: 调度和执行工作流中的任务

```python
class WorkflowExecutor:
    def __init__(
        self,
        orchestrator: ToolOrchestrator,
        database,
        max_parallel: int = 5,           # 最大并行任务数
        enable_retry: bool = True,       # 是否启用重试
        base_backoff: float = 1.0,       # 基础退避时间
        max_backoff: float = 60.0        # 最大退避时间
    ):
        self._paused = False             # 暂停标志
        self._stop_requested = False     # 停止标志
```

#### 3.2 主执行循环

```python
async def execute(self, workflow: Workflow,
                  on_task_complete: Optional[callable],
                  on_approval_required: Optional[callable]) -> Dict:
    """
    执行工作流的主循环

    流程：
    1. 创建执行上下文
    2. 拓扑排序获取执行顺序
    3. 循环执行任务批次：
       a. 检查停止信号
       b. 处理暂停
       c. 获取就绪任务（依赖已满足）
       d. 并行执行任务批次
       e. 处理审批请求
       f. 更新任务状态
    4. 返回执行结果
    """
```

**关键算法**:

**拓扑排序** - 确定任务执行顺序:
```python
def _topological_sort(self, workflow: Workflow) -> List[List[Task]]:
    """
    拓扑排序，返回任务批次列表

    输入：
        A → B → D
        A → C → D
        E (独立任务)

    输出：
        [
            [A, E],      # 第一批：无依赖
            [B, C],      # 第二批：依赖 A
            [D]          # 第三批：依赖 B 和 C
        ]

    算法：Kahn's 算法
    1. 计算每个任务的入度（被依赖次数）
    2. 将入度为 0 的任务加入队列
    3. 逐层处理：
       - 取出队列中所有任务作为一批
       - 减少依赖这些任务的入度
       - 将新的入度为 0 的任务加入队列
    """
```

**并行执行批次**:
```python
async def _execute_batch(
    self,
    tasks: List[Task],
    workflow: Workflow,
    context: ExecutionContext,
    on_task_complete,
    on_approval_required
) -> List[TaskResult]:
    """
    并行执行一批任务

    使用 asyncio.gather() 同时执行多个任务
    每个任务在单独的协程中运行
    """
    # 创建任务协程
    task_coroutines = [
        self._execute_single_task(task, workflow, context,
                                  on_approval_required)
        for task in tasks
    ]

    # 并行执行（最多 max_parallel 个）
    semaphore = asyncio.Semaphore(self.max_parallel)

    async def limited_task(coro):
        async with semaphore:
            return await coro

    results = await asyncio.gather(
        *[limited_task(coro) for coro in task_coroutines],
        return_exceptions=True
    )

    # 处理结果和异常
    for task, result in zip(tasks, results):
        if isinstance(result, Exception):
            task.mark_failed(str(result))
        else:
            task.mark_success(result)
            if on_task_complete:
                await on_task_complete(task)

    return results
```

#### 3.3 单任务执行

```python
async def _execute_single_task(
    self,
    task: Task,
    workflow: Workflow,
    context: ExecutionContext,
    on_approval_required
) -> Any:
    """
    执行单个任务

    流程：
    1. 标记任务为 RUNNING
    2. 检查是否需要审批
    3. 应用超时控制
    4. 执行任务（带重试）
    5. 更新任务状态
    6. 持久化到数据库
    """
    task.mark_running()
    await self._save_task_state(task)

    # 审批流程
    if task.requires_approval:
        if on_approval_required:
            decision = await on_approval_required(task)
            if not decision.approved:
                task.mark_skipped()
                return None

    # 执行任务（带超时）
    try:
        result = await asyncio.wait_for(
            self._execute_with_retry(task, context),
            timeout=task.timeout
        )
        task.mark_success(result)
        return result
    except asyncio.TimeoutError:
        task.mark_failed("Task execution timeout")
        raise
    except Exception as e:
        task.mark_failed(str(e))
        raise
    finally:
        await self._save_task_state(task)
```

#### 3.4 重试机制

```python
async def _execute_with_retry(self, task: Task, context: ExecutionContext) -> Any:
    """
    执行任务（带重试）

    重试策略：
    - 指数退避：1s, 2s, 4s, 8s, ...
    - 添加随机抖动（±25%）防止雪崩
    - 最大重试次数：task.max_retries
    """
    last_error = None

    for attempt in range(task.max_retries + 1):
        try:
            # 执行任务
            result = await self.orchestrator.execute_tool(
                task.tool,
                task.params
            )

            # 成功，记录上下文
            context.set(f"task_{task.id}_result", result)
            return result

        except Exception as e:
            last_error = e
            task.retry_count = attempt + 1

            # 如果还有重试机会，等待后重试
            if attempt < task.max_retries:
                backoff = self._calculate_backoff(attempt + 1)
                logger.warning(
                    f"Task {task.id} failed (attempt {attempt + 1}), "
                    f"retrying in {backoff:.2f}s: {e}"
                )
                await asyncio.sleep(backoff)

    # 所有重试都失败
    raise last_error

def _calculate_backoff(self, retry_count: int) -> float:
    """计算退避时间（指数退避 + 抖动）"""
    backoff = self.base_backoff * (2 ** (retry_count - 1))
    backoff = min(backoff, self.max_backoff)
    jitter = backoff * 0.25 * (2 * random.random() - 1)
    return max(0.1, backoff + jitter)
```

#### 3.5 控制信号处理

```python
def pause(self):
    """暂停工作流执行"""
    self._paused = True

def resume(self):
    """恢复工作流执行"""
    self._paused = False

def stop(self):
    """停止工作流执行"""
    self._stop_requested = True

# 在主循环中检查
async def execute(self, workflow):
    while True:
        # 处理暂停
        while self._paused and not self._stop_requested:
            await asyncio.sleep(0.5)

        # 检查停止
        if self._stop_requested:
            workflow.status = WorkflowStatus.CANCELLED
            break

        # ... 执行任务 ...
```

---

### 4. 审批系统 (`src/workflow/approval.py`)

#### 4.1 审批策略

```python
class ApprovalStrategy(Enum):
    MANUAL = "manual"      # 总是需要人工审批
    AUTO = "auto"          # 自动审批
    THRESHOLD = "threshold"  # 基于风险阈值

class RiskLevel(Enum):
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CRITICAL = "critical"
```

#### 4.2 ApprovalManager

```python
class ApprovalManager:
    def __init__(self, strategy: ApprovalStrategy):
        self.strategy = strategy
        self.pending_requests: Dict[str, ApprovalRequest] = {}

    async def request_approval(self, task: Task) -> ApprovalDecision:
        """
        请求任务审批

        根据策略：
        - MANUAL: 总是请求人工审批
        - AUTO: 自动批准
        - THRESHOLD: 检查风险等级
        """
        if self.strategy == ApprovalStrategy.AUTO:
            return ApprovalDecision(approved=True)

        if self.strategy == ApprovalStrategy.THRESHOLD:
            risk = self._assess_risk(task)
            if risk <= RiskLevel.LOW:
                return ApprovalDecision(approved=True)

        # 需要人工审批
        request = ApprovalRequest(
            task_id=task.id,
            task_name=task.name,
            tool=task.tool,
            params=task.params,
            reason=task.approval_reason,
            risk_level=self._assess_risk(task)
        )

        self.pending_requests[task.id] = request

        # 等待审批决策（通过回调或事件）
        decision = await self._wait_for_decision(task.id)
        return decision

    def _assess_risk(self, task: Task) -> RiskLevel:
        """
        风险评估

        规则：
        - 写文件操作 → HIGH
        - 执行系统命令 → CRITICAL
        - 读文件操作 → MEDIUM
        - LLM 调用 → LOW
        """
        if task.tool.startswith("mcp:"):
            tool_name = task.tool.split(":")[-1]
            if "write" in tool_name or "delete" in tool_name:
                return RiskLevel.HIGH
            if "execute" in tool_name or "run" in tool_name:
                return RiskLevel.CRITICAL
            if "read" in tool_name or "list" in tool_name:
                return RiskLevel.MEDIUM

        return RiskLevel.LOW
```

#### 4.3 Rich TUI 审批界面 (`src/workflow/approval_ui.py`)

```python
class ApprovalUI:
    """
    使用 Rich 库创建交互式审批界面

    功能：
    - 显示任务详情（名称、工具、参数）
    - 显示风险等级（彩色标识）
    - 提供选项：批准 / 拒绝 / 跳过
    - 支持批量审批
    """

    def show_approval_request(self, request: ApprovalRequest) -> ApprovalDecision:
        """显示审批请求并等待用户输入"""
        console = Console()

        # 显示任务信息
        table = Table(title=f"审批请求: {request.task_name}")
        table.add_row("任务ID", request.task_id)
        table.add_row("工具", request.tool)
        table.add_row("参数", json.dumps(request.params, indent=2))
        table.add_row("风险等级", self._format_risk(request.risk_level))

        console.print(table)

        # 获取用户选择
        choice = Prompt.ask(
            "请选择操作",
            choices=["approve", "reject", "skip"],
            default="approve"
        )

        return ApprovalDecision(
            approved=(choice == "approve"),
            reason=None if choice == "approve" else "用户拒绝"
        )
```

---

### 5. 通知系统 (`src/workflow/notification.py`)

#### 5.1 通知渠道

```python
class NotificationChannel(ABC):
    """通知渠道抽象基类"""

    @abstractmethod
    async def send(self, notification: Notification):
        pass

class TerminalChannel(NotificationChannel):
    """终端输出通知"""
    async def send(self, notification: Notification):
        print(f"[{notification.level}] {notification.message}")

class DesktopChannel(NotificationChannel):
    """桌面通知（使用 plyer 库）"""
    async def send(self, notification: Notification):
        from plyer import notification as plyer_notif
        plyer_notif.notify(
            title=notification.title,
            message=notification.message,
            timeout=10
        )

class LogChannel(NotificationChannel):
    """日志记录"""
    async def send(self, notification: Notification):
        logger.log(
            self._level_to_log_level(notification.level),
            notification.message
        )
```

#### 5.2 通知路由

```python
class NotificationRouter:
    """通知路由器 - 根据通知类型分发到不同渠道"""

    def __init__(self):
        self.routes: Dict[NotificationType, List[NotificationChannel]] = {
            NotificationType.TASK_START: [TerminalChannel()],
            NotificationType.TASK_COMPLETE: [TerminalChannel(), LogChannel()],
            NotificationType.TASK_FAILED: [TerminalChannel(), DesktopChannel(), LogChannel()],
            NotificationType.WORKFLOW_COMPLETE: [DesktopChannel(), LogChannel()],
            NotificationType.APPROVAL_REQUIRED: [DesktopChannel()],
        }

    async def notify(self, notification: Notification):
        """发送通知到所有配置的渠道"""
        channels = self.routes.get(notification.type, [])
        await asyncio.gather(
            *[channel.send(notification) for channel in channels]
        )
```

---

### 6. 性能监控框架 (`src/workflow/performance/`)

#### 6.1 指标收集器 (`collector.py`)

```python
class MetricCollector:
    """指标收集器 - 收集任务和工作流的性能指标"""

    def __init__(self, storage: MetricStorage):
        self.storage = storage
        self.current_metrics: Dict[str, Metric] = {}

    async def record_task_start(self, task_id: str):
        """记录任务开始"""
        self.current_metrics[task_id] = Metric(
            name=f"task.{task_id}.execution_time",
            start_time=time.time()
        )

    async def record_task_end(self, task_id: str, result: TaskResult):
        """记录任务结束"""
        metric = self.current_metrics.pop(task_id)
        metric.end_time = time.time()
        metric.duration = metric.end_time - metric.start_time
        metric.success = (result.status == TaskStatus.SUCCESS)

        await self.storage.save(metric)

    async def record_memory_usage(self, task_id: str):
        """记录内存使用"""
        import psutil
        process = psutil.Process()
        memory_mb = process.memory_info().rss / 1024 / 1024

        await self.storage.save(Metric(
            name=f"task.{task_id}.memory_mb",
            value=memory_mb,
            timestamp=time.time()
        ))
```

#### 6.2 链路追踪 (`tracer.py`)

```python
class Tracer:
    """链路追踪器 - 追踪完整的调用链"""

    def __init__(self):
        self.spans: Dict[str, Span] = {}
        self.current_span_stack: List[str] = []

    def start_span(self, name: str, parent_id: Optional[str] = None) -> str:
        """开始一个追踪 Span"""
        span_id = str(uuid.uuid4())

        span = Span(
            span_id=span_id,
            trace_id=self._get_or_create_trace_id(),
            name=name,
            parent_id=parent_id or self._current_span_id(),
            start_time=time.time(),
            attributes={}
        )

        self.spans[span_id] = span
        self.current_span_stack.append(span_id)

        return span_id

    def end_span(self, span_id: str, **attributes):
        """结束一个 Span"""
        span = self.spans[span_id]
        span.end_time = time.time()
        span.duration = span.end_time - span.start_time
        span.attributes.update(attributes)

        self.current_span_stack.remove(span_id)

    @contextmanager
    def trace(self, name: str):
        """上下文管理器：自动追踪代码块"""
        span_id = self.start_span(name)
        try:
            yield span_id
        finally:
            self.end_span(span_id)

# 使用示例
async def execute_task(self, task: Task):
    with tracer.trace(f"task.{task.id}"):
        # 自动记录执行时间和调用链
        result = await self.orchestrator.execute_tool(task.tool, task.params)
        return result
```

#### 6.3 监控面板 (`dashboard.py`)

```python
class PerformanceDashboard:
    """
    实时监控面板 - 使用 Rich Live Display

    显示内容：
    - 工作流执行进度
    - 实时任务状态（运行中/完成/失败）
    - CPU 和内存使用率
    - 任务吞吐量（任务/秒）
    - 最近的错误
    """

    def __init__(self, executor: WorkflowExecutor):
        self.executor = executor
        self.console = Console()

    def render(self):
        """渲染监控面板"""
        layout = Layout()

        layout.split_column(
            Layout(name="header", size=3),
            Layout(name="body"),
            Layout(name="footer", size=3)
        )

        layout["header"].update(self._render_header())
        layout["body"].split_row(
            Layout(self._render_tasks(), name="tasks"),
            Layout(self._render_metrics(), name="metrics")
        )
        layout["footer"].update(self._render_footer())

        return layout

    def _render_tasks(self) -> Table:
        """渲染任务列表"""
        table = Table(title="任务状态")
        table.add_column("ID")
        table.add_column("名称")
        table.add_column("状态")
        table.add_column("耗时")

        for task in self.executor.current_tasks:
            table.add_row(
                task.id,
                task.name,
                self._format_status(task.status),
                f"{task.execution_time:.2f}s"
            )

        return table
```

#### 6.4 告警系统 (`alerts.py`)

```python
class AlertManager:
    """告警管理器 - 检测异常并发送告警"""

    def __init__(self, notification_router: NotificationRouter):
        self.notification_router = notification_router
        self.rules = [
            ThresholdRule("task.execution_time", ">", 60),  # 任务超过60秒
            ErrorRateRule("task.error_rate", ">", 0.1),     # 错误率超过10%
            MemoryLeakRule("memory_growth_rate", ">", 10),  # 内存增长超过10MB/分钟
        ]

    async def check_alerts(self, metrics: List[Metric]):
        """检查指标是否触发告警"""
        for rule in self.rules:
            if rule.evaluate(metrics):
                alert = Alert(
                    level=AlertLevel.WARNING,
                    message=rule.get_message(),
                    metrics=metrics
                )
                await self._send_alert(alert)

    async def _send_alert(self, alert: Alert):
        """发送告警通知"""
        notification = Notification(
            type=NotificationType.ALERT,
            level=alert.level,
            title="性能告警",
            message=alert.message
        )
        await self.notification_router.notify(notification)
```

---

## Rust 技术选型

### 核心库选择

| 功能 | Python 库 | Rust 替代 | 理由 |
|------|-----------|----------|------|
| 异步运行时 | asyncio | tokio | 事实标准，生态完善 |
| 图结构/DAG | networkx | petgraph | 高性能图算法库 |
| 数据库 | sqlite3 | sqlx | 异步、类型安全 |
| 序列化 | json, pydantic | serde, serde_json | 零拷贝、类型安全 |
| 日期时间 | datetime | chrono | 功能完善 |
| 桌面通知 | plyer | notify-rust | 跨平台通知 |
| 指标收集 | 自定义 | metrics | 标准指标库 |
| 链路追踪 | 自定义 | tracing | 结构化日志和追踪 |
| TUI | rich | ratatui | 已在使用 |
| 并发控制 | asyncio.Lock | tokio::sync | 更丰富的同步原语 |

### 关键设计决策

#### 1. 并发模型

**Python**:
```python
# 使用 asyncio Task
tasks = [asyncio.create_task(execute(t)) for t in task_batch]
results = await asyncio.gather(*tasks)
```

**Rust**:
```rust
// 使用 tokio::spawn + JoinHandle
let handles: Vec<_> = task_batch.iter()
    .map(|task| {
        let task = task.clone();
        tokio::spawn(async move {
            execute(task).await
        })
    })
    .collect();

let results = futures::future::join_all(handles).await;
```

**关键区别**:
- Rust 需要显式 `Arc<T>` 或 `clone()` 来跨任务共享数据
- Rust 的 `Send + Sync` trait 确保线程安全
- Python 的 GIL 简化了并发，但 Rust 更安全

#### 2. 状态管理

**Python**:
```python
# 简单的实例变量
self.current_tasks = []
self._paused = False
```

**Rust**:
```rust
// 需要考虑并发访问
use tokio::sync::RwLock;

struct WorkflowExecutor {
    current_tasks: Arc<RwLock<Vec<Task>>>,
    paused: Arc<RwLock<bool>>,
}

// 读取
let paused = self.paused.read().await;

// 写入
let mut paused = self.paused.write().await;
*paused = true;
```

**设计原则**:
- 使用 `Arc<RwLock<T>>` 共享可变状态
- 使用 `Arc<T>` 共享不可变状态
- 优先使用消息传递（mpsc channel）而非共享状态

#### 3. 错误处理

**Python**:
```python
try:
    result = await execute_task(task)
except Exception as e:
    task.mark_failed(str(e))
    raise
```

**Rust**:
```rust
// 使用 Result + ?
match execute_task(&task).await {
    Ok(result) => {
        task.mark_success(result);
        Ok(result)
    }
    Err(e) => {
        task.mark_failed(&e.to_string());
        Err(e)
    }
}

// 或使用 thiserror 自定义错误
#[derive(Debug, thiserror::Error)]
pub enum WorkflowError {
    #[error("Task execution failed: {0}")]
    TaskFailed(String),

    #[error("Workflow timeout")]
    Timeout,

    #[error("Database error: {0}")]
    Database(#[from] sqlx::Error),
}
```

---

## 数据库 Schema 设计

### 工作流表

```sql
CREATE TABLE workflows (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL,  -- 'pending', 'running', 'waiting_approval', 'completed', 'failed', 'cancelled'
    session_id TEXT,
    created_at DATETIME NOT NULL,
    started_at DATETIME,
    completed_at DATETIME,
    metadata TEXT,  -- JSON

    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

CREATE INDEX idx_workflows_session ON workflows(session_id);
CREATE INDEX idx_workflows_status ON workflows(status);
```

### 任务表

```sql
CREATE TABLE workflow_tasks (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    name TEXT NOT NULL,
    tool TEXT NOT NULL,
    params TEXT NOT NULL,  -- JSON
    dependencies TEXT,  -- JSON array of task IDs
    requires_approval BOOLEAN NOT NULL DEFAULT 0,
    approval_reason TEXT,
    status TEXT NOT NULL,  -- 'pending', 'running', 'success', 'failed', 'skipped', 'cancelled'
    result TEXT,  -- JSON
    error TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    timeout INTEGER,
    started_at DATETIME,
    completed_at DATETIME,

    FOREIGN KEY (workflow_id) REFERENCES workflows(id) ON DELETE CASCADE
);

CREATE INDEX idx_tasks_workflow ON workflow_tasks(workflow_id);
CREATE INDEX idx_tasks_status ON workflow_tasks(status);
```

### 审批记录表

```sql
CREATE TABLE approval_decisions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id TEXT NOT NULL,
    workflow_id TEXT NOT NULL,
    requested_at DATETIME NOT NULL,
    decided_at DATETIME,
    approved BOOLEAN,
    decision_by TEXT,  -- 'user', 'auto', 'threshold'
    reason TEXT,
    risk_level TEXT,  -- 'low', 'medium', 'high', 'critical'

    FOREIGN KEY (task_id) REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (workflow_id) REFERENCES workflows(id) ON DELETE CASCADE
);

CREATE INDEX idx_approvals_task ON approval_decisions(task_id);
CREATE INDEX idx_approvals_workflow ON approval_decisions(workflow_id);
```

### 性能指标表

```sql
CREATE TABLE performance_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME NOT NULL,
    metric_type TEXT NOT NULL,  -- 'task_execution_time', 'memory_usage', etc.
    metric_name TEXT NOT NULL,
    value REAL NOT NULL,
    unit TEXT,  -- 'ms', 'bytes', 'percent'
    labels TEXT,  -- JSON (e.g., {"task_id": "abc", "workflow_id": "xyz"})
    workflow_id TEXT,
    task_id TEXT,

    FOREIGN KEY (workflow_id) REFERENCES workflows(id) ON DELETE CASCADE,
    FOREIGN KEY (task_id) REFERENCES workflow_tasks(id) ON DELETE CASCADE
);

CREATE INDEX idx_metrics_timestamp ON performance_metrics(timestamp);
CREATE INDEX idx_metrics_type ON performance_metrics(metric_type);
CREATE INDEX idx_metrics_workflow ON performance_metrics(workflow_id);
```

### 链路追踪表

```sql
CREATE TABLE trace_spans (
    span_id TEXT PRIMARY KEY,
    trace_id TEXT NOT NULL,
    parent_span_id TEXT,
    name TEXT NOT NULL,
    start_time DATETIME NOT NULL,
    end_time DATETIME,
    duration_ms REAL,
    attributes TEXT,  -- JSON
    workflow_id TEXT,
    task_id TEXT,

    FOREIGN KEY (workflow_id) REFERENCES workflows(id) ON DELETE CASCADE,
    FOREIGN KEY (task_id) REFERENCES workflow_tasks(id) ON DELETE CASCADE
);

CREATE INDEX idx_spans_trace ON trace_spans(trace_id);
CREATE INDEX idx_spans_parent ON trace_spans(parent_span_id);
CREATE INDEX idx_spans_workflow ON trace_spans(workflow_id);
```

---

## API 设计

### Rust API 示例

```rust
// 创建工作流
let workflow = Workflow::builder()
    .name("数据处理流水线")
    .task(Task::new("fetch_data")
        .tool("mcp:http:get")
        .params(json!({"url": "https://api.example.com/data"}))
    )
    .task(Task::new("process_data")
        .tool("skill:data_processor")
        .depends_on("fetch_data")
    )
    .task(Task::new("store_data")
        .tool("mcp:database:insert")
        .depends_on("process_data")
        .requires_approval(true, "写入生产数据库")
    )
    .build()?;

// 执行工作流
let executor = WorkflowExecutor::new(orchestrator, storage);
let result = executor.execute(workflow).await?;

// 查询工作流状态
let status = storage.get_workflow_status(&workflow_id).await?;

// 暂停/恢复/取消
executor.pause().await;
executor.resume().await;
executor.cancel(&workflow_id).await;
```

### API 兼容性考虑

**保持与 Python 版本的 API 一致性**:
1. 工具名称格式相同：`mcp:server:tool`, `skill:name`
2. 任务状态相同：`pending`, `running`, `success`, `failed`
3. 数据库 schema 兼容（可以读取 Python 创建的数据）

**差异点**:
- Rust 使用 Builder 模式构建工作流（更类型安全）
- Python 使用字典传参，Rust 使用结构体
- Rust API 全部异步（`async fn`）

---

## 测试策略

### 单元测试覆盖

```rust
// 每个模块的测试
#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_task_ready_check() {
        let task = Task::new("test")
            .depends_on("task_a")
            .depends_on("task_b");

        let completed = HashSet::from(["task_a".to_string()]);
        assert!(!task.is_ready(&completed));

        let completed = HashSet::from([
            "task_a".to_string(),
            "task_b".to_string()
        ]);
        assert!(task.is_ready(&completed));
    }

    #[tokio::test]
    async fn test_retry_backoff() {
        let executor = WorkflowExecutor::new(...);

        assert_eq!(executor.calculate_backoff(1), 1.0);  // 允许误差
        assert!(executor.calculate_backoff(2) >= 2.0);
        assert!(executor.calculate_backoff(3) >= 4.0);
    }
}
```

### 集成测试

```rust
// tests/workflow_integration.rs
#[tokio::test]
async fn test_complete_workflow_execution() {
    // 1. 设置测试环境
    let storage = setup_test_storage().await;
    let orchestrator = setup_test_orchestrator();
    let executor = WorkflowExecutor::new(orchestrator, storage);

    // 2. 创建工作流
    let workflow = create_test_workflow();

    // 3. 执行工作流
    let result = executor.execute(workflow.clone()).await.unwrap();

    // 4. 验证结果
    assert_eq!(result.status, WorkflowStatus::Completed);
    assert_eq!(result.task_results.len(), workflow.tasks.len());

    // 5. 验证持久化
    let loaded = storage.load_workflow(&workflow.id).await.unwrap();
    assert_eq!(loaded.status, WorkflowStatus::Completed);
}
```

### 性能测试

```rust
#[tokio::test]
async fn benchmark_parallel_execution() {
    let workflow = create_large_workflow(100); // 100个独立任务

    let start = Instant::now();
    let result = executor.execute(workflow).await.unwrap();
    let duration = start.elapsed();

    // 验证并行性：100个任务应该在合理时间内完成
    assert!(duration.as_secs() < 10);

    // 验证所有任务成功
    assert!(result.task_results.values().all(|r| r.success));
}
```

### 测试覆盖率目标

- 单元测试：每个公共函数至少一个测试
- 分支覆盖率：> 80%
- 集成测试：覆盖所有主要用例
- 性能测试：基准测试所有关键路径

---

## 迁移检查清单

### Week 1-2: 核心编排器
- [ ] 数据模型定义（Workflow, Task, TaskStatus）
- [ ] DAG 依赖解析（petgraph）
- [ ] 拓扑排序算法
- [ ] 循环依赖检测
- [ ] 基础任务执行器
- [ ] 重试机制（指数退避）
- [ ] 超时控制
- [ ] 并行执行批次
- [ ] 状态持久化
- [ ] 取消和暂停支持

### Week 3: 审批和通知
- [ ] 审批策略（Manual/Auto/Threshold）
- [ ] 风险评估逻辑
- [ ] ApprovalManager
- [ ] TUI 审批界面
- [ ] 审批历史记录
- [ ] 通知渠道抽象
- [ ] 终端通知
- [ ] 桌面通知（notify-rust）
- [ ] 日志通知
- [ ] 通知路由器

### Week 4: 性能监控
- [ ] 指标定义和模型
- [ ] MetricCollector
- [ ] MetricStorage（SQLite）
- [ ] Trace 上下文
- [ ] Span 追踪
- [ ] 自动追踪注入
- [ ] TUI 监控面板
- [ ] 报告生成器（Markdown/JSON）
- [ ] 告警规则引擎
- [ ] AlertManager

### Week 5: Subagent 系统
- [ ] SubagentOrchestrator
- [ ] /subagent 命令解析
- [ ] 后台任务调度
- [ ] TUI 数据绑定
- [ ] 实时状态更新
- [ ] Subagent 取消

### Week 6: 集成测试和文档
- [ ] 端到端工作流测试
- [ ] 错误场景测试
- [ ] 性能基准测试
- [ ] 功能对等验证
- [ ] 架构文档更新
- [ ] 用户指南
- [ ] API 文档（Rustdoc）
- [ ] 迁移指南

---

**创建日期**: 2026-03-13
**最后更新**: 2026-03-13
**目标读者**: Rust 开发者、迁移工程师
**维护者**: 项目团队
