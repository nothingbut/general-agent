"""
嵌入模型工厂
"""
from ..config import EmbeddingConfig
from ..exceptions import ConfigError
from .base import EmbeddingProvider
from .bge import BGEEmbedding


def create_embedding_provider(config: EmbeddingConfig) -> EmbeddingProvider:
    """
    根据配置创建嵌入模型提供者

    Args:
        config: 嵌入模型配置

    Returns:
        嵌入模型实例

    Raises:
        ConfigError: 配置错误或不支持的提供者类型
    """
    provider = config.provider.lower()

    if provider == "bge":
        return BGEEmbedding(
            model_name=config.model,
            device=config.device,
            normalize=config.normalize,
            batch_size=config.batch_size
        )
    # 未来可扩展其他提供者
    # elif provider == "multilingual":
    #     return MultilingualEmbedding(...)
    # elif provider == "openai":
    #     return OpenAIEmbedding(...)
    else:
        raise ConfigError(f"Unsupported embedding provider: {provider}")
