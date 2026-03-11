"""
固定长度分块器
"""
from typing import List, Dict, Any, Optional

from .base import Chunker, Chunk
from ..utils.tokenizer import get_token_counter
from ..exceptions import ChunkingError


class FixedLengthChunker(Chunker):
    """
    固定长度分块器

    按固定 token 数量分块，支持重叠
    """

    def __init__(self, chunk_size: int = 512, overlap: int = 50):
        """
        初始化固定长度分块器

        Args:
            chunk_size: 每块大小（tokens）
            overlap: 重叠大小（tokens）
        """
        if chunk_size <= 0:
            raise ValueError("chunk_size must be positive")
        if overlap < 0:
            raise ValueError("overlap must be non-negative")
        if overlap >= chunk_size:
            raise ValueError("overlap must be less than chunk_size")

        self.chunk_size = chunk_size
        self.overlap = overlap
        self.token_counter = get_token_counter()

    async def chunk(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        """
        按固定长度分块

        Args:
            text: 文档文本
            metadata: 元数据

        Returns:
            Chunk 列表

        Raises:
            ChunkingError: 分块失败
        """
        if not text.strip():
            return []

        try:
            # 将文本编码为 tokens
            tokens = self.token_counter.encode(text)
            total_tokens = len(tokens)

            chunks = []
            start = 0
            chunk_index = 0

            while start < total_tokens:
                # 计算结束位置
                end = min(start + self.chunk_size, total_tokens)

                # 提取块 tokens
                chunk_tokens = tokens[start:end]

                # 解码回文本
                chunk_text = self.token_counter.decode(chunk_tokens)

                # 创建 Chunk 对象
                chunk_metadata = self._inherit_metadata(metadata, {
                    'chunk_index': chunk_index,
                    'chunk_size': len(chunk_tokens),
                    'chunking_strategy': 'fixed_length',
                })

                chunk = Chunk(
                    text=chunk_text,
                    metadata=chunk_metadata,
                    chunk_id=self._create_chunk_id(
                        metadata.get('file_path', 'unknown') if metadata else 'unknown',
                        chunk_index
                    ),
                    start_index=start,
                    end_index=end
                )

                chunks.append(chunk)

                # 移动到下一个块（考虑重叠）
                start = end - self.overlap
                chunk_index += 1

                # 避免无限循环
                if end >= total_tokens:
                    break

            return chunks

        except Exception as e:
            raise ChunkingError(f"Failed to chunk text with fixed length: {e}")

    def __repr__(self) -> str:
        """字符串表示"""
        return f"FixedLengthChunker(chunk_size={self.chunk_size}, overlap={self.overlap})"
