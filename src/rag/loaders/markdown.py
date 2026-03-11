"""
Markdown 文档加载器
"""
import re
from pathlib import Path
from typing import Dict, Any

from .base import DocumentLoader, Document
from ..exceptions import LoaderError


class MarkdownLoader(DocumentLoader):
    """
    Markdown 文档加载器

    支持：
    - 标准 Markdown 文件（.md）
    - 元数据提取（标题、链接等）
    - UTF-8 编码
    """

    SUPPORTED_EXTENSIONS = ['.md', '.markdown']

    async def load(self, file_path: str) -> Document:
        """
        加载 Markdown 文档

        Args:
            file_path: Markdown 文件路径

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
            with open(path, 'r', encoding='utf-8') as f:
                content = f.read()

            # 提取元数据
            metadata = self._extract_metadata(content, path)

            return Document(
                content=content,
                metadata=metadata,
                source=str(path.absolute())
            )

        except Exception as e:
            raise LoaderError(f"Failed to load markdown file: {e}")

    def supports(self, file_path: str) -> bool:
        """检查是否支持该文件"""
        return Path(file_path).suffix.lower() in self.SUPPORTED_EXTENSIONS

    def _extract_metadata(self, content: str, path: Path) -> Dict[str, Any]:
        """
        提取 Markdown 元数据

        Args:
            content: 文档内容
            path: 文件路径

        Returns:
            元数据字典
        """
        metadata = self._extract_basic_metadata(str(path))
        metadata['file_type'] = 'markdown'

        # 提取一级标题
        title_match = re.search(r'^#\s+(.+)$', content, re.MULTILINE)
        if title_match:
            metadata['title'] = title_match.group(1).strip()

        # 提取所有标题（用于统计）
        headings = re.findall(r'^#{1,6}\s+(.+)$', content, re.MULTILINE)
        metadata['headings_count'] = len(headings)

        # 提取链接数量
        links = re.findall(r'\[([^\]]+)\]\(([^\)]+)\)', content)
        metadata['links_count'] = len(links)

        # 提取代码块数量
        code_blocks = re.findall(r'```[\s\S]*?```', content)
        metadata['code_blocks_count'] = len(code_blocks)

        # 计算行数
        metadata['lines_count'] = len(content.split('\n'))

        return metadata
