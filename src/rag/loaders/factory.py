"""
文档加载器工厂
"""
from pathlib import Path
from typing import Optional

from .base import DocumentLoader
from .markdown import MarkdownLoader
from .text import TextLoader
from .pdf import PDFLoader
from ..exceptions import LoaderError


class LoaderFactory:
    """
    文档加载器工厂

    根据文件类型自动选择合适的加载器
    """

    def __init__(self) -> None:
        """初始化工厂，注册所有加载器"""
        self._loaders: list[DocumentLoader] = [
            MarkdownLoader(),
            TextLoader(),
            PDFLoader(),
        ]

    def get_loader(self, file_path: str) -> DocumentLoader:
        """
        根据文件路径获取合适的加载器

        Args:
            file_path: 文件路径

        Returns:
            DocumentLoader 实例

        Raises:
            LoaderError: 没有支持该文件类型的加载器
        """
        for loader in self._loaders:
            if loader.supports(file_path):
                return loader

        ext = Path(file_path).suffix
        raise LoaderError(f"No loader available for file type: {ext}")

    def register_loader(self, loader: DocumentLoader) -> None:
        """
        注册自定义加载器

        Args:
            loader: 文档加载器实例
        """
        self._loaders.append(loader)

    def get_supported_extensions(self) -> set[str]:
        """
        获取所有支持的文件扩展名

        Returns:
            扩展名集合
        """
        extensions = set()
        for loader in self._loaders:
            if hasattr(loader, 'SUPPORTED_EXTENSIONS'):
                extensions.update(loader.SUPPORTED_EXTENSIONS)
        return extensions


# 默认工厂实例
_default_factory: Optional[LoaderFactory] = None


def get_loader_factory() -> LoaderFactory:
    """
    获取默认加载器工厂（单例）

    Returns:
        LoaderFactory 实例
    """
    global _default_factory
    if _default_factory is None:
        _default_factory = LoaderFactory()
    return _default_factory


def create_loader(file_path: str) -> DocumentLoader:
    """
    便捷方法：根据文件路径创建加载器

    Args:
        file_path: 文件路径

    Returns:
        DocumentLoader 实例

    Raises:
        LoaderError: 没有支持该文件类型的加载器
    """
    factory = get_loader_factory()
    return factory.get_loader(file_path)
