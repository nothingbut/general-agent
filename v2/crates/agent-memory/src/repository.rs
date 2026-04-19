use crate::error::{MemoryError, Result};
use crate::models::{Memory, MemoryQuery, MemoryType};
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use sqlx::{Row, SqlitePool};
use uuid::Uuid;

#[async_trait]
pub trait MemoryRepository: Send + Sync {
    async fn create(&self, memory: Memory) -> Result<Memory>;
    async fn find_by_id(&self, id: Uuid) -> Result<Option<Memory>>;
    async fn update(&self, memory: &Memory) -> Result<Memory>;
    async fn delete(&self, id: Uuid) -> Result<()>;
    async fn query(&self, query: &MemoryQuery) -> Result<Vec<Memory>>;
    async fn search(&self, keyword: &str, limit: u32) -> Result<Vec<Memory>>;
    async fn count_by_type(&self, memory_type: MemoryType) -> Result<u64>;
}

pub struct SqliteMemoryRepository {
    pool: SqlitePool,
}

impl SqliteMemoryRepository {
    pub fn new(pool: SqlitePool) -> Self {
        Self { pool }
    }

    fn row_to_memory(&self, row: &sqlx::sqlite::SqliteRow) -> Result<Memory> {
        let id_str: String = row.get("id");
        let type_str: String = row.get("memory_type");
        let content: String = row.get("content");
        let source: Option<String> = row.get("source");
        let session_id_str: Option<String> = row.get("session_id");
        let created_at_str: String = row.get("created_at");
        let updated_at_str: String = row.get("updated_at");
        let metadata_str: Option<String> = row.get("metadata");

        let id = Uuid::parse_str(&id_str)
            .map_err(|e| MemoryError::Database(format!("Invalid UUID: {}", e)))?;

        let memory_type = MemoryType::from_str(&type_str)
            .ok_or_else(|| MemoryError::InvalidType(type_str))?;

        let session_id = session_id_str
            .map(|s| Uuid::parse_str(&s))
            .transpose()
            .map_err(|e| MemoryError::Database(format!("Invalid session UUID: {}", e)))?;

        let created_at = DateTime::parse_from_rfc3339(&created_at_str)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| MemoryError::Database(format!("Invalid datetime: {}", e)))?;

        let updated_at = DateTime::parse_from_rfc3339(&updated_at_str)
            .map(|dt| dt.with_timezone(&Utc))
            .map_err(|e| MemoryError::Database(format!("Invalid datetime: {}", e)))?;

        let metadata = metadata_str
            .map(|s| serde_json::from_str(&s))
            .transpose()?;

        Ok(Memory {
            id,
            memory_type,
            content,
            source,
            session_id,
            created_at,
            updated_at,
            metadata,
        })
    }
}

#[async_trait]
impl MemoryRepository for SqliteMemoryRepository {
    async fn create(&self, memory: Memory) -> Result<Memory> {
        let metadata_json = memory
            .metadata
            .as_ref()
            .map(|m| serde_json::to_string(m))
            .transpose()?;

        sqlx::query(
            "INSERT INTO memories (id, memory_type, content, source, session_id, created_at, updated_at, metadata)
             VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
        )
        .bind(memory.id.to_string())
        .bind(memory.memory_type.as_str())
        .bind(&memory.content)
        .bind(&memory.source)
        .bind(memory.session_id.map(|id| id.to_string()))
        .bind(memory.created_at.to_rfc3339())
        .bind(memory.updated_at.to_rfc3339())
        .bind(metadata_json)
        .execute(&self.pool)
        .await?;

        Ok(memory)
    }

    async fn find_by_id(&self, id: Uuid) -> Result<Option<Memory>> {
        let row = sqlx::query("SELECT * FROM memories WHERE id = ?")
            .bind(id.to_string())
            .fetch_optional(&self.pool)
            .await?;

        match row {
            Some(row) => Ok(Some(self.row_to_memory(&row)?)),
            None => Ok(None),
        }
    }

    async fn update(&self, memory: &Memory) -> Result<Memory> {
        let metadata_json = memory
            .metadata
            .as_ref()
            .map(|m| serde_json::to_string(m))
            .transpose()?;

        let now = Utc::now();
        sqlx::query(
            "UPDATE memories SET content = ?, source = ?, memory_type = ?, metadata = ?, updated_at = ?
             WHERE id = ?",
        )
        .bind(&memory.content)
        .bind(&memory.source)
        .bind(memory.memory_type.as_str())
        .bind(metadata_json)
        .bind(now.to_rfc3339())
        .bind(memory.id.to_string())
        .execute(&self.pool)
        .await?;

        let mut updated = memory.clone();
        updated.updated_at = now;
        Ok(updated)
    }

    async fn delete(&self, id: Uuid) -> Result<()> {
        let result = sqlx::query("DELETE FROM memories WHERE id = ?")
            .bind(id.to_string())
            .execute(&self.pool)
            .await?;

        if result.rows_affected() == 0 {
            return Err(MemoryError::NotFound(id.to_string()));
        }

        Ok(())
    }

    async fn query(&self, query: &MemoryQuery) -> Result<Vec<Memory>> {
        let mut sql = String::from("SELECT * FROM memories WHERE 1=1");
        let mut bindings: Vec<String> = Vec::new();

        if let Some(ref mt) = query.memory_type {
            sql.push_str(" AND memory_type = ?");
            bindings.push(mt.as_str().to_string());
        }

        if let Some(ref sid) = query.session_id {
            sql.push_str(" AND session_id = ?");
            bindings.push(sid.to_string());
        }

        if let Some(ref keyword) = query.keyword {
            sql.push_str(" AND content LIKE ?");
            bindings.push(format!("%{}%", keyword));
        }

        sql.push_str(" ORDER BY updated_at DESC");

        let limit = query.limit.unwrap_or(50);
        let offset = query.offset.unwrap_or(0);
        sql.push_str(&format!(" LIMIT {} OFFSET {}", limit, offset));

        let mut q = sqlx::query(&sql);
        for b in &bindings {
            q = q.bind(b);
        }

        let rows = q.fetch_all(&self.pool).await?;
        rows.iter().map(|row| self.row_to_memory(row)).collect()
    }

    async fn search(&self, keyword: &str, limit: u32) -> Result<Vec<Memory>> {
        let rows = sqlx::query(
            "SELECT m.* FROM memories m
             JOIN memories_fts fts ON m.rowid = fts.rowid
             WHERE memories_fts MATCH ?
             ORDER BY rank
             LIMIT ?",
        )
        .bind(keyword)
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        rows.iter().map(|row| self.row_to_memory(row)).collect()
    }

    async fn count_by_type(&self, memory_type: MemoryType) -> Result<u64> {
        let row = sqlx::query("SELECT COUNT(*) as count FROM memories WHERE memory_type = ?")
            .bind(memory_type.as_str())
            .fetch_one(&self.pool)
            .await?;

        let count: i64 = row.get("count");
        Ok(count as u64)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn setup() -> (SqlitePool, SqliteMemoryRepository) {
        let pool = SqlitePool::connect("sqlite::memory:")
            .await
            .unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        let repo = SqliteMemoryRepository::new(pool.clone());
        (pool, repo)
    }

    #[tokio::test]
    async fn test_create_and_find() {
        let (_pool, repo) = setup().await;

        let memory = Memory::new(MemoryType::User, "User is a data scientist".to_string())
            .with_source("conversation".to_string());

        let created = repo.create(memory.clone()).await.unwrap();
        assert_eq!(created.id, memory.id);

        let found = repo.find_by_id(created.id).await.unwrap().unwrap();
        assert_eq!(found.content, "User is a data scientist");
        assert_eq!(found.memory_type, MemoryType::User);
        assert_eq!(found.source.as_deref(), Some("conversation"));
    }

    #[tokio::test]
    async fn test_find_nonexistent() {
        let (_pool, repo) = setup().await;
        let result = repo.find_by_id(Uuid::new_v4()).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_update() {
        let (_pool, repo) = setup().await;

        let memory = Memory::new(MemoryType::Feedback, "Don't use mocks".to_string());
        let created = repo.create(memory).await.unwrap();

        let mut to_update = created.clone();
        to_update.content = "Use real DB in tests, not mocks".to_string();

        let updated = repo.update(&to_update).await.unwrap();
        assert_eq!(updated.content, "Use real DB in tests, not mocks");
        assert!(updated.updated_at >= created.updated_at);

        let found = repo.find_by_id(created.id).await.unwrap().unwrap();
        assert_eq!(found.content, "Use real DB in tests, not mocks");
    }

    #[tokio::test]
    async fn test_delete() {
        let (_pool, repo) = setup().await;

        let memory = Memory::new(MemoryType::Project, "Merge freeze Thursday".to_string());
        let created = repo.create(memory).await.unwrap();

        repo.delete(created.id).await.unwrap();
        let found = repo.find_by_id(created.id).await.unwrap();
        assert!(found.is_none());
    }

    #[tokio::test]
    async fn test_delete_nonexistent() {
        let (_pool, repo) = setup().await;
        let result = repo.delete(Uuid::new_v4()).await;
        assert!(result.is_err());
    }

    #[tokio::test]
    async fn test_query_by_type() {
        let (_pool, repo) = setup().await;

        repo.create(Memory::new(MemoryType::User, "user info".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::User, "user pref".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "feedback".to_string())).await.unwrap();

        let results = repo.query(&MemoryQuery::by_type(MemoryType::User)).await.unwrap();
        assert_eq!(results.len(), 2);
        assert!(results.iter().all(|m| m.memory_type == MemoryType::User));
    }

    #[tokio::test]
    async fn test_query_by_session() {
        let (_pool, repo) = setup().await;

        let sid = Uuid::new_v4();
        repo.create(Memory::new(MemoryType::Project, "p1".to_string()).with_session(sid)).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "f1".to_string()).with_session(sid)).await.unwrap();
        repo.create(Memory::new(MemoryType::User, "u1".to_string())).await.unwrap();

        let results = repo.query(&MemoryQuery::by_session(sid)).await.unwrap();
        assert_eq!(results.len(), 2);
    }

    #[tokio::test]
    async fn test_query_with_keyword() {
        let (_pool, repo) = setup().await;

        repo.create(Memory::new(MemoryType::User, "user likes Rust".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::User, "user likes Python".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "avoid Rust unsafe".to_string())).await.unwrap();

        let results = repo
            .query(&MemoryQuery::default().with_keyword("Rust".to_string()))
            .await
            .unwrap();
        assert_eq!(results.len(), 2);
    }

    #[tokio::test]
    async fn test_query_with_limit_offset() {
        let (_pool, repo) = setup().await;

        for i in 0..10 {
            repo.create(Memory::new(MemoryType::Knowledge, format!("fact {}", i))).await.unwrap();
        }

        let page1 = repo
            .query(&MemoryQuery::by_type(MemoryType::Knowledge).with_limit(3))
            .await
            .unwrap();
        assert_eq!(page1.len(), 3);

        let page2 = repo
            .query(&MemoryQuery::by_type(MemoryType::Knowledge).with_limit(3).with_offset(3))
            .await
            .unwrap();
        assert_eq!(page2.len(), 3);
        assert_ne!(page1[0].id, page2[0].id);
    }

    #[tokio::test]
    async fn test_fts_search() {
        let (_pool, repo) = setup().await;

        repo.create(Memory::new(MemoryType::User, "prefers dark mode in editor".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "avoid using global state".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Project, "dark theme redesign planned".to_string())).await.unwrap();

        let results = repo.search("dark", 10).await.unwrap();
        assert_eq!(results.len(), 2);
    }

    #[tokio::test]
    async fn test_count_by_type() {
        let (_pool, repo) = setup().await;

        repo.create(Memory::new(MemoryType::User, "u1".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::User, "u2".to_string())).await.unwrap();
        repo.create(Memory::new(MemoryType::Feedback, "f1".to_string())).await.unwrap();

        assert_eq!(repo.count_by_type(MemoryType::User).await.unwrap(), 2);
        assert_eq!(repo.count_by_type(MemoryType::Feedback).await.unwrap(), 1);
        assert_eq!(repo.count_by_type(MemoryType::Project).await.unwrap(), 0);
    }

    #[tokio::test]
    async fn test_create_with_metadata() {
        let (_pool, repo) = setup().await;

        let memory = Memory::new(MemoryType::Reference, "Linear project INGEST".to_string())
            .with_metadata(serde_json::json!({
                "url": "https://linear.app/project/INGEST",
                "priority": "high"
            }));

        let created = repo.create(memory).await.unwrap();
        let found = repo.find_by_id(created.id).await.unwrap().unwrap();

        let meta = found.metadata.unwrap();
        assert_eq!(meta["url"], "https://linear.app/project/INGEST");
        assert_eq!(meta["priority"], "high");
    }
}
