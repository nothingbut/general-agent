"""
Token 计数工具
"""
import tiktoken
from typing import Optional


class TokenCounter:
    """
    Token 计数器

    使用 OpenAI 的 tiktoken 库进行 token 计数
    """

    def __init__(self, encoding_name: str = "cl100k_base"):
        """
        初始化 Token 计数器

        Args:
            encoding_name: 编码名称
                - cl100k_base: GPT-3.5/GPT-4 使用的编码
                - p50k_base: Codex 使用的编码
                - r50k_base: GPT-3 使用的编码
        """
        self.encoding = tiktoken.get_encoding(encoding_name)
        self.encoding_name = encoding_name

    def count(self, text: str) -> int:
        """
        计算文本的 token 数量

        Args:
            text: 文本内容

        Returns:
            Token 数量
        """
        return len(self.encoding.encode(text))

    def encode(self, text: str) -> list[int]:
        """
        将文本编码为 token IDs

        Args:
            text: 文本内容

        Returns:
            Token IDs 列表
        """
        return self.encoding.encode(text)

    def decode(self, tokens: list[int]) -> str:
        """
        将 token IDs 解码为文本

        Args:
            tokens: Token IDs 列表

        Returns:
            文本内容
        """
        return self.encoding.decode(tokens)

    def truncate(self, text: str, max_tokens: int) -> str:
        """
        截断文本到指定 token 数量

        Args:
            text: 文本内容
            max_tokens: 最大 token 数量

        Returns:
            截断后的文本
        """
        tokens = self.encode(text)
        if len(tokens) <= max_tokens:
            return text
        return self.decode(tokens[:max_tokens])


# 默认计数器实例（单例）
_default_counter: Optional[TokenCounter] = None


def get_token_counter() -> TokenCounter:
    """
    获取默认 Token 计数器（单例）

    Returns:
        TokenCounter 实例
    """
    global _default_counter
    if _default_counter is None:
        _default_counter = TokenCounter()
    return _default_counter


def count_tokens(text: str) -> int:
    """
    便捷方法：计算文本的 token 数量

    Args:
        text: 文本内容

    Returns:
        Token 数量
    """
    counter = get_token_counter()
    return counter.count(text)


def truncate_text(text: str, max_tokens: int) -> str:
    """
    便捷方法：截断文本到指定 token 数量

    Args:
        text: 文本内容
        max_tokens: 最大 token 数量

    Returns:
        截断后的文本
    """
    counter = get_token_counter()
    return counter.truncate(text, max_tokens)
