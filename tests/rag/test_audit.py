"""审计日志测试"""
import pytest
import tempfile
import shutil
from pathlib import Path
from src.rag.audit import AuditLogger


@pytest.fixture
def temp_dir():
    temp_dir = tempfile.mkdtemp()
    yield temp_dir
    shutil.rmtree(temp_dir, ignore_errors=True)


def test_audit_logger_basic(temp_dir):
    """测试基础审计日志功能"""
    logger = AuditLogger(enabled=True, log_dir=temp_dir)

    logger.log_query(
        query="测试查询",
        results=[{"id": "1", "score": 0.9}],
        stats={"total": 1}
    )

    queries = logger.get_recent_queries(limit=10)
    assert len(queries) == 1
    assert queries[0]["query"] == "测试查询"


def test_audit_logger_disabled(temp_dir):
    """测试禁用审计日志"""
    logger = AuditLogger(enabled=False, log_dir=temp_dir)

    logger.log_query("测试", [], {})
    queries = logger.get_recent_queries()

    assert len(queries) == 0
