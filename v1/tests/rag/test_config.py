"""
RAG 配置测试
"""
import pytest
from pathlib import Path
from src.rag.config import (
    RAGConfig,
    EmbeddingConfig,
    VectorStoreConfig,
    ChunkingConfig,
)


def test_load_config() -> None:
    """测试加载配置文件"""
    config = RAGConfig.load("config/rag_config.yaml")

    assert config.enabled is True
    assert config.auto_mode is False
    assert config.embedding.provider == "bge"
    assert config.embedding.dimension == 768
    assert config.vector_store.type == "chromadb"
    assert config.chunking.strategy == "hybrid"


def test_embedding_config_validation() -> None:
    """测试嵌入配置验证"""
    # 正常配置
    config = EmbeddingConfig(device="cpu")
    assert config.device == "cpu"

    # 错误的设备类型
    with pytest.raises(ValueError, match="device must be"):
        EmbeddingConfig(device="invalid")


def test_vector_store_config_validation() -> None:
    """测试向量存储配置验证"""
    # 正常配置
    config = VectorStoreConfig(type="chromadb")
    assert config.type == "chromadb"

    # 错误的类型
    with pytest.raises(ValueError, match="type must be"):
        VectorStoreConfig(type="invalid")


def test_chunking_config_validation() -> None:
    """测试分块配置验证"""
    # 正常配置
    config = ChunkingConfig(strategy="hybrid", max_size=512)
    assert config.strategy == "hybrid"
    assert config.max_size == 512

    # 错误的策略
    with pytest.raises(ValueError, match="strategy must be"):
        ChunkingConfig(strategy="invalid")

    # max_size 超出范围
    with pytest.raises(ValueError, match="max_size must be"):
        ChunkingConfig(max_size=5000)


def test_config_ensure_directories() -> None:
    """测试目录创建"""
    config = RAGConfig.load("config/rag_config.yaml")
    config.ensure_directories()

    # 验证目录存在
    assert Path(config.vector_store.path).exists()
    assert config.get_data_path().exists()
