"""
分块器工厂
"""
from ..config import ChunkingConfig
from ..exceptions import ConfigError
from .base import Chunker
from .fixed import FixedLengthChunker
from .semantic import SemanticChunker
from .hybrid import HybridChunker


def create_chunker(config: ChunkingConfig) -> Chunker:
    """
    根据配置创建分块器

    Args:
        config: 分块配置

    Returns:
        Chunker 实例

    Raises:
        ConfigError: 配置错误或不支持的策略
    """
    strategy = config.strategy.lower()

    if strategy == "fixed":
        return FixedLengthChunker(
            chunk_size=config.max_size,
            overlap=config.overlap
        )
    elif strategy == "semantic":
        return SemanticChunker(
            max_heading_level=config.semantic.markdown_max_heading_level,
            preserve_code_blocks=config.semantic.preserve_code_blocks,
            preserve_tables=config.semantic.preserve_tables
        )
    elif strategy == "hybrid":
        return HybridChunker(
            max_size=config.max_size,
            min_size=config.min_size,
            overlap=config.overlap,
            semantic_config={
                'max_heading_level': config.semantic.markdown_max_heading_level,
                'preserve_code_blocks': config.semantic.preserve_code_blocks,
                'preserve_tables': config.semantic.preserve_tables
            }
        )
    else:
        raise ConfigError(f"Unsupported chunking strategy: {strategy}")
