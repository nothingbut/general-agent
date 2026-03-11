# Phase 8: 错误处理和进度记录设计

**日期:** 2026-03-09
**版本:** 1.0
**策略:** 整体失败并记录进度及问题

---

## 设计原则

根据用户反馈，Phase 8 的错误处理策略是：
- **整体失败**: 当任何 Agent 失败时，整个协作任务失败
- **记录进度**: 完整记录已完成的工作和失败的原因
- **可恢复性**: 提供足够的信息以支持后续恢复或人工介入

---

## 架构设计

### 1. 错误类型定义

```python
from enum import Enum
from dataclasses import dataclass
from typing import Optional, Dict, Any, List
from datetime import datetime

class ErrorSeverity(Enum):
    """错误严重程度"""
    LOW = "low"           # 可忽略的警告
    MEDIUM = "medium"     # 需要注意但不致命
    HIGH = "high"         # 导致任务失败
    CRITICAL = "critical" # 导致系统失败

class ErrorCategory(Enum):
    """错误类别"""
    TIMEOUT = "timeout"               # 超时
    RATE_LIMIT = "rate_limit"         # 速率限制
    AUTHENTICATION = "authentication" # 认证失败
    PERMISSION = "permission"         # 权限不足
    VALIDATION = "validation"         # 验证错误
    EXECUTION = "execution"           # 执行错误
    COMMUNICATION = "communication"   # 通信错误
    RESOURCE = "resource"             # 资源不足
    DEPENDENCY = "dependency"         # 依赖失败
    UNKNOWN = "unknown"               # 未知错误

@dataclass
class AgentError:
    """Agent 错误"""
    error_id: str
    agent_id: str
    category: ErrorCategory
    severity: ErrorSeverity
    message: str

    # 错误上下文
    task_id: Optional[str] = None
    timestamp: datetime = field(default_factory=datetime.now)
    stack_trace: Optional[str] = None

    # 错误详情
    details: Dict[str, Any] = field(default_factory=dict)

    # 可恢复性
    is_recoverable: bool = False
    recovery_suggestion: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "error_id": self.error_id,
            "agent_id": self.agent_id,
            "category": self.category.value,
            "severity": self.severity.value,
            "message": self.message,
            "task_id": self.task_id,
            "timestamp": self.timestamp.isoformat(),
            "stack_trace": self.stack_trace,
            "details": self.details,
            "is_recoverable": self.is_recoverable,
            "recovery_suggestion": self.recovery_suggestion
        }
```

### 2. 进度追踪模型

```python
from enum import Enum

class TaskStatus(Enum):
    """任务状态"""
    PENDING = "pending"       # 待执行
    RUNNING = "running"       # 执行中
    COMPLETED = "completed"   # 已完成
    FAILED = "failed"         # 失败
    CANCELLED = "cancelled"   # 已取消
    BLOCKED = "blocked"       # 被阻塞

@dataclass
class AgentTaskProgress:
    """Agent 任务进度"""
    task_id: str
    agent_id: str
    status: TaskStatus

    # 时间信息
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    updated_at: datetime = field(default_factory=datetime.now)

    # 进度信息
    progress_percent: float = 0.0  # 0-100
    current_step: Optional[str] = None
    total_steps: Optional[int] = None
    completed_steps: int = 0

    # 输出和错误
    output: Optional[Dict[str, Any]] = None
    error: Optional[AgentError] = None

    # 资源使用
    tokens_used: int = 0
    cost: float = 0.0

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "task_id": self.task_id,
            "agent_id": self.agent_id,
            "status": self.status.value,
            "started_at": self.started_at.isoformat() if self.started_at else None,
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "updated_at": self.updated_at.isoformat(),
            "progress_percent": self.progress_percent,
            "current_step": self.current_step,
            "total_steps": self.total_steps,
            "completed_steps": self.completed_steps,
            "output": self.output,
            "error": self.error.to_dict() if self.error else None,
            "tokens_used": self.tokens_used,
            "cost": self.cost
        }

@dataclass
class CoordinationProgress:
    """协作任务进度"""
    coordination_id: str
    plan_id: str
    status: TaskStatus

    # 时间信息
    started_at: datetime
    completed_at: Optional[datetime] = None
    updated_at: datetime = field(default_factory=datetime.now)

    # Agent 进度
    agent_progress: Dict[str, AgentTaskProgress] = field(default_factory=dict)

    # 整体统计
    total_agents: int = 0
    completed_agents: int = 0
    failed_agents: int = 0

    # 错误汇总
    errors: List[AgentError] = field(default_factory=list)

    # 输出结果
    final_output: Optional[Dict[str, Any]] = None

    def get_progress_percent(self) -> float:
        """计算整体进度百分比"""
        if self.total_agents == 0:
            return 0.0

        # 加权平均所有 Agent 的进度
        total_progress = sum(
            p.progress_percent for p in self.agent_progress.values()
        )
        return total_progress / self.total_agents

    def get_status_summary(self) -> Dict[str, int]:
        """获取状态摘要"""
        summary: Dict[str, int] = {
            "pending": 0,
            "running": 0,
            "completed": 0,
            "failed": 0,
            "cancelled": 0,
            "blocked": 0
        }

        for progress in self.agent_progress.values():
            summary[progress.status.value] += 1

        return summary

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "coordination_id": self.coordination_id,
            "plan_id": self.plan_id,
            "status": self.status.value,
            "started_at": self.started_at.isoformat(),
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
            "updated_at": self.updated_at.isoformat(),
            "progress_percent": self.get_progress_percent(),
            "agent_progress": {
                agent_id: progress.to_dict()
                for agent_id, progress in self.agent_progress.items()
            },
            "status_summary": self.get_status_summary(),
            "total_agents": self.total_agents,
            "completed_agents": self.completed_agents,
            "failed_agents": self.failed_agents,
            "errors": [e.to_dict() for e in self.errors],
            "final_output": self.final_output
        }
```

### 3. 进度记录器

```python
class ProgressRecorder:
    """进度记录器"""

    def __init__(self, storage: MetricsStorage):
        self.storage = storage
        self.active_progress: Dict[str, CoordinationProgress] = {}

    async def start_coordination(
        self,
        coordination_id: str,
        plan: CoordinationPlan
    ) -> CoordinationProgress:
        """开始协作任务"""
        progress = CoordinationProgress(
            coordination_id=coordination_id,
            plan_id=plan.plan_id,
            status=TaskStatus.RUNNING,
            started_at=datetime.now(),
            total_agents=len(plan.agents)
        )

        # 初始化每个 Agent 的进度
        for agent_id in plan.agents:
            progress.agent_progress[agent_id] = AgentTaskProgress(
                task_id=f"{coordination_id}-{agent_id}",
                agent_id=agent_id,
                status=TaskStatus.PENDING
            )

        self.active_progress[coordination_id] = progress

        # 持久化
        await self._save_progress(progress)

        return progress

    async def update_agent_progress(
        self,
        coordination_id: str,
        agent_id: str,
        status: TaskStatus,
        progress_percent: Optional[float] = None,
        current_step: Optional[str] = None,
        output: Optional[Dict[str, Any]] = None,
        error: Optional[AgentError] = None
    ) -> None:
        """更新 Agent 进度"""
        progress = self.active_progress.get(coordination_id)
        if not progress:
            raise ValueError(f"Coordination {coordination_id} not found")

        agent_progress = progress.agent_progress.get(agent_id)
        if not agent_progress:
            raise ValueError(f"Agent {agent_id} not found in coordination")

        # 更新状态
        agent_progress.status = status
        agent_progress.updated_at = datetime.now()

        if progress_percent is not None:
            agent_progress.progress_percent = progress_percent

        if current_step is not None:
            agent_progress.current_step = current_step
            agent_progress.completed_steps += 1

        if output is not None:
            agent_progress.output = output

        if error is not None:
            agent_progress.error = error
            progress.errors.append(error)

        # 更新开始和完成时间
        if status == TaskStatus.RUNNING and agent_progress.started_at is None:
            agent_progress.started_at = datetime.now()

        if status in [TaskStatus.COMPLETED, TaskStatus.FAILED, TaskStatus.CANCELLED]:
            agent_progress.completed_at = datetime.now()

            if status == TaskStatus.COMPLETED:
                progress.completed_agents += 1
                agent_progress.progress_percent = 100.0
            elif status == TaskStatus.FAILED:
                progress.failed_agents += 1

        # 更新整体进度
        progress.updated_at = datetime.now()

        # 持久化
        await self._save_progress(progress)

    async def complete_coordination(
        self,
        coordination_id: str,
        status: TaskStatus,
        final_output: Optional[Dict[str, Any]] = None
    ) -> CoordinationProgress:
        """完成协作任务"""
        progress = self.active_progress.get(coordination_id)
        if not progress:
            raise ValueError(f"Coordination {coordination_id} not found")

        progress.status = status
        progress.completed_at = datetime.now()
        progress.final_output = final_output

        # 持久化
        await self._save_progress(progress)

        # 从活动列表中移除
        del self.active_progress[coordination_id]

        return progress

    async def get_progress(self, coordination_id: str) -> Optional[CoordinationProgress]:
        """获取进度"""
        # 先检查活动列表
        if coordination_id in self.active_progress:
            return self.active_progress[coordination_id]

        # 从数据库加载
        return await self._load_progress(coordination_id)

    async def _save_progress(self, progress: CoordinationProgress) -> None:
        """持久化进度"""
        if self.storage.db is None:
            return

        import json

        # 保存到数据库
        await self.storage.db.execute(
            """
            INSERT OR REPLACE INTO coordination_progress
            (coordination_id, plan_id, status, started_at, completed_at,
             progress_data, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """,
            (
                progress.coordination_id,
                progress.plan_id,
                progress.status.value,
                progress.started_at.isoformat(),
                progress.completed_at.isoformat() if progress.completed_at else None,
                json.dumps(progress.to_dict()),
                progress.updated_at.isoformat()
            )
        )
        await self.storage.db.commit()

    async def _load_progress(
        self,
        coordination_id: str
    ) -> Optional[CoordinationProgress]:
        """从数据库加载进度"""
        if self.storage.db is None:
            return None

        cursor = await self.storage.db.execute(
            "SELECT progress_data FROM coordination_progress WHERE coordination_id = ?",
            (coordination_id,)
        )
        row = await cursor.fetchone()

        if not row:
            return None

        import json
        data = json.loads(row[0])

        # 重建对象（简化版，实际需要完整反序列化）
        # TODO: 实现完整的反序列化逻辑
        return None
```

### 4. 错误处理策略

```python
class ErrorHandler:
    """错误处理器"""

    def __init__(
        self,
        progress_recorder: ProgressRecorder,
        notification_manager: Optional[NotificationManager] = None
    ):
        self.progress_recorder = progress_recorder
        self.notification_manager = notification_manager

    async def handle_agent_error(
        self,
        coordination_id: str,
        agent_id: str,
        exception: Exception,
        context: Dict[str, Any]
    ) -> AgentError:
        """处理 Agent 错误"""

        # 分类错误
        category = self._categorize_error(exception)
        severity = self._determine_severity(category, exception)

        # 创建错误对象
        error = AgentError(
            error_id=f"{coordination_id}-{agent_id}-{datetime.now().timestamp()}",
            agent_id=agent_id,
            category=category,
            severity=severity,
            message=str(exception),
            task_id=context.get("task_id"),
            stack_trace=self._get_stack_trace(exception),
            details=context,
            is_recoverable=self._is_recoverable(category, exception),
            recovery_suggestion=self._get_recovery_suggestion(category, exception)
        )

        # 记录进度
        await self.progress_recorder.update_agent_progress(
            coordination_id=coordination_id,
            agent_id=agent_id,
            status=TaskStatus.FAILED,
            error=error
        )

        # 发送通知（如果配置）
        if self.notification_manager and severity == ErrorSeverity.CRITICAL:
            await self._send_error_notification(error)

        return error

    async def handle_coordination_failure(
        self,
        coordination_id: str,
        reason: str,
        errors: List[AgentError]
    ) -> None:
        """处理协作任务失败"""

        # 获取进度
        progress = await self.progress_recorder.get_progress(coordination_id)
        if not progress:
            return

        # 生成失败报告
        failure_report = self._generate_failure_report(progress, reason, errors)

        # 保存失败报告
        await self._save_failure_report(coordination_id, failure_report)

        # 完成协作（状态：失败）
        await self.progress_recorder.complete_coordination(
            coordination_id=coordination_id,
            status=TaskStatus.FAILED,
            final_output=failure_report
        )

        # 发送通知
        if self.notification_manager:
            await self.notification_manager.send_notification(
                title="Multi-Agent Coordination Failed",
                message=f"Coordination {coordination_id} failed: {reason}",
                priority="high",
                channels=["terminal", "desktop"]
            )

    def _categorize_error(self, exception: Exception) -> ErrorCategory:
        """错误分类"""
        if isinstance(exception, asyncio.TimeoutError):
            return ErrorCategory.TIMEOUT
        elif isinstance(exception, PermissionError):
            return ErrorCategory.PERMISSION
        elif isinstance(exception, ValueError):
            return ErrorCategory.VALIDATION
        # TODO: 添加更多错误类型
        else:
            return ErrorCategory.UNKNOWN

    def _determine_severity(
        self,
        category: ErrorCategory,
        exception: Exception
    ) -> ErrorSeverity:
        """确定严重程度"""
        # 超时和认证错误通常是高严重性
        if category in [ErrorCategory.TIMEOUT, ErrorCategory.AUTHENTICATION]:
            return ErrorSeverity.HIGH

        # 权限和资源错误是中等严重性
        if category in [ErrorCategory.PERMISSION, ErrorCategory.RESOURCE]:
            return ErrorSeverity.MEDIUM

        # 未知错误默认为高严重性
        return ErrorSeverity.HIGH

    def _is_recoverable(
        self,
        category: ErrorCategory,
        exception: Exception
    ) -> bool:
        """判断是否可恢复"""
        # 超时和速率限制通常可恢复
        return category in [ErrorCategory.TIMEOUT, ErrorCategory.RATE_LIMIT]

    def _get_recovery_suggestion(
        self,
        category: ErrorCategory,
        exception: Exception
    ) -> Optional[str]:
        """获取恢复建议"""
        if category == ErrorCategory.TIMEOUT:
            return "Increase timeout setting or retry the operation"
        elif category == ErrorCategory.RATE_LIMIT:
            return "Wait and retry, or use a different API key"
        elif category == ErrorCategory.AUTHENTICATION:
            return "Check API credentials and permissions"
        return None

    def _get_stack_trace(self, exception: Exception) -> str:
        """获取堆栈跟踪"""
        import traceback
        return "".join(traceback.format_exception(
            type(exception),
            exception,
            exception.__traceback__
        ))

    def _generate_failure_report(
        self,
        progress: CoordinationProgress,
        reason: str,
        errors: List[AgentError]
    ) -> Dict[str, Any]:
        """生成失败报告"""
        return {
            "coordination_id": progress.coordination_id,
            "plan_id": progress.plan_id,
            "failure_reason": reason,
            "started_at": progress.started_at.isoformat(),
            "failed_at": datetime.now().isoformat(),
            "duration": (datetime.now() - progress.started_at).total_seconds(),

            # 进度摘要
            "progress_summary": {
                "total_agents": progress.total_agents,
                "completed_agents": progress.completed_agents,
                "failed_agents": progress.failed_agents,
                "progress_percent": progress.get_progress_percent()
            },

            # 已完成的工作
            "completed_work": {
                agent_id: p.output
                for agent_id, p in progress.agent_progress.items()
                if p.status == TaskStatus.COMPLETED
            },

            # 错误详情
            "errors": [e.to_dict() for e in errors],

            # 恢复信息
            "recovery_info": {
                "last_successful_agent": self._get_last_successful_agent(progress),
                "checkpoint_data": self._create_checkpoint(progress),
                "recovery_suggestions": [
                    e.recovery_suggestion for e in errors
                    if e.is_recoverable and e.recovery_suggestion
                ]
            }
        }

    def _get_last_successful_agent(
        self,
        progress: CoordinationProgress
    ) -> Optional[str]:
        """获取最后成功完成的 Agent"""
        completed = [
            (agent_id, p.completed_at)
            for agent_id, p in progress.agent_progress.items()
            if p.status == TaskStatus.COMPLETED and p.completed_at
        ]

        if not completed:
            return None

        # 按完成时间排序，返回最后一个
        completed.sort(key=lambda x: x[1])
        return completed[-1][0]

    def _create_checkpoint(
        self,
        progress: CoordinationProgress
    ) -> Dict[str, Any]:
        """创建检查点数据（用于恢复）"""
        return {
            "completed_agents": [
                agent_id
                for agent_id, p in progress.agent_progress.items()
                if p.status == TaskStatus.COMPLETED
            ],
            "agent_outputs": {
                agent_id: p.output
                for agent_id, p in progress.agent_progress.items()
                if p.output is not None
            },
            "pending_agents": [
                agent_id
                for agent_id, p in progress.agent_progress.items()
                if p.status == TaskStatus.PENDING
            ]
        }

    async def _save_failure_report(
        self,
        coordination_id: str,
        report: Dict[str, Any]
    ) -> None:
        """保存失败报告"""
        import json

        # 保存到文件
        report_path = f".planning/failures/{coordination_id}.json"
        import os
        os.makedirs(os.path.dirname(report_path), exist_ok=True)

        with open(report_path, 'w') as f:
            json.dump(report, f, indent=2)

    async def _send_error_notification(self, error: AgentError) -> None:
        """发送错误通知"""
        if not self.notification_manager:
            return

        await self.notification_manager.send_notification(
            title=f"Agent Error: {error.category.value}",
            message=error.message,
            priority="high" if error.severity == ErrorSeverity.HIGH else "medium",
            channels=["terminal"]
        )
```

### 5. 更新后的 AgentCoordinator

```python
class AgentCoordinator:
    """Agent 协调器（更新版 - 支持错误处理）"""

    def __init__(
        self,
        message_bus: 'MessageBus',
        state_manager: 'StateManager',
        performance_monitor: Optional['PerformanceMonitor'] = None,
        notification_manager: Optional['NotificationManager'] = None
    ):
        self.message_bus = message_bus
        self.state_manager = state_manager
        self.performance_monitor = performance_monitor

        # Agent 注册表
        self.agents: Dict[str, AgentInstance] = {}

        # 进度记录和错误处理
        storage = performance_monitor.storage if performance_monitor else MetricsStorage(":memory:")
        self.progress_recorder = ProgressRecorder(storage)
        self.error_handler = ErrorHandler(self.progress_recorder, notification_manager)

    async def coordinate(
        self,
        plan: CoordinationPlan,
        task: str,
        context: Dict[str, Any]
    ) -> Dict[str, Any]:
        """执行协作任务（带错误处理）"""

        # 生成协作 ID
        coordination_id = f"coord-{datetime.now().timestamp()}"

        # 开始记录进度
        progress = await self.progress_recorder.start_coordination(
            coordination_id, plan
        )

        try:
            # 验证计划
            self._validate_plan(plan)

            # 根据策略执行
            if plan.strategy == CoordinationStrategy.SEQUENTIAL:
                result = await self._execute_sequential_with_error_handling(
                    coordination_id, plan, task, context
                )
            elif plan.strategy == CoordinationStrategy.PARALLEL:
                result = await self._execute_parallel_with_error_handling(
                    coordination_id, plan, task, context
                )
            # ... 其他策略

            # 成功完成
            await self.progress_recorder.complete_coordination(
                coordination_id=coordination_id,
                status=TaskStatus.COMPLETED,
                final_output=result
            )

            return result

        except Exception as e:
            # 整体失败 - 记录进度和问题
            await self.error_handler.handle_coordination_failure(
                coordination_id=coordination_id,
                reason=str(e),
                errors=progress.errors
            )

            # 重新抛出异常
            raise

    async def _execute_sequential_with_error_handling(
        self,
        coordination_id: str,
        plan: CoordinationPlan,
        task: str,
        context: Dict[str, Any]
    ) -> Dict[str, Any]:
        """顺序执行（带错误处理）"""
        results = {}
        current_context = context.copy()

        for agent_id in plan.agents:
            agent = self.agents[agent_id]

            try:
                # 更新进度：开始
                await self.progress_recorder.update_agent_progress(
                    coordination_id=coordination_id,
                    agent_id=agent_id,
                    status=TaskStatus.RUNNING,
                    current_step=f"Executing task: {task}"
                )

                # 执行任务
                result = await self._execute_agent_task(
                    agent, task, current_context
                )

                # 更新进度：完成
                await self.progress_recorder.update_agent_progress(
                    coordination_id=coordination_id,
                    agent_id=agent_id,
                    status=TaskStatus.COMPLETED,
                    output=result
                )

                results[agent_id] = result
                current_context.update(result)

            except Exception as e:
                # 处理错误
                error = await self.error_handler.handle_agent_error(
                    coordination_id=coordination_id,
                    agent_id=agent_id,
                    exception=e,
                    context=current_context
                )

                # 整体失败
                raise RuntimeError(
                    f"Agent {agent_id} failed: {error.message}"
                ) from e

        return results
```

---

## 数据库 Schema

```sql
-- 协作进度表
CREATE TABLE coordination_progress (
    coordination_id TEXT PRIMARY KEY,
    plan_id TEXT NOT NULL,
    status TEXT NOT NULL,
    started_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP,
    progress_data TEXT NOT NULL,  -- JSON
    updated_at TIMESTAMP NOT NULL,
    FOREIGN KEY (plan_id) REFERENCES coordination_plans(plan_id)
);

-- 失败报告表
CREATE TABLE coordination_failures (
    coordination_id TEXT PRIMARY KEY,
    plan_id TEXT NOT NULL,
    failure_reason TEXT NOT NULL,
    failure_report TEXT NOT NULL,  -- JSON
    failed_at TIMESTAMP NOT NULL,
    FOREIGN KEY (coordination_id) REFERENCES coordination_progress(coordination_id)
);

-- Agent 错误表
CREATE TABLE agent_errors (
    error_id TEXT PRIMARY KEY,
    coordination_id TEXT NOT NULL,
    agent_id TEXT NOT NULL,
    category TEXT NOT NULL,
    severity TEXT NOT NULL,
    message TEXT NOT NULL,
    stack_trace TEXT,
    details TEXT,  -- JSON
    is_recoverable BOOLEAN,
    recovery_suggestion TEXT,
    timestamp TIMESTAMP NOT NULL,
    FOREIGN KEY (coordination_id) REFERENCES coordination_progress(coordination_id)
);
```

---

## 使用示例

```python
async def demo_error_handling():
    """演示错误处理"""

    # 初始化
    coordinator = AgentCoordinator(...)

    # 创建计划
    plan = CoordinationPlan(...)

    try:
        # 执行协作任务
        result = await coordinator.coordinate(
            plan=plan,
            task="Write an article",
            context={"topic": "AI"}
        )

        print("Success:", result)

    except Exception as e:
        print(f"Coordination failed: {e}")

        # 获取失败报告
        progress = await coordinator.progress_recorder.get_progress(...)

        if progress:
            # 查看已完成的工作
            print("Completed work:")
            for agent_id, p in progress.agent_progress.items():
                if p.status == TaskStatus.COMPLETED:
                    print(f"  {agent_id}: {p.output}")

            # 查看错误
            print("\nErrors:")
            for error in progress.errors:
                print(f"  {error.agent_id}: {error.message}")
                if error.recovery_suggestion:
                    print(f"    Suggestion: {error.recovery_suggestion}")
```

---

## 验收标准

- [ ] Agent 失败时整个协作任务失败
- [ ] 完整记录所有已完成的工作
- [ ] 记录所有错误的详细信息
- [ ] 提供恢复建议（对于可恢复的错误）
- [ ] 生成失败报告并保存
- [ ] 发送关键错误通知
- [ ] 支持从检查点恢复（未来）
- [ ] 进度持久化到数据库
- [ ] 可以查询历史失败记录

---

**设计完成时间:** 2026-03-09
**策略:** 整体失败并记录进度及问题
**下一步:** 实现错误处理和进度记录系统
