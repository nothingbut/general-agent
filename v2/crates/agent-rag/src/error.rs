//! RAG 错误类型定义

use thiserror::Error;

#[derive(Debug, Error)]
pub enum RAGError {
    #[error("Document loading failed: {0}")]
    LoadingFailed(String),

    #[error("Embedding generation failed: {0}")]
    EmbeddingFailed(String),

    #[error("Vector store error: {0}")]
    VectorStoreError(String),

    #[error("Retrieval failed: {0}")]
    RetrievalFailed(String),

    #[error("Chunking failed: {0}")]
    ChunkingFailed(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
}

pub type Result<T> = std::result::Result<T, RAGError>;
