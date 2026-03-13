"""
RAG 模块自定义异常
"""


class RAGException(Exception):
    """RAG 基础异常类"""
    pass


class ConfigError(RAGException):
    """配置错误"""
    pass


class EmbeddingError(RAGException):
    """嵌入模型错误"""
    pass


class LoaderError(RAGException):
    """文档加载错误"""
    pass


class ChunkingError(RAGException):
    """文档分块错误"""
    pass


class StorageError(RAGException):
    """向量存储错误"""
    pass


class RetrievalError(RAGException):
    """检索错误"""
    pass


class IndexingError(RAGException):
    """索引错误"""
    pass
