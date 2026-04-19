use chrono::{DateTime, Utc};
use sqlx::SqlitePool;
use uuid::Uuid;

use crate::error::{ExtractionError, Result};
use crate::models::{ExtractionRecord, ExtractionStats, ExtractionStatus};

pub struct ExtractionRepository {
    pool: SqlitePool,
}

impl ExtractionRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }

    pub async fn run_migrations(&self) -> Result<()> {
        sqlx::migrate!("./migrations").run(&self.pool).await.map_err(|e| {
            ExtractionError::DatabaseError(sqlx::Error::Protocol(format!("迁移失败: {}", e)))
        })?;
        Ok(())
    }

    pub async fn save_record(&self, record: &ExtractionRecord) -> Result<()> {
        let id = record.id.to_string();
        let session_id = record.session_id.to_string();
        let extracted_at = record.extracted_at.to_rfc3339();
        let status = record.status.as_str();

        sqlx::query(
            r#"INSERT INTO extraction_history
               (id, session_id, extracted_at, status, skill_name, skill_namespace,
                message_count, error_message)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)"#,
        )
        .bind(&id)
        .bind(&session_id)
        .bind(&extracted_at)
        .bind(status)
        .bind(&record.skill_name)
        .bind(&record.skill_namespace)
        .bind(record.message_count)
        .bind(&record.error_message)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn list_records(
        &self,
        status: Option<ExtractionStatus>,
        limit: u32,
    ) -> Result<Vec<ExtractionRecord>> {
        let rows = match status {
            Some(s) => {
                sqlx::query_as::<_, RecordRow>(
                    "SELECT * FROM extraction_history WHERE status = ? ORDER BY extracted_at DESC LIMIT ?",
                )
                .bind(s.as_str())
                .bind(limit)
                .fetch_all(&self.pool)
                .await?
            }
            None => {
                sqlx::query_as::<_, RecordRow>(
                    "SELECT * FROM extraction_history ORDER BY extracted_at DESC LIMIT ?",
                )
                .bind(limit)
                .fetch_all(&self.pool)
                .await?
            }
        };

        rows.into_iter().map(|r| r.into_record()).collect()
    }

    pub async fn get_record(&self, id: Uuid) -> Result<ExtractionRecord> {
        let id_str = id.to_string();
        let row = sqlx::query_as::<_, RecordRow>(
            "SELECT * FROM extraction_history WHERE id = ?",
        )
        .bind(&id_str)
        .fetch_optional(&self.pool)
        .await?;

        match row {
            Some(r) => Ok(r.into_record()?),
            None => Err(ExtractionError::NotFound(id.to_string())),
        }
    }

    pub async fn stats(&self) -> Result<ExtractionStats> {
        let total: (i64,) = sqlx::query_as("SELECT COUNT(*) FROM extraction_history")
            .fetch_one(&self.pool)
            .await?;

        let successful: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM extraction_history WHERE status = 'success'",
        )
        .fetch_one(&self.pool)
        .await?;

        let failed: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM extraction_history WHERE status = 'failed'",
        )
        .fetch_one(&self.pool)
        .await?;

        let unique: (i64,) = sqlx::query_as(
            "SELECT COUNT(DISTINCT skill_name) FROM extraction_history WHERE skill_name IS NOT NULL",
        )
        .fetch_one(&self.pool)
        .await?;

        Ok(ExtractionStats {
            total_extractions: total.0,
            successful: successful.0,
            failed: failed.0,
            unique_skills: unique.0,
        })
    }
}

#[derive(sqlx::FromRow)]
struct RecordRow {
    id: String,
    session_id: String,
    extracted_at: String,
    status: String,
    skill_name: Option<String>,
    skill_namespace: Option<String>,
    message_count: i32,
    error_message: Option<String>,
}

impl RecordRow {
    fn into_record(self) -> Result<ExtractionRecord> {
        let id = Uuid::parse_str(&self.id)
            .map_err(|e| ExtractionError::ParseError(e.to_string()))?;
        let session_id = Uuid::parse_str(&self.session_id)
            .map_err(|e| ExtractionError::ParseError(e.to_string()))?;
        let extracted_at = DateTime::parse_from_rfc3339(&self.extracted_at)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| ExtractionError::ParseError(e.to_string()))?;
        let status = ExtractionStatus::from_str(&self.status).unwrap_or(ExtractionStatus::Pending);

        Ok(ExtractionRecord {
            id,
            session_id,
            extracted_at,
            status,
            skill_name: self.skill_name,
            skill_namespace: self.skill_namespace,
            message_count: self.message_count,
            error_message: self.error_message,
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

    #[tokio::test]
    async fn test_save_and_get_record() {
        let pool = setup_db().await;
        let repo = ExtractionRepository::new(pool);

        let session_id = Uuid::new_v4();
        let record = ExtractionRecord::new(session_id, 10)
            .mark_success("greet".to_string(), Some("personal".to_string()));

        repo.save_record(&record).await.unwrap();

        let fetched = repo.get_record(record.id).await.unwrap();
        assert_eq!(fetched.status, ExtractionStatus::Success);
        assert_eq!(fetched.skill_name, Some("greet".to_string()));
        assert_eq!(fetched.message_count, 10);
    }

    #[tokio::test]
    async fn test_list_records() {
        let pool = setup_db().await;
        let repo = ExtractionRepository::new(pool);

        let r1 = ExtractionRecord::new(Uuid::new_v4(), 5)
            .mark_success("skill1".to_string(), None);
        let r2 = ExtractionRecord::new(Uuid::new_v4(), 8)
            .mark_failed("error".to_string());

        repo.save_record(&r1).await.unwrap();
        repo.save_record(&r2).await.unwrap();

        let all = repo.list_records(None, 10).await.unwrap();
        assert_eq!(all.len(), 2);

        let success = repo.list_records(Some(ExtractionStatus::Success), 10).await.unwrap();
        assert_eq!(success.len(), 1);

        let failed = repo.list_records(Some(ExtractionStatus::Failed), 10).await.unwrap();
        assert_eq!(failed.len(), 1);
    }

    #[tokio::test]
    async fn test_stats() {
        let pool = setup_db().await;
        let repo = ExtractionRepository::new(pool);

        let r1 = ExtractionRecord::new(Uuid::new_v4(), 5)
            .mark_success("s1".to_string(), None);
        let r2 = ExtractionRecord::new(Uuid::new_v4(), 8)
            .mark_success("s2".to_string(), None);
        let r3 = ExtractionRecord::new(Uuid::new_v4(), 3)
            .mark_failed("err".to_string());

        repo.save_record(&r1).await.unwrap();
        repo.save_record(&r2).await.unwrap();
        repo.save_record(&r3).await.unwrap();

        let stats = repo.stats().await.unwrap();
        assert_eq!(stats.total_extractions, 3);
        assert_eq!(stats.successful, 2);
        assert_eq!(stats.failed, 1);
        assert_eq!(stats.unique_skills, 2);
    }

    #[tokio::test]
    async fn test_get_not_found() {
        let pool = setup_db().await;
        let repo = ExtractionRepository::new(pool);

        let result = repo.get_record(Uuid::new_v4()).await;
        assert!(matches!(result, Err(ExtractionError::NotFound(_))));
    }
}
