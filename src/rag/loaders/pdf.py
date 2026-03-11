"""
PDF 文档加载器
"""
from pathlib import Path
from typing import Dict, Any

from pypdf import PdfReader

from .base import DocumentLoader, Document
from ..exceptions import LoaderError


class PDFLoader(DocumentLoader):
    """
    PDF 文档加载器

    支持：
    - 标准 PDF 文件（.pdf）
    - 文本提取
    - 元数据提取（标题、作者、页数等）
    """

    SUPPORTED_EXTENSIONS = ['.pdf']

    async def load(self, file_path: str) -> Document:
        """
        加载 PDF 文档

        Args:
            file_path: PDF 文件路径

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
            # 读取 PDF
            reader = PdfReader(str(path))

            # 提取所有页面的文本
            pages_text = []
            for page in reader.pages:
                text = page.extract_text()
                if text:
                    pages_text.append(text)

            content = '\n\n'.join(pages_text)

            # 提取元数据
            metadata = self._extract_metadata(reader, path)

            return Document(
                content=content,
                metadata=metadata,
                source=str(path.absolute())
            )

        except Exception as e:
            raise LoaderError(f"Failed to load PDF file: {e}")

    def supports(self, file_path: str) -> bool:
        """检查是否支持该文件"""
        return Path(file_path).suffix.lower() in self.SUPPORTED_EXTENSIONS

    def _extract_metadata(self, reader: PdfReader, path: Path) -> Dict[str, Any]:
        """
        提取 PDF 元数据

        Args:
            reader: PDF 阅读器
            path: 文件路径

        Returns:
            元数据字典
        """
        metadata = self._extract_basic_metadata(str(path))
        metadata['file_type'] = 'pdf'

        # 基础信息
        metadata['page_count'] = len(reader.pages)

        # PDF 元数据
        pdf_metadata = reader.metadata
        if pdf_metadata:
            # 标题
            if pdf_metadata.title:
                metadata['title'] = pdf_metadata.title

            # 作者
            if pdf_metadata.author:
                metadata['author'] = pdf_metadata.author

            # 主题
            if pdf_metadata.subject:
                metadata['subject'] = pdf_metadata.subject

            # 创建日期
            if pdf_metadata.creation_date:
                metadata['creation_date'] = str(pdf_metadata.creation_date)

            # 修改日期
            if pdf_metadata.modification_date:
                metadata['modification_date'] = str(pdf_metadata.modification_date)

        return metadata
