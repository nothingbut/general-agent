"""
混合分块器
"""
from typing import List, Dict, Any, Optional

from .base import Chunker, Chunk
from .semantic import SemanticChunker
from .fixed import FixedLengthChunker
from ..utils.tokenizer import count_tokens
from ..exceptions import ChunkingError


class HybridChunker(Chunker):
    """
    混合分块器

    策略：
    1. 先按语义分块（标题/段落）
    2. 检查每个块的大小
    3. 超长块 → 固定长度切分
    4. 超短块 → 与相邻块合并
    5. 合适块 → 保持不变
    """

    def __init__(
        self,
        max_size: int = 512,
        min_size: int = 100,
        overlap: int = 50,
        semantic_config: Optional[Dict[str, Any]] = None
    ):
        """
        初始化混合分块器

        Args:
            max_size: 最大块大小（tokens）
            min_size: 最小块大小（tokens）
            overlap: 重叠大小（tokens，用于切分超长块）
            semantic_config: 语义分块器配置
        """
        self.max_size = max_size
        self.min_size = min_size
        self.overlap = overlap

        # 初始化子分块器
        semantic_config = semantic_config or {}
        self.semantic_chunker = SemanticChunker(**semantic_config)
        self.fixed_chunker = FixedLengthChunker(
            chunk_size=max_size,
            overlap=overlap
        )

    async def chunk(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        """
        混合分块

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
            # Step 1: 语义分块
            semantic_chunks = await self.semantic_chunker.chunk(text, metadata)

            if not semantic_chunks:
                return []

            # Step 2: 调整块大小
            final_chunks = []
            buffer: List[Chunk] = []

            for chunk in semantic_chunks:
                size = count_tokens(chunk.text)

                if size > self.max_size:
                    # 超长块：固定长度切分
                    # 先清空缓冲区
                    if buffer:
                        merged = await self._merge_chunks(buffer)
                        final_chunks.append(merged)
                        buffer = []

                    # 切分超长块
                    sub_chunks = await self.fixed_chunker.chunk(chunk.text, chunk.metadata)
                    final_chunks.extend(sub_chunks)

                elif size < self.min_size:
                    # 超短块：加入缓冲区
                    buffer.append(chunk)

                    # 检查缓冲区大小
                    buffer_size = sum(count_tokens(c.text) for c in buffer)
                    if buffer_size >= self.min_size:
                        # 合并缓冲区
                        merged = await self._merge_chunks(buffer)
                        final_chunks.append(merged)
                        buffer = []

                else:
                    # 合适大小
                    # 先清空缓冲区
                    if buffer:
                        buffer.append(chunk)
                        merged = await self._merge_chunks(buffer)
                        final_chunks.append(merged)
                        buffer = []
                    else:
                        final_chunks.append(chunk)

            # 处理剩余缓冲区
            if buffer:
                merged = await self._merge_chunks(buffer)
                final_chunks.append(merged)

            # 重新编号
            for i, chunk in enumerate(final_chunks):
                chunk.metadata['chunk_index'] = i
                chunk.metadata['chunking_strategy'] = 'hybrid'
                chunk.chunk_id = self._create_chunk_id(
                    metadata.get('file_path', 'unknown') if metadata else 'unknown',
                    i
                )

            return final_chunks

        except Exception as e:
            raise ChunkingError(f"Failed to chunk text with hybrid strategy: {e}")

    async def _merge_chunks(self, chunks: List[Chunk]) -> Chunk:
        """
        合并多个块

        Args:
            chunks: 块列表

        Returns:
            合并后的块
        """
        if not chunks:
            raise ValueError("Cannot merge empty chunk list")

        if len(chunks) == 1:
            return chunks[0]

        # 合并文本
        merged_text = '\n\n'.join(chunk.text for chunk in chunks)

        # 合并元数据
        metadata = chunks[0].metadata.copy()
        metadata['merged_from'] = [c.chunk_id for c in chunks]
        metadata['merged_count'] = len(chunks)

        # 创建合并后的块
        return Chunk(
            text=merged_text,
            metadata=metadata,
            chunk_id=chunks[0].chunk_id,  # 使用第一个块的 ID
            start_index=chunks[0].start_index,
            end_index=chunks[-1].end_index
        )

    def __repr__(self) -> str:
        """字符串表示"""
        return (
            f"HybridChunker(max_size={self.max_size}, "
            f"min_size={self.min_size}, overlap={self.overlap})"
        )
