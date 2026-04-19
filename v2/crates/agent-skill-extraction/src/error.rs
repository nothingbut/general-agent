use thiserror::Error;

#[derive(Debug, Error)]
pub enum ExtractionError {
    #[error("LLM 调用失败: {0}")]
    LlmError(String),

    #[error("JSON 解析失败: {0}")]
    ParseError(String),

    #[error("技能名称冲突: {0}")]
    SkillConflict(String),

    #[error("会话消息不足，无法抽取")]
    InsufficientMessages,

    #[error("技能验证失败: {0}")]
    ValidationError(String),

    #[error("数据库错误: {0}")]
    DatabaseError(#[from] sqlx::Error),

    #[error("IO 错误: {0}")]
    IoError(#[from] std::io::Error),

    #[error("记录未找到: {0}")]
    NotFound(String),
}

pub type Result<T> = std::result::Result<T, ExtractionError>;
