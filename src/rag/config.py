"""
RAG 配置加载模块
"""
from pathlib import Path
from typing import Any, List
import yaml
from pydantic import BaseModel, Field, field_validator


class EmbeddingConfig(BaseModel):
    """嵌入模型配置"""
    provider: str = "bge"
    model: str = "BAAI/bge-base-zh-v1.5"
    dimension: int = 768
    device: str = "cpu"
    batch_size: int = 32
    normalize: bool = True

    @field_validator("device")
    @classmethod
    def validate_device(cls, v: str) -> str:
        """验证设备类型"""
        if v not in ["cpu", "cuda"]:
            raise ValueError("device must be 'cpu' or 'cuda'")
        return v


class VectorStoreConfig(BaseModel):
    """向量存储配置"""
    type: str = "chromadb"
    path: str = "data/rag/vector_db"
    collection_name: str = "general_agent_docs"

    @field_validator("type")
    @classmethod
    def validate_type(cls, v: str) -> str:
        """验证存储类型"""
        if v not in ["chromadb"]:  # 未来可扩展: faiss, qdrant
            raise ValueError("type must be 'chromadb'")
        return v


class SemanticChunkingConfig(BaseModel):
    """语义分块特定配置"""
    markdown_max_heading_level: int = 3
    preserve_code_blocks: bool = True
    preserve_tables: bool = True


class ChunkingConfig(BaseModel):
    """文档分块配置"""
    strategy: str = "hybrid"
    max_size: int = 512
    min_size: int = 100
    overlap: int = 50
    semantic: SemanticChunkingConfig = Field(default_factory=SemanticChunkingConfig)

    @field_validator("strategy")
    @classmethod
    def validate_strategy(cls, v: str) -> str:
        """验证分块策略"""
        if v not in ["fixed", "semantic", "hybrid"]:
            raise ValueError("strategy must be 'fixed', 'semantic', or 'hybrid'")
        return v

    @field_validator("max_size")
    @classmethod
    def validate_max_size(cls, v: int) -> int:
        """验证最大块大小"""
        if v < 100 or v > 2000:
            raise ValueError("max_size must be between 100 and 2000")
        return v


class RetrievalConfig(BaseModel):
    """检索配置"""
    top_k: int = 5
    similarity_threshold: float = 0.7
    max_context_tokens: int = 2000
    enable_metadata_filter: bool = True

    @field_validator("similarity_threshold")
    @classmethod
    def validate_threshold(cls, v: float) -> float:
        """验证相似度阈值"""
        if v < 0.0 or v > 1.0:
            raise ValueError("similarity_threshold must be between 0.0 and 1.0")
        return v


class IndexingConfig(BaseModel):
    """索引配置"""
    batch_size: int = 100
    show_progress: bool = True
    recursive: bool = True
    supported_extensions: List[str] = Field(default=[".md", ".txt", ".pdf"])


class AutoRulesConfig(BaseModel):
    """自动检测规则配置"""
    question_words: bool = True
    knowledge_keywords: bool = True
    similarity_check: bool = True
    min_similarity: float = 0.6


class RoutingConfig(BaseModel):
    """路由配置"""
    explicit_syntax: bool = True
    auto_detection: bool = False
    auto_rules: AutoRulesConfig = Field(default_factory=AutoRulesConfig)


class AuditConfig(BaseModel):
    """审计日志配置"""
    enabled: bool = True
    log_queries: bool = True
    log_results: bool = True


class RAGConfig(BaseModel):
    """RAG 总配置"""
    enabled: bool = True
    auto_mode: bool = False
    vector_store: VectorStoreConfig
    embedding: EmbeddingConfig
    chunking: ChunkingConfig
    retrieval: RetrievalConfig
    indexing: IndexingConfig = Field(default_factory=IndexingConfig)
    routing: RoutingConfig = Field(default_factory=RoutingConfig)
    audit: AuditConfig = Field(default_factory=AuditConfig)

    @classmethod
    def load(cls, config_path: str = "config/rag_config.yaml") -> "RAGConfig":
        """
        从 YAML 文件加载配置

        Args:
            config_path: 配置文件路径

        Returns:
            RAGConfig 实例

        Raises:
            FileNotFoundError: 配置文件不存在
            ValueError: 配置格式错误
        """
        path = Path(config_path)
        if not path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")

        with open(path, 'r', encoding='utf-8') as f:
            data = yaml.safe_load(f)

        if 'rag' not in data:
            raise ValueError("Config file must contain 'rag' key")

        return cls(**data['rag'])

    def get_data_path(self) -> Path:
        """获取数据目录路径"""
        return Path(self.vector_store.path).parent

    def ensure_directories(self) -> None:
        """确保必要的目录存在"""
        data_path = self.get_data_path()
        data_path.mkdir(parents=True, exist_ok=True)

        vector_path = Path(self.vector_store.path)
        vector_path.mkdir(parents=True, exist_ok=True)
