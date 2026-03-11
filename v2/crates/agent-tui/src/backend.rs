//! 后台任务管理

use uuid::Uuid;

/// 后台更新（Backend → UI）
#[derive(Debug, Clone)]
pub enum BackendUpdate {
    /// 段落完成（逐段显示）
    ParagraphComplete {
        session_id: Uuid,
        paragraph: String,
    },

    /// 响应完成
    ResponseComplete {
        session_id: Uuid,
    },

    /// 错误
    Error {
        session_id: Uuid,
        error: String,
    },

    /// 会话列表更新
    SessionsLoaded {
        sessions: Vec<SessionInfo>,
    },

    /// 消息加载完成
    MessagesLoaded {
        session_id: Uuid,
        messages: Vec<MessageInfo>,
    },
}

/// 后台命令（UI → Backend）
#[derive(Debug, Clone)]
pub enum BackendCommand {
    /// 发送消息
    SendMessage {
        session_id: Uuid,
        content: String,
    },

    /// 创建会话
    CreateSession {
        title: Option<String>,
    },

    /// 删除会话
    DeleteSession {
        session_id: Uuid,
    },

    /// 加载会话列表
    LoadSessions,

    /// 加载消息
    LoadMessages {
        session_id: Uuid,
    },
}

/// 会话信息
#[derive(Debug, Clone)]
pub struct SessionInfo {
    pub id: Uuid,
    pub title: Option<String>,
    pub updated_at: chrono::DateTime<chrono::Utc>,
}

/// 消息信息
#[derive(Debug, Clone)]
pub struct MessageInfo {
    pub role: String,
    pub content: String,
    pub timestamp: chrono::DateTime<chrono::Utc>,
}