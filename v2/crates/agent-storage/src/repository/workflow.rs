//! Workflow 持久化仓储

use crate::error::Result;
use chrono::{DateTime, Utc};
use sqlx::SqlitePool;

/// Workflow 数据库记录
#[derive(Debug, Clone, sqlx::FromRow)]
pub struct WorkflowRecord {
    pub id: String,
    pub name: String,
    pub status: String,
    pub created_at: DateTime<Utc>,
    pub started_at: Option<DateTime<Utc>>,
    pub completed_at: Option<DateTime<Utc>>,
    pub paused_at: Option<DateTime<Utc>>,
    pub metadata: Option<String>,
    pub last_completed_task: Option<String>,
    pub checkpoint_data: Option<String>,
    pub total_tasks: Option<i64>,
    pub completed_tasks: Option<i64>,
}

/// Task 数据库记录
#[derive(Debug, Clone, sqlx::FromRow)]
pub struct TaskRecord {
    pub id: String,
    pub workflow_id: String,
    pub name: String,
    pub task_type: String,
    pub status: String,
    pub dependencies: Option<String>,
    pub result: Option<String>,
    pub error: Option<String>,
    pub execution_time_ms: Option<i64>,
    pub retry_history: Option<String>,
    pub created_at: DateTime<Utc>,
    pub started_at: Option<DateTime<Utc>>,
    pub completed_at: Option<DateTime<Utc>>,
}

/// Workflow 执行日志记录
#[derive(Debug, Clone, sqlx::FromRow)]
pub struct WorkflowExecutionLogRecord {
    pub id: i64,
    pub workflow_id: String,
    pub task_id: Option<String>,
    pub event_type: String,
    pub event_data: Option<String>,
    pub timestamp: DateTime<Utc>,
}

/// Workflow 仓储
pub struct WorkflowRepository {
    pool: SqlitePool,
}

impl WorkflowRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }

    /// 保存 workflow
    pub async fn save_workflow(&self, record: &WorkflowRecord) -> Result<()> {
        sqlx::query(
            r#"
            INSERT INTO workflows (
                id, name, status, created_at, started_at, completed_at, paused_at, metadata,
                last_completed_task, checkpoint_data, total_tasks, completed_tasks
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                status = excluded.status,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                paused_at = excluded.paused_at,
                metadata = excluded.metadata,
                last_completed_task = excluded.last_completed_task,
                checkpoint_data = excluded.checkpoint_data,
                total_tasks = excluded.total_tasks,
                completed_tasks = excluded.completed_tasks
            "#
        )
        .bind(&record.id)
        .bind(&record.name)
        .bind(&record.status)
        .bind(record.created_at)
        .bind(record.started_at)
        .bind(record.completed_at)
        .bind(record.paused_at)
        .bind(&record.metadata)
        .bind(&record.last_completed_task)
        .bind(&record.checkpoint_data)
        .bind(record.total_tasks)
        .bind(record.completed_tasks)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取 workflow
    pub async fn get_workflow(&self, workflow_id: &str) -> Result<Option<WorkflowRecord>> {
        let record = sqlx::query_as::<_, WorkflowRecord>(
            r#"
            SELECT id, name, status, created_at, started_at, completed_at, paused_at, metadata,
                   last_completed_task, checkpoint_data, total_tasks, completed_tasks
            FROM workflows
            WHERE id = ?
            "#
        )
        .bind(workflow_id)
        .fetch_optional(&self.pool)
        .await?;

        Ok(record)
    }

    /// 保存 task
    pub async fn save_task(&self, record: &TaskRecord) -> Result<()> {
        sqlx::query(
            r#"
            INSERT INTO workflow_tasks (
                id, workflow_id, name, task_type, status, dependencies,
                result, error, execution_time_ms, retry_history, created_at, started_at, completed_at
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                result = excluded.result,
                error = excluded.error,
                execution_time_ms = excluded.execution_time_ms,
                retry_history = excluded.retry_history,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at
            "#
        )
        .bind(&record.id)
        .bind(&record.workflow_id)
        .bind(&record.name)
        .bind(&record.task_type)
        .bind(&record.status)
        .bind(&record.dependencies)
        .bind(&record.result)
        .bind(&record.error)
        .bind(record.execution_time_ms)
        .bind(&record.retry_history)
        .bind(record.created_at)
        .bind(record.started_at)
        .bind(record.completed_at)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取 workflow 的所有 tasks
    pub async fn get_tasks(&self, workflow_id: &str) -> Result<Vec<TaskRecord>> {
        let records = sqlx::query_as::<_, TaskRecord>(
            r#"
            SELECT id, workflow_id, name, task_type, status, dependencies,
                   result, error, execution_time_ms, retry_history, created_at, started_at, completed_at
            FROM workflow_tasks
            WHERE workflow_id = ?
            ORDER BY created_at
            "#
        )
        .bind(workflow_id)
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }

    /// 列出最近的 workflows
    pub async fn list_recent(&self, limit: i64) -> Result<Vec<WorkflowRecord>> {
        let records = sqlx::query_as::<_, WorkflowRecord>(
            r#"
            SELECT id, name, status, created_at, started_at, completed_at, paused_at, metadata,
                   last_completed_task, checkpoint_data, total_tasks, completed_tasks
            FROM workflows
            ORDER BY created_at DESC
            LIMIT ?
            "#
        )
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }

    /// 更新 workflow 状态
    pub async fn update_status(&self, workflow_id: &str, status: &str) -> Result<()> {
        let now = Utc::now();

        sqlx::query(
            r#"
            UPDATE workflows
            SET status = ?,
                completed_at = CASE
                    WHEN ? IN ('completed', 'failed', 'cancelled') THEN ?
                    ELSE completed_at
                END,
                paused_at = CASE
                    WHEN ? = 'paused' THEN ?
                    ELSE NULL
                END
            WHERE id = ?
            "#
        )
        .bind(status)
        .bind(status)
        .bind(now)
        .bind(status)
        .bind(now)
        .bind(workflow_id)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 更新 workflow 断点信息
    ///
    /// 记录当前执行进度，以便后续恢复
    pub async fn update_checkpoint(
        &self,
        workflow_id: &str,
        last_completed_task: Option<&str>,
        checkpoint_data: Option<&str>,
    ) -> Result<()> {
        sqlx::query(
            r#"
            UPDATE workflows
            SET last_completed_task = ?,
                checkpoint_data = ?
            WHERE id = ?
            "#,
        )
        .bind(last_completed_task)
        .bind(checkpoint_data)
        .bind(workflow_id)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 更新 workflow 进度计数
    pub async fn update_progress(
        &self,
        workflow_id: &str,
        total_tasks: i64,
        completed_tasks: i64,
    ) -> Result<()> {
        sqlx::query(
            r#"
            UPDATE workflows
            SET total_tasks = ?,
                completed_tasks = ?
            WHERE id = ?
            "#,
        )
        .bind(total_tasks)
        .bind(completed_tasks)
        .bind(workflow_id)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取可恢复的 workflows（状态为 paused）
    pub async fn get_resumable_workflows(&self) -> Result<Vec<WorkflowRecord>> {
        let records = sqlx::query_as::<_, WorkflowRecord>(
            r#"
            SELECT id, name, status, created_at, started_at, completed_at, paused_at, metadata,
                   last_completed_task, checkpoint_data, total_tasks, completed_tasks
            FROM workflows
            WHERE status = 'paused'
            ORDER BY paused_at DESC
            "#,
        )
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }

    /// 获取指定任务之后的所有待执行任务
    ///
    /// 用于从断点恢复执行
    pub async fn get_pending_tasks_after(
        &self,
        workflow_id: &str,
        last_completed_task_id: Option<&str>,
    ) -> Result<Vec<TaskRecord>> {
        if let Some(last_task_id) = last_completed_task_id {
            // 获取已完成任务及其之后的任务
            let records = sqlx::query_as::<_, TaskRecord>(
                r#"
                SELECT id, workflow_id, name, task_type, status, dependencies,
                       result, error, execution_time_ms, retry_history, created_at, started_at, completed_at
                FROM workflow_tasks
                WHERE workflow_id = ?
                  AND status IN ('pending', 'failed')
                  AND created_at > (
                      SELECT created_at FROM workflow_tasks WHERE id = ?
                  )
                ORDER BY created_at
                "#,
            )
            .bind(workflow_id)
            .bind(last_task_id)
            .fetch_all(&self.pool)
            .await?;

            Ok(records)
        } else {
            // 获取所有待执行任务
            let records = sqlx::query_as::<_, TaskRecord>(
                r#"
                SELECT id, workflow_id, name, task_type, status, dependencies,
                       result, error, execution_time_ms, retry_history, created_at, started_at, completed_at
                FROM workflow_tasks
                WHERE workflow_id = ?
                  AND status IN ('pending', 'failed')
                ORDER BY created_at
                "#,
            )
            .bind(workflow_id)
            .fetch_all(&self.pool)
            .await?;

            Ok(records)
        }
    }

    /// 保存执行日志
    pub async fn save_execution_log(
        &self,
        workflow_id: &str,
        task_id: Option<&str>,
        event_type: &str,
        event_data: Option<&str>,
    ) -> Result<()> {
        let now = Utc::now();

        sqlx::query(
            r#"
            INSERT INTO workflow_execution_log (workflow_id, task_id, event_type, event_data, timestamp)
            VALUES (?, ?, ?, ?, ?)
            "#,
        )
        .bind(workflow_id)
        .bind(task_id)
        .bind(event_type)
        .bind(event_data)
        .bind(now)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取执行日志
    pub async fn get_execution_logs(
        &self,
        workflow_id: &str,
        limit: Option<i64>,
    ) -> Result<Vec<WorkflowExecutionLogRecord>> {
        let limit = limit.unwrap_or(100);

        let records = sqlx::query_as::<_, WorkflowExecutionLogRecord>(
            r#"
            SELECT id, workflow_id, task_id, event_type, event_data, timestamp
            FROM workflow_execution_log
            WHERE workflow_id = ?
            ORDER BY timestamp DESC
            LIMIT ?
            "#,
        )
        .bind(workflow_id)
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }

    /// 删除 workflow 及其所有相关数据
    ///
    /// 注意：由于设置了 ON DELETE CASCADE，删除 workflow 会自动删除相关的 tasks 和 logs
    pub async fn delete_workflow(&self, workflow_id: &str) -> Result<()> {
        sqlx::query(
            r#"
            DELETE FROM workflows WHERE id = ?
            "#,
        )
        .bind(workflow_id)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取 workflow 统计信息
    pub async fn get_workflow_stats(&self, workflow_id: &str) -> Result<Option<WorkflowStats>> {
        let stats = sqlx::query_as::<_, WorkflowStatsRow>(
            r#"
            SELECT
                COUNT(*) as total_tasks,
                SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as completed_tasks,
                SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END) as failed_tasks,
                SUM(CASE WHEN status = 'pending' THEN 1 ELSE 0 END) as pending_tasks,
                SUM(CASE WHEN status = 'running' THEN 1 ELSE 0 END) as running_tasks,
                SUM(CASE WHEN retry_history IS NOT NULL AND retry_history != 'null' THEN 1 ELSE 0 END) as tasks_with_retries,
                SUM(execution_time_ms) as total_execution_time_ms
            FROM workflow_tasks
            WHERE workflow_id = ?
            "#,
        )
        .bind(workflow_id)
        .fetch_optional(&self.pool)
        .await?;

        Ok(stats.map(|s| WorkflowStats {
            total_tasks: s.total_tasks,
            completed_tasks: s.completed_tasks,
            failed_tasks: s.failed_tasks,
            pending_tasks: s.pending_tasks,
            running_tasks: s.running_tasks,
            tasks_with_retries: s.tasks_with_retries,
            total_execution_time_ms: s.total_execution_time_ms.unwrap_or(0),
        }))
    }
}

/// Workflow 统计信息（用于查询）
#[derive(Debug, Clone, sqlx::FromRow)]
struct WorkflowStatsRow {
    total_tasks: i64,
    completed_tasks: i64,
    failed_tasks: i64,
    pending_tasks: i64,
    running_tasks: i64,
    tasks_with_retries: i64,
    total_execution_time_ms: Option<i64>,
}

/// Workflow 统计信息（公开）
#[derive(Debug, Clone)]
pub struct WorkflowStats {
    pub total_tasks: i64,
    pub completed_tasks: i64,
    pub failed_tasks: i64,
    pub pending_tasks: i64,
    pub running_tasks: i64,
    pub tasks_with_retries: i64,
    pub total_execution_time_ms: i64,
}
