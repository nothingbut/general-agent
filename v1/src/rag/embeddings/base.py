"""
嵌入模型抽象基类
"""
from abc import ABC, abstractmethod
from typing import List, Union
import numpy as np


class EmbeddingProvider(ABC):
    """
    嵌入模型抽象基类

    所有嵌入模型实现都必须继承此类并实现其抽象方法
    """

    @abstractmethod
    async def embed_query(self, text: str) -> np.ndarray:
        """
        嵌入单个查询

        Args:
            text: 查询文本

        Returns:
            查询的向量表示（1维数组）

        Raises:
            EmbeddingError: 嵌入失败
        """
        pass

    @abstractmethod
    async def embed_documents(self, texts: List[str]) -> np.ndarray:
        """
        嵌入多个文档

        Args:
            texts: 文档文本列表

        Returns:
            文档的向量表示（2维数组，shape: [len(texts), dimension]）

        Raises:
            EmbeddingError: 嵌入失败
        """
        pass

    @abstractmethod
    def get_dimension(self) -> int:
        """
        获取向量维度

        Returns:
            向量维度
        """
        pass

    async def embed(self, text: Union[str, List[str]]) -> np.ndarray:
        """
        通用嵌入接口（自动判断单个/批量）

        Args:
            text: 单个文本或文本列表

        Returns:
            向量表示
        """
        if isinstance(text, str):
            return await self.embed_query(text)
        else:
            return await self.embed_documents(text)
