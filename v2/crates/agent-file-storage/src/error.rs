use thiserror::Error;
use uuid::Uuid;

#[derive(Debug, Error)]
pub enum FileStorageError {
    #[error("文件未找到: {0}")]
    FileNotFound(Uuid),

    #[error("文件名未找到: {0}")]
    FileNotFoundByName(String),

    #[error("版本未找到: 文件 {file_id} 版本 {version}")]
    VersionNotFound { file_id: Uuid, version: i32 },

    #[error("权限不足: 用户 {user_id} 无法访问文件 {file_id}")]
    PermissionDenied { file_id: Uuid, user_id: String },

    #[error("文件过大: {size} 字节，最大允许 {max_size} 字节")]
    FileTooLarge { size: i64, max_size: i64 },

    #[error("不支持的文件类型: {0}")]
    UnsupportedFileType(String),

    #[error("存储空间不足")]
    InsufficientStorage,

    #[error("文件系统错误: {0}")]
    IoError(#[from] std::io::Error),

    #[error("数据库错误: {0}")]
    DatabaseError(#[from] sqlx::Error),

    #[error("序列化错误: {0}")]
    SerializationError(#[from] serde_json::Error),

    #[error("重复权限: 用户 {user_id} 已有文件 {file_id} 的 {permission} 权限")]
    DuplicatePermission {
        file_id: Uuid,
        user_id: String,
        permission: String,
    },
}

pub type Result<T> = std::result::Result<T, FileStorageError>;
