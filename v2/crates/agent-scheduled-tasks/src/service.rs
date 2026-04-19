use chrono::Utc;
use tracing::{info, warn};
use uuid::Uuid;

use crate::error::Result;
use crate::models::*;
use crate::parser::{next_execution_time, parse_schedule};
use crate::repository::TaskRepository;

pub struct TaskService {
    repository: TaskRepository,
}

impl TaskService {
    pub fn new(repository: TaskRepository) -> Self {
        Self { repository }
    }

    pub async fn create_task(
        &self,
        name: String,
        owner_id: String,
        schedule_input: String,
        task_type: TaskType,
        payload: TaskPayload,
    ) -> Result<ScheduledTask> {
        let parsed = parse_schedule(&schedule_input)?;

        let next = next_execution_time(&parsed.cron_expression).ok();

        let mut task = ScheduledTask::new(
            name,
            owner_id,
            parsed.cron_expression,
            parsed.schedule_type,
            task_type,
            payload,
        );
        task.next_execution_at = next;

        self.repository.create_task(&task).await?;

        info!("任务已创建: {} ({})", task.name, task.id);
        Ok(task)
    }

    pub async fn get_task(&self, id: Uuid) -> Result<ScheduledTask> {
        self.repository.get_task(id).await
    }

    pub async fn list_tasks(&self, owner_id: &str, status: Option<TaskStatus>) -> Result<Vec<ScheduledTask>> {
        self.repository.list_tasks(owner_id, status).await
    }

    pub async fn pause_task(&self, id: Uuid) -> Result<ScheduledTask> {
        let mut task = self.repository.get_task(id).await?;

        if task.status == TaskStatus::Paused {
            return Ok(task);
        }

        task.status = TaskStatus::Paused;
        task.updated_at = Some(Utc::now());
        self.repository.update_task(&task).await?;

        info!("任务已暂停: {}", task.name);
        Ok(task)
    }

    pub async fn resume_task(&self, id: Uuid) -> Result<ScheduledTask> {
        let mut task = self.repository.get_task(id).await?;

        if task.status != TaskStatus::Paused {
            return Ok(task);
        }

        task.status = TaskStatus::Pending;
        task.next_execution_at = next_execution_time(&task.schedule).ok();
        task.updated_at = Some(Utc::now());
        self.repository.update_task(&task).await?;

        info!("任务已恢复: {}", task.name);
        Ok(task)
    }

    pub async fn delete_task(&self, id: Uuid) -> Result<()> {
        self.repository.delete_task(id).await
    }

    pub async fn get_due_tasks(&self) -> Result<Vec<ScheduledTask>> {
        self.repository.get_due_tasks().await
    }

    pub async fn record_execution(
        &self,
        task_id: Uuid,
        success: bool,
        result: Option<String>,
        error: Option<String>,
        retry_count: i32,
    ) -> Result<()> {
        let mut exec = TaskExecution::new(task_id);
        exec = if success {
            exec.mark_success(result)
        } else if let Some(err) = error {
            exec.mark_failed(err, retry_count)
        } else {
            exec.mark_timeout()
        };

        self.repository.save_execution(&exec).await?;

        let mut task = self.repository.get_task(task_id).await?;
        task.execution_count += 1;
        task.last_execution_at = Some(Utc::now());

        if success {
            task.next_execution_at = next_execution_time(&task.schedule).ok();
            task.status = TaskStatus::Pending;
        } else if retry_count >= task.max_retries {
            task.status = TaskStatus::Failed;
            warn!("任务已达最大重试次数: {} ({}次)", task.name, retry_count);
        } else {
            task.next_execution_at = next_execution_time(&task.schedule).ok();
            task.status = TaskStatus::Pending;
        }

        task.updated_at = Some(Utc::now());
        self.repository.update_task(&task).await?;

        Ok(())
    }

    pub async fn list_executions(&self, task_id: Uuid, limit: u32) -> Result<Vec<TaskExecution>> {
        self.repository.list_executions(task_id, limit).await
    }

    pub async fn stats(&self, owner_id: &str) -> Result<TaskStats> {
        self.repository.stats(owner_id).await
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use sqlx::SqlitePool;

    async fn setup_service() -> TaskService {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        let repo = TaskRepository::new(pool);
        TaskService::new(repo)
    }

    #[tokio::test]
    async fn test_create_task_with_cron() {
        let service = setup_service().await;
        let task = service
            .create_task(
                "daily_greet".into(),
                "user1".into(),
                "0 9 * * *".into(),
                TaskType::SkillInvocation,
                TaskPayload::skill("greet".into(), None),
            )
            .await
            .unwrap();

        assert_eq!(task.name, "daily_greet");
        assert_eq!(task.schedule_type, ScheduleType::Cron);
        assert!(task.next_execution_at.is_some());
    }

    #[tokio::test]
    async fn test_create_task_with_natural() {
        let service = setup_service().await;
        let task = service
            .create_task(
                "morning_reminder".into(),
                "user1".into(),
                "每天上午9点".into(),
                TaskType::MemoryReminder,
                TaskPayload::reminder("早安提醒".into()),
            )
            .await
            .unwrap();

        assert_eq!(task.schedule_type, ScheduleType::Natural);
        assert!(task.next_execution_at.is_some());
    }

    #[tokio::test]
    async fn test_pause_and_resume() {
        let service = setup_service().await;
        let task = service
            .create_task(
                "test".into(),
                "user1".into(),
                "0 9 * * *".into(),
                TaskType::CustomCommand,
                TaskPayload::command("echo hi".into()),
            )
            .await
            .unwrap();

        let paused = service.pause_task(task.id).await.unwrap();
        assert_eq!(paused.status, TaskStatus::Paused);

        let resumed = service.resume_task(task.id).await.unwrap();
        assert_eq!(resumed.status, TaskStatus::Pending);
        assert!(resumed.next_execution_at.is_some());
    }

    #[tokio::test]
    async fn test_record_execution_success() {
        let service = setup_service().await;
        let task = service
            .create_task(
                "test".into(),
                "user1".into(),
                "每30分钟".into(),
                TaskType::CustomCommand,
                TaskPayload::command("echo".into()),
            )
            .await
            .unwrap();

        service
            .record_execution(task.id, true, Some("OK".into()), None, 0)
            .await
            .unwrap();

        let updated = service.get_task(task.id).await.unwrap();
        assert_eq!(updated.execution_count, 1);
        assert!(updated.last_execution_at.is_some());
        assert_eq!(updated.status, TaskStatus::Pending);

        let execs = service.list_executions(task.id, 10).await.unwrap();
        assert_eq!(execs.len(), 1);
        assert_eq!(execs[0].status, ExecutionStatus::Success);
    }

    #[tokio::test]
    async fn test_record_execution_max_retries() {
        let service = setup_service().await;
        let task = service
            .create_task(
                "test".into(),
                "user1".into(),
                "0 9 * * *".into(),
                TaskType::CustomCommand,
                TaskPayload::command("failing".into()),
            )
            .await
            .unwrap();

        service
            .record_execution(task.id, false, None, Some("err".into()), 3)
            .await
            .unwrap();

        let updated = service.get_task(task.id).await.unwrap();
        assert_eq!(updated.status, TaskStatus::Failed);
    }

    #[tokio::test]
    async fn test_stats() {
        let service = setup_service().await;

        service
            .create_task("t1".into(), "user1".into(), "0 9 * * *".into(),
                TaskType::CustomCommand, TaskPayload::command("echo".into()))
            .await.unwrap();

        let t2 = service
            .create_task("t2".into(), "user1".into(), "每天".into(),
                TaskType::MemoryReminder, TaskPayload::reminder("hi".into()))
            .await.unwrap();
        service.pause_task(t2.id).await.unwrap();

        let stats = service.stats("user1").await.unwrap();
        assert_eq!(stats.total_tasks, 2);
        assert_eq!(stats.active_tasks, 1);
        assert_eq!(stats.paused_tasks, 1);
    }

    #[tokio::test]
    async fn test_invalid_schedule() {
        let service = setup_service().await;
        let result = service
            .create_task(
                "bad".into(),
                "user1".into(),
                "随便什么时候".into(),
                TaskType::CustomCommand,
                TaskPayload::command("echo".into()),
            )
            .await;

        assert!(result.is_err());
    }
}
