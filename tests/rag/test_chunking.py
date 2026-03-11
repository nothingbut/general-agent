"""分块器测试"""
import pytest
from src.rag.chunking import FixedLengthChunker, SemanticChunker, HybridChunker

@pytest.mark.asyncio
async def test_fixed_chunker():
    """测试固定长度分块"""
    chunker = FixedLengthChunker(chunk_size=50, overlap=10)
    text = "This is a test. " * 20
    chunks = await chunker.chunk(text)
    assert len(chunks) > 1

@pytest.mark.asyncio
async def test_semantic_chunker():
    """测试语义分块"""
    chunker = SemanticChunker()
    text = "# Title\n\nParagraph 1\n\n## Section\n\nParagraph 2"
    chunks = await chunker.chunk(text)
    assert len(chunks) >= 1

@pytest.mark.asyncio
async def test_hybrid_chunker():
    """测试混合分块"""
    chunker = HybridChunker(max_size=100, min_size=20)
    text = "# Title\n\n" + "Content. " * 50
    chunks = await chunker.chunk(text)
    assert len(chunks) >= 1
