use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use serde::Serialize;

#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    #[error("{0}")]
    NotFound(String),
    #[error("{0}")]
    BadRequest(String),
    #[error("{0}")]
    Internal(String),
    #[error("{0}")]
    Unauthorized(String),
}

#[derive(Serialize)]
struct ErrorResponse {
    error: String,
}

impl IntoResponse for ApiError {
    fn into_response(self) -> Response {
        let (status, message) = match &self {
            ApiError::NotFound(msg) => (StatusCode::NOT_FOUND, msg.clone()),
            ApiError::BadRequest(msg) => (StatusCode::BAD_REQUEST, msg.clone()),
            ApiError::Internal(msg) => (StatusCode::INTERNAL_SERVER_ERROR, msg.clone()),
            ApiError::Unauthorized(msg) => (StatusCode::UNAUTHORIZED, msg.clone()),
        };
        let body = axum::Json(ErrorResponse { error: message });
        (status, body).into_response()
    }
}

impl From<agent_core::Error> for ApiError {
    fn from(e: agent_core::Error) -> Self {
        match &e {
            agent_core::Error::SessionNotFound(_) => ApiError::NotFound(e.to_string()),
            agent_core::Error::SkillNotFound(_) => ApiError::NotFound(e.to_string()),
            agent_core::Error::InvalidInput(_) => ApiError::BadRequest(e.to_string()),
            _ => ApiError::Internal(e.to_string()),
        }
    }
}

impl From<agent_memory::MemoryError> for ApiError {
    fn from(e: agent_memory::MemoryError) -> Self {
        match &e {
            agent_memory::MemoryError::NotFound(_) => ApiError::NotFound(e.to_string()),
            agent_memory::MemoryError::InvalidType(_) => ApiError::BadRequest(e.to_string()),
            _ => ApiError::Internal(e.to_string()),
        }
    }
}

impl From<agent_file_storage::FileStorageError> for ApiError {
    fn from(e: agent_file_storage::FileStorageError) -> Self {
        match &e {
            agent_file_storage::FileStorageError::FileNotFound(_) => {
                ApiError::NotFound(e.to_string())
            }
            agent_file_storage::FileStorageError::FileNotFoundByName(_) => {
                ApiError::NotFound(e.to_string())
            }
            agent_file_storage::FileStorageError::VersionNotFound { .. } => {
                ApiError::NotFound(e.to_string())
            }
            agent_file_storage::FileStorageError::PermissionDenied { .. } => {
                ApiError::Unauthorized(e.to_string())
            }
            agent_file_storage::FileStorageError::FileTooLarge { .. } => {
                ApiError::BadRequest(e.to_string())
            }
            agent_file_storage::FileStorageError::UnsupportedFileType(_) => {
                ApiError::BadRequest(e.to_string())
            }
            _ => ApiError::Internal(e.to_string()),
        }
    }
}

impl From<anyhow::Error> for ApiError {
    fn from(e: anyhow::Error) -> Self {
        ApiError::Internal(e.to_string())
    }
}
