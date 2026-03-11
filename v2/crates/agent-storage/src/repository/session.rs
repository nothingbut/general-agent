//! Session Repository SQLite 实现

use agent_core::{
    models::Session,
    traits::SessionRepository,
    Result as CoreResult,
};
use async_trait::async_trait;
use sqlx::{SqlitePool, Row};
use uuid::Uuid;

/// SQLite Session Repository 实现
pub struct SqliteSessionRepository {
    pool: SqlitePool,
}

impl SqliteSessionRepository {
    /// 创建新的 Session Repository
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }
}

#[async_trait]
impl SessionRepository for SqliteSessionRepository {
    async fn create(&self, session: Session) -> CoreResult<Session> {
        let context_json = serde_json::to_string(&session.context)
            .map_err(|e| agent_core::Error::Serde(e))?;

        sqlx::query(
            "INSERT INTO sessions (id, title, created_at, updated_at, context)
             VALUES (?, ?, ?, ?, ?)"
        )
        .bind(session.id.to_string())
        .bind(session.title.clone())
        .bind(session.created_at.to_rfc3339())
        .bind(session.updated_at.to_rfc3339())
        .bind(context_json)
        .execute(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        Ok(session)
    }

    async fn find_by_id(&self, id: Uuid) -> CoreResult<Option<Session>> {
        let id_str = id.to_string();

        let row = sqlx::query(
            "SELECT id, title, created_at, updated_at, context
             FROM sessions
             WHERE id = ?"
        )
        .bind(id_str)
        .fetch_optional(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        match row {
            Some(r) => {
                let session = Session {
                    id: Uuid::parse_str(
                        &r.try_get::<String, _>("id")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                    title: r.try_get("title")
                        .map_err(|e| agent_core::Error::Database(e.to_string()))?,
                    created_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("created_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    updated_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("updated_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    context: serde_json::from_str(
                        &r.try_get::<String, _>("context")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::Serde(e))?,
                };
                Ok(Some(session))
            }
            None => Ok(None),
        }
    }

    async fn list(&self, limit: u32, offset: u32) -> CoreResult<Vec<Session>> {
        let rows = sqlx::query(
            "SELECT id, title, created_at, updated_at, context
             FROM sessions
             ORDER BY updated_at DESC
             LIMIT ? OFFSET ?"
        )
        .bind(limit)
        .bind(offset)
        .fetch_all(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        let sessions: Result<Vec<Session>, agent_core::Error> = rows
            .into_iter()
            .map(|r| {
                Ok(Session {
                    id: Uuid::parse_str(
                        &r.try_get::<String, _>("id")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                    title: r.try_get("title")
                        .map_err(|e| agent_core::Error::Database(e.to_string()))?,
                    created_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("created_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    updated_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("updated_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    context: serde_json::from_str(
                        &r.try_get::<String, _>("context")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::Serde(e))?,
                })
            })
            .collect();

        sessions
    }

    async fn update(&self, session: Session) -> CoreResult<Session> {
        let context_json = serde_json::to_string(&session.context)
            .map_err(|e| agent_core::Error::Serde(e))?;

        let result = sqlx::query(
            "UPDATE sessions
             SET title = ?, updated_at = ?, context = ?
             WHERE id = ?"
        )
        .bind(session.title.clone())
        .bind(session.updated_at.to_rfc3339())
        .bind(context_json)
        .bind(session.id.to_string())
        .execute(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        if result.rows_affected() == 0 {
            return Err(agent_core::Error::SessionNotFound(session.id.to_string()));
        }

        Ok(session)
    }

    async fn delete(&self, id: Uuid) -> CoreResult<()> {
        let id_str = id.to_string();

        let result = sqlx::query("DELETE FROM sessions WHERE id = ?")
            .bind(id_str)
            .execute(&self.pool)
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        if result.rows_affected() == 0 {
            return Err(agent_core::Error::SessionNotFound(id.to_string()));
        }

        Ok(())
    }

    async fn count(&self) -> CoreResult<u64> {
        let row = sqlx::query("SELECT COUNT(*) as count FROM sessions")
            .fetch_one(&self.pool)
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        let count: i64 = row.try_get("count")
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;
        Ok(count as u64)
    }

    async fn search(&self, query: &str, limit: u32) -> CoreResult<Vec<Session>> {
        let search_pattern = format!("%{}%", query);

        let rows = sqlx::query(
            "SELECT id, title, created_at, updated_at, context
             FROM sessions
             WHERE title LIKE ?
             ORDER BY updated_at DESC
             LIMIT ?"
        )
        .bind(search_pattern)
        .bind(limit)
        .fetch_all(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        let sessions: Result<Vec<Session>, agent_core::Error> = rows
            .into_iter()
            .map(|r| {
                Ok(Session {
                    id: Uuid::parse_str(
                        &r.try_get::<String, _>("id")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                    title: r.try_get("title")
                        .map_err(|e| agent_core::Error::Database(e.to_string()))?,
                    created_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("created_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    updated_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("updated_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    context: serde_json::from_str(
                        &r.try_get::<String, _>("context")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::Serde(e))?,
                })
            })
            .collect();

        sessions
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::Database;

    async fn setup() -> (Database, SqliteSessionRepository) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();
        let repo = SqliteSessionRepository::new(db.pool().clone());
        (db, repo)
    }

    #[tokio::test]
    async fn test_create_session() {
        let (_db, repo) = setup().await;

        let session = Session::new(Some("Test Session".to_string()));
        let created = repo.create(session.clone()).await.unwrap();

        assert_eq!(created.id, session.id);
        assert_eq!(created.title, Some("Test Session".to_string()));
    }

    #[tokio::test]
    async fn test_find_by_id() {
        let (_db, repo) = setup().await;

        let session = Session::new(Some("Test".to_string()));
        repo.create(session.clone()).await.unwrap();

        let found = repo.find_by_id(session.id).await.unwrap();
        assert!(found.is_some());
        assert_eq!(found.unwrap().id, session.id);
    }

    #[tokio::test]
    async fn test_find_by_id_not_found() {
        let (_db, repo) = setup().await;

        let found = repo.find_by_id(Uuid::new_v4()).await.unwrap();
        assert!(found.is_none());
    }

    #[tokio::test]
    async fn test_list_sessions() {
        let (_db, repo) = setup().await;

        // 创建 3 个会话
        for i in 1..=3 {
            let session = Session::new(Some(format!("Session {}", i)));
            repo.create(session).await.unwrap();
        }

        let sessions = repo.list(10, 0).await.unwrap();
        assert_eq!(sessions.len(), 3);
    }

    #[tokio::test]
    async fn test_list_with_pagination() {
        let (_db, repo) = setup().await;

        // 创建 5 个会话
        for i in 1..=5 {
            let session = Session::new(Some(format!("Session {}", i)));
            repo.create(session).await.unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;
        }

        // 第一页（2 条）
        let page1 = repo.list(2, 0).await.unwrap();
        assert_eq!(page1.len(), 2);

        // 第二页（2 条）
        let page2 = repo.list(2, 2).await.unwrap();
        assert_eq!(page2.len(), 2);

        // 第三页（1 条）
        let page3 = repo.list(2, 4).await.unwrap();
        assert_eq!(page3.len(), 1);
    }

    #[tokio::test]
    async fn test_update_session() {
        let (_db, repo) = setup().await;

        let mut session = Session::new(Some("Original".to_string()));
        repo.create(session.clone()).await.unwrap();

        session.update_title("Updated".to_string());
        let updated = repo.update(session.clone()).await.unwrap();

        assert_eq!(updated.title, Some("Updated".to_string()));

        let found = repo.find_by_id(session.id).await.unwrap().unwrap();
        assert_eq!(found.title, Some("Updated".to_string()));
    }

    #[tokio::test]
    async fn test_update_nonexistent() {
        let (_db, repo) = setup().await;

        let session = Session::new(Some("Test".to_string()));
        let result = repo.update(session).await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn test_delete_session() {
        let (_db, repo) = setup().await;

        let session = Session::new(Some("Test".to_string()));
        repo.create(session.clone()).await.unwrap();

        repo.delete(session.id).await.unwrap();

        let found = repo.find_by_id(session.id).await.unwrap();
        assert!(found.is_none());
    }

    #[tokio::test]
    async fn test_delete_nonexistent() {
        let (_db, repo) = setup().await;

        let result = repo.delete(Uuid::new_v4()).await;
        assert!(result.is_err());
    }

    #[tokio::test]
    async fn test_count() {
        let (_db, repo) = setup().await;

        assert_eq!(repo.count().await.unwrap(), 0);

        for i in 1..=5 {
            let session = Session::new(Some(format!("Session {}", i)));
            repo.create(session).await.unwrap();
        }

        assert_eq!(repo.count().await.unwrap(), 5);
    }

    #[tokio::test]
    async fn test_search() {
        let (_db, repo) = setup().await;

        repo.create(Session::new(Some("Apple Pie".to_string())))
            .await
            .unwrap();
        repo.create(Session::new(Some("Banana Split".to_string())))
            .await
            .unwrap();
        repo.create(Session::new(Some("Apple Juice".to_string())))
            .await
            .unwrap();

        let results = repo.search("Apple", 10).await.unwrap();
        assert_eq!(results.len(), 2);

        let results = repo.search("Banana", 10).await.unwrap();
        assert_eq!(results.len(), 1);

        let results = repo.search("Orange", 10).await.unwrap();
        assert_eq!(results.len(), 0);
    }
}
