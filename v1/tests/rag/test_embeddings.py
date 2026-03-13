"""
嵌入模型测试
"""
import pytest
import numpy as np
from src.rag.embeddings import BGEEmbedding, create_embedding_provider
from src.rag.config import EmbeddingConfig


@pytest.mark.asyncio
async def test_bge_query_embedding() -> None:
    """测试 BGE 查询嵌入"""
    embedding = BGEEmbedding()

    query = "如何配置 FastAPI？"
    result = await embedding.embed_query(query)

    # 验证形状
    assert result.shape == (768,)

    # 验证归一化（如果启用）
    if embedding.normalize:
        norm = np.linalg.norm(result)
        assert abs(norm - 1.0) < 0.01  # 归一化向量的范数应该接近 1


@pytest.mark.asyncio
async def test_bge_document_embedding() -> None:
    """测试 BGE 文档嵌入"""
    embedding = BGEEmbedding()

    docs = ["FastAPI 配置指南", "Django 教程", "Python 异步编程"]
    result = await embedding.embed_documents(docs)

    # 验证形状
    assert result.shape == (3, 768)

    # 验证每个向量都已归一化
    if embedding.normalize:
        norms = np.linalg.norm(result, axis=1)
        for norm in norms:
            assert abs(norm - 1.0) < 0.01


@pytest.mark.asyncio
async def test_bge_similarity() -> None:
    """测试 BGE 相似度计算"""
    embedding = BGEEmbedding()

    # 查询和相关文档
    query = "如何配置数据库？"
    doc1 = "数据库配置指南"
    doc2 = "前端开发教程"

    query_vec = await embedding.embed_query(query)
    doc_vecs = await embedding.embed_documents([doc1, doc2])

    # 计算余弦相似度
    sim1 = np.dot(query_vec, doc_vecs[0])
    sim2 = np.dot(query_vec, doc_vecs[1])

    # doc1 应该比 doc2 更相关
    assert sim1 > sim2
    assert sim1 > 0.5  # 相关文档相似度应该较高


@pytest.mark.asyncio
async def test_bge_empty_documents() -> None:
    """测试空文档列表"""
    embedding = BGEEmbedding()

    with pytest.raises(Exception):  # 应该抛出错误
        await embedding.embed_documents([])


@pytest.mark.asyncio
async def test_embedding_factory() -> None:
    """测试嵌入模型工厂"""
    config = EmbeddingConfig(provider="bge")
    embedding = create_embedding_provider(config)

    assert isinstance(embedding, BGEEmbedding)
    assert embedding.get_dimension() == 768


def test_bge_dimension() -> None:
    """测试获取向量维度"""
    embedding = BGEEmbedding()
    assert embedding.get_dimension() == 768


def test_bge_repr() -> None:
    """测试字符串表示"""
    embedding = BGEEmbedding()
    repr_str = repr(embedding)

    assert "BGEEmbedding" in repr_str
    assert "768" in repr_str
    assert "cpu" in repr_str
