"""
向量存储工厂
"""
from ..config import VectorStoreConfig
from ..exceptions import ConfigError
from .base import VectorStore
from .chromadb import ChromaDBStore


def create_vector_store(
    config: VectorStoreConfig,
    dimension: int | None = None
) -> VectorStore:
    """
    根据配置创建向量存储实例

    Args:
        config: 向量存储配置
        dimension: 向量维度（可选，用于验证）

    Returns:
        向量存储实例

    Raises:
        ConfigError: 配置错误或不支持的存储类型
    """
    store_type = config.type.lower()

    if store_type == "chromadb":
        return ChromaDBStore(
            path=config.path,
            collection_name=config.collection_name,
            dimension=dimension
        )
    # 未来可扩展其他存储
    # elif store_type == "faiss":
    #     return FAISSStore(...)
    # elif store_type == "qdrant":
    #     return QdrantStore(...)
    else:
        raise ConfigError(f"Unsupported vector store type: {store_type}")
