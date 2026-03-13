"""
语义分块器
"""
import re
from typing import List, Dict, Any, Optional

from .base import Chunker, Chunk
from ..exceptions import ChunkingError


class SemanticChunker(Chunker):
    """
    语义分块器

    按语义单元分块（如 Markdown 标题、段落等）
    """

    def __init__(
        self,
        max_heading_level: int = 3,
        preserve_code_blocks: bool = True,
        preserve_tables: bool = True
    ):
        """
        初始化语义分块器

        Args:
            max_heading_level: 最大标题层级（1-6）
            preserve_code_blocks: 保持代码块完整
            preserve_tables: 保持表格完整
        """
        self.max_heading_level = max_heading_level
        self.preserve_code_blocks = preserve_code_blocks
        self.preserve_tables = preserve_tables

    async def chunk(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        """
        按语义分块

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
            # 检测文档类型
            if self._is_markdown(text):
                return await self._chunk_markdown(text, metadata)
            else:
                return await self._chunk_plain_text(text, metadata)

        except Exception as e:
            raise ChunkingError(f"Failed to chunk text semantically: {e}")

    def _is_markdown(self, text: str) -> bool:
        """检测是否是 Markdown 文档"""
        # 简单检测：查找 Markdown 标题
        return bool(re.search(r'^#{1,6}\s+', text, re.MULTILINE))

    async def _chunk_markdown(
        self,
        text: str,
        metadata: Optional[Dict[str, Any]]
    ) -> List[Chunk]:
        """
        按 Markdown 标题分块

        Args:
            text: Markdown 文本
            metadata: 元数据

        Returns:
            Chunk 列表
        """
        chunks = []
        chunk_index = 0

        # 按标题切分
        # 匹配 # 到 ### 标题（根据 max_heading_level）
        heading_pattern = f'^#{{1,{self.max_heading_level}}}\\s+'
        sections = re.split(f'({heading_pattern})', text, flags=re.MULTILINE)

        current_chunk = ""
        current_heading = None

        for i, section in enumerate(sections):
            # 跳过空部分
            if not section.strip():
                continue

            # 检查是否是标题
            if re.match(heading_pattern, section, re.MULTILINE):
                # 如果有当前块，先保存
                if current_chunk.strip():
                    chunk_metadata = self._inherit_metadata(metadata, {
                        'chunk_index': chunk_index,
                        'heading': current_heading,
                        'chunking_strategy': 'semantic_markdown',
                    })

                    chunk = Chunk(
                        text=current_chunk.strip(),
                        metadata=chunk_metadata,
                        chunk_id=self._create_chunk_id(
                            metadata.get('file_path', 'unknown') if metadata else 'unknown',
                            chunk_index
                        )
                    )
                    chunks.append(chunk)
                    chunk_index += 1

                # 开始新块
                current_heading = section.strip()
                current_chunk = section
            else:
                # 添加到当前块
                current_chunk += section

        # 保存最后一个块
        if current_chunk.strip():
            chunk_metadata = self._inherit_metadata(metadata, {
                'chunk_index': chunk_index,
                'heading': current_heading,
                'chunking_strategy': 'semantic_markdown',
            })

            chunk = Chunk(
                text=current_chunk.strip(),
                metadata=chunk_metadata,
                chunk_id=self._create_chunk_id(
                    metadata.get('file_path', 'unknown') if metadata else 'unknown',
                    chunk_index
                )
            )
            chunks.append(chunk)

        return chunks

    async def _chunk_plain_text(
        self,
        text: str,
        metadata: Optional[Dict[str, Any]]
    ) -> List[Chunk]:
        """
        按段落分块（纯文本）

        Args:
            text: 纯文本
            metadata: 元数据

        Returns:
            Chunk 列表
        """
        chunks = []
        chunk_index = 0

        # 按双换行符切分段落
        paragraphs = re.split(r'\n\n+', text)

        for paragraph in paragraphs:
            if not paragraph.strip():
                continue

            chunk_metadata = self._inherit_metadata(metadata, {
                'chunk_index': chunk_index,
                'chunking_strategy': 'semantic_paragraph',
            })

            chunk = Chunk(
                text=paragraph.strip(),
                metadata=chunk_metadata,
                chunk_id=self._create_chunk_id(
                    metadata.get('file_path', 'unknown') if metadata else 'unknown',
                    chunk_index
                )
            )
            chunks.append(chunk)
            chunk_index += 1

        return chunks

    def __repr__(self) -> str:
        """字符串表示"""
        return f"SemanticChunker(max_heading_level={self.max_heading_level})"
