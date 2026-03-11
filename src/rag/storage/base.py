"""
向量存储抽象基类
"""
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional
import numpy as np


class VectorStore(ABC):
    """
    向量存储抽象基类

    所有向量存储实现都必须继承此类并实现其抽象方法
    """

    @abstractmethod
    async def add(
        self,
        ids: List[str],
        embeddings: np.ndarray,
        documents: List[str],
        metadatas: Optional[List[Dict[str, Any]]] = None
    ) -> None:
        """
        添加文档到向量库

        Args:
            ids: 文档唯一标识符列表
            embeddings: 文档向量矩阵 (shape: [len(ids), dimension])
            documents: 文档文本列表
            metadatas: 文档元数据列表（可选）

        Raises:
            VectorStoreError: 添加失败
        """
        pass

    @abstractmethod
    async def search(
        self,
        query_embedding: np.ndarray,
        top_k: int = 5,
        where: Optional[Dict[str, Any]] = None,
        where_document: Optional[Dict[str, Any]] = None
    ) -> List[Dict[str, Any]]:
        """
        检索相似文档

        Args:
            query_embedding: 查询向量 (shape: [dimension])
            top_k: 返回结果数量
            where: 元数据过滤条件（可选）
            where_document: 文档内容过滤条件（可选）

        Returns:
            检索结果列表，每个元素包含:
                - id: 文档ID
                - document: 文档文本
                - metadata: 文档元数据
                - distance: 距离/相似度分数

        Raises:
            VectorStoreError: 检索失败
        """
        pass

    @abstractmethod
    async def update(
        self,
        ids: List[str],
        embeddings: Optional[np.ndarray] = None,
        documents: Optional[List[str]] = None,
        metadatas: Optional[List[Dict[str, Any]]] = None
    ) -> None:
        """
        更新文档

        Args:
            ids: 要更新的文档ID列表
            embeddings: 新的向量矩阵（可选）
            documents: 新的文档文本列表（可选）
            metadatas: 新的元数据列表（可选）

        Raises:
            VectorStoreError: 更新失败
        """
        pass

    @abstractmethod
    async def delete(self, ids: List[str]) -> None:
        """
        删除文档

        Args:
            ids: 要删除的文档ID列表

        Raises:
            VectorStoreError: 删除失败
        """
        pass

    @abstractmethod
    async def get(self, ids: List[str]) -> List[Dict[str, Any]]:
        """
        根据ID获取文档

        Args:
            ids: 文档ID列表

        Returns:
            文档列表，每个元素包含:
                - id: 文档ID
                - document: 文档文本
                - metadata: 文档元数据
                - embedding: 文档向量（可选）

        Raises:
            VectorStoreError: 获取失败
        """
        pass

    @abstractmethod
    async def count(self) -> int:
        """
        获取向量库中的文档总数

        Returns:
            文档数量

        Raises:
            VectorStoreError: 获取失败
        """
        pass

    @abstractmethod
    async def clear(self) -> None:
        """
        清空向量库

        Raises:
            VectorStoreError: 清空失败
        """
        pass

    @abstractmethod
    async def get_stats(self) -> Dict[str, Any]:
        """
        获取向量库统计信息

        Returns:
            统计信息字典，包含:
                - total_documents: 文档总数
                - collection_name: 集合名称
                - dimension: 向量维度
                - storage_path: 存储路径（如果适用）
                - 其他实现特定的统计信息

        Raises:
            VectorStoreError: 获取失败
        """
        pass

    async def exists(self, ids: List[str]) -> List[bool]:
        """
        检查文档是否存在

        Args:
            ids: 文档ID列表

        Returns:
            布尔值列表，对应每个ID是否存在

        Raises:
            VectorStoreError: 检查失败
        """
        try:
            results = await self.get(ids)
            exists_map = {r['id']: True for r in results}
            return [exists_map.get(id_, False) for id_ in ids]
        except Exception as e:
            # 默认实现：如果 get 方法不支持批量获取或出错，返回 False
            return [False] * len(ids)
