"""指标存储"""
import aiosqlite
import json
from datetime import datetime
from typing import Dict, Any, Optional
from .collector import WorkflowMetrics, TaskMetrics
from .tracer import TaskTrace


class InMemoryBuffer:
    """内存缓冲"""

    def __init__(self, max_age: int = 3600) -> None:
        """初始化

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
    """指标存储（双层：内存 + SQLite）"""

    def __init__(self, db_path: str) -> None:
        self.db_path = db_path
        self.memory_buffer = InMemoryBuffer(max_age=3600)
        self.db: Optional[aiosqlite.Connection] = None

    async def initialize(self) -> None:
        """初始化数据库"""
        self.db = await aiosqlite.connect(self.db_path)
        await self._create_tables()

    async def _create_tables(self) -> None:
        """创建表"""
        assert self.db is not None

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
        assert self.db is not None
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
        assert self.db is not None
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

        assert self.db is not None
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

        # 查数据库（同步操作）
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
