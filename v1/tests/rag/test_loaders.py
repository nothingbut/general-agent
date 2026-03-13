"""文档加载器测试"""
import pytest
from pathlib import Path
from src.rag.loaders import MarkdownLoader, TextLoader, create_loader, Document

@pytest.mark.asyncio
async def test_markdown_loader():
    """测试 Markdown 加载器"""
    test_file = Path("tests/fixtures/test.md")
    test_file.parent.mkdir(exist_ok=True)
    test_file.write_text("# Test\n\nContent")
    
    loader = MarkdownLoader()
    doc = await loader.load(str(test_file))
    
    assert isinstance(doc, Document)
    assert "Test" in doc.content
    assert doc.metadata['file_type'] == 'markdown'
    test_file.unlink()

@pytest.mark.asyncio
async def test_text_loader():
    """测试文本加载器"""
    test_file = Path("tests/fixtures/test.txt")
    test_file.parent.mkdir(exist_ok=True)
    test_file.write_text("Test content")
    
    loader = TextLoader()
    doc = await loader.load(str(test_file))
    
    assert doc.content == "Test content"
    assert doc.metadata['file_type'] == 'text'
    test_file.unlink()

@pytest.mark.asyncio
async def test_create_loader():
    """测试工厂方法"""
    loader = create_loader("test.md")
    assert isinstance(loader, MarkdownLoader)
