"""Ollama LLM 客户端"""
import httpx
from typing import List, Optional
from dataclasses import dataclass
from .llm_client import ChatMessage


@dataclass
class OllamaConfig:
    """Ollama 配置"""
    base_url: str = "http://localhost:11434"
    model: str = "qwen2.5:7b"
    temperature: float = 0.7
    timeout: float = 120.0


class OllamaClient:
    """Ollama LLM 客户端"""

    def __init__(
        self,
        base_url: str = "http://localhost:11434",
        model: str = "qwen2.5:7b",
        timeout: int = 120,
        config: Optional[OllamaConfig] = None
    ):
        if config:
            self.base_url = config.base_url.rstrip("/")
            self.model = config.model
            self.timeout = int(config.timeout)
        else:
            self.base_url = base_url.rstrip("/")
            self.model = model
            self.timeout = timeout

    async def chat(self, messages: List[ChatMessage]) -> str:
        """聊天完成"""
        if not messages:
            raise ValueError("Messages cannot be empty")

        payload = {
            "model": self.model,
            "messages": messages,
            "stream": False
        }

        async with httpx.AsyncClient(timeout=self.timeout) as client:
            try:
                response = await client.post(
                    f"{self.base_url}/api/chat",
                    json=payload
                )
                response.raise_for_status()
                result = response.json()
                return result["message"]["content"]
            except httpx.ConnectError:
                raise ConnectionError(
                    f"无法连接到 Ollama ({self.base_url})。请确保已启动：ollama serve"
                )
            except httpx.TimeoutException:
                raise TimeoutError(f"请求超时 ({self.timeout}s)")
            except Exception as e:
                raise RuntimeError(f"Ollama 失败: {e}")
