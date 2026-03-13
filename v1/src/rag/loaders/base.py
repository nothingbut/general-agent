"""
文档加载器抽象基类
"""
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Any, Optional


@dataclass
class Document:
    """
    文档数据类

    Attributes:
        content: 文档内容
        metadata: 元数据字典
        source: 文档来源路径
        doc_id: 文档唯一标识（可选）
    """
    content: str
    metadata: Dict[str, Any] = field(default_factory=dict)
    source: str = ""
    doc_id: Optional[str] = None

    def __post_init__(self) -> None:
        """初始化后处理"""
        # 如果没有 doc_id，使用 source 作为标识
        if not self.doc_id and self.source:
            self.doc_id = self.source

    def __len__(self) -> int:
        """返回文档内容长度"""
        return len(self.content)

    def __repr__(self) -> str:
        """字符串表示"""
        content_preview = self.content[:50] + "..." if len(self.content) > 50 else self.content
        return f"Document(source={self.source}, length={len(self.content)}, content={content_preview!r})"


class DocumentLoader(ABC):
    """
    文档加载器抽象基类

    所有文档加载器都必须继承此类并实现其抽象方法
    """

    @abstractmethod
    async def load(self, file_path: str) -> Document:
        """
        加载单个文档

        Args:
            file_path: 文件路径

        Returns:
            Document 实例

        Raises:
            LoaderError: 加载失败
        """
        pass

    @abstractmethod
    def supports(self, file_path: str) -> bool:
        """
        检查是否支持该文件类型

        Args:
            file_path: 文件路径

        Returns:
            是否支持
        """
        pass

    async def load_batch(self, file_paths: list[str]) -> list[Document]:
        """
        批量加载文档

        Args:
            file_paths: 文件路径列表

        Returns:
            Document 列表
        """
        documents = []
        for path in file_paths:
            if self.supports(path):
                doc = await self.load(path)
                documents.append(doc)
        return documents

    def _extract_basic_metadata(self, file_path: str) -> Dict[str, Any]:
        """
        提取基础文件元数据

        Args:
            file_path: 文件路径

        Returns:
            元数据字典
        """
        path = Path(file_path)

        metadata = {
            'file_name': path.name,
            'file_path': str(path.absolute()),
            'file_size': path.stat().st_size,
            'file_extension': path.suffix,
        }

        return metadata
