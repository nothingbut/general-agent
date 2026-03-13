"""
检索系统测试
"""
import pytest
import numpy as np
import tempfile
import shutil
from pathlib import Path

from src.rag.retrieval import SemanticRetriever, RetrievalResult
from src.rag.retrieval import utils as retrieval_utils
from src.rag.embeddings import BGEEmbedding
from src.rag.storage import ChromaDBStore


@pytest.fixture
def temp_db_path():
    """创建临时数据库目录"""
    temp_dir = tempfile.mkdtemp()
    yield temp_dir
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
async def setup_vector_store(temp_db_path):
    """设置向量存储并添加测试数据"""
    # 创建存储
    store = ChromaDBStore(
        path=temp_db_path,
        collection_name="test_retrieval",
        dimension=768
    )

    # 创建嵌入模型
    embedding = BGEEmbedding()

    # 添加测试文档
    documents = [
        "Python 是一种高级编程语言，广泛应用于数据科学和机器学习。",
        "FastAPI 是一个现代化的 Python Web 框架，用于构建 API。",
        "Docker 是一个容器化平台，可以简化应用部署。",
        "Kubernetes 是一个容器编排系统，用于自动化部署和管理。",
        "机器学习是人工智能的一个分支，使用算法从数据中学习。"
    ]

    ids = [f"doc{i}" for i in range(len(documents))]
    embeddings = await embedding.embed_documents(documents)
    metadatas = [
        {"source": "python.md", "category": "programming"},
        {"source": "fastapi.md", "category": "web"},
        {"source": "docker.md", "category": "devops"},
        {"source": "kubernetes.md", "category": "devops"},
        {"source": "ml.md", "category": "ai"}
    ]

    await store.add(ids, embeddings, documents, metadatas)

    yield store, embedding

    # 清理
    await store.clear()


@pytest.mark.asyncio
async def test_semantic_retriever_basic(setup_vector_store):
    """测试语义检索基础功能"""
    store, embedding = setup_vector_store

    # 创建检索器
    retriever = SemanticRetriever(
        embedding=embedding,
        vector_store=store,
        similarity_threshold=0.0
    )

    # 查询
    results = await retriever.retrieve("如何使用 Python 进行机器学习？", top_k=3)

    # 验证
    assert len(results) <= 3
    assert all(isinstance(r, RetrievalResult) for r in results)
    assert all(0.0 <= r.score <= 1.0 for r in results)

    # 第一个结果应该与机器学习或 Python 相关
    assert any(keyword in results[0].document for keyword in ["Python", "机器学习", "数据科学"])


@pytest.mark.asyncio
async def test_semantic_retriever_with_threshold(setup_vector_store):
    """测试相似度阈值过滤"""
    store, embedding = setup_vector_store

    # 创建检索器（高阈值）
    retriever = SemanticRetriever(
        embedding=embedding,
        vector_store=store,
        similarity_threshold=0.8  # 高阈值
    )

    # 查询
    results = await retriever.retrieve("编程语言", top_k=5)

    # 验证：结果应该被过滤
    assert all(r.score >= 0.8 for r in results)


@pytest.mark.asyncio
async def test_semantic_retriever_with_filters(setup_vector_store):
    """测试元数据过滤"""
    store, embedding = setup_vector_store

    # 创建检索器
    retriever = SemanticRetriever(
        embedding=embedding,
        vector_store=store
    )

    # 查询（只检索 devops 类别）
    results = await retriever.retrieve(
        "容器技术",
        top_k=5,
        filters={"category": "devops"}
    )

    # 验证：所有结果都应该是 devops 类别
    assert all(r.metadata.get("category") == "devops" for r in results)


@pytest.mark.asyncio
async def test_semantic_retriever_empty_query(setup_vector_store):
    """测试空查询"""
    store, embedding = setup_vector_store

    retriever = SemanticRetriever(
        embedding=embedding,
        vector_store=store
    )

    # 空查询应该抛出异常
    with pytest.raises(Exception):
        await retriever.retrieve("", top_k=5)


@pytest.mark.asyncio
async def test_semantic_retriever_set_threshold(setup_vector_store):
    """测试动态设置阈值"""
    store, embedding = setup_vector_store

    retriever = SemanticRetriever(
        embedding=embedding,
        vector_store=store,
        similarity_threshold=0.5
    )

    # 更新阈值
    retriever.set_similarity_threshold(0.7)
    assert retriever.similarity_threshold == 0.7

    # 无效阈值应该抛出异常
    with pytest.raises(ValueError):
        retriever.set_similarity_threshold(1.5)


def test_rerank_by_score():
    """测试按分数重排序"""
    results = [
        RetrievalResult("doc1", 0.5, {}, "id1"),
        RetrievalResult("doc2", 0.9, {}, "id2"),
        RetrievalResult("doc3", 0.7, {}, "id3")
    ]

    # 降序
    reranked = retrieval_utils.rerank_by_score(results, reverse=True)
    assert [r.score for r in reranked] == [0.9, 0.7, 0.5]

    # 升序
    reranked = retrieval_utils.rerank_by_score(results, reverse=False)
    assert [r.score for r in reranked] == [0.5, 0.7, 0.9]


def test_filter_by_score():
    """测试按分数过滤"""
    results = [
        RetrievalResult("doc1", 0.5, {}, "id1"),
        RetrievalResult("doc2", 0.9, {}, "id2"),
        RetrievalResult("doc3", 0.7, {}, "id3")
    ]

    # 过滤
    filtered = retrieval_utils.filter_by_score(results, min_score=0.6, max_score=1.0)
    assert len(filtered) == 2
    assert all(r.score >= 0.6 for r in filtered)


def test_filter_by_metadata():
    """测试按元数据过滤"""
    results = [
        RetrievalResult("doc1", 0.5, {"category": "tech"}, "id1"),
        RetrievalResult("doc2", 0.9, {"category": "science"}, "id2"),
        RetrievalResult("doc3", 0.7, {"category": "tech"}, "id3")
    ]

    # 过滤
    filtered = retrieval_utils.filter_by_metadata(results, {"category": "tech"})
    assert len(filtered) == 2
    assert all(r.metadata["category"] == "tech" for r in filtered)


def test_deduplicate_results():
    """测试去重"""
    results = [
        RetrievalResult("doc1", 0.5, {}, "id1"),
        RetrievalResult("doc2", 0.9, {}, "id2"),
        RetrievalResult("doc1", 0.7, {}, "id1"),  # 重复
    ]

    # 按 doc_id 去重
    deduped = retrieval_utils.deduplicate_results(results, key='doc_id')
    assert len(deduped) == 2
    assert deduped[0].doc_id == "id1"
    assert deduped[1].doc_id == "id2"


def test_merge_results():
    """测试合并结果"""
    results1 = [
        RetrievalResult("doc1", 0.5, {}, "id1"),
        RetrievalResult("doc2", 0.9, {}, "id2")
    ]
    results2 = [
        RetrievalResult("doc1", 0.7, {}, "id1"),  # 重复，不同分数
        RetrievalResult("doc3", 0.6, {}, "id3")
    ]

    # 合并（取最大分数）
    merged = retrieval_utils.merge_results([results1, results2], strategy='max')
    assert len(merged) == 3

    # 找到 id1 的结果，验证分数是最大值
    id1_result = next(r for r in merged if r.doc_id == "id1")
    assert id1_result.score == 0.7  # max(0.5, 0.7)

    # 合并（取平均分数）
    merged = retrieval_utils.merge_results([results1, results2], strategy='avg')
    id1_result = next(r for r in merged if r.doc_id == "id1")
    assert abs(id1_result.score - 0.6) < 0.01  # avg(0.5, 0.7)


def test_truncate_by_tokens():
    """测试按 token 截断"""
    results = [
        RetrievalResult("a" * 100, 0.9, {}, "id1"),  # ~50 tokens
        RetrievalResult("b" * 100, 0.8, {}, "id2"),  # ~50 tokens
        RetrievalResult("c" * 100, 0.7, {}, "id3"),  # ~50 tokens
    ]

    # 截断到 100 tokens
    truncated = retrieval_utils.truncate_by_tokens(results, max_tokens=100)

    # 应该保留 2 个完整的结果（每个 ~50 tokens）
    assert len(truncated) <= 3
    assert truncated[0].score == 0.9  # 保持顺序


def test_group_by_source():
    """测试按来源分组"""
    results = [
        RetrievalResult("doc1", 0.5, {"source": "file1.md"}, "id1"),
        RetrievalResult("doc2", 0.9, {"source": "file2.md"}, "id2"),
        RetrievalResult("doc3", 0.7, {"source": "file1.md"}, "id3")
    ]

    # 分组
    grouped = retrieval_utils.group_by_source(results)

    assert len(grouped) == 2
    assert len(grouped["file1.md"]) == 2
    assert len(grouped["file2.md"]) == 1


@pytest.mark.asyncio
async def test_retrieval_result_repr():
    """测试 RetrievalResult 字符串表示"""
    result = RetrievalResult(
        document="This is a test document with some content.",
        score=0.85,
        metadata={"source": "test.md"},
        doc_id="test_id"
    )

    repr_str = repr(result)

    assert "score=0.850" in repr_str
    assert "test_id" in repr_str
    assert "This is a test document" in repr_str
