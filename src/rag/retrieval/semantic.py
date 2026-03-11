"""
语义检索器实现
"""
from typing import List, Dict, Any, Optional
import logging

from .base import Retriever, RetrievalResult
from ..embeddings import EmbeddingProvider
from ..storage import VectorStore
from ..exceptions import RetrievalError

logger = logging.getLogger(__name__)


class SemanticRetriever(Retriever):
    """
    语义检索器

    基于向量相似度进行语义检索
    """

    def __init__(
        self,
        embedding: EmbeddingProvider,
        vector_store: VectorStore,
        similarity_threshold: float = 0.0
    ):
        """
        初始化语义检索器

        Args:
            embedding: 嵌入模型
            vector_store: 向量存储
            similarity_threshold: 相似度阈值（0-1），低于此值的结果会被过滤

        Raises:
            ValueError: 参数无效
        """
        if not 0.0 <= similarity_threshold <= 1.0:
            raise ValueError("similarity_threshold must be between 0.0 and 1.0")

        self.embedding = embedding
        self.vector_store = vector_store
        self.similarity_threshold = similarity_threshold

    async def retrieve(
        self,
        query: str,
        top_k: int = 5,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[RetrievalResult]:
        """
        检索相关文档

        Args:
            query: 查询文本
            top_k: 返回结果数量
            filters: 元数据过滤条件（可选）

        Returns:
            检索结果列表，按相似度降序排列

        Raises:
            RetrievalError: 检索失败
        """
        try:
            if not query or not query.strip():
                raise ValueError("Query cannot be empty")

            # 1. 生成查询向量
            logger.debug(f"Embedding query: {query[:100]}...")
            query_embedding = await self.embedding.embed_query(query)

            # 2. 向量检索
            logger.debug(f"Searching vector store with top_k={top_k}")
            search_results = await self.vector_store.search(
                query_embedding=query_embedding,
                top_k=top_k,
                where=filters
            )

            # 3. 转换为 RetrievalResult
            results = []
            for result in search_results:
                # ChromaDB 返回的是距离（越小越相似）
                # 需要转换为相似度分数（越大越相似）
                distance = result['distance']

                # 余弦距离转换为余弦相似度
                # distance = 1 - similarity（因为 ChromaDB 使用 cosine distance）
                # 所以 similarity = 1 - distance
                similarity = 1.0 - distance

                # 应用相似度阈值过滤
                if similarity < self.similarity_threshold:
                    logger.debug(
                        f"Filtering result with similarity {similarity:.3f} "
                        f"(threshold: {self.similarity_threshold})"
                    )
                    continue

                results.append(RetrievalResult(
                    document=result['document'],
                    score=similarity,
                    metadata=result['metadata'],
                    doc_id=result['id']
                ))

            logger.info(
                f"Retrieved {len(results)} results "
                f"(filtered from {len(search_results)} with threshold {self.similarity_threshold})"
            )

            return results

        except Exception as e:
            raise RetrievalError(f"Failed to retrieve documents: {e}")

    def set_similarity_threshold(self, threshold: float) -> None:
        """
        设置相似度阈值

        Args:
            threshold: 新的相似度阈值（0-1）

        Raises:
            ValueError: 阈值无效
        """
        if not 0.0 <= threshold <= 1.0:
            raise ValueError("similarity_threshold must be between 0.0 and 1.0")

        self.similarity_threshold = threshold
        logger.info(f"Similarity threshold updated to {threshold}")
