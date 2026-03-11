"""
文档分块器抽象基类
"""
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import List, Dict, Any, Optional


@dataclass
class Chunk:
    """
    文档块数据类

    Attributes:
        text: 块内容
        metadata: 元数据字典
        chunk_id: 块标识符
        start_index: 在原文档中的起始位置
        end_index: 在原文档中的结束位置
    """
    text: str
    metadata: Dict[str, Any] = field(default_factory=dict)
    chunk_id: Optional[str] = None
    start_index: int = 0
    end_index: int = 0

    def __post_init__(self) -> None:
        """初始化后处理"""
        if self.end_index == 0:
            self.end_index = self.start_index + len(self.text)

    def __len__(self) -> int:
        """返回块内容长度"""
        return len(self.text)

    def __repr__(self) -> str:
        """字符串表示"""
        text_preview = self.text[:50] + "..." if len(self.text) > 50 else self.text
        return f"Chunk(id={self.chunk_id}, length={len(self.text)}, text={text_preview!r})"


class Chunker(ABC):
    """
    文档分块器抽象基类

    所有分块器都必须继承此类并实现其抽象方法
    """

    @abstractmethod
    async def chunk(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        """
        将文本分块

        Args:
            text: 文档文本
            metadata: 元数据（可选）

        Returns:
            Chunk 列表

        Raises:
            ChunkingError: 分块失败
        """
        pass

    def _create_chunk_id(self, base_id: str, index: int) -> str:
        """
        创建块 ID

        Args:
            base_id: 基础 ID（通常是文档 ID）
            index: 块索引

        Returns:
            块 ID
        """
        return f"{base_id}_chunk_{index}"

    def _inherit_metadata(
        self,
        doc_metadata: Optional[Dict[str, Any]],
        chunk_metadata: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        继承文档元数据到块

        Args:
            doc_metadata: 文档元数据
            chunk_metadata: 块特定元数据

        Returns:
            合并后的元数据
        """
        metadata = {}

        # 继承文档元数据
        if doc_metadata:
            metadata.update(doc_metadata)

        # 添加块特定元数据
        if chunk_metadata:
            metadata.update(chunk_metadata)

        return metadata
