"""
ChromaDB 向量存储实现
"""
from pathlib import Path
from typing import List, Dict, Any, Optional
import numpy as np
import chromadb
from chromadb.config import Settings

from .base import VectorStore
from ..exceptions import StorageError


class ChromaDBStore(VectorStore):
    """
    ChromaDB 向量存储实现

    使用 ChromaDB 作为持久化向量数据库，支持余弦相似度搜索
    """

    def __init__(
        self,
        path: str,
        collection_name: str = "documents",
        dimension: Optional[int] = None
    ):
        """
        初始化 ChromaDB 存储

        Args:
            path: 数据库存储路径
            collection_name: 集合名称
            dimension: 向量维度（可选，用于验证）

        Raises:
            StorageError: 初始化失败
        """
        try:
            self.path = Path(path)
            self.collection_name = collection_name
            self.dimension = dimension

            # 确保目录存在
            self.path.mkdir(parents=True, exist_ok=True)

            # 初始化 ChromaDB 客户端
            self.client = chromadb.PersistentClient(
                path=str(self.path),
                settings=Settings(
                    anonymized_telemetry=False,
                    allow_reset=True
                )
            )

            # 获取或创建集合（禁用自动嵌入，我们自己提供嵌入向量）
            self.collection = self.client.get_or_create_collection(
                name=self.collection_name,
                metadata={"hnsw:space": "cosine"},  # 使用余弦相似度
                embedding_function=None  # 不使用自动嵌入
            )

        except Exception as e:
            raise StorageError(f"Failed to initialize ChromaDB: {e}")

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
            StorageError: 添加失败
        """
        try:
            # 验证输入
            if len(ids) != len(documents):
                raise ValueError("ids and documents must have the same length")

            if embeddings.shape[0] != len(ids):
                raise ValueError("embeddings and ids must have the same length")

            # 验证维度（如果指定）
            if self.dimension is not None and embeddings.shape[1] != self.dimension:
                raise ValueError(
                    f"Expected embedding dimension {self.dimension}, "
                    f"got {embeddings.shape[1]}"
                )

            # 转换 numpy 数组为列表（ChromaDB 需要）
            embeddings_list = embeddings.tolist()

            # 准备元数据（ChromaDB 要求非空字典）
            if metadatas is None:
                metadatas = [{"_default": True}] * len(ids)
            elif len(metadatas) != len(ids):
                raise ValueError("metadatas and ids must have the same length")
            else:
                # 确保所有元数据都非空
                metadatas = [m if m else {"_default": True} for m in metadatas]

            # 添加到集合
            self.collection.add(
                ids=ids,
                embeddings=embeddings_list,
                documents=documents,
                metadatas=metadatas
            )

        except Exception as e:
            raise StorageError(f"Failed to add documents: {e}")

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
                - distance: 距离分数

        Raises:
            StorageError: 检索失败
        """
        try:
            # 确保 query_embedding 是 1D 数组
            if query_embedding.ndim != 1:
                raise ValueError("query_embedding must be a 1D array")

            # 验证维度（如果指定）
            if self.dimension is not None and len(query_embedding) != self.dimension:
                raise ValueError(
                    f"Expected embedding dimension {self.dimension}, "
                    f"got {len(query_embedding)}"
                )

            # 转换为列表
            query_list = query_embedding.tolist()

            # 查询
            results = self.collection.query(
                query_embeddings=[query_list],
                n_results=top_k,
                where=where,
                where_document=where_document,
                include=["documents", "metadatas", "distances"]
            )

            # 格式化结果
            formatted_results = []
            if results['ids'] and len(results['ids'][0]) > 0:
                for i in range(len(results['ids'][0])):
                    formatted_results.append({
                        'id': results['ids'][0][i],
                        'document': results['documents'][0][i],
                        'metadata': results['metadatas'][0][i],
                        'distance': results['distances'][0][i]
                    })

            return formatted_results

        except Exception as e:
            raise StorageError(f"Failed to search documents: {e}")

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
            StorageError: 更新失败
        """
        try:
            # 至少需要提供一个更新内容
            if embeddings is None and documents is None and metadatas is None:
                raise ValueError("At least one of embeddings, documents, or metadatas must be provided")

            # 准备更新参数
            update_kwargs = {"ids": ids}

            if embeddings is not None:
                if embeddings.shape[0] != len(ids):
                    raise ValueError("embeddings and ids must have the same length")
                update_kwargs["embeddings"] = embeddings.tolist()

            if documents is not None:
                if len(documents) != len(ids):
                    raise ValueError("documents and ids must have the same length")
                update_kwargs["documents"] = documents

            if metadatas is not None:
                if len(metadatas) != len(ids):
                    raise ValueError("metadatas and ids must have the same length")
                update_kwargs["metadatas"] = metadatas

            # 更新集合
            self.collection.update(**update_kwargs)

        except Exception as e:
            raise StorageError(f"Failed to update documents: {e}")

    async def delete(self, ids: List[str]) -> None:
        """
        删除文档

        Args:
            ids: 要删除的文档ID列表

        Raises:
            StorageError: 删除失败
        """
        try:
            if not ids:
                return

            self.collection.delete(ids=ids)

        except Exception as e:
            raise StorageError(f"Failed to delete documents: {e}")

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
                - embedding: 文档向量

        Raises:
            StorageError: 获取失败
        """
        try:
            if not ids:
                return []

            results = self.collection.get(
                ids=ids,
                include=["documents", "metadatas", "embeddings"]
            )

            # 格式化结果
            formatted_results = []
            if results['ids']:
                for i in range(len(results['ids'])):
                    result = {
                        'id': results['ids'][i],
                        'document': results['documents'][i],
                        'metadata': results['metadatas'][i]
                    }
                    # 检查 embeddings 是否存在且不为空
                    if 'embeddings' in results and results['embeddings'] is not None and len(results['embeddings']) > 0:
                        result['embedding'] = np.array(results['embeddings'][i])
                    formatted_results.append(result)

            return formatted_results

        except Exception as e:
            raise StorageError(f"Failed to get documents: {e}")

    async def count(self) -> int:
        """
        获取向量库中的文档总数

        Returns:
            文档数量

        Raises:
            StorageError: 获取失败
        """
        try:
            return self.collection.count()
        except Exception as e:
            raise StorageError(f"Failed to count documents: {e}")

    async def clear(self) -> None:
        """
        清空向量库

        Raises:
            StorageError: 清空失败
        """
        try:
            # 删除集合
            self.client.delete_collection(name=self.collection_name)

            # 重新创建空集合
            self.collection = self.client.get_or_create_collection(
                name=self.collection_name,
                metadata={"hnsw:space": "cosine"},
                embedding_function=None  # 不使用自动嵌入
            )

        except Exception as e:
            raise StorageError(f"Failed to clear vector store: {e}")

    async def get_stats(self) -> Dict[str, Any]:
        """
        获取向量库统计信息

        Returns:
            统计信息字典，包含:
                - total_documents: 文档总数
                - collection_name: 集合名称
                - dimension: 向量维度
                - storage_path: 存储路径

        Raises:
            StorageError: 获取失败
        """
        try:
            count = await self.count()

            # 获取集合元数据
            metadata = self.collection.metadata or {}

            stats = {
                "total_documents": count,
                "collection_name": self.collection_name,
                "storage_path": str(self.path),
                "distance_metric": metadata.get("hnsw:space", "unknown")
            }

            # 如果有文档，获取第一个文档的维度
            if count > 0:
                # 获取一个文档来确定维度
                sample = self.collection.peek(limit=1)
                if 'embeddings' in sample and sample['embeddings'] is not None and len(sample['embeddings']) > 0:
                    stats["dimension"] = len(sample['embeddings'][0])
            elif self.dimension is not None:
                stats["dimension"] = self.dimension

            return stats

        except Exception as e:
            raise StorageError(f"Failed to get stats: {e}")
