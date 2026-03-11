"""
检索器抽象基类
"""
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional
from dataclasses import dataclass


@dataclass
class RetrievalResult:
    """
    检索结果数据类
    """
    document: str  # 文档内容
    score: float  # 相似度分数 (0-1，越高越相似)
    metadata: Dict[str, Any]  # 文档元数据
    doc_id: str  # 文档ID

    def __repr__(self) -> str:
        return (
            f"RetrievalResult(score={self.score:.3f}, "
            f"doc_id='{self.doc_id}', "
            f"doc_preview='{self.document[:50]}...')"
        )


class Retriever(ABC):
    """
    检索器抽象基类

    所有检索器实现都必须继承此类并实现其抽象方法
    """

    @abstractmethod
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
        pass

    def format_context(
        self,
        results: List[RetrievalResult],
        max_tokens: Optional[int] = None,
        include_metadata: bool = False
    ) -> str:
        """
        格式化检索结果为上下文文本

        Args:
            results: 检索结果列表
            max_tokens: 最大 token 数（可选）
            include_metadata: 是否包含元数据

        Returns:
            格式化后的上下文文本
        """
        context_parts = []

        for idx, result in enumerate(results, 1):
            # 构建文档部分
            doc_part = f"【文档 {idx}】\n{result.document}"

            # 添加元数据（可选）
            if include_metadata and result.metadata:
                source = result.metadata.get('source', 'Unknown')
                doc_part += f"\n来源: {source}"

            context_parts.append(doc_part)

        context = "\n\n".join(context_parts)

        # TODO: 如果指定了 max_tokens，需要截断
        # 这里暂时返回完整内容，后续可以添加 token 计数和截断

        return context
