"""
向量存储测试
"""
import pytest
import numpy as np
import tempfile
import shutil
from pathlib import Path

from src.rag.storage import ChromaDBStore, create_vector_store
from src.rag.config import VectorStoreConfig
from src.rag.exceptions import StorageError


@pytest.fixture
def temp_db_path():
    """创建临时数据库目录"""
    temp_dir = tempfile.mkdtemp()
    yield temp_dir
    # 清理
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
async def chroma_store(temp_db_path):
    """创建 ChromaDB 存储实例"""
    store = ChromaDBStore(
        path=temp_db_path,
        collection_name="test_collection",
        dimension=768
    )
    yield store
    # 清理
    await store.clear()


@pytest.mark.asyncio
async def test_chromadb_add_and_get(chroma_store):
    """测试添加和获取文档"""
    # 准备测试数据
    ids = ["doc1", "doc2", "doc3"]
    embeddings = np.random.rand(3, 768).astype(np.float32)
    documents = ["文档1内容", "文档2内容", "文档3内容"]
    metadatas = [
        {"source": "file1.md", "type": "markdown"},
        {"source": "file2.md", "type": "markdown"},
        {"source": "file3.txt", "type": "text"}
    ]

    # 添加文档
    await chroma_store.add(ids, embeddings, documents, metadatas)

    # 获取文档
    results = await chroma_store.get(["doc1", "doc3"])

    # 验证结果
    assert len(results) == 2
    assert results[0]['id'] == "doc1"
    assert results[0]['document'] == "文档1内容"
    assert results[0]['metadata']['source'] == "file1.md"
    assert results[1]['id'] == "doc3"


@pytest.mark.asyncio
async def test_chromadb_search(chroma_store):
    """测试向量检索"""
    # 准备测试数据
    ids = ["doc1", "doc2", "doc3"]
    embeddings = np.array([
        [1.0] + [0.0] * 767,  # 文档1
        [0.0, 1.0] + [0.0] * 766,  # 文档2
        [0.5, 0.5] + [0.0] * 766  # 文档3（介于1和2之间）
    ], dtype=np.float32)
    documents = ["文档1", "文档2", "文档3"]

    # 添加文档
    await chroma_store.add(ids, embeddings, documents)

    # 搜索：查询向量接近文档1
    query = np.array([0.9, 0.1] + [0.0] * 766, dtype=np.float32)
    results = await chroma_store.search(query, top_k=2)

    # 验证结果
    assert len(results) == 2
    # 第一个结果应该是 doc1（最相似）
    assert results[0]['id'] == "doc1"
    # 验证返回了距离
    assert 'distance' in results[0]
    assert results[0]['distance'] >= 0  # 余弦距离


@pytest.mark.asyncio
async def test_chromadb_update(chroma_store):
    """测试更新文档"""
    # 添加初始文档
    ids = ["doc1"]
    embeddings = np.random.rand(1, 768).astype(np.float32)
    documents = ["原始内容"]
    metadatas = [{"version": 1}]

    await chroma_store.add(ids, embeddings, documents, metadatas)

    # 更新文档（在实际使用中，更新文档内容时也需要更新嵌入向量）
    new_embeddings = np.random.rand(1, 768).astype(np.float32)
    new_documents = ["更新后的内容"]
    new_metadatas = [{"version": 2}]
    await chroma_store.update(ids, embeddings=new_embeddings, documents=new_documents, metadatas=new_metadatas)

    # 验证更新
    results = await chroma_store.get(ids)
    assert len(results) == 1
    assert results[0]['document'] == "更新后的内容"
    assert results[0]['metadata']['version'] == 2


@pytest.mark.asyncio
async def test_chromadb_delete(chroma_store):
    """测试删除文档"""
    # 添加文档
    ids = ["doc1", "doc2", "doc3"]
    embeddings = np.random.rand(3, 768).astype(np.float32)
    documents = ["文档1", "文档2", "文档3"]

    await chroma_store.add(ids, embeddings, documents)

    # 删除一个文档
    await chroma_store.delete(["doc2"])

    # 验证删除
    count = await chroma_store.count()
    assert count == 2

    results = await chroma_store.get(["doc1", "doc2", "doc3"])
    # 只应该返回 doc1 和 doc3
    assert len(results) == 2
    assert all(r['id'] in ["doc1", "doc3"] for r in results)


@pytest.mark.asyncio
async def test_chromadb_count(chroma_store):
    """测试文档计数"""
    # 初始应该为空
    count = await chroma_store.count()
    assert count == 0

    # 添加文档
    ids = ["doc1", "doc2"]
    embeddings = np.random.rand(2, 768).astype(np.float32)
    documents = ["文档1", "文档2"]

    await chroma_store.add(ids, embeddings, documents)

    # 验证计数
    count = await chroma_store.count()
    assert count == 2


@pytest.mark.asyncio
async def test_chromadb_clear(chroma_store):
    """测试清空向量库"""
    # 添加一些文档
    ids = ["doc1", "doc2", "doc3"]
    embeddings = np.random.rand(3, 768).astype(np.float32)
    documents = ["文档1", "文档2", "文档3"]

    await chroma_store.add(ids, embeddings, documents)

    # 验证已添加
    count = await chroma_store.count()
    assert count == 3

    # 清空
    await chroma_store.clear()

    # 验证已清空
    count = await chroma_store.count()
    assert count == 0


@pytest.mark.asyncio
async def test_chromadb_get_stats(chroma_store):
    """测试获取统计信息"""
    # 添加一些文档
    ids = ["doc1", "doc2"]
    embeddings = np.random.rand(2, 768).astype(np.float32)
    documents = ["文档1", "文档2"]

    await chroma_store.add(ids, embeddings, documents)

    # 获取统计信息
    stats = await chroma_store.get_stats()

    # 验证统计信息
    assert stats['total_documents'] == 2
    assert stats['collection_name'] == "test_collection"
    assert stats['dimension'] == 768
    assert 'storage_path' in stats
    assert stats['distance_metric'] == "cosine"


@pytest.mark.asyncio
async def test_chromadb_search_with_filter(chroma_store):
    """测试带过滤条件的检索"""
    # 添加文档
    ids = ["doc1", "doc2", "doc3"]
    embeddings = np.random.rand(3, 768).astype(np.float32)
    documents = ["文档1", "文档2", "文档3"]
    metadatas = [
        {"category": "tech"},
        {"category": "science"},
        {"category": "tech"}
    ]

    await chroma_store.add(ids, embeddings, documents, metadatas)

    # 搜索：只检索 tech 类别
    query = np.random.rand(768).astype(np.float32)
    results = await chroma_store.search(
        query,
        top_k=5,
        where={"category": "tech"}
    )

    # 验证结果
    assert len(results) <= 2  # 只有 doc1 和 doc3 是 tech
    for result in results:
        assert result['metadata']['category'] == "tech"


@pytest.mark.asyncio
async def test_chromadb_batch_operations(chroma_store):
    """测试批量操作"""
    # 批量添加大量文档
    batch_size = 100
    ids = [f"doc{i}" for i in range(batch_size)]
    embeddings = np.random.rand(batch_size, 768).astype(np.float32)
    documents = [f"文档{i}内容" for i in range(batch_size)]

    # 添加
    await chroma_store.add(ids, embeddings, documents)

    # 验证
    count = await chroma_store.count()
    assert count == batch_size

    # 批量删除
    delete_ids = [f"doc{i}" for i in range(0, batch_size, 2)]  # 删除偶数ID
    await chroma_store.delete(delete_ids)

    # 验证
    count = await chroma_store.count()
    assert count == batch_size // 2


@pytest.mark.asyncio
async def test_chromadb_error_dimension_mismatch(chroma_store):
    """测试维度不匹配错误"""
    # 尝试添加错误维度的向量
    ids = ["doc1"]
    embeddings = np.random.rand(1, 512).astype(np.float32)  # 错误维度
    documents = ["文档1"]

    with pytest.raises(StorageError) as exc_info:
        await chroma_store.add(ids, embeddings, documents)

    assert "dimension" in str(exc_info.value).lower()


@pytest.mark.asyncio
async def test_chromadb_error_length_mismatch(chroma_store):
    """测试长度不匹配错误"""
    # IDs 和 documents 长度不匹配
    ids = ["doc1", "doc2"]
    embeddings = np.random.rand(2, 768).astype(np.float32)
    documents = ["文档1"]  # 只有1个

    with pytest.raises(StorageError) as exc_info:
        await chroma_store.add(ids, embeddings, documents)

    assert "length" in str(exc_info.value).lower()


@pytest.mark.asyncio
async def test_vector_store_factory(temp_db_path):
    """测试向量存储工厂"""
    config = VectorStoreConfig(
        type="chromadb",
        path=temp_db_path,
        collection_name="factory_test"
    )

    store = create_vector_store(config, dimension=768)

    # 验证
    assert isinstance(store, ChromaDBStore)
    assert store.collection_name == "factory_test"

    # 清理
    await store.clear()


@pytest.mark.asyncio
async def test_chromadb_exists(chroma_store):
    """测试文档存在检查"""
    # 添加一些文档
    ids = ["doc1", "doc2"]
    embeddings = np.random.rand(2, 768).astype(np.float32)
    documents = ["文档1", "文档2"]

    await chroma_store.add(ids, embeddings, documents)

    # 检查存在性
    exists = await chroma_store.exists(["doc1", "doc2", "doc3"])

    # 验证
    assert exists[0] == True  # doc1 存在
    assert exists[1] == True  # doc2 存在
    assert exists[2] == False  # doc3 不存在


@pytest.mark.asyncio
async def test_chromadb_empty_operations(chroma_store):
    """测试空操作"""
    # 空删除应该不报错
    await chroma_store.delete([])

    # 空获取应该返回空列表
    results = await chroma_store.get([])
    assert results == []

    # 空库搜索应该返回空列表
    query = np.random.rand(768).astype(np.float32)
    results = await chroma_store.search(query, top_k=5)
    assert results == []
