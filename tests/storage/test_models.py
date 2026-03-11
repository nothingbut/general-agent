"""测试数据模型"""
from datetime import datetime
from src.storage.models import Message, Session


def test_message_creation():
    """测试创建消息对象"""
    msg = Message(
        id="msg-1",
        session_id="sess-1",
        role="user",
        content="Hello",
        timestamp=datetime.now()
    )
    assert msg.id == "msg-1"
    assert msg.role == "user"
    assert msg.content == "Hello"


def test_session_creation():
    """测试创建会话对象"""
    session = Session(
        id="sess-1",
        title="Test Session",
        created_at=datetime.now(),
        updated_at=datetime.now()
    )
    assert session.id == "sess-1"
    assert session.title == "Test Session"


def test_message_to_dict():
    """测试消息序列化"""
    msg = Message(
        id="msg-1",
        session_id="sess-1",
        role="assistant",
        content="Hi there",
        timestamp=datetime.now()
    )
    data = msg.to_dict()
    assert data["id"] == "msg-1"
    assert data["role"] == "assistant"
    assert "timestamp" in data
