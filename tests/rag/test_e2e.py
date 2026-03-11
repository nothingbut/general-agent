"""
RAG 端到端测试
"""
import pytest
import tempfile
import shutil
from pathlib import Path
from src.rag import RAGEngine, RAGConfig
from src.rag.config import VectorStoreConfig, EmbeddingConfig, ChunkingConfig, RetrievalConfig, IndexingConfig


@pytest.fixture
def temp_dir():
    temp_dir = tempfile.mkdtemp()
    yield temp_dir
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
def test_docs_dir(temp_dir):
    """创建测试文档"""
    docs_dir = Path(temp_dir) / "docs"
    docs_dir.mkdir()

    (docs_dir / "python.md").write_text("# Python\nPython 是一种编程语言。", encoding='utf-8')
    (docs_dir / "docker.md").write_text("# Docker\nDocker 是容器技术。", encoding='utf-8')

    return docs_dir


@pytest.mark.asyncio
async def test_rag_full_workflow(temp_dir, test_docs_dir):
    """测试完整 RAG 工作流程"""
    # 1. 创建配置
    config = RAGConfig(
        enabled=True,
        vector_store=VectorStoreConfig(
            type="chromadb",
            path=str(Path(temp_dir) / "db"),
            collection_name="test"
        ),
        embedding=EmbeddingConfig(provider="bge", dimension=768),
        chunking=ChunkingConfig(strategy="hybrid"),
        retrieval=RetrievalConfig(top_k=3, similarity_threshold=0.3),
        indexing=IndexingConfig(show_progress=False)
    )
    config.ensure_directories()

    # 2. 创建引擎并索引
    engine = RAGEngine(config)
    stats = await engine.index_documents(str(test_docs_dir), show_progress=False)

    assert stats['indexed_files'] == 2
    assert stats['total_chunks'] > 0

    # 3. 检索
    results = await engine.retrieve("Python", top_k=2)
    assert len(results) > 0

    # 4. 完整查询
    result = await engine.query("什么是 Docker？", top_k=2)

    assert "query" in result
    assert "context" in result
    assert "stats" in result
    assert result['stats']['total_results'] > 0

    # 清理
    await engine.clear_index()
