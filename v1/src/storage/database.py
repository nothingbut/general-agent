"""SQLite数据库操作"""
import aiosqlite
import json
import logging
import uuid
from pathlib import Path
from datetime import datetime
from typing import List, Optional, Dict, Any
from .models import Message, Session

logger = logging.getLogger(__name__)


class Database:
    """SQLite数据库管理器"""

    def __init__(self, db_path: Path) -> None:
        self.db_path = db_path
        self.conn: Optional[aiosqlite.Connection] = None

    async def initialize(self) -> None:
        """初始化数据库和表结构"""
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        self.conn = await aiosqlite.connect(str(self.db_path))
        self.conn.row_factory = aiosqlite.Row

        # Enable foreign key constraints
        await self.conn.execute("PRAGMA foreign_keys = ON")

        # 创建会话表
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                created_at TIMESTAMP NOT NULL,
                updated_at TIMESTAMP NOT NULL,
                metadata TEXT
            )
        """)

        # 创建消息表
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                timestamp TIMESTAMP NOT NULL,
                metadata TEXT,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        # MCP audit logs table
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS mcp_audit_logs (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                server_name TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                arguments TEXT NOT NULL,
                result TEXT,
                status TEXT NOT NULL,
                error_message TEXT,
                timestamp TIMESTAMP NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        # Indexes for performance
        await self.conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_mcp_logs_session
            ON mcp_audit_logs(session_id)
        """)

        await self.conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_mcp_logs_timestamp
            ON mcp_audit_logs(timestamp)
        """)

        # Execute workflow tables migration
        await self._execute_migration_007()

        await self.conn.commit()

    async def close(self) -> None:
        """关闭数据库连接"""
        if self.conn:
            await self.conn.close()

    def _parse_metadata(self, raw_metadata: Optional[str], context_id: str) -> Optional[dict]:
        """Parse JSON metadata with error handling"""
        if not raw_metadata:
            return None
        try:
            return json.loads(raw_metadata)
        except json.JSONDecodeError as e:
            logger.warning(f"Failed to parse metadata for {context_id}: {e}")
            return None

    async def create_session(self, session: Session) -> None:
        """创建新会话"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            await self.conn.execute(
                """
                INSERT INTO sessions (id, title, created_at, updated_at, metadata)
                VALUES (?, ?, ?, ?, ?)
                """,
                (
                    session.id,
                    session.title,
                    session.created_at.isoformat(),
                    session.updated_at.isoformat(),
                    json.dumps(session.metadata) if session.metadata else None
                )
            )
            await self.conn.commit()
        except aiosqlite.IntegrityError as e:
            await self.conn.rollback()
            logger.error(f"Failed to create session {session.id}: {e}")
            raise
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Unexpected error creating session {session.id}: {e}")
            raise

    async def get_session(self, session_id: str) -> Optional[Session]:
        """获取会话"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM sessions WHERE id = ?",
                (session_id,)
            ) as cursor:
                row = await cursor.fetchone()
                if not row:
                    return None

                return Session(
                    id=row['id'],
                    title=row['title'],
                    created_at=datetime.fromisoformat(row['created_at']),
                    updated_at=datetime.fromisoformat(row['updated_at']),
                    metadata=self._parse_metadata(row['metadata'], f"session {session_id}")
                )
        except Exception as e:
            logger.error(f"Failed to get session {session_id}: {e}")
            return None

    async def get_all_sessions(self, limit: int = 50) -> List[Session]:
        """获取所有会话，按更新时间倒序排列

        Args:
            limit: 返回的最大会话数

        Returns:
            会话列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM sessions ORDER BY updated_at DESC LIMIT ?",
                (limit,)
            ) as cursor:
                rows = await cursor.fetchall()
                sessions = []
                for row in rows:
                    sessions.append(Session(
                        id=row['id'],
                        title=row['title'],
                        created_at=datetime.fromisoformat(row['created_at']),
                        updated_at=datetime.fromisoformat(row['updated_at']),
                        metadata=self._parse_metadata(row['metadata'], f"session {row['id']}")
                    ))
                return sessions
        except Exception as e:
            logger.error(f"Failed to get all sessions: {e}")
            return []

    async def add_message(self, message: Message) -> None:
        """添加消息"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            await self.conn.execute(
                """
                INSERT INTO messages (id, session_id, role, content, timestamp, metadata)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    message.id,
                    message.session_id,
                    message.role,
                    message.content,
                    message.timestamp.isoformat(),
                    json.dumps(message.metadata) if message.metadata else None
                )
            )
            await self.conn.commit()
        except aiosqlite.IntegrityError as e:
            await self.conn.rollback()
            logger.error(f"Failed to add message {message.id}: {e}")
            raise
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Unexpected error adding message {message.id}: {e}")
            raise

    async def get_messages(self, session_id: str) -> List[Message]:
        """获取会话的所有消息"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM messages WHERE session_id = ? ORDER BY timestamp",
                (session_id,)
            ) as cursor:
                rows = await cursor.fetchall()
                messages = []
                for row in rows:
                    messages.append(Message(
                        id=row['id'],
                        session_id=row['session_id'],
                        role=row['role'],
                        content=row['content'],
                        timestamp=datetime.fromisoformat(row['timestamp']),
                        metadata=self._parse_metadata(row['metadata'], f"message {row['id']}")
                    ))
                return messages
        except Exception as e:
            logger.error(f"Failed to get messages for session {session_id}: {e}")
            return []

    async def get_recent_messages(
        self,
        session_id: str,
        limit: int = 10
    ) -> List[Message]:
        """获取最近的N条消息"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                """
                SELECT * FROM messages
                WHERE session_id = ?
                ORDER BY timestamp DESC
                LIMIT ?
                """,
                (session_id, limit)
            ) as cursor:
                rows = await cursor.fetchall()
                messages = []
                for row in rows:
                    messages.append(Message(
                        id=row['id'],
                        session_id=row['session_id'],
                        role=row['role'],
                        content=row['content'],
                        timestamp=datetime.fromisoformat(row['timestamp']),
                        metadata=self._parse_metadata(row['metadata'], f"message {row['id']}")
                    ))
                # 反转顺序（最旧的在前）
                return list(reversed(messages))
        except Exception as e:
            logger.error(f"Failed to get recent messages for session {session_id}: {e}")
            return []

    async def log_mcp_operation(
        self,
        session_id: str,
        server: str,
        tool: str,
        arguments: dict,
        status: str,
        result: Any = None,
        error: str = None,
        timestamp: datetime = None
    ) -> None:
        """Log MCP operation to audit trail.

        Args:
            session_id: Session ID
            server: MCP server name
            tool: Tool name
            arguments: Tool arguments
            status: Operation status (success/denied/failed)
            result: Tool result (optional)
            error: Error message (optional)
            timestamp: Operation timestamp
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        if timestamp is None:
            timestamp = datetime.now()

        log_id = str(uuid.uuid4())

        try:
            await self.conn.execute(
                """
                INSERT INTO mcp_audit_logs
                (id, session_id, server_name, tool_name, arguments, result, status, error_message, timestamp)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    log_id,
                    session_id,
                    server,
                    tool,
                    json.dumps(arguments),
                    json.dumps(result) if result else None,
                    status,
                    error,
                    timestamp.isoformat()
                )
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to log MCP operation: {e}")
            raise

    async def get_mcp_audit_logs(
        self,
        session_id: str,
        limit: int = 100
    ) -> List[Dict[str, Any]]:
        """Get MCP audit logs for a session.

        Args:
            session_id: Session ID
            limit: Maximum number of logs to return

        Returns:
            List of audit log dictionaries
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            cursor = await self.conn.execute(
                """
                SELECT id, session_id, server_name, tool_name, arguments,
                       result, status, error_message, timestamp
                FROM mcp_audit_logs
                WHERE session_id = ?
                ORDER BY timestamp DESC
                LIMIT ?
                """,
                (session_id, limit)
            )
            rows = await cursor.fetchall()

            logs = []
            for row in rows:
                logs.append({
                    "id": row[0],
                    "session_id": row[1],
                    "server": row[2],
                    "tool": row[3],
                    "arguments": json.loads(row[4]),
                    "result": json.loads(row[5]) if row[5] else None,
                    "status": row[6],
                    "error": row[7],
                    "timestamp": row[8]
                })

            return logs
        except Exception as e:
            logger.error(f"Failed to get MCP audit logs for session {session_id}: {e}")
            return []

    async def _execute_migration_007(self) -> None:
        """Execute migration 007: workflow tables"""
        if not self.conn:
            raise RuntimeError("Database not initialized")

        migration_file = Path(__file__).parent / "migrations" / "007_workflow_tables.sql"
        if migration_file.exists():
            with open(migration_file, 'r', encoding='utf-8') as f:
                migration_sql = f.read()
                await self.conn.executescript(migration_sql)
        else:
            # Fallback: create tables inline if migration file doesn't exist
            logger.warning(f"Migration file not found: {migration_file}, creating tables inline")
            await self._create_workflow_tables_inline()

    async def _create_workflow_tables_inline(self) -> None:
        """Create workflow tables inline (fallback)"""
        # workflows table
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS workflows (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                goal TEXT NOT NULL,
                plan JSON NOT NULL,
                status TEXT NOT NULL,
                current_task_id TEXT,
                created_at TIMESTAMP NOT NULL,
                completed_at TIMESTAMP,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            )
        """)

        # task_executions table
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS task_executions (
                id TEXT PRIMARY KEY,
                workflow_id TEXT NOT NULL,
                task_id TEXT NOT NULL,
                task_name TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                params JSON NOT NULL,
                status TEXT NOT NULL,
                result JSON,
                error TEXT,
                started_at TIMESTAMP,
                completed_at TIMESTAMP,
                retry_count INTEGER DEFAULT 0,
                FOREIGN KEY (workflow_id) REFERENCES workflows(id)
            )
        """)

        # workflow_approvals table
        await self.conn.execute("""
            CREATE TABLE IF NOT EXISTS workflow_approvals (
                id TEXT PRIMARY KEY,
                workflow_id TEXT NOT NULL,
                task_id TEXT NOT NULL,
                status TEXT NOT NULL,
                user_comment TEXT,
                created_at TIMESTAMP NOT NULL,
                responded_at TIMESTAMP,
                FOREIGN KEY (workflow_id) REFERENCES workflows(id)
            )
        """)

        # Indexes
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_workflows_session ON workflows(session_id)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_workflows_status ON workflows(status)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_workflows_created ON workflows(created_at)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_task_executions_workflow ON task_executions(workflow_id)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_task_executions_status ON task_executions(status)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_task_executions_started ON task_executions(started_at)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_approvals_workflow ON workflow_approvals(workflow_id)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_approvals_status ON workflow_approvals(status)")
        await self.conn.execute("CREATE INDEX IF NOT EXISTS idx_approvals_created ON workflow_approvals(created_at)")

    # ==================== Workflow Methods ====================

    async def create_workflow(
        self,
        workflow_id: str,
        session_id: str,
        goal: str,
        plan: Dict[str, Any],
        status: str,
        created_at: datetime,
        current_task_id: Optional[str] = None
    ) -> None:
        """创建工作流

        Args:
            workflow_id: 工作流ID
            session_id: 会话ID
            goal: 工作流目标
            plan: 任务计划（JSON）
            status: 状态（pending/running/completed/failed/cancelled）
            created_at: 创建时间
            current_task_id: 当前任务ID（可选）
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            await self.conn.execute(
                """
                INSERT INTO workflows (id, session_id, goal, plan, status, current_task_id, created_at)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    workflow_id,
                    session_id,
                    goal,
                    json.dumps(plan),
                    status,
                    current_task_id,
                    created_at.isoformat()
                )
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to create workflow {workflow_id}: {e}")
            raise

    async def get_workflow(self, workflow_id: str) -> Optional[Dict[str, Any]]:
        """获取工作流

        Args:
            workflow_id: 工作流ID

        Returns:
            工作流字典或None
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM workflows WHERE id = ?",
                (workflow_id,)
            ) as cursor:
                row = await cursor.fetchone()
                if not row:
                    return None

                return {
                    "id": row["id"],
                    "session_id": row["session_id"],
                    "goal": row["goal"],
                    "plan": json.loads(row["plan"]),
                    "status": row["status"],
                    "current_task_id": row["current_task_id"],
                    "created_at": row["created_at"],
                    "completed_at": row["completed_at"]
                }
        except Exception as e:
            logger.error(f"Failed to get workflow {workflow_id}: {e}")
            return None

    async def update_workflow_status(
        self,
        workflow_id: str,
        status: str,
        current_task_id: Optional[str] = None,
        completed_at: Optional[datetime] = None
    ) -> None:
        """更新工作流状态

        Args:
            workflow_id: 工作流ID
            status: 新状态
            current_task_id: 当前任务ID（可选）
            completed_at: 完成时间（可选）
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            if completed_at:
                await self.conn.execute(
                    """
                    UPDATE workflows
                    SET status = ?, current_task_id = ?, completed_at = ?
                    WHERE id = ?
                    """,
                    (status, current_task_id, completed_at.isoformat(), workflow_id)
                )
            else:
                await self.conn.execute(
                    """
                    UPDATE workflows
                    SET status = ?, current_task_id = ?
                    WHERE id = ?
                    """,
                    (status, current_task_id, workflow_id)
                )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to update workflow {workflow_id}: {e}")
            raise

    async def get_session_workflows(self, session_id: str) -> List[Dict[str, Any]]:
        """获取会话的所有工作流

        Args:
            session_id: 会话ID

        Returns:
            工作流列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM workflows WHERE session_id = ? ORDER BY created_at DESC",
                (session_id,)
            ) as cursor:
                rows = await cursor.fetchall()
                workflows = []
                for row in rows:
                    workflows.append({
                        "id": row["id"],
                        "session_id": row["session_id"],
                        "goal": row["goal"],
                        "plan": json.loads(row["plan"]),
                        "status": row["status"],
                        "current_task_id": row["current_task_id"],
                        "created_at": row["created_at"],
                        "completed_at": row["completed_at"]
                    })
                return workflows
        except Exception as e:
            logger.error(f"Failed to get workflows for session {session_id}: {e}")
            return []

    # ==================== Task Execution Methods ====================

    async def create_task_execution(
        self,
        execution_id: str,
        workflow_id: str,
        task_id: str,
        task_name: str,
        tool_name: str,
        params: Dict[str, Any],
        status: str,
        started_at: datetime,
        result: Optional[Dict[str, Any]] = None,
        error: Optional[str] = None,
        retry_count: int = 0
    ) -> None:
        """创建任务执行记录

        Args:
            execution_id: 执行记录ID
            workflow_id: 工作流ID
            task_id: 任务ID
            task_name: 任务名称
            tool_name: 工具名称
            params: 任务参数
            status: 状态（pending/running/completed/failed/skipped）
            started_at: 开始时间
            result: 执行结果（可选）
            error: 错误信息（可选）
            retry_count: 重试次数
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            await self.conn.execute(
                """
                INSERT INTO task_executions
                (id, workflow_id, task_id, task_name, tool_name, params, status, result, error, started_at, retry_count)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    execution_id,
                    workflow_id,
                    task_id,
                    task_name,
                    tool_name,
                    json.dumps(params),
                    status,
                    json.dumps(result) if result else None,
                    error,
                    started_at.isoformat(),
                    retry_count
                )
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to create task execution {execution_id}: {e}")
            raise

    async def get_task_execution(self, execution_id: str) -> Optional[Dict[str, Any]]:
        """获取任务执行记录

        Args:
            execution_id: 执行记录ID

        Returns:
            执行记录字典或None
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM task_executions WHERE id = ?",
                (execution_id,)
            ) as cursor:
                row = await cursor.fetchone()
                if not row:
                    return None

                return {
                    "id": row["id"],
                    "workflow_id": row["workflow_id"],
                    "task_id": row["task_id"],
                    "task_name": row["task_name"],
                    "tool_name": row["tool_name"],
                    "params": json.loads(row["params"]),
                    "status": row["status"],
                    "result": json.loads(row["result"]) if row["result"] else None,
                    "error": row["error"],
                    "started_at": row["started_at"],
                    "completed_at": row["completed_at"],
                    "retry_count": row["retry_count"]
                }
        except Exception as e:
            logger.error(f"Failed to get task execution {execution_id}: {e}")
            return None

    async def update_task_execution(
        self,
        execution_id: str,
        status: str,
        result: Optional[Dict[str, Any]] = None,
        error: Optional[str] = None,
        completed_at: Optional[datetime] = None,
        retry_count: Optional[int] = None
    ) -> None:
        """更新任务执行状态

        Args:
            execution_id: 执行记录ID
            status: 新状态
            result: 执行结果（可选）
            error: 错误信息（可选）
            completed_at: 完成时间（可选）
            retry_count: 重试次数（可选）
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            # Build update query dynamically
            updates = ["status = ?"]
            params = [status]

            if result is not None:
                updates.append("result = ?")
                params.append(json.dumps(result))

            if error is not None:
                updates.append("error = ?")
                params.append(error)

            if completed_at is not None:
                updates.append("completed_at = ?")
                params.append(completed_at.isoformat())

            if retry_count is not None:
                updates.append("retry_count = ?")
                params.append(retry_count)

            params.append(execution_id)

            await self.conn.execute(
                f"UPDATE task_executions SET {', '.join(updates)} WHERE id = ?",
                params
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to update task execution {execution_id}: {e}")
            raise

    async def get_workflow_executions(self, workflow_id: str) -> List[Dict[str, Any]]:
        """获取工作流的所有任务执行记录

        Args:
            workflow_id: 工作流ID

        Returns:
            执行记录列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM task_executions WHERE workflow_id = ? ORDER BY started_at",
                (workflow_id,)
            ) as cursor:
                rows = await cursor.fetchall()
                executions = []
                for row in rows:
                    executions.append({
                        "id": row["id"],
                        "workflow_id": row["workflow_id"],
                        "task_id": row["task_id"],
                        "task_name": row["task_name"],
                        "tool_name": row["tool_name"],
                        "params": json.loads(row["params"]),
                        "status": row["status"],
                        "result": json.loads(row["result"]) if row["result"] else None,
                        "error": row["error"],
                        "started_at": row["started_at"],
                        "completed_at": row["completed_at"],
                        "retry_count": row["retry_count"]
                    })
                return executions
        except Exception as e:
            logger.error(f"Failed to get executions for workflow {workflow_id}: {e}")
            return []

    # ==================== Workflow Approval Methods ====================

    async def create_approval(
        self,
        approval_id: str,
        workflow_id: str,
        task_id: str,
        status: str,
        created_at: datetime,
        user_comment: Optional[str] = None
    ) -> None:
        """创建审批记录

        Args:
            approval_id: 审批记录ID
            workflow_id: 工作流ID
            task_id: 任务ID
            status: 状态（pending/approved/rejected）
            created_at: 创建时间
            user_comment: 用户评论（可选）
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            await self.conn.execute(
                """
                INSERT INTO workflow_approvals (id, workflow_id, task_id, status, user_comment, created_at)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    approval_id,
                    workflow_id,
                    task_id,
                    status,
                    user_comment,
                    created_at.isoformat()
                )
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to create approval {approval_id}: {e}")
            raise

    async def get_approval(self, approval_id: str) -> Optional[Dict[str, Any]]:
        """获取审批记录

        Args:
            approval_id: 审批记录ID

        Returns:
            审批记录字典或None
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM workflow_approvals WHERE id = ?",
                (approval_id,)
            ) as cursor:
                row = await cursor.fetchone()
                if not row:
                    return None

                return {
                    "id": row["id"],
                    "workflow_id": row["workflow_id"],
                    "task_id": row["task_id"],
                    "status": row["status"],
                    "user_comment": row["user_comment"],
                    "created_at": row["created_at"],
                    "responded_at": row["responded_at"]
                }
        except Exception as e:
            logger.error(f"Failed to get approval {approval_id}: {e}")
            return None

    async def update_approval(
        self,
        approval_id: str,
        status: str,
        user_comment: Optional[str] = None,
        responded_at: Optional[datetime] = None
    ) -> None:
        """更新审批状态

        Args:
            approval_id: 审批记录ID
            status: 新状态
            user_comment: 用户评论（可选）
            responded_at: 响应时间（可选）
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            updates = ["status = ?"]
            params = [status]

            if user_comment is not None:
                updates.append("user_comment = ?")
                params.append(user_comment)

            if responded_at is not None:
                updates.append("responded_at = ?")
                params.append(responded_at.isoformat())

            params.append(approval_id)

            await self.conn.execute(
                f"UPDATE workflow_approvals SET {', '.join(updates)} WHERE id = ?",
                params
            )
            await self.conn.commit()
        except Exception as e:
            await self.conn.rollback()
            logger.error(f"Failed to update approval {approval_id}: {e}")
            raise

    async def get_pending_approvals(self, workflow_id: str) -> List[Dict[str, Any]]:
        """获取工作流的待审批记录

        Args:
            workflow_id: 工作流ID

        Returns:
            待审批记录列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM workflow_approvals WHERE workflow_id = ? AND status = 'pending' ORDER BY created_at",
                (workflow_id,)
            ) as cursor:
                rows = await cursor.fetchall()
                approvals = []
                for row in rows:
                    approvals.append({
                        "id": row["id"],
                        "workflow_id": row["workflow_id"],
                        "task_id": row["task_id"],
                        "status": row["status"],
                        "user_comment": row["user_comment"],
                        "created_at": row["created_at"],
                        "responded_at": row["responded_at"]
                    })
                return approvals
        except Exception as e:
            logger.error(f"Failed to get pending approvals for workflow {workflow_id}: {e}")
            return []

    async def get_workflow_approvals(self, workflow_id: str) -> List[Dict[str, Any]]:
        """获取工作流的所有审批记录

        Args:
            workflow_id: 工作流ID

        Returns:
            审批记录列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                "SELECT * FROM workflow_approvals WHERE workflow_id = ? ORDER BY created_at DESC",
                (workflow_id,)
            ) as cursor:
                rows = await cursor.fetchall()
                approvals = []
                for row in rows:
                    approvals.append({
                        "id": row["id"],
                        "workflow_id": row["workflow_id"],
                        "task_id": row["task_id"],
                        "status": row["status"],
                        "user_comment": row["user_comment"],
                        "created_at": row["created_at"],
                        "responded_at": row["responded_at"]
                    })
                return approvals
        except Exception as e:
            logger.error(f"Failed to get approvals for workflow {workflow_id}: {e}")
            return []

    async def get_all_approvals(self, limit: int = 100) -> List[Dict[str, Any]]:
        """获取所有审批记录

        Args:
            limit: 返回数量限制

        Returns:
            审批记录列表
        """
        if not self.conn:
            raise RuntimeError("Database not initialized")

        try:
            async with self.conn.execute(
                f"SELECT * FROM workflow_approvals ORDER BY created_at DESC LIMIT {limit}"
            ) as cursor:
                rows = await cursor.fetchall()
                approvals = []
                for row in rows:
                    approvals.append({
                        "id": row["id"],
                        "workflow_id": row["workflow_id"],
                        "task_id": row["task_id"],
                        "status": row["status"],
                        "user_comment": row["user_comment"],
                        "created_at": row["created_at"],
                        "responded_at": row["responded_at"]
                    })
                return approvals
        except Exception as e:
            logger.error(f"Failed to get all approvals: {e}")
            return []
