use chrono::{DateTime, Utc};
use sqlx::SqlitePool;
use uuid::Uuid;

use crate::error::{TaskError, Result};
use crate::models::*;

pub struct TaskRepository {
    pool: SqlitePool,
}

impl TaskRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }

    pub async fn run_migrations(&self) -> Result<()> {
        sqlx::migrate!("./migrations").run(&self.pool).await.map_err(|e| {
            TaskError::DatabaseError(sqlx::Error::Protocol(format!("迁移失败: {}", e)))
        })?;
        Ok(())
    }

    // === 任务 CRUD ===

    pub async fn create_task(&self, task: &ScheduledTask) -> Result<()> {
        let id = task.id.to_string();
        let created_at = task.created_at.to_rfc3339();
        let updated_at = task.updated_at.map(|t| t.to_rfc3339());
        let next_exec = task.next_execution_at.map(|t| t.to_rfc3339());
        let payload = serde_json::to_string(&task.task_payload)
            .map_err(|e| TaskError::SerializationError(e.to_string()))?;

        sqlx::query(
            r#"INSERT INTO scheduled_tasks
               (id, name, description, owner_id, schedule, schedule_type,
                task_type, task_payload, status, max_retries, timeout_seconds,
                created_at, updated_at, next_execution_at, execution_count)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&task.name)
        .bind(&task.description)
        .bind(&task.owner_id)
        .bind(&task.schedule)
        .bind(task.schedule_type as i32)
        .bind(task.task_type as i32)
        .bind(&payload)
        .bind(task.status as i32)
        .bind(task.max_retries)
        .bind(task.timeout_seconds)
        .bind(&created_at)
        .bind(&updated_at)
        .bind(&next_exec)
        .bind(task.execution_count)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn get_task(&self, id: Uuid) -> Result<ScheduledTask> {
        let id_str = id.to_string();
        let row = sqlx::query_as::<_, TaskRow>("SELECT * FROM scheduled_tasks WHERE id = ?")
            .bind(&id_str)
            .fetch_optional(&self.pool)
            .await?;

        match row {
            Some(r) => r.into_task(),
            None => Err(TaskError::NotFound(id.to_string())),
        }
    }

    pub async fn list_tasks(&self, owner_id: &str, status: Option<TaskStatus>) -> Result<Vec<ScheduledTask>> {
        let rows = match status {
            Some(s) => {
                sqlx::query_as::<_, TaskRow>(
                    "SELECT * FROM scheduled_tasks WHERE owner_id = ? AND status = ? ORDER BY created_at DESC",
                )
                .bind(owner_id)
                .bind(s as i32)
                .fetch_all(&self.pool)
                .await?
            }
            None => {
                sqlx::query_as::<_, TaskRow>(
                    "SELECT * FROM scheduled_tasks WHERE owner_id = ? ORDER BY created_at DESC",
                )
                .bind(owner_id)
                .fetch_all(&self.pool)
                .await?
            }
        };

        rows.into_iter().map(|r| r.into_task()).collect()
    }

    pub async fn update_task(&self, task: &ScheduledTask) -> Result<()> {
        let id = task.id.to_string();
        let updated_at = Utc::now().to_rfc3339();
        let last_exec = task.last_execution_at.map(|t| t.to_rfc3339());
        let next_exec = task.next_execution_at.map(|t| t.to_rfc3339());
        let payload = serde_json::to_string(&task.task_payload)
            .map_err(|e| TaskError::SerializationError(e.to_string()))?;

        sqlx::query(
            r#"UPDATE scheduled_tasks SET
               name = ?, description = ?, schedule = ?, schedule_type = ?,
               task_type = ?, task_payload = ?, status = ?, max_retries = ?,
               timeout_seconds = ?, updated_at = ?, last_execution_at = ?,
               next_execution_at = ?, execution_count = ?
               WHERE id = ?"#,
        )
        .bind(&task.name)
        .bind(&task.description)
        .bind(&task.schedule)
        .bind(task.schedule_type as i32)
        .bind(task.task_type as i32)
        .bind(&payload)
        .bind(task.status as i32)
        .bind(task.max_retries)
        .bind(task.timeout_seconds)
        .bind(&updated_at)
        .bind(&last_exec)
        .bind(&next_exec)
        .bind(task.execution_count)
        .bind(&id)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn delete_task(&self, id: Uuid) -> Result<()> {
        let id_str = id.to_string();
        sqlx::query("DELETE FROM scheduled_tasks WHERE id = ?")
            .bind(&id_str)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    pub async fn get_due_tasks(&self) -> Result<Vec<ScheduledTask>> {
        let now = Utc::now().to_rfc3339();
        let rows = sqlx::query_as::<_, TaskRow>(
            r#"SELECT * FROM scheduled_tasks
               WHERE status IN (0, 1)
               AND next_execution_at IS NOT NULL
               AND next_execution_at <= ?
               ORDER BY next_execution_at ASC"#,
        )
        .bind(&now)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_task()).collect()
    }

    // === 执行历史 ===

    pub async fn save_execution(&self, exec: &TaskExecution) -> Result<()> {
        let id = exec.id.to_string();
        let task_id = exec.task_id.to_string();
        let started_at = exec.started_at.to_rfc3339();
        let completed_at = exec.completed_at.map(|t| t.to_rfc3339());

        sqlx::query(
            r#"INSERT INTO task_executions
               (id, task_id, started_at, completed_at, status, result, error_message, retry_count)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&task_id)
        .bind(&started_at)
        .bind(&completed_at)
        .bind(exec.status as i32)
        .bind(&exec.result)
        .bind(&exec.error_message)
        .bind(exec.retry_count)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn list_executions(&self, task_id: Uuid, limit: u32) -> Result<Vec<TaskExecution>> {
        let task_id_str = task_id.to_string();
        let rows = sqlx::query_as::<_, ExecutionRow>(
            "SELECT * FROM task_executions WHERE task_id = ? ORDER BY started_at DESC LIMIT ?",
        )
        .bind(&task_id_str)
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        rows.into_iter().map(|r| r.into_execution()).collect()
    }

    // === 统计 ===

    pub async fn stats(&self, owner_id: &str) -> Result<TaskStats> {
        let total: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM scheduled_tasks WHERE owner_id = ?",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let active: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM scheduled_tasks WHERE owner_id = ? AND status IN (0, 1)",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let paused: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM scheduled_tasks WHERE owner_id = ? AND status = 4",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let completed: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM scheduled_tasks WHERE owner_id = ? AND status = 2",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let failed: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM scheduled_tasks WHERE owner_id = ? AND status = 3",
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let total_exec: (i64,) = sqlx::query_as(
            r#"SELECT COUNT(*) FROM task_executions e
               JOIN scheduled_tasks t ON e.task_id = t.id
               WHERE t.owner_id = ?"#,
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let success_exec: (i64,) = sqlx::query_as(
            r#"SELECT COUNT(*) FROM task_executions e
               JOIN scheduled_tasks t ON e.task_id = t.id
               WHERE t.owner_id = ? AND e.status = 0"#,
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        let failed_exec: (i64,) = sqlx::query_as(
            r#"SELECT COUNT(*) FROM task_executions e
               JOIN scheduled_tasks t ON e.task_id = t.id
               WHERE t.owner_id = ? AND e.status IN (1, 2)"#,
        )
        .bind(owner_id)
        .fetch_one(&self.pool)
        .await?;

        Ok(TaskStats {
            total_tasks: total.0,
            active_tasks: active.0,
            paused_tasks: paused.0,
            completed_tasks: completed.0,
            failed_tasks: failed.0,
            total_executions: total_exec.0,
            successful_executions: success_exec.0,
            failed_executions: failed_exec.0,
        })
    }
}

// === Row 类型 ===

#[derive(sqlx::FromRow)]
struct TaskRow {
    id: String,
    name: String,
    description: Option<String>,
    owner_id: String,
    schedule: String,
    schedule_type: i32,
    task_type: i32,
    task_payload: String,
    status: i32,
    max_retries: i32,
    timeout_seconds: i32,
    created_at: String,
    updated_at: Option<String>,
    last_execution_at: Option<String>,
    next_execution_at: Option<String>,
    execution_count: i32,
}

impl TaskRow {
    fn into_task(self) -> Result<ScheduledTask> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let created_at = DateTime::parse_from_rfc3339(&self.created_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let updated_at = self.updated_at.as_deref()
            .map(|s| DateTime::parse_from_rfc3339(s).map(|dt| dt.with_timezone(&Utc)))
            .transpose()
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let last_execution_at = self.last_execution_at.as_deref()
            .map(|s| DateTime::parse_from_rfc3339(s).map(|dt| dt.with_timezone(&Utc)))
            .transpose()
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let next_execution_at = self.next_execution_at.as_deref()
            .map(|s| DateTime::parse_from_rfc3339(s).map(|dt| dt.with_timezone(&Utc)))
            .transpose()
            .map_err(|e| TaskError::ParseError(e.to_string()))?;

        let schedule_type = ScheduleType::from_i32(self.schedule_type)
            .ok_or_else(|| TaskError::ParseError(format!("无效的调度类型: {}", self.schedule_type)))?;
        let task_type = TaskType::from_i32(self.task_type)
            .ok_or_else(|| TaskError::ParseError(format!("无效的任务类型: {}", self.task_type)))?;
        let status = TaskStatus::from_i32(self.status)
            .ok_or_else(|| TaskError::ParseError(format!("无效的状态: {}", self.status)))?;
        let task_payload: TaskPayload = serde_json::from_str(&self.task_payload)
            .map_err(|e| TaskError::SerializationError(e.to_string()))?;

        Ok(ScheduledTask {
            id,
            name: self.name,
            description: self.description,
            owner_id: self.owner_id,
            schedule: self.schedule,
            schedule_type,
            task_type,
            task_payload,
            status,
            max_retries: self.max_retries,
            timeout_seconds: self.timeout_seconds,
            created_at,
            updated_at,
            last_execution_at,
            next_execution_at,
            execution_count: self.execution_count,
        })
    }
}

#[derive(sqlx::FromRow)]
struct ExecutionRow {
    id: String,
    task_id: String,
    started_at: String,
    completed_at: Option<String>,
    status: i32,
    result: Option<String>,
    error_message: Option<String>,
    retry_count: i32,
}

impl ExecutionRow {
    fn into_execution(self) -> Result<TaskExecution> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let task_id = Uuid::parse_str(&self.task_id)
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let started_at = DateTime::parse_from_rfc3339(&self.started_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let completed_at = self.completed_at.as_deref()
            .map(|s| DateTime::parse_from_rfc3339(s).map(|dt| dt.with_timezone(&Utc)))
            .transpose()
            .map_err(|e| TaskError::ParseError(e.to_string()))?;
        let status = ExecutionStatus::from_i32(self.status)
            .ok_or_else(|| TaskError::ParseError(format!("无效的执行状态: {}", self.status)))?;

        Ok(TaskExecution {
            id,
            task_id,
            started_at,
            completed_at,
            status,
            result: self.result,
            error_message: self.error_message,
            retry_count: self.retry_count,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn setup_db() -> SqlitePool {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        pool
    }

    fn make_task() -> ScheduledTask {
        ScheduledTask::new(
            "test_task".into(),
            "user1".into(),
            "0 9 * * *".into(),
            ScheduleType::Cron,
            TaskType::SkillInvocation,
            TaskPayload::skill("greet".into(), None),
        )
    }

    #[tokio::test]
    async fn test_create_and_get_task() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);
        let task = make_task();

        repo.create_task(&task).await.unwrap();
        let fetched = repo.get_task(task.id).await.unwrap();

        assert_eq!(fetched.name, "test_task");
        assert_eq!(fetched.owner_id, "user1");
        assert_eq!(fetched.schedule, "0 9 * * *");
        assert_eq!(fetched.schedule_type, ScheduleType::Cron);
        assert_eq!(fetched.task_type, TaskType::SkillInvocation);
        assert_eq!(fetched.status, TaskStatus::Pending);
    }

    #[tokio::test]
    async fn test_list_tasks() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);

        let t1 = make_task();
        let mut t2 = make_task();
        t2.name = "task2".into();
        t2.status = TaskStatus::Paused;

        repo.create_task(&t1).await.unwrap();
        repo.create_task(&t2).await.unwrap();

        let all = repo.list_tasks("user1", None).await.unwrap();
        assert_eq!(all.len(), 2);

        let pending = repo.list_tasks("user1", Some(TaskStatus::Pending)).await.unwrap();
        assert_eq!(pending.len(), 1);

        let paused = repo.list_tasks("user1", Some(TaskStatus::Paused)).await.unwrap();
        assert_eq!(paused.len(), 1);
    }

    #[tokio::test]
    async fn test_update_task() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);
        let mut task = make_task();

        repo.create_task(&task).await.unwrap();

        task.status = TaskStatus::Running;
        task.execution_count = 1;
        repo.update_task(&task).await.unwrap();

        let fetched = repo.get_task(task.id).await.unwrap();
        assert_eq!(fetched.status, TaskStatus::Running);
        assert_eq!(fetched.execution_count, 1);
    }

    #[tokio::test]
    async fn test_delete_task() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);
        let task = make_task();

        repo.create_task(&task).await.unwrap();
        repo.delete_task(task.id).await.unwrap();

        let result = repo.get_task(task.id).await;
        assert!(matches!(result, Err(TaskError::NotFound(_))));
    }

    #[tokio::test]
    async fn test_save_and_list_executions() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);
        let task = make_task();
        repo.create_task(&task).await.unwrap();

        let e1 = TaskExecution::new(task.id).mark_success(Some("OK".into()));
        let e2 = TaskExecution::new(task.id).mark_failed("error".into(), 1);

        repo.save_execution(&e1).await.unwrap();
        repo.save_execution(&e2).await.unwrap();

        let execs = repo.list_executions(task.id, 10).await.unwrap();
        assert_eq!(execs.len(), 2);
    }

    #[tokio::test]
    async fn test_stats() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);

        let t1 = make_task();
        let mut t2 = make_task();
        t2.status = TaskStatus::Paused;

        repo.create_task(&t1).await.unwrap();
        repo.create_task(&t2).await.unwrap();

        let exec = TaskExecution::new(t1.id).mark_success(None);
        repo.save_execution(&exec).await.unwrap();

        let stats = repo.stats("user1").await.unwrap();
        assert_eq!(stats.total_tasks, 2);
        assert_eq!(stats.active_tasks, 1);
        assert_eq!(stats.paused_tasks, 1);
        assert_eq!(stats.total_executions, 1);
        assert_eq!(stats.successful_executions, 1);
    }

    #[tokio::test]
    async fn test_get_not_found() {
        let pool = setup_db().await;
        let repo = TaskRepository::new(pool);
        let result = repo.get_task(Uuid::new_v4()).await;
        assert!(matches!(result, Err(TaskError::NotFound(_))));
    }
}
