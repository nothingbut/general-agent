use thiserror::Error;

#[derive(Debug, Error)]
pub enum TaskError {
    #[error("任务未找到: {0}")]
    NotFound(String),

    #[error("无效的 Cron 表达式: {0}")]
    InvalidCron(String),

    #[error("无效的调度表达式: {0}")]
    InvalidSchedule(String),

    #[error("任务已暂停")]
    TaskPaused,

    #[error("任务执行超时")]
    Timeout,

    #[error("任务执行失败: {0}")]
    ExecutionFailed(String),

    #[error("数据库错误: {0}")]
    DatabaseError(#[from] sqlx::Error),

    #[error("序列化错误: {0}")]
    SerializationError(String),

    #[error("解析错误: {0}")]
    ParseError(String),
}

pub type Result<T> = std::result::Result<T, TaskError>;
