//! Workflow 持久化仓储

use agent_core::Result;
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
    pub metadata: Option<String>,
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
    pub created_at: DateTime<Utc>,
    pub started_at: Option<DateTime<Utc>>,
    pub completed_at: Option<DateTime<Utc>>,
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
        sqlx::query!(
            r#"
            INSERT INTO workflows (id, name, status, created_at, started_at, completed_at, metadata)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                status = excluded.status,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at,
                metadata = excluded.metadata
            "#,
            record.id,
            record.name,
            record.status,
            record.created_at,
            record.started_at,
            record.completed_at,
            record.metadata
        )
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取 workflow
    pub async fn get_workflow(&self, workflow_id: &str) -> Result<Option<WorkflowRecord>> {
        let record = sqlx::query_as!(
            WorkflowRecord,
            r#"
            SELECT id, name, status, created_at, started_at, completed_at, metadata
            FROM workflows
            WHERE id = ?
            "#,
            workflow_id
        )
        .fetch_optional(&self.pool)
        .await?;

        Ok(record)
    }

    /// 保存 task
    pub async fn save_task(&self, record: &TaskRecord) -> Result<()> {
        sqlx::query!(
            r#"
            INSERT INTO workflow_tasks (
                id, workflow_id, name, task_type, status, dependencies,
                result, error, execution_time_ms, created_at, started_at, completed_at
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                result = excluded.result,
                error = excluded.error,
                execution_time_ms = excluded.execution_time_ms,
                started_at = excluded.started_at,
                completed_at = excluded.completed_at
            "#,
            record.id,
            record.workflow_id,
            record.name,
            record.task_type,
            record.status,
            record.dependencies,
            record.result,
            record.error,
            record.execution_time_ms,
            record.created_at,
            record.started_at,
            record.completed_at
        )
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    /// 获取 workflow 的所有 tasks
    pub async fn get_tasks(&self, workflow_id: &str) -> Result<Vec<TaskRecord>> {
        let records = sqlx::query_as!(
            TaskRecord,
            r#"
            SELECT id, workflow_id, name, task_type, status, dependencies,
                   result, error, execution_time_ms, created_at, started_at, completed_at
            FROM workflow_tasks
            WHERE workflow_id = ?
            ORDER BY created_at
            "#,
            workflow_id
        )
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }

    /// 列出最近的 workflows
    pub async fn list_recent(&self, limit: i64) -> Result<Vec<WorkflowRecord>> {
        let records = sqlx::query_as!(
            WorkflowRecord,
            r#"
            SELECT id, name, status, created_at, started_at, completed_at, metadata
            FROM workflows
            ORDER BY created_at DESC
            LIMIT ?
            "#,
            limit
        )
        .fetch_all(&self.pool)
        .await?;

        Ok(records)
    }
}
