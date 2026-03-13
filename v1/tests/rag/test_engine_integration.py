"""
RAG 引擎集成测试
"""
import pytest
import tempfile
import shutil
from pathlib import Path

from src.rag.engine import RAGEngine
from src.rag.config import (
    RAGConfig, VectorStoreConfig, EmbeddingConfig,
    ChunkingConfig, RetrievalConfig, IndexingConfig
)


@pytest.fixture
def temp_dir():
    """创建临时目录"""
    temp_dir = tempfile.mkdtemp()
    yield temp_dir
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
def temp_docs_dir(temp_dir):
    """创建临时文档目录并添加测试文件"""
    docs_dir = Path(temp_dir) / "docs"
    docs_dir.mkdir()

    # 创建测试 Markdown 文件
    (docs_dir / "test1.md").write_text("""
# 测试文档1

这是一个测试文档，用于验证 RAG 引擎的索引功能。

## 功能特点

- 支持 Markdown 格式
- 自动分块处理
- 向量化存储

## 使用方法

只需要调用 index_documents 方法即可完成索引。
""", encoding='utf-8')

    # 创建测试文本文件
    (docs_dir / "test2.txt").write_text("""
这是一个纯文本测试文档。

包含多个段落来测试文档分块功能。

每个段落应该被正确处理。
""", encoding='utf-8')

    return docs_dir


@pytest.fixture
async def rag_engine(temp_dir):
    """创建 RAG 引擎实例"""
    # 创建测试配置
    config = RAGConfig(
        enabled=True,
        vector_store=VectorStoreConfig(
            type="chromadb",
            path=str(Path(temp_dir) / "vector_db"),
            collection_name="test_collection"
        ),
        embedding=EmbeddingConfig(
            provider="bge",
            model="BAAI/bge-base-zh-v1.5",
            dimension=768
        ),
        chunking=ChunkingConfig(
            strategy="hybrid",
            max_size=512,
            min_size=100,
            overlap=50
        ),
        retrieval=RetrievalConfig(
            top_k=5,
            similarity_threshold=0.7
        ),
        indexing=IndexingConfig(
            batch_size=100,
            show_progress=False
        )
    )

    # 确保目录存在
    config.ensure_directories()

    # 创建引擎
    engine = RAGEngine(config)

    yield engine

    # 清理
    await engine.clear_index()


@pytest.mark.asyncio
async def test_engine_index_documents(rag_engine, temp_docs_dir):
    """测试索引文档"""
    # 索引文档
    stats = await rag_engine.index_documents(
        str(temp_docs_dir),
        recursive=False,
        show_progress=False
    )

    # 验证统计信息
    assert stats['total_files'] == 2
    assert stats['indexed_files'] == 2
    assert stats['total_chunks'] > 0
    assert len(stats['failed_files']) == 0

    # 验证向量库
    store_stats = await rag_engine.get_stats()
    assert store_stats['total_documents'] == stats['total_chunks']


@pytest.mark.asyncio
async def test_engine_update_document(rag_engine, temp_docs_dir):
    """测试更新文档"""
    # 先索引
    await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)

    # 修改文件
    test_file = temp_docs_dir / "test1.md"
    test_file.write_text("# 更新后的内容\n\n这是更新后的文档内容。", encoding='utf-8')

    # 更新文档
    await rag_engine.update_document(str(test_file))

    # 验证：文档数应该保持不变或略有变化（因为分块可能不同）
    stats = await rag_engine.get_stats()
    assert stats['total_documents'] > 0


@pytest.mark.asyncio
async def test_engine_delete_document(rag_engine, temp_docs_dir):
    """测试删除文档"""
    # 先索引
    stats = await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)
    initial_chunks = stats['total_chunks']

    # 删除一个文档
    test_file = temp_docs_dir / "test1.md"
    await rag_engine.delete_document(str(test_file))

    # 验证：文档数应该减少
    store_stats = await rag_engine.get_stats()
    assert store_stats['total_documents'] < initial_chunks


@pytest.mark.asyncio
async def test_engine_clear_index(rag_engine, temp_docs_dir):
    """测试清空索引"""
    # 先索引
    await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)

    # 验证有数据
    stats = await rag_engine.get_stats()
    assert stats['total_documents'] > 0

    # 清空
    await rag_engine.clear_index()

    # 验证已清空
    stats = await rag_engine.get_stats()
    assert stats['total_documents'] == 0


@pytest.mark.asyncio
async def test_engine_incremental_indexing(rag_engine, temp_docs_dir):
    """测试增量索引（跳过未修改的文件）"""
    # 第一次索引
    stats1 = await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)
    assert stats1['indexed_files'] == 2
    assert stats1['skipped_files'] == 0

    # 第二次索引（文件未修改）
    stats2 = await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)
    assert stats2['indexed_files'] == 0
    assert stats2['skipped_files'] == 2


@pytest.mark.asyncio
async def test_engine_get_stats(rag_engine, temp_docs_dir):
    """测试获取统计信息"""
    # 索引文档
    await rag_engine.index_documents(str(temp_docs_dir), show_progress=False)

    # 获取统计信息
    stats = await rag_engine.get_stats()

    # 验证
    assert 'total_documents' in stats
    assert 'collection_name' in stats
    assert 'dimension' in stats
    assert stats['total_documents'] > 0
    assert stats['dimension'] == 768
