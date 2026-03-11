//! 仓储模式 trait 定义
//!
//! 定义数据访问层的抽象接口，遵循仓储模式

use crate::{
    error::Result,
    models::{Message, Session},
};
use async_trait::async_trait;
use uuid::Uuid;

/// 会话仓储接口
///
/// 定义会话数据的 CRUD 操作
#[async_trait]
pub trait SessionRepository: Send + Sync {
    /// 创建新会话
    ///
    /// # Arguments
    ///
    /// * `session` - 要创建的会话实体
    ///
    /// # Returns
    ///
    /// 创建成功的会话实体
    async fn create(&self, session: Session) -> Result<Session>;

    /// 根据 ID 查找会话
    ///
    /// # Arguments
    ///
    /// * `id` - 会话 ID
    ///
    /// # Returns
    ///
    /// 如果找到返回 Some(Session)，否则返回 None
    async fn find_by_id(&self, id: Uuid) -> Result<Option<Session>>;

    /// 列出会话
    ///
    /// # Arguments
    ///
    /// * `limit` - 返回的最大数量
    /// * `offset` - 跳过的数量（用于分页）
    ///
    /// # Returns
    ///
    /// 会话列表，按更新时间倒序排列
    async fn list(&self, limit: u32, offset: u32) -> Result<Vec<Session>>;

    /// 更新会话
    ///
    /// # Arguments
    ///
    /// * `session` - 要更新的会话实体
    ///
    /// # Returns
    ///
    /// 更新后的会话实体
    async fn update(&self, session: Session) -> Result<Session>;

    /// 删除会话
    ///
    /// # Arguments
    ///
    /// * `id` - 要删除的会话 ID
    async fn delete(&self, id: Uuid) -> Result<()>;

    /// 统计会话总数
    async fn count(&self) -> Result<u64>;

    /// 搜索会话
    ///
    /// # Arguments
    ///
    /// * `query` - 搜索关键词（匹配标题）
    /// * `limit` - 返回的最大数量
    ///
    /// # Returns
    ///
    /// 匹配的会话列表
    async fn search(&self, query: &str, limit: u32) -> Result<Vec<Session>>;
}

/// 消息仓储接口
///
/// 定义消息数据的 CRUD 操作
#[async_trait]
pub trait MessageRepository: Send + Sync {
    /// 创建新消息
    ///
    /// # Arguments
    ///
    /// * `message` - 要创建的消息实体
    ///
    /// # Returns
    ///
    /// 创建成功的消息实体
    async fn create(&self, message: Message) -> Result<Message>;

    /// 批量创建消息
    ///
    /// # Arguments
    ///
    /// * `messages` - 要创建的消息列表
    ///
    /// # Returns
    ///
    /// 创建成功的消息列表
    async fn create_batch(&self, messages: Vec<Message>) -> Result<Vec<Message>>;

    /// 根据 ID 查找消息
    ///
    /// # Arguments
    ///
    /// * `id` - 消息 ID
    ///
    /// # Returns
    ///
    /// 如果找到返回 Some(Message)，否则返回 None
    async fn find_by_id(&self, id: Uuid) -> Result<Option<Message>>;

    /// 列出会话的所有消息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `limit` - 返回的最大数量（None 表示不限制）
    ///
    /// # Returns
    ///
    /// 消息列表，按创建时间正序排列
    async fn list_by_session(&self, session_id: Uuid, limit: Option<u32>) -> Result<Vec<Message>>;

    /// 获取会话的最新 N 条消息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `limit` - 返回的消息数量
    ///
    /// # Returns
    ///
    /// 最新的消息列表，按创建时间倒序排列
    async fn get_recent(&self, session_id: Uuid, limit: u32) -> Result<Vec<Message>>;

    /// 删除会话的所有消息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    async fn delete_by_session(&self, session_id: Uuid) -> Result<()>;

    /// 统计会话的消息数量
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    ///
    /// # Returns
    ///
    /// 消息总数
    async fn count_by_session(&self, session_id: Uuid) -> Result<u64>;
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{MessageRole, SessionContext};
    use std::sync::Arc;

    // Mock implementation for testing
    struct MockSessionRepository {
        sessions: std::sync::Mutex<Vec<Session>>,
    }

    impl MockSessionRepository {
        fn new() -> Self {
            Self {
                sessions: std::sync::Mutex::new(Vec::new()),
            }
        }
    }

    #[async_trait]
    impl SessionRepository for MockSessionRepository {
        async fn create(&self, session: Session) -> Result<Session> {
            let mut sessions = self.sessions.lock().unwrap();
            sessions.push(session.clone());
            Ok(session)
        }

        async fn find_by_id(&self, id: Uuid) -> Result<Option<Session>> {
            let sessions = self.sessions.lock().unwrap();
            Ok(sessions.iter().find(|s| s.id == id).cloned())
        }

        async fn list(&self, limit: u32, offset: u32) -> Result<Vec<Session>> {
            let sessions = self.sessions.lock().unwrap();
            Ok(sessions
                .iter()
                .skip(offset as usize)
                .take(limit as usize)
                .cloned()
                .collect())
        }

        async fn update(&self, session: Session) -> Result<Session> {
            let mut sessions = self.sessions.lock().unwrap();
            if let Some(pos) = sessions.iter().position(|s| s.id == session.id) {
                sessions[pos] = session.clone();
            }
            Ok(session)
        }

        async fn delete(&self, id: Uuid) -> Result<()> {
            let mut sessions = self.sessions.lock().unwrap();
            sessions.retain(|s| s.id != id);
            Ok(())
        }

        async fn count(&self) -> Result<u64> {
            let sessions = self.sessions.lock().unwrap();
            Ok(sessions.len() as u64)
        }

        async fn search(&self, query: &str, limit: u32) -> Result<Vec<Session>> {
            let sessions = self.sessions.lock().unwrap();
            Ok(sessions
                .iter()
                .filter(|s| s.title.as_ref().map(|t| t.contains(query)).unwrap_or(false))
                .take(limit as usize)
                .cloned()
                .collect())
        }
    }

    #[tokio::test]
    async fn test_session_repository() {
        let repo = Arc::new(MockSessionRepository::new());

        // Test create
        let session = Session::new(Some("Test".to_string()));
        let created = repo.create(session.clone()).await.unwrap();
        assert_eq!(created.id, session.id);

        // Test find_by_id
        let found = repo.find_by_id(session.id).await.unwrap();
        assert!(found.is_some());
        assert_eq!(found.unwrap().id, session.id);

        // Test count
        let count = repo.count().await.unwrap();
        assert_eq!(count, 1);

        // Test delete
        repo.delete(session.id).await.unwrap();
        let found = repo.find_by_id(session.id).await.unwrap();
        assert!(found.is_none());
    }
}
