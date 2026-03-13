"""LLM客户端模块

提供与大语言模型交互的接口。
"""
from typing import List, TypedDict


class ChatMessage(TypedDict):
    """聊天消息类型定义"""
    role: str
    content: str


class LLMClient:
    """LLM客户端基类（占位符）

    未来将支持：
    - OpenAI API (GPT-3.5, GPT-4)
    - Anthropic Claude API
    - 本地模型（通过Ollama等）

    该类目前为占位符，将在后续实现中完成真实的API调用功能。
    """
    pass


class MockLLMClient:
    """模拟LLM客户端

    用于测试和MVP开发的模拟客户端。
    返回简单的回显响应，不实际调用任何LLM API。
    """

    async def chat(self, messages: List[ChatMessage]) -> str:
        """模拟聊天完成

        Args:
            messages: 消息列表，每条消息包含role和content字段
                格式: [{"role": "user", "content": "..."}]

        Returns:
            模拟的响应字符串

        Raises:
            ValueError: 当消息列表为空、内容为空、格式无效或缺少用户消息时
        """
        # 验证消息列表不为空
        if not messages:
            raise ValueError("Messages list cannot be empty")

        # 验证消息格式
        for msg in messages:
            # 检查必需字段
            if "role" not in msg or "content" not in msg:
                raise ValueError("Invalid message format: missing 'role' or 'content' field")

            # 检查字段类型
            if not isinstance(msg["role"], str):
                raise ValueError("Invalid message format: 'role' must be a string")

            if not isinstance(msg["content"], str):
                raise ValueError("Invalid message format: 'content' must be a string")

            # 检查内容不为空
            if not msg["content"].strip():
                raise ValueError("Message content cannot be empty")

        # 查找最后一条用户消息
        user_input = None
        for msg in reversed(messages):
            if msg["role"] == "user":
                user_input = msg["content"]
                break

        # 验证至少有一条用户消息
        if user_input is None:
            raise ValueError("Messages must contain at least one user message")

        # 生成模拟响应
        response = f"我收到了你的消息：「{user_input}」。这是一个模拟响应。"

        return response
