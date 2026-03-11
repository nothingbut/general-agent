//! 数据库连接管理

use crate::error::Result;
use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePoolOptions};
use sqlx::SqlitePool;
use std::path::Path;
use std::str::FromStr;
use tracing::info;

/// 数据库连接管理器
///
/// 负责创建和管理 SQLite 连接池
#[derive(Clone)]
pub struct Database {
    pool: SqlitePool,
}

impl Database {
    /// 创建新的数据库连接
    ///
    /// # Arguments
    ///
    /// * `database_url` - 数据库 URL (如 "sqlite://data.db" 或 "sqlite::memory:")
    ///
    /// # Examples
    ///
    /// ```no_run
    /// use agent_storage::Database;
    ///
    /// #[tokio::main]
    /// async fn main() -> Result<(), Box<dyn std::error::Error>> {
    ///     let db = Database::new("sqlite::memory:").await?;
    ///     Ok(())
    /// }
    /// ```
    pub async fn new(database_url: &str) -> Result<Self> {
        info!("Connecting to database: {}", database_url);

        let options = SqliteConnectOptions::from_str(database_url)?
            .create_if_missing(true)
            .journal_mode(SqliteJournalMode::Wal)
            .busy_timeout(std::time::Duration::from_secs(30));

        let pool = SqlitePoolOptions::new()
            .max_connections(10)
            .min_connections(1)
            .acquire_timeout(std::time::Duration::from_secs(10))
            .connect_with(options)
            .await?;

        info!("Database connected successfully");

        Ok(Self { pool })
    }

    /// 从文件路径创建数据库连接
    ///
    /// # Arguments
    ///
    /// * `path` - 数据库文件路径
    pub async fn from_path<P: AsRef<Path>>(path: P) -> Result<Self> {
        let path_str = path.as_ref().to_string_lossy();
        let url = format!("sqlite://{}", path_str);
        Self::new(&url).await
    }

    /// 创建内存数据库（用于测试）
    pub async fn in_memory() -> Result<Self> {
        Self::new("sqlite::memory:").await
    }

    /// 运行数据库迁移
    ///
    /// 执行 migrations 目录中的所有 SQL 脚本
    pub async fn migrate(&self) -> Result<()> {
        info!("Running database migrations...");
        sqlx::migrate!("./migrations").run(&self.pool).await?;
        info!("Database migrations completed");
        Ok(())
    }

    /// 获取连接池引用
    pub fn pool(&self) -> &SqlitePool {
        &self.pool
    }

    /// 关闭数据库连接
    pub async fn close(&self) {
        info!("Closing database connection...");
        self.pool.close().await;
        info!("Database connection closed");
    }

    /// 检查数据库健康状态
    pub async fn health_check(&self) -> Result<()> {
        sqlx::query("SELECT 1").execute(&self.pool).await?;
        Ok(())
    }

    /// 获取数据库统计信息
    pub async fn stats(&self) -> DatabaseStats {
        DatabaseStats {
            connections: self.pool.size() as u32,
            idle_connections: self.pool.num_idle(),
        }
    }
}

/// 数据库统计信息
#[derive(Debug, Clone)]
pub struct DatabaseStats {
    /// 总连接数
    pub connections: u32,
    /// 空闲连接数
    pub idle_connections: usize,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_database_creation() {
        let db = Database::in_memory().await.unwrap();
        assert!(db.health_check().await.is_ok());
    }

    #[tokio::test]
    async fn test_database_migration() {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        // 验证表已创建
        let result: (i32,) = sqlx::query_as("SELECT COUNT(*) FROM sqlite_master WHERE type='table'")
            .fetch_one(db.pool())
            .await
            .unwrap();

        assert!(result.0 >= 2); // 至少有 sessions 和 messages 表
    }

    #[tokio::test]
    async fn test_database_stats() {
        let db = Database::in_memory().await.unwrap();
        let stats = db.stats().await;

        assert!(stats.connections > 0);
    }
}
