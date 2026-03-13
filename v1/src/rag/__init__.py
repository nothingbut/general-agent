"""
RAG (Retrieval-Augmented Generation) 模块
"""
from .config import RAGConfig
from .engine import RAGEngine
from .retrieval import Retriever, RetrievalResult, SemanticRetriever
from .exceptions import (
    RAGException,
    ConfigError,
    EmbeddingError,
    LoaderError,
    ChunkingError,
    StorageError,
    RetrievalError,
    IndexingError
)

__all__ = [
    # 核心类
    'RAGConfig',
    'RAGEngine',
    # 检索类
    'Retriever',
    'RetrievalResult',
    'SemanticRetriever',
    # 异常
    'RAGException',
    'ConfigError',
    'EmbeddingError',
    'LoaderError',
    'ChunkingError',
    'StorageError',
    'RetrievalError',
    'IndexingError'
]
