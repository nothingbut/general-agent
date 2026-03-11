//! Message Repository SQLite 实现

use agent_core::{
    models::{Message, MessageRole},
    traits::MessageRepository,
    Result as CoreResult,
};
use async_trait::async_trait;
use sqlx::{SqlitePool, Row};
use uuid::Uuid;

/// SQLite Message Repository 实现
pub struct SqliteMessageRepository {
    pool: SqlitePool,
}

impl SqliteMessageRepository {
    /// 创建新的 Message Repository
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }
}

#[async_trait]
impl MessageRepository for SqliteMessageRepository {
    async fn create(&self, message: Message) -> CoreResult<Message> {
        let metadata_json = message
            .metadata
            .as_ref()
            .map(|m| serde_json::to_string(m))
            .transpose()
            .map_err(|e| agent_core::Error::Serde(e))?;

        sqlx::query(
            "INSERT INTO messages (id, session_id, role, content, created_at, metadata)
             VALUES (?, ?, ?, ?, ?, ?)"
        )
        .bind(message.id.to_string())
        .bind(message.session_id.to_string())
        .bind(message.role.to_string())
        .bind(message.content.clone())
        .bind(message.created_at.to_rfc3339())
        .bind(metadata_json)
        .execute(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        Ok(message)
    }

    async fn create_batch(&self, messages: Vec<Message>) -> CoreResult<Vec<Message>> {
        let mut tx = self
            .pool
            .begin()
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        for message in &messages {
            let metadata_json = message
                .metadata
                .as_ref()
                .map(|m| serde_json::to_string(m))
                .transpose()
                .map_err(|e| agent_core::Error::Serde(e))?;

            sqlx::query(
                "INSERT INTO messages (id, session_id, role, content, created_at, metadata)
                 VALUES (?, ?, ?, ?, ?, ?)"
            )
            .bind(message.id.to_string())
            .bind(message.session_id.to_string())
            .bind(message.role.to_string())
            .bind(message.content.clone())
            .bind(message.created_at.to_rfc3339())
            .bind(metadata_json)
            .execute(&mut *tx)
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;
        }

        tx.commit()
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        Ok(messages)
    }

    async fn find_by_id(&self, id: Uuid) -> CoreResult<Option<Message>> {
        let id_str = id.to_string();

        let row = sqlx::query(
            "SELECT id, session_id, role, content, created_at, metadata
             FROM messages
             WHERE id = ?"
        )
        .bind(id_str)
        .fetch_optional(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        match row {
            Some(r) => {
                let role_str: String = r.try_get("role")
                    .map_err(|e| agent_core::Error::Database(e.to_string()))?;
                let role = match role_str.as_str() {
                    "user" => MessageRole::User,
                    "assistant" => MessageRole::Assistant,
                    "system" => MessageRole::System,
                    _ => {
                        return Err(agent_core::Error::InvalidInput(format!(
                            "Invalid message role: {}",
                            role_str
                        )))
                    }
                };

                let metadata_str: Option<String> = r.try_get("metadata")
                    .map_err(|e| agent_core::Error::Database(e.to_string()))?;
                let metadata = metadata_str
                    .as_ref()
                    .map(|m| serde_json::from_str(m))
                    .transpose()
                    .map_err(|e| agent_core::Error::Serde(e))?;

                let message = Message {
                    id: Uuid::parse_str(
                        &r.try_get::<String, _>("id")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                    session_id: Uuid::parse_str(
                        &r.try_get::<String, _>("session_id")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    ).map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                    role,
                    content: r.try_get("content")
                        .map_err(|e| agent_core::Error::Database(e.to_string()))?,
                    created_at: chrono::DateTime::parse_from_rfc3339(
                        &r.try_get::<String, _>("created_at")
                            .map_err(|e| agent_core::Error::Database(e.to_string()))?
                    )
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                    metadata,
                };

                Ok(Some(message))
            }
            None => Ok(None),
        }
    }

    async fn list_by_session(
        &self,
        session_id: Uuid,
        limit: Option<u32>,
    ) -> CoreResult<Vec<Message>> {
        let session_id_str = session_id.to_string();

        let rows = if let Some(lim) = limit {
            sqlx::query(
                "SELECT id, session_id, role, content, created_at, metadata
                 FROM messages
                 WHERE session_id = ?
                 ORDER BY created_at ASC
                 LIMIT ?"
            )
            .bind(session_id_str)
            .bind(lim)
            .fetch_all(&self.pool)
            .await
        } else {
            sqlx::query(
                "SELECT id, session_id, role, content, created_at, metadata
                 FROM messages
                 WHERE session_id = ?
                 ORDER BY created_at ASC"
            )
            .bind(session_id_str)
            .fetch_all(&self.pool)
            .await
        }
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        parse_messages(rows)
    }

    async fn get_recent(&self, session_id: Uuid, limit: u32) -> CoreResult<Vec<Message>> {
        let session_id_str = session_id.to_string();

        let rows = sqlx::query(
            "SELECT id, session_id, role, content, created_at, metadata
             FROM messages
             WHERE session_id = ?
             ORDER BY created_at DESC
             LIMIT ?"
        )
        .bind(session_id_str)
        .bind(limit)
        .fetch_all(&self.pool)
        .await
        .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        let mut messages = parse_messages(rows)?;
        messages.reverse(); // 反转为正序
        Ok(messages)
    }

    async fn delete_by_session(&self, session_id: Uuid) -> CoreResult<()> {
        let session_id_str = session_id.to_string();

        sqlx::query("DELETE FROM messages WHERE session_id = ?")
            .bind(session_id_str)
            .execute(&self.pool)
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        Ok(())
    }

    async fn count_by_session(&self, session_id: Uuid) -> CoreResult<u64> {
        let session_id_str = session_id.to_string();

        let row = sqlx::query("SELECT COUNT(*) as count FROM messages WHERE session_id = ?")
            .bind(session_id_str)
            .fetch_one(&self.pool)
            .await
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;

        let count: i64 = row.try_get("count")
            .map_err(|e| agent_core::Error::Database(e.to_string()))?;
        Ok(count as u64)
    }
}

// 辅助函数：解析消息行
fn parse_messages(
    rows: Vec<sqlx::sqlite::SqliteRow>,
) -> CoreResult<Vec<Message>> {
    let messages: Result<Vec<Message>, agent_core::Error> = rows
        .into_iter()
        .map(|r| {
            let role_str: String = r.get("role");
            let role = match role_str.as_str() {
                "user" => MessageRole::User,
                "assistant" => MessageRole::Assistant,
                "system" => MessageRole::System,
                _ => {
                    return Err(agent_core::Error::InvalidInput(format!(
                        "Invalid message role: {}",
                        role_str
                    )))
                }
            };

            let metadata_str: Option<String> = r.get("metadata");
            let metadata = metadata_str
                .as_ref()
                .map(|m| serde_json::from_str(m))
                .transpose()
                .map_err(|e| agent_core::Error::Serde(e))?;

            let id_str: String = r.get("id");
            let session_id_str: String = r.get("session_id");
            let content: String = r.get("content");
            let created_at_str: String = r.get("created_at");

            Ok(Message {
                id: Uuid::parse_str(&id_str)
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                session_id: Uuid::parse_str(&session_id_str)
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?,
                role,
                content,
                created_at: chrono::DateTime::parse_from_rfc3339(&created_at_str)
                    .map_err(|e| agent_core::Error::InvalidInput(e.to_string()))?
                    .with_timezone(&chrono::Utc),
                metadata,
            })
        })
        .collect();

    messages
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::Database;
    use agent_core::models::Session;
    use agent_core::traits::SessionRepository;
    use crate::repository::SqliteSessionRepository;

    async fn setup() -> (Database, SqliteMessageRepository, SqliteSessionRepository) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();
        let msg_repo = SqliteMessageRepository::new(db.pool().clone());
        let sess_repo = SqliteSessionRepository::new(db.pool().clone());
        (db, msg_repo, sess_repo)
    }

    #[tokio::test]
    async fn test_create_message() {
        let (_db, repo, sess_repo) = setup().await;

        // 先创建会话
        let session = Session::new(Some("Test".to_string()));
        sess_repo.create(session.clone()).await.unwrap();

        // 创建消息
        let message = Message::new(session.id, MessageRole::User, "Hello".to_string());
        let created = repo.create(message.clone()).await.unwrap();

        assert_eq!(created.id, message.id);
        assert_eq!(created.content, "Hello");
    }

    #[tokio::test]
    async fn test_create_batch() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        let messages = vec![
            Message::new(session.id, MessageRole::User, "Message 1".to_string()),
            Message::new(session.id, MessageRole::Assistant, "Message 2".to_string()),
            Message::new(session.id, MessageRole::User, "Message 3".to_string()),
        ];

        let created = repo.create_batch(messages.clone()).await.unwrap();
        assert_eq!(created.len(), 3);

        let count = repo.count_by_session(session.id).await.unwrap();
        assert_eq!(count, 3);
    }

    #[tokio::test]
    async fn test_find_by_id() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        let message = Message::new(session.id, MessageRole::User, "Test".to_string());
        repo.create(message.clone()).await.unwrap();

        let found = repo.find_by_id(message.id).await.unwrap();
        assert!(found.is_some());
        assert_eq!(found.unwrap().content, "Test");
    }

    #[tokio::test]
    async fn test_list_by_session() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        // 创建 3 条消息
        for i in 1..=3 {
            let msg = Message::new(
                session.id,
                MessageRole::User,
                format!("Message {}", i),
            );
            repo.create(msg).await.unwrap();
        }

        let messages = repo.list_by_session(session.id, None).await.unwrap();
        assert_eq!(messages.len(), 3);
        assert_eq!(messages[0].content, "Message 1");
        assert_eq!(messages[2].content, "Message 3");
    }

    #[tokio::test]
    async fn test_list_with_limit() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        for i in 1..=5 {
            let msg = Message::new(
                session.id,
                MessageRole::User,
                format!("Message {}", i),
            );
            repo.create(msg).await.unwrap();
        }

        let messages = repo.list_by_session(session.id, Some(3)).await.unwrap();
        assert_eq!(messages.len(), 3);
    }

    #[tokio::test]
    async fn test_get_recent() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        for i in 1..=5 {
            let msg = Message::new(
                session.id,
                MessageRole::User,
                format!("Message {}", i),
            );
            repo.create(msg).await.unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;
        }

        let messages = repo.get_recent(session.id, 3).await.unwrap();
        assert_eq!(messages.len(), 3);
        // 应该返回最新的 3 条，但按正序排列
        assert_eq!(messages[0].content, "Message 3");
        assert_eq!(messages[2].content, "Message 5");
    }

    #[tokio::test]
    async fn test_delete_by_session() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        for i in 1..=3 {
            let msg = Message::new(
                session.id,
                MessageRole::User,
                format!("Message {}", i),
            );
            repo.create(msg).await.unwrap();
        }

        assert_eq!(repo.count_by_session(session.id).await.unwrap(), 3);

        repo.delete_by_session(session.id).await.unwrap();

        assert_eq!(repo.count_by_session(session.id).await.unwrap(), 0);
    }

    #[tokio::test]
    async fn test_count_by_session() {
        let (_db, repo, sess_repo) = setup().await;

        let session = Session::new(None);
        sess_repo.create(session.clone()).await.unwrap();

        assert_eq!(repo.count_by_session(session.id).await.unwrap(), 0);

        for _ in 1..=7 {
            let msg = Message::new(session.id, MessageRole::User, "Test".to_string());
            repo.create(msg).await.unwrap();
        }

        assert_eq!(repo.count_by_session(session.id).await.unwrap(), 7);
    }
}
