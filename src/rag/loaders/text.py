"""
文本文件加载器
"""
from pathlib import Path
from typing import Dict, Any

from .base import DocumentLoader, Document
from ..exceptions import LoaderError


class TextLoader(DocumentLoader):
    """
    纯文本文件加载器

    支持：
    - 纯文本文件（.txt）
    - UTF-8 编码
    - 基础元数据提取
    """

    SUPPORTED_EXTENSIONS = ['.txt']

    def __init__(self, encoding: str = 'utf-8'):
        """
        初始化文本加载器

        Args:
            encoding: 文件编码（默认 utf-8）
        """
        self.encoding = encoding

    async def load(self, file_path: str) -> Document:
        """
        加载文本文档

        Args:
            file_path: 文本文件路径

        Returns:
            Document 实例

        Raises:
            LoaderError: 加载失败
        """
        path = Path(file_path)

        if not path.exists():
            raise LoaderError(f"File not found: {file_path}")

        if not self.supports(file_path):
            raise LoaderError(f"Unsupported file type: {path.suffix}")

        try:
            # 读取文件内容
            with open(path, 'r', encoding=self.encoding) as f:
                content = f.read()

            # 提取元数据
            metadata = self._extract_metadata(content, path)

            return Document(
                content=content,
                metadata=metadata,
                source=str(path.absolute())
            )

        except UnicodeDecodeError as e:
            raise LoaderError(f"Failed to decode file with {self.encoding}: {e}")
        except Exception as e:
            raise LoaderError(f"Failed to load text file: {e}")

    def supports(self, file_path: str) -> bool:
        """检查是否支持该文件"""
        return Path(file_path).suffix.lower() in self.SUPPORTED_EXTENSIONS

    def _extract_metadata(self, content: str, path: Path) -> Dict[str, Any]:
        """
        提取文本文件元数据

        Args:
            content: 文档内容
            path: 文件路径

        Returns:
            元数据字典
        """
        metadata = self._extract_basic_metadata(str(path))
        metadata['file_type'] = 'text'
        metadata['encoding'] = self.encoding

        # 统计行数
        lines = content.split('\n')
        metadata['lines_count'] = len(lines)

        # 统计非空行数
        non_empty_lines = [line for line in lines if line.strip()]
        metadata['non_empty_lines_count'] = len(non_empty_lines)

        # 统计字符数
        metadata['char_count'] = len(content)

        # 统计单词数（简单分词）
        words = content.split()
        metadata['word_count'] = len(words)

        return metadata
