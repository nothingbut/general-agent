"""
BGE (BAAI General Embedding) 嵌入模型实现
"""
from typing import List
import numpy as np
from sentence_transformers import SentenceTransformer

from .base import EmbeddingProvider
from ..exceptions import EmbeddingError


class BGEEmbedding(EmbeddingProvider):
    """
    BGE 嵌入模型实现

    BGE 是北京智源研究院（BAAI）开发的中文优化嵌入模型。
    特点：
    - 中文效果优秀
    - 查询和文档使用不同的编码方式（查询需要前缀）
    - 支持 768 维向量

    使用方法：
        >>> embedding = BGEEmbedding()
        >>> query_vec = await embedding.embed_query("如何配置 FastAPI？")
        >>> doc_vecs = await embedding.embed_documents(["文档1", "文档2"])
    """

    # BGE 查询前缀（重要！必须添加）
    QUERY_PREFIX = "为这个句子生成表示以用于检索相关文章："

    def __init__(
        self,
        model_name: str = "BAAI/bge-base-zh-v1.5",
        device: str = "cpu",
        normalize: bool = True,
        batch_size: int = 32
    ):
        """
        初始化 BGE 嵌入模型

        Args:
            model_name: 模型名称（HuggingFace 路径）
            device: 运行设备 (cpu | cuda)
            normalize: 是否归一化向量
            batch_size: 批量处理大小

        Raises:
            EmbeddingError: 模型加载失败
        """
        try:
            self.model = SentenceTransformer(model_name, device=device)
            self.normalize = normalize
            self.batch_size = batch_size
            self.device = device

            # 获取实际维度（应该是 768）
            test_vec = self.model.encode("test", normalize_embeddings=False)
            self._dimension = test_vec.shape[0]

        except Exception as e:
            raise EmbeddingError(f"Failed to load BGE model: {e}")

    async def embed_query(self, text: str) -> np.ndarray:
        """
        嵌入查询（需要添加前缀）

        重要：BGE 模型要求查询和文档使用不同的编码方式。
        查询必须添加特定前缀，文档则不需要。

        Args:
            text: 查询文本

        Returns:
            查询向量（1维，shape: [dimension]）

        Raises:
            EmbeddingError: 嵌入失败
        """
        try:
            # 添加查询前缀
            text_with_prefix = self.QUERY_PREFIX + text

            # 编码
            embedding = self.model.encode(
                text_with_prefix,
                normalize_embeddings=self.normalize,
                show_progress_bar=False
            )

            return embedding

        except Exception as e:
            raise EmbeddingError(f"Failed to embed query: {e}")

    async def embed_documents(self, texts: List[str]) -> np.ndarray:
        """
        嵌入文档（不需要前缀）

        Args:
            texts: 文档文本列表

        Returns:
            文档向量（2维，shape: [len(texts), dimension]）

        Raises:
            EmbeddingError: 嵌入失败
        """
        if not texts:
            raise EmbeddingError("Cannot embed empty document list")

        try:
            # 编码（文档不需要前缀）
            embeddings = self.model.encode(
                texts,
                normalize_embeddings=self.normalize,
                batch_size=self.batch_size,
                show_progress_bar=len(texts) > 100  # 大量文档显示进度
            )

            return embeddings

        except Exception as e:
            raise EmbeddingError(f"Failed to embed documents: {e}")

    def get_dimension(self) -> int:
        """
        获取向量维度

        Returns:
            向量维度（768）
        """
        return self._dimension

    def __repr__(self) -> str:
        """字符串表示"""
        return (
            f"BGEEmbedding(dimension={self._dimension}, "
            f"device={self.device}, normalize={self.normalize})"
        )
