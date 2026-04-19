pub mod error;
pub mod extractor;
pub mod models;
pub mod repository;
pub mod service;
pub mod vector_store;

pub use error::{MemoryError, Result};
pub use extractor::{ExtractionResult, ExtractedMemory, MemoryExtractor};
pub use models::{Memory, MemoryQuery, MemoryType};
pub use repository::{MemoryRepository, SqliteMemoryRepository};
pub use service::{MemoryService, MemoryStats};
pub use vector_store::{EmbeddingProvider, VectorMemoryStore, VectorSearchResult, VectorStoreProvider};

#[cfg(feature = "vector")]
pub use vector_store::{RagEmbeddingAdapter, RagVectorStoreAdapter};
