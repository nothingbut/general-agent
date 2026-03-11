//! 会话管理器
//!
//! 负责管理会话的生命周期和消息历史

use agent_core::{
    error::Result,
    models::{Message, Session},
    traits::{MessageRepository, SessionRepository},
};
use std::sync::Arc;
use tracing::{debug, info};
use uuid::Uuid;

/// 会话管理器
///
/// 集成 SessionRepository 和 MessageRepository，
/// 提供统一的会话和消息管理接口
pub struct SessionManager {
    session_repo: Arc<dyn SessionRepository>,
    message_repo: Arc<dyn MessageRepository>,
}

impl SessionManager {
    /// 创建新的会话管理器
    ///
    /// # Arguments
    ///
    /// * `session_repo` - 会话仓库
    /// * `message_repo` - 消息仓库
    pub fn new(
        session_repo: Arc<dyn SessionRepository>,
        message_repo: Arc<dyn MessageRepository>,
    ) -> Self {
        Self {
            session_repo,
            message_repo,
        }
    }

    /// 创建新会话
    ///
    /// # Arguments
    ///
    /// * `title` - 会话标题（可选）
    ///
    /// # Returns
    ///
    /// 新创建的会话
    pub async fn create_session(&self, title: Option<String>) -> Result<Session> {
        info!("Creating new session with title: {:?}", title);

        let session = Session::new(title);
        let created = self.session_repo.create(session).await?;

        debug!("Session created with id: {}", created.id);

        Ok(created)
    }

    /// 加载会话
    ///
    /// # Arguments
    ///
    /// * `id` - 会话 ID
    ///
    /// # Returns
    ///
    /// 会话对象，如果不存在则返回错误
    pub async fn load_session(&self, id: Uuid) -> Result<Session> {
        debug!("Loading session: {}", id);

        let session = self
            .session_repo
            .find_by_id(id)
            .await?
            .ok_or_else(|| agent_core::Error::SessionNotFound(id.to_string()))?;

        debug!("Session loaded: {}", session.id);

        Ok(session)
    }

    /// 添加消息到会话
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `message` - 消息对象
    ///
    /// # Returns
    ///
    /// 创建的消息
    pub async fn add_message(&self, session_id: Uuid, message: Message) -> Result<Message> {
        // 验证会话存在
        self.load_session(session_id).await?;

        debug!("Adding message to session: {}", session_id);

        let created = self.message_repo.create(message).await?;

        debug!("Message added with id: {}", created.id);

        Ok(created)
    }

    /// 批量添加消息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `messages` - 消息列表
    ///
    /// # Returns
    ///
    /// 创建的消息列表
    pub async fn add_messages(&self, session_id: Uuid, messages: Vec<Message>) -> Result<Vec<Message>> {
        // 验证会话存在
        self.load_session(session_id).await?;

        debug!("Adding {} messages to session: {}", messages.len(), session_id);

        let created = self.message_repo.create_batch(messages).await?;

        debug!("Messages added: {} items", created.len());

        Ok(created)
    }

    /// 获取会话的消息历史
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `limit` - 返回的最大消息数（可选）
    ///
    /// # Returns
    ///
    /// 消息列表（按时间正序）
    pub async fn get_messages(&self, session_id: Uuid, limit: Option<u32>) -> Result<Vec<Message>> {
        debug!("Getting messages for session: {}, limit: {:?}", session_id, limit);

        let messages = self.message_repo.list_by_session(session_id, limit).await?;

        debug!("Retrieved {} messages", messages.len());

        Ok(messages)
    }

    /// 获取最近的消息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `limit` - 返回的消息数
    ///
    /// # Returns
    ///
    /// 最近的消息列表（按时间正序）
    pub async fn get_recent_messages(&self, session_id: Uuid, limit: u32) -> Result<Vec<Message>> {
        debug!("Getting {} recent messages for session: {}", limit, session_id);

        let messages = self.message_repo.get_recent(session_id, limit).await?;

        debug!("Retrieved {} recent messages", messages.len());

        Ok(messages)
    }

    /// 统计会话的消息数
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    ///
    /// # Returns
    ///
    /// 消息总数
    pub async fn count_messages(&self, session_id: Uuid) -> Result<u64> {
        debug!("Counting messages for session: {}", session_id);

        let count = self.message_repo.count_by_session(session_id).await?;

        debug!("Session {} has {} messages", session_id, count);

        Ok(count)
    }

    /// 更新会话
    ///
    /// # Arguments
    ///
    /// * `session` - 会话对象
    ///
    /// # Returns
    ///
    /// 更新后的会话
    pub async fn update_session(&self, session: Session) -> Result<Session> {
        debug!("Updating session: {}", session.id);

        let updated = self.session_repo.update(session).await?;

        debug!("Session updated: {}", updated.id);

        Ok(updated)
    }

    /// 更新会话标题
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    /// * `title` - 新标题
    ///
    /// # Returns
    ///
    /// 更新后的会话
    pub async fn update_session_title(&self, session_id: Uuid, title: String) -> Result<Session> {
        debug!("Updating session title: {} -> {}", session_id, title);

        let mut session = self.load_session(session_id).await?;
        session.update_title(title);

        let updated = self.session_repo.update(session).await?;

        debug!("Session title updated: {}", updated.id);

        Ok(updated)
    }

    /// 删除会话及其所有消息
    ///
    /// # Arguments
    ///
    /// * `id` - 会话 ID
    pub async fn delete_session(&self, id: Uuid) -> Result<()> {
        info!("Deleting session: {}", id);

        // 先删除所有消息
        self.message_repo.delete_by_session(id).await?;

        // 再删除会话
        self.session_repo.delete(id).await?;

        info!("Session deleted: {}", id);

        Ok(())
    }

    /// 列出所有会话
    ///
    /// # Arguments
    ///
    /// * `limit` - 返回的最大会话数
    /// * `offset` - 偏移量
    ///
    /// # Returns
    ///
    /// 会话列表（按更新时间倒序）
    pub async fn list_sessions(&self, limit: u32, offset: u32) -> Result<Vec<Session>> {
        debug!("Listing sessions: limit={}, offset={}", limit, offset);

        let sessions = self.session_repo.list(limit, offset).await?;

        debug!("Retrieved {} sessions", sessions.len());

        Ok(sessions)
    }

    /// 搜索会话
    ///
    /// # Arguments
    ///
    /// * `query` - 搜索关键词
    /// * `limit` - 返回的最大会话数
    ///
    /// # Returns
    ///
    /// 匹配的会话列表
    pub async fn search_sessions(&self, query: &str, limit: u32) -> Result<Vec<Session>> {
        debug!("Searching sessions: query='{}', limit={}", query, limit);

        let sessions = self.session_repo.search(query, limit).await?;

        debug!("Found {} matching sessions", sessions.len());

        Ok(sessions)
    }

    /// 统计会话总数
    ///
    /// # Returns
    ///
    /// 会话总数
    pub async fn count_sessions(&self) -> Result<u64> {
        debug!("Counting sessions");

        let count = self.session_repo.count().await?;

        debug!("Total sessions: {}", count);

        Ok(count)
    }

    /// 获取会话统计信息
    ///
    /// # Arguments
    ///
    /// * `session_id` - 会话 ID
    ///
    /// # Returns
    ///
    /// (会话对象, 消息总数)
    pub async fn get_session_stats(&self, session_id: Uuid) -> Result<(Session, u64)> {
        debug!("Getting stats for session: {}", session_id);

        let session = self.load_session(session_id).await?;
        let message_count = self.count_messages(session_id).await?;

        debug!("Session {} has {} messages", session_id, message_count);

        Ok((session, message_count))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::models::MessageRole;
    use agent_storage::{repository::SqliteSessionRepository, repository::SqliteMessageRepository, Database};

    async fn setup() -> (Database, SessionManager) {
        let db = Database::in_memory().await.unwrap();
        db.migrate().await.unwrap();

        let session_repo = Arc::new(SqliteSessionRepository::new(db.pool().clone()));
        let message_repo = Arc::new(SqliteMessageRepository::new(db.pool().clone()));

        let manager = SessionManager::new(session_repo, message_repo);

        (db, manager)
    }

    #[tokio::test]
    async fn test_create_session() {
        let (_db, manager) = setup().await;

        let session = manager
            .create_session(Some("Test Session".to_string()))
            .await
            .unwrap();

        assert_eq!(session.title, Some("Test Session".to_string()));
    }

    #[tokio::test]
    async fn test_load_session() {
        let (_db, manager) = setup().await;

        let created = manager
            .create_session(Some("Test".to_string()))
            .await
            .unwrap();

        let loaded = manager.load_session(created.id).await.unwrap();

        assert_eq!(loaded.id, created.id);
        assert_eq!(loaded.title, created.title);
    }

    #[tokio::test]
    async fn test_load_nonexistent_session() {
        let (_db, manager) = setup().await;

        let result = manager.load_session(Uuid::new_v4()).await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn test_add_message() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        let message = Message::new(session.id, MessageRole::User, "Hello".to_string());
        let created = manager.add_message(session.id, message).await.unwrap();

        assert_eq!(created.content, "Hello");
        assert_eq!(created.session_id, session.id);
    }

    #[tokio::test]
    async fn test_add_messages_batch() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        let messages = vec![
            Message::new(session.id, MessageRole::User, "Message 1".to_string()),
            Message::new(session.id, MessageRole::Assistant, "Message 2".to_string()),
            Message::new(session.id, MessageRole::User, "Message 3".to_string()),
        ];

        let created = manager.add_messages(session.id, messages).await.unwrap();

        assert_eq!(created.len(), 3);
    }

    #[tokio::test]
    async fn test_get_messages() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        // 添加消息
        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            manager.add_message(session.id, msg).await.unwrap();
        }

        let messages = manager.get_messages(session.id, None).await.unwrap();

        assert_eq!(messages.len(), 5);
        assert_eq!(messages[0].content, "Message 1");
        assert_eq!(messages[4].content, "Message 5");
    }

    #[tokio::test]
    async fn test_get_messages_with_limit() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            manager.add_message(session.id, msg).await.unwrap();
        }

        let messages = manager.get_messages(session.id, Some(3)).await.unwrap();

        assert_eq!(messages.len(), 3);
    }

    #[tokio::test]
    async fn test_get_recent_messages() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            manager.add_message(session.id, msg).await.unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;
        }

        let messages = manager.get_recent_messages(session.id, 3).await.unwrap();

        assert_eq!(messages.len(), 3);
        assert_eq!(messages[0].content, "Message 3");
        assert_eq!(messages[2].content, "Message 5");
    }

    #[tokio::test]
    async fn test_count_messages() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        assert_eq!(manager.count_messages(session.id).await.unwrap(), 0);

        for i in 1..=7 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            manager.add_message(session.id, msg).await.unwrap();
        }

        assert_eq!(manager.count_messages(session.id).await.unwrap(), 7);
    }

    #[tokio::test]
    async fn test_update_session_title() {
        let (_db, manager) = setup().await;

        let session = manager
            .create_session(Some("Original".to_string()))
            .await
            .unwrap();

        let updated = manager
            .update_session_title(session.id, "Updated".to_string())
            .await
            .unwrap();

        assert_eq!(updated.title, Some("Updated".to_string()));

        let loaded = manager.load_session(session.id).await.unwrap();
        assert_eq!(loaded.title, Some("Updated".to_string()));
    }

    #[tokio::test]
    async fn test_delete_session() {
        let (_db, manager) = setup().await;

        let session = manager.create_session(None).await.unwrap();

        // 添加消息
        let msg = Message::new(session.id, MessageRole::User, "Test".to_string());
        manager.add_message(session.id, msg).await.unwrap();

        // 删除会话
        manager.delete_session(session.id).await.unwrap();

        // 验证会话不存在
        let result = manager.load_session(session.id).await;
        assert!(result.is_err());

        // 验证消息也被删除
        let messages = manager.get_messages(session.id, None).await.unwrap();
        assert_eq!(messages.len(), 0);
    }

    #[tokio::test]
    async fn test_list_sessions() {
        let (_db, manager) = setup().await;

        // 创建多个会话
        for i in 1..=5 {
            manager
                .create_session(Some(format!("Session {}", i)))
                .await
                .unwrap();
            tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;
        }

        let sessions = manager.list_sessions(10, 0).await.unwrap();

        assert_eq!(sessions.len(), 5);
    }

    #[tokio::test]
    async fn test_list_sessions_pagination() {
        let (_db, manager) = setup().await;

        for i in 1..=5 {
            manager
                .create_session(Some(format!("Session {}", i)))
                .await
                .unwrap();
        }

        let page1 = manager.list_sessions(2, 0).await.unwrap();
        assert_eq!(page1.len(), 2);

        let page2 = manager.list_sessions(2, 2).await.unwrap();
        assert_eq!(page2.len(), 2);

        let page3 = manager.list_sessions(2, 4).await.unwrap();
        assert_eq!(page3.len(), 1);
    }

    #[tokio::test]
    async fn test_search_sessions() {
        let (_db, manager) = setup().await;

        manager
            .create_session(Some("Apple Pie".to_string()))
            .await
            .unwrap();
        manager
            .create_session(Some("Banana Split".to_string()))
            .await
            .unwrap();
        manager
            .create_session(Some("Apple Juice".to_string()))
            .await
            .unwrap();

        let results = manager.search_sessions("Apple", 10).await.unwrap();
        assert_eq!(results.len(), 2);

        let results = manager.search_sessions("Banana", 10).await.unwrap();
        assert_eq!(results.len(), 1);
    }

    #[tokio::test]
    async fn test_count_sessions() {
        let (_db, manager) = setup().await;

        assert_eq!(manager.count_sessions().await.unwrap(), 0);

        for i in 1..=3 {
            manager
                .create_session(Some(format!("Session {}", i)))
                .await
                .unwrap();
        }

        assert_eq!(manager.count_sessions().await.unwrap(), 3);
    }

    #[tokio::test]
    async fn test_get_session_stats() {
        let (_db, manager) = setup().await;

        let session = manager
            .create_session(Some("Test".to_string()))
            .await
            .unwrap();

        for i in 1..=5 {
            let msg = Message::new(session.id, MessageRole::User, format!("Message {}", i));
            manager.add_message(session.id, msg).await.unwrap();
        }

        let (loaded_session, message_count) = manager.get_session_stats(session.id).await.unwrap();

        assert_eq!(loaded_session.id, session.id);
        assert_eq!(message_count, 5);
    }
}
