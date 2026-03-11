"""pytest配置和共享fixtures"""
import pytest
import asyncio
from pathlib import Path


@pytest.fixture(scope="session")
def event_loop():
    """创建事件循环"""
    loop = asyncio.get_event_loop_policy().new_event_loop()
    yield loop
    loop.close()


@pytest.fixture
def test_data_dir(tmp_path: Path) -> Path:
    """测试数据目录"""
    return tmp_path / "data"


@pytest.fixture
def test_db_path(test_data_dir: Path) -> Path:
    """测试数据库路径"""
    test_data_dir.mkdir(parents=True, exist_ok=True)
    return test_data_dir / "test.db"
