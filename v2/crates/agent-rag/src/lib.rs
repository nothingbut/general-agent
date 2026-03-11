//! Agent RAG - Retrieval Augmented Generation 实现

pub mod error;
pub mod loader;
pub mod chunker;
pub mod embeddings;
pub mod vector_store;
pub mod retriever;

pub use error::{RAGError, Result};
pub use loader::{DocumentLoader, MarkdownLoader, TextLoader};
pub use chunker::{Chunker, ChunkConfig, ChunkStrategy, Chunk};
pub use embeddings::{Embedder, ollama::{OllamaEmbedder, OllamaConfig}};
pub use vector_store::{VectorStore, SearchResult, qdrant::{QdrantStore, QdrantConfig}};
pub use retriever::DefaultRAGRetriever;
