"""测试LLM客户端"""
import pytest
from src.core.llm_client import MockLLMClient


@pytest.mark.asyncio
async def test_mock_llm_response() -> None:
    """测试基本响应功能"""
    client = MockLLMClient()

    messages = [
        {"role": "user", "content": "你好"}
    ]

    response = await client.chat(messages)

    # 响应应该是字符串类型
    assert isinstance(response, str)
    # 响应不应该为空
    assert len(response) > 0
    # 响应应该包含模拟响应的标识
    assert "模拟响应" in response


@pytest.mark.asyncio
async def test_mock_llm_echo() -> None:
    """测试回显行为包含用户输入"""
    client = MockLLMClient()

    user_input = "请帮我写一个Python函数"
    messages = [
        {"role": "user", "content": user_input}
    ]

    response = await client.chat(messages)

    # 响应应该包含用户输入
    assert user_input in response
    # 响应应该包含接收消息的提示
    assert "我收到了你的消息" in response


@pytest.mark.asyncio
async def test_mock_llm_empty_messages() -> None:
    """测试空消息列表应抛出ValueError"""
    client = MockLLMClient()

    with pytest.raises(ValueError, match="Messages list cannot be empty"):
        await client.chat([])


@pytest.mark.asyncio
async def test_mock_llm_empty_content() -> None:
    """测试空内容应抛出ValueError"""
    client = MockLLMClient()

    messages = [
        {"role": "user", "content": ""}
    ]

    with pytest.raises(ValueError, match="Message content cannot be empty"):
        await client.chat(messages)

    # 测试只有空白字符
    messages = [
        {"role": "user", "content": "   "}
    ]

    with pytest.raises(ValueError, match="Message content cannot be empty"):
        await client.chat(messages)


@pytest.mark.asyncio
async def test_mock_llm_invalid_message_format() -> None:
    """测试无效消息格式应抛出ValueError"""
    client = MockLLMClient()

    # 缺少role字段
    messages = [
        {"content": "Hello"}
    ]

    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat(messages)

    # 缺少content字段
    messages = [
        {"role": "user"}
    ]

    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat(messages)

    # role不是字符串
    messages = [
        {"role": 123, "content": "Hello"}
    ]

    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat(messages)

    # content不是字符串
    messages = [
        {"role": "user", "content": 123}
    ]

    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat(messages)


@pytest.mark.asyncio
async def test_mock_llm_multi_message_history() -> None:
    """测试多条消息历史记录"""
    client = MockLLMClient()

    messages = [
        {"role": "user", "content": "第一条消息"},
        {"role": "assistant", "content": "第一条回复"},
        {"role": "user", "content": "第二条消息"}
    ]

    response = await client.chat(messages)

    # 应该基于最后一条用户消息
    assert "第二条消息" in response
    assert isinstance(response, str)


@pytest.mark.asyncio
async def test_mock_llm_system_message() -> None:
    """测试包含系统消息的对话"""
    client = MockLLMClient()

    messages = [
        {"role": "system", "content": "你是一个有帮助的助手"},
        {"role": "user", "content": "你好"}
    ]

    response = await client.chat(messages)

    # 应该正常处理
    assert isinstance(response, str)
    assert "你好" in response


@pytest.mark.asyncio
async def test_mock_llm_no_user_messages() -> None:
    """测试没有用户消息应抛出ValueError"""
    client = MockLLMClient()

    # 只有系统消息
    messages = [
        {"role": "system", "content": "你是一个有帮助的助手"}
    ]

    with pytest.raises(ValueError, match="Messages must contain at least one user message"):
        await client.chat(messages)

    # 只有助手消息
    messages = [
        {"role": "assistant", "content": "我是助手"}
    ]

    with pytest.raises(ValueError, match="Messages must contain at least one user message"):
        await client.chat(messages)

    # 系统和助手消息，但没有用户消息
    messages = [
        {"role": "system", "content": "你是一个有帮助的助手"},
        {"role": "assistant", "content": "我准备好了"}
    ]

    with pytest.raises(ValueError, match="Messages must contain at least one user message"):
        await client.chat(messages)
