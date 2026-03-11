# Phase 5: RAG 引擎实现计划

**创建日期：** 2026-03-05
**预计工期：** 8-10 天
**目标：** 实现完整的 RAG（检索增强生成）功能

---

## 📋 目录

1. [技术架构](#技术架构)
2. [任务分解](#任务分解)
3. [实现步骤](#实现步骤)
4. [文件结构](#文件结构)
5. [测试策略](#测试策略)
6. [验收标准](#验收标准)
7. [风险评估](#风险评估)

---

## 🏗️ 技术架构

### 技术栈确定

```yaml
向量存储: ChromaDB
  - 版本: 0.4.22+
  - 部署: 嵌入式（PersistentClient）
  - 持久化路径: data/rag/vector_db

嵌入模型: BAAI/bge-base-zh-v1.5
  - 大小: 400MB
  - 维度: 768
  - 设备: CPU（可选 CUDA）
  - 来源: HuggingFace

分块策略: 混合策略
  - 优先语义分块（Markdown 标题、段落）
  - 超长块固定长度切分（max: 512 tokens）
  - 超短块合并（min: 100 tokens）
  - 重叠: 50 tokens

路由模式: 混合模式
  - 显式语法: @rag:search, @rag:index
  - 自动检测: auto_mode 可配置（初期 false）
  - 优先级: 显式 > MCP > Skill > 自动 RAG > Simple
```

### 架构图

```
┌─────────────────────────────────────────────────────────┐
│                       用户查询                            │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                    Router (路由)                          │
│  - 检查 @rag: 语法                                        │
│  - 自动检测（如果启用）                                     │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                  RAG Engine (核心)                        │
│  ┌───────────────────────────────────────────────┐      │
│  │  1. Query Processing (查询处理)                │      │
│  │     - 查询预处理                                │      │
│  │     - 查询嵌入（BGE + 前缀）                     │      │
│  └───────────────┬───────────────────────────────┘      │
│                  │                                       │
│  ┌───────────────▼───────────────────────────────┐      │
│  │  2. Document Retrieval (文档检索)              │      │
│  │     - 向量相似度搜索                             │      │
│  │     - Top-K 选择                                │      │
│  │     - 元数据过滤（可选）                          │      │
│  └───────────────┬───────────────────────────────┘      │
│                  │                                       │
│  ┌───────────────▼───────────────────────────────┐      │
│  │  3. Context Assembly (上下文组装)               │      │
│  │     - 结果排序                                  │      │
│  │     - 上下文拼接                                 │      │
│  │     - Prompt 构建                               │      │
│  └───────────────┬───────────────────────────────┘      │
│                  │                                       │
└──────────────────┼───────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────┐
│              LLM Generator (生成器)                       │
│  - 结合检索上下文                                          │
│  - 调用 Ollama/OpenAI                                    │
│  - 生成最终答案                                           │
└─────────────────────────────────────────────────────────┘

持久化组件：
┌──────────────────┐  ┌──────────────────┐
│  ChromaDB        │  │  SQLite          │
│  (向量 + 元数据)  │  │  (审计日志)       │
└──────────────────┘  └──────────────────┘
```

---

## 📦 任务分解

### 阶段 5.1：基础设施搭建（2天）

**Task 5.1.1: 依赖安装与配置**
- 安装 ChromaDB
- 安装 Sentence Transformers
- 安装 tokenizers（用于分词）
- 创建 requirements/依赖配置
- 验证环境

**Task 5.1.2: 目录结构与模块初始化**
- 创建 `src/rag/` 模块结构
- 初始化配置加载器
- 创建数据目录（data/rag/）
- 编写基础抽象类

**Task 5.1.3: 配置文件设计**
- 创建 `config/rag_config.yaml`
- 实现配置验证逻辑
- 编写配置加载测试

**Task 5.1.4: 嵌入模型集成**
- 实现 BGE 嵌入提供者
- 处理查询前缀逻辑
- 模型缓存机制
- 单元测试

---

### 阶段 5.2：文档处理（2天）

**Task 5.2.1: 文档加载器**
- Markdown 加载器
- 文本文件加载器
- PDF 加载器（使用 pypdf）
- 加载器工厂模式
- 单元测试

**Task 5.2.2: 文档分块器**
- 固定长度分块器（基础）
- Markdown 语义分块器
- 混合分块策略
- Token 计数工具
- 分块测试

**Task 5.2.3: 元数据提取**
- 文件路径、大小、修改时间
- Markdown 标题提取
- 自定义标签支持
- 元数据测试

---

### 阶段 5.3：向量存储与索引（2天）

**Task 5.3.1: ChromaDB 集成**
- ChromaDB 客户端初始化
- Collection 管理
- 持久化配置
- 连接测试

**Task 5.3.2: 索引构建**
- 批量文档嵌入
- 向量存储
- 元数据关联
- 增量索引支持

**Task 5.3.3: 索引管理**
- 更新索引（单文档/批量）
- 删除文档
- 清空索引
- 索引统计信息

**Task 5.3.4: 性能优化**
- 批量处理优化
- 进度显示
- 错误处理和重试
- 性能基准测试

---

### 阶段 5.4：检索系统（1.5天）

**Task 5.4.1: 基础检索**
- 查询嵌入
- 相似度搜索（Top-K）
- 结果解析
- 检索测试

**Task 5.4.2: 高级检索**
- 元数据过滤
- 相似度阈值过滤
- 结果去重
- 多查询合并

**Task 5.4.3: 结果处理**
- 结果排序
- 得分归一化
- 上下文窗口构建
- 结果测试

---

### 阶段 5.5：系统集成（1.5天）

**Task 5.5.1: Router 集成**
- 添加 RAG 路由识别（`@rag:...`）
- 自动检测逻辑（可选）
- 路由优先级管理
- 路由测试

**Task 5.5.2: Executor 集成**
- RAG 命令执行
  - `@rag:search` - 检索
  - `@rag:index` - 索引
  - `@rag:stats` - 统计
- 错误处理
- 执行测试

**Task 5.5.3: LLM 上下文注入**
- Prompt 模板设计
- 上下文拼接
- Token 限制处理
- 生成测试

**Task 5.5.4: 数据库审计**
- RAG 操作日志表设计
- 日志记录逻辑
- 查询统计
- 审计测试

---

### 阶段 5.6：测试与文档（1天）

**Task 5.6.1: 集成测试**
- 端到端索引流程测试
- 端到端检索流程测试
- HTTP API 测试
- 性能测试

**Task 5.6.2: 用户文档**
- RAG 用户指南（docs/rag-guide.md）
- 配置说明
- 使用示例
- 故障排查

**Task 5.6.3: 开发文档**
- 架构设计文档
- API 参考文档
- 扩展指南

**Task 5.6.4: README 更新**
- 添加 RAG 功能说明
- 更新特性列表
- 添加使用示例

---

## 📂 文件结构

```
general-agent/
├── src/
│   ├── rag/
│   │   ├── __init__.py
│   │   ├── engine.py              # RAG 引擎主类
│   │   ├── config.py              # 配置加载
│   │   ├── exceptions.py          # 自定义异常
│   │   │
│   │   ├── embeddings/            # 嵌入模型
│   │   │   ├── __init__.py
│   │   │   ├── base.py           # 抽象基类
│   │   │   ├── bge.py            # BGE 实现
│   │   │   └── factory.py        # 工厂模式
│   │   │
│   │   ├── loaders/               # 文档加载器
│   │   │   ├── __init__.py
│   │   │   ├── base.py           # 抽象基类
│   │   │   ├── markdown.py       # Markdown 加载器
│   │   │   ├── text.py           # 文本加载器
│   │   │   ├── pdf.py            # PDF 加载器
│   │   │   └── factory.py        # 工厂模式
│   │   │
│   │   ├── chunking/              # 文档分块
│   │   │   ├── __init__.py
│   │   │   ├── base.py           # 抽象基类
│   │   │   ├── fixed.py          # 固定长度
│   │   │   ├── semantic.py       # 语义分块
│   │   │   ├── hybrid.py         # 混合策略
│   │   │   └── utils.py          # 工具函数
│   │   │
│   │   ├── storage/               # 向量存储
│   │   │   ├── __init__.py
│   │   │   ├── base.py           # 抽象基类
│   │   │   ├── chromadb.py       # ChromaDB 实现
│   │   │   └── factory.py        # 工厂模式
│   │   │
│   │   ├── retrieval/             # 检索系统
│   │   │   ├── __init__.py
│   │   │   ├── retriever.py      # 检索器
│   │   │   ├── reranker.py       # 重排序（预留）
│   │   │   └── utils.py          # 工具函数
│   │   │
│   │   └── utils/                 # 工具模块
│   │       ├── __init__.py
│   │       ├── tokenizer.py      # 分词工具
│   │       └── metadata.py       # 元数据处理
│   │
│   ├── core/
│   │   ├── router.py              # ← 扩展 RAG 路由
│   │   └── executor.py            # ← 扩展 RAG 执行
│   │
│   └── storage/
│       └── database.py            # ← 扩展 RAG 审计日志
│
├── config/
│   └── rag_config.yaml            # RAG 配置文件
│
├── data/
│   └── rag/
│       ├── vector_db/             # ChromaDB 数据（自动生成）
│       ├── documents/             # 文档缓存（可选）
│       └── metadata.json          # 索引元数据
│
├── tests/
│   ├── rag/
│   │   ├── __init__.py
│   │   ├── conftest.py           # 测试配置
│   │   ├── test_config.py
│   │   ├── test_embeddings.py
│   │   ├── test_loaders.py
│   │   ├── test_chunking.py
│   │   ├── test_storage.py
│   │   ├── test_retrieval.py
│   │   ├── test_engine.py
│   │   └── test_e2e.py           # 端到端测试
│   │
│   ├── core/
│   │   ├── test_router.py        # ← 新增 RAG 路由测试
│   │   └── test_executor.py      # ← 新增 RAG 执行测试
│   │
│   └── integration/
│       └── test_rag_integration.py
│
├── docs/
│   ├── rag-guide.md              # RAG 用户指南
│   ├── rag-architecture.md       # 架构文档
│   └── rag-api.md                # API 文档
│
└── pyproject.toml                 # ← 更新依赖

```

---

## 🔧 实现步骤详解

### Step 1: 基础设施搭建（Day 1-2）

#### 1.1 依赖安装

```toml
# pyproject.toml
[project.optional-dependencies]
rag = [
    "chromadb>=0.4.22",
    "sentence-transformers>=2.3.0",
    "pypdf>=3.17.0",
    "tiktoken>=0.5.0",        # OpenAI tokenizer
    "markdown>=3.5.0",
]
```

```bash
# 安装命令
uv pip install -e ".[rag]"

# 验证
python -c "import chromadb; print(chromadb.__version__)"
python -c "from sentence_transformers import SentenceTransformer; print('OK')"
```

#### 1.2 配置文件

```yaml
# config/rag_config.yaml
rag:
  enabled: true
  auto_mode: false  # 自动检测模式（初期关闭）

  # 向量存储
  vector_store:
    type: "chromadb"
    path: "data/rag/vector_db"
    collection_name: "general_agent_docs"

  # 嵌入模型
  embedding:
    provider: "bge"
    model: "BAAI/bge-base-zh-v1.5"
    dimension: 768
    device: "cpu"  # cpu | cuda
    batch_size: 32
    normalize: true

  # 文档分块
  chunking:
    strategy: "hybrid"  # fixed | semantic | hybrid
    max_size: 512       # tokens
    min_size: 100       # tokens
    overlap: 50         # tokens

    # 语义分块配置
    semantic:
      markdown_max_heading_level: 3
      preserve_code_blocks: true
      preserve_tables: true

  # 检索配置
  retrieval:
    top_k: 5
    similarity_threshold: 0.7
    max_context_tokens: 2000
    enable_metadata_filter: true

  # 索引配置
  indexing:
    batch_size: 100
    show_progress: true
    recursive: true
    supported_extensions:
      - ".md"
      - ".txt"
      - ".pdf"

  # 路由配置
  routing:
    explicit_syntax: true    # 支持 @rag: 语法
    auto_detection: false    # 自动检测（初期关闭）

    # 自动检测规则
    auto_rules:
      question_words: true
      knowledge_keywords: true
      similarity_check: true
      min_similarity: 0.6

  # 审计日志
  audit:
    enabled: true
    log_queries: true
    log_results: true
```

#### 1.3 配置加载器

```python
# src/rag/config.py
from pathlib import Path
from typing import Any
import yaml
from pydantic import BaseModel, Field, validator

class EmbeddingConfig(BaseModel):
    provider: str = "bge"
    model: str = "BAAI/bge-base-zh-v1.5"
    dimension: int = 768
    device: str = "cpu"
    batch_size: int = 32
    normalize: bool = True

class VectorStoreConfig(BaseModel):
    type: str = "chromadb"
    path: str = "data/rag/vector_db"
    collection_name: str = "general_agent_docs"

class ChunkingConfig(BaseModel):
    strategy: str = "hybrid"
    max_size: int = 512
    min_size: int = 100
    overlap: int = 50

class RetrievalConfig(BaseModel):
    top_k: int = 5
    similarity_threshold: float = 0.7
    max_context_tokens: int = 2000

class RAGConfig(BaseModel):
    enabled: bool = True
    auto_mode: bool = False
    vector_store: VectorStoreConfig
    embedding: EmbeddingConfig
    chunking: ChunkingConfig
    retrieval: RetrievalConfig

    @classmethod
    def load(cls, config_path: str = "config/rag_config.yaml") -> "RAGConfig":
        """从 YAML 文件加载配置"""
        path = Path(config_path)
        if not path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")

        with open(path, 'r', encoding='utf-8') as f:
            data = yaml.safe_load(f)

        return cls(**data['rag'])
```

#### 1.4 嵌入模型实现

```python
# src/rag/embeddings/base.py
from abc import ABC, abstractmethod
import numpy as np

class EmbeddingProvider(ABC):
    """嵌入模型抽象基类"""

    @abstractmethod
    async def embed_query(self, text: str) -> np.ndarray:
        """嵌入单个查询"""
        pass

    @abstractmethod
    async def embed_documents(self, texts: list[str]) -> np.ndarray:
        """嵌入多个文档"""
        pass

    @abstractmethod
    def get_dimension(self) -> int:
        """获取向量维度"""
        pass
```

```python
# src/rag/embeddings/bge.py
from sentence_transformers import SentenceTransformer
import numpy as np
from .base import EmbeddingProvider

class BGEEmbedding(EmbeddingProvider):
    """BGE 嵌入模型实现"""

    # BGE 查询前缀（重要！）
    QUERY_PREFIX = "为这个句子生成表示以用于检索相关文章："

    def __init__(
        self,
        model_name: str = "BAAI/bge-base-zh-v1.5",
        device: str = "cpu",
        normalize: bool = True
    ):
        self.model = SentenceTransformer(model_name, device=device)
        self.normalize = normalize
        self.dimension = 768

    async def embed_query(self, text: str) -> np.ndarray:
        """
        嵌入查询（需要添加前缀）

        重要：BGE 模型要求查询和文档使用不同的编码方式
        """
        text_with_prefix = self.QUERY_PREFIX + text
        embedding = self.model.encode(
            text_with_prefix,
            normalize_embeddings=self.normalize
        )
        return embedding

    async def embed_documents(self, texts: list[str]) -> np.ndarray:
        """
        嵌入文档（不需要前缀）
        """
        embeddings = self.model.encode(
            texts,
            normalize_embeddings=self.normalize,
            show_progress_bar=len(texts) > 10
        )
        return embeddings

    def get_dimension(self) -> int:
        return self.dimension
```

---

### Step 2: 文档处理（Day 3-4）

#### 2.1 文档加载器

```python
# src/rag/loaders/base.py
from abc import ABC, abstractmethod
from dataclasses import dataclass
from pathlib import Path

@dataclass
class Document:
    """文档数据类"""
    content: str
    metadata: dict
    source: str

class DocumentLoader(ABC):
    """文档加载器抽象基类"""

    @abstractmethod
    async def load(self, file_path: str) -> Document:
        """加载单个文档"""
        pass

    @abstractmethod
    def supports(self, file_path: str) -> bool:
        """检查是否支持该文件类型"""
        pass
```

```python
# src/rag/loaders/markdown.py
import re
from pathlib import Path
from .base import DocumentLoader, Document

class MarkdownLoader(DocumentLoader):
    """Markdown 文档加载器"""

    async def load(self, file_path: str) -> Document:
        path = Path(file_path)

        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()

        # 提取元数据
        metadata = self._extract_metadata(content, path)

        return Document(
            content=content,
            metadata=metadata,
            source=str(path)
        )

    def supports(self, file_path: str) -> bool:
        return file_path.endswith('.md')

    def _extract_metadata(self, content: str, path: Path) -> dict:
        """提取 Markdown 元数据"""
        metadata = {
            'file_name': path.name,
            'file_path': str(path),
            'file_size': path.stat().st_size,
            'file_type': 'markdown',
        }

        # 提取一级标题
        title_match = re.search(r'^#\s+(.+)$', content, re.MULTILINE)
        if title_match:
            metadata['title'] = title_match.group(1).strip()

        return metadata
```

#### 2.2 文档分块器

```python
# src/rag/chunking/hybrid.py
from typing import List
from .base import Chunker, Chunk
from .semantic import SemanticChunker
from .fixed import FixedLengthChunker

class HybridChunker(Chunker):
    """混合分块策略"""

    def __init__(
        self,
        max_size: int = 512,
        min_size: int = 100,
        overlap: int = 50
    ):
        self.semantic_chunker = SemanticChunker()
        self.fixed_chunker = FixedLengthChunker(
            chunk_size=max_size,
            overlap=overlap
        )
        self.max_size = max_size
        self.min_size = min_size

    async def chunk(self, text: str, metadata: dict = None) -> List[Chunk]:
        """混合分块"""
        # Step 1: 语义分块
        semantic_chunks = await self.semantic_chunker.chunk(text, metadata)

        # Step 2: 调整块大小
        final_chunks = []
        buffer = []

        for chunk in semantic_chunks:
            size = self._count_tokens(chunk.text)

            if size > self.max_size:
                # 超长块：固定长度切分
                sub_chunks = await self.fixed_chunker.chunk(chunk.text, metadata)
                final_chunks.extend(sub_chunks)

            elif size < self.min_size:
                # 超短块：加入缓冲区
                buffer.append(chunk)
                buffer_size = sum(self._count_tokens(c.text) for c in buffer)

                if buffer_size >= self.min_size:
                    # 合并缓冲区
                    merged = self._merge_chunks(buffer)
                    final_chunks.append(merged)
                    buffer = []
            else:
                # 合适大小：直接输出
                if buffer:
                    buffer.append(chunk)
                    merged = self._merge_chunks(buffer)
                    final_chunks.append(merged)
                    buffer = []
                else:
                    final_chunks.append(chunk)

        # 处理剩余缓冲区
        if buffer:
            merged = self._merge_chunks(buffer)
            final_chunks.append(merged)

        return final_chunks
```

---

### Step 3: 向量存储（Day 5-6）

```python
# src/rag/storage/chromadb.py
import chromadb
from chromadb.config import Settings
from typing import List, Dict, Any
from .base import VectorStore

class ChromaDBStore(VectorStore):
    """ChromaDB 向量存储实现"""

    def __init__(
        self,
        persist_path: str = "data/rag/vector_db",
        collection_name: str = "general_agent_docs"
    ):
        self.client = chromadb.PersistentClient(
            path=persist_path,
            settings=Settings(anonymized_telemetry=False)
        )
        self.collection = self.client.get_or_create_collection(
            name=collection_name,
            metadata={"hnsw:space": "cosine"}
        )

    async def add(
        self,
        ids: List[str],
        embeddings: List[List[float]],
        documents: List[str],
        metadatas: List[Dict[str, Any]]
    ) -> None:
        """添加文档到向量库"""
        self.collection.add(
            ids=ids,
            embeddings=embeddings,
            documents=documents,
            metadatas=metadatas
        )

    async def search(
        self,
        query_embedding: List[float],
        top_k: int = 5,
        where: Dict[str, Any] = None
    ) -> List[Dict[str, Any]]:
        """检索相似文档"""
        results = self.collection.query(
            query_embeddings=[query_embedding],
            n_results=top_k,
            where=where,
            include=["documents", "metadatas", "distances"]
        )

        # 转换结果格式
        return self._parse_results(results)
```

---

### Step 4: RAG 引擎（Day 6-7）

```python
# src/rag/engine.py
from typing import List, Dict, Any, Optional
from pathlib import Path
from .config import RAGConfig
from .embeddings.factory import create_embedding_provider
from .storage.factory import create_vector_store
from .loaders.factory import create_loader
from .chunking.factory import create_chunker

class RAGEngine:
    """RAG 引擎主类"""

    def __init__(self, config: RAGConfig):
        self.config = config

        # 初始化组件
        self.embedding = create_embedding_provider(config.embedding)
        self.vector_store = create_vector_store(config.vector_store)
        self.chunker = create_chunker(config.chunking)

    async def index_documents(
        self,
        path: str,
        recursive: bool = True,
        show_progress: bool = True
    ) -> Dict[str, Any]:
        """
        索引文档

        Args:
            path: 文档路径（文件或目录）
            recursive: 是否递归处理子目录
            show_progress: 是否显示进度

        Returns:
            索引结果统计
        """
        # 实现索引逻辑
        pass

    async def search(
        self,
        query: str,
        top_k: int = 5,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[Dict[str, Any]]:
        """
        检索相关文档

        Args:
            query: 查询文本
            top_k: 返回结果数量
            filters: 元数据过滤条件

        Returns:
            检索结果列表
        """
        # 1. 查询嵌入
        query_embedding = await self.embedding.embed_query(query)

        # 2. 向量检索
        results = await self.vector_store.search(
            query_embedding=query_embedding,
            top_k=top_k,
            where=filters
        )

        # 3. 过滤和排序
        filtered_results = self._filter_by_threshold(results)

        return filtered_results

    async def generate_with_context(
        self,
        query: str,
        llm_provider: str = "ollama",
        model: str = "qwen2.5:7b"
    ) -> str:
        """
        RAG 生成（检索 + 生成）

        Args:
            query: 用户查询
            llm_provider: LLM 提供者
            model: 模型名称

        Returns:
            生成的答案
        """
        # 1. 检索相关文档
        results = await self.search(query, top_k=self.config.retrieval.top_k)

        # 2. 构建上下文
        context = self._build_context(results)

        # 3. 构建 Prompt
        prompt = self._build_prompt(query, context)

        # 4. 调用 LLM 生成
        response = await self._generate(prompt, llm_provider, model)

        return response
```

---

### Step 5: 系统集成（Day 8-9）

#### 5.1 Router 集成

```python
# src/core/router.py（扩展）
class Router:
    def __init__(self, config: Config):
        # ... 现有初始化代码 ...

        # RAG 初始化
        if config.rag.enabled:
            from ..rag.engine import RAGEngine
            from ..rag.config import RAGConfig

            rag_config = RAGConfig.load()
            self.rag_engine = RAGEngine(rag_config)
            self.rag_auto_mode = rag_config.auto_mode

    def route(self, query: str) -> RouteResult:
        """路由逻辑"""
        # 优先级 1: RAG 显式语法
        if query.startswith('@rag:'):
            return self._route_rag(query)

        # 优先级 2-3: MCP 和 Skill
        if query.startswith('@mcp:'):
            return self._route_mcp(query)
        if query.startswith('@skill:'):
            return self._route_skill(query)

        # 优先级 4: 自动 RAG（如果启用）
        if self.rag_auto_mode and self._is_rag_suitable(query):
            return self._route_rag_auto(query)

        # 优先级 5: 默认
        return self._route_simple(query)

    def _route_rag(self, query: str) -> RouteResult:
        """解析 RAG 语法"""
        # @rag:search query="..." top_k=5
        # @rag:index path="/docs"
        # @rag:stats
        pattern = r'@rag:(\w+)\s+(.*)'
        match = re.match(pattern, query)

        if not match:
            raise ValueError("Invalid RAG syntax")

        action = match.group(1)
        params = self._parse_params(match.group(2))

        return RouteResult(
            type="rag",
            action=action,
            params=params
        )
```

#### 5.2 Executor 集成

```python
# src/core/executor.py（扩展）
class Executor:
    async def execute(self, route_result: RouteResult) -> ExecutionResult:
        """执行路由结果"""
        if route_result.type == "rag":
            return await self._execute_rag(route_result)
        # ... 其他类型 ...

    async def _execute_rag(self, route: RouteResult) -> ExecutionResult:
        """执行 RAG 命令"""
        action = route.action
        params = route.params

        try:
            if action == "search":
                # RAG 检索
                query = params.get("query")
                top_k = params.get("top_k", 5)

                results = await self.rag_engine.search(query, top_k)

                return ExecutionResult(
                    success=True,
                    data=results,
                    message=f"Found {len(results)} results"
                )

            elif action == "index":
                # 索引文档
                path = params.get("path")
                recursive = params.get("recursive", True)

                stats = await self.rag_engine.index_documents(path, recursive)

                return ExecutionResult(
                    success=True,
                    data=stats,
                    message=f"Indexed {stats['total_docs']} documents"
                )

            elif action == "stats":
                # 获取统计信息
                stats = await self.rag_engine.get_stats()
                return ExecutionResult(success=True, data=stats)

            else:
                raise ValueError(f"Unknown RAG action: {action}")

        except Exception as e:
            return ExecutionResult(
                success=False,
                error=str(e),
                message=f"RAG execution failed: {e}"
            )
```

---

## 🧪 测试策略

### 单元测试（每个模块 80%+ 覆盖率）

```python
# tests/rag/test_embeddings.py
import pytest
from src.rag.embeddings.bge import BGEEmbedding

@pytest.mark.asyncio
async def test_bge_query_embedding():
    """测试查询嵌入"""
    embedding = BGEEmbedding()

    query = "如何配置 FastAPI？"
    result = await embedding.embed_query(query)

    assert result.shape == (768,)
    assert -1 <= result.min() <= 1
    assert -1 <= result.max() <= 1

@pytest.mark.asyncio
async def test_bge_document_embedding():
    """测试文档嵌入"""
    embedding = BGEEmbedding()

    docs = ["FastAPI 配置指南", "Django 教程"]
    result = await embedding.embed_documents(docs)

    assert result.shape == (2, 768)
```

### 集成测试

```python
# tests/rag/test_e2e.py
@pytest.mark.asyncio
async def test_e2e_indexing_and_search():
    """端到端测试：索引 + 检索"""
    # 1. 准备测试文档
    test_docs = Path("tests/fixtures/docs")
    test_docs.mkdir(exist_ok=True)

    (test_docs / "fastapi.md").write_text(
        "# FastAPI 配置\n\nFastAPI 是现代化的 Python Web 框架..."
    )

    # 2. 初始化 RAG 引擎
    config = RAGConfig.load("tests/fixtures/rag_config.yaml")
    engine = RAGEngine(config)

    # 3. 索引文档
    stats = await engine.index_documents(str(test_docs))
    assert stats['total_docs'] == 1
    assert stats['total_chunks'] > 0

    # 4. 检索
    results = await engine.search("如何配置 FastAPI？", top_k=3)
    assert len(results) > 0
    assert "FastAPI" in results[0]['text']
    assert results[0]['score'] > 0.7
```

### 性能测试

```python
# tests/rag/test_performance.py
import time

@pytest.mark.asyncio
async def test_search_performance():
    """测试检索性能"""
    engine = RAGEngine(config)

    # 索引 1000 个文档
    await engine.index_documents("tests/fixtures/large_corpus")

    # 测试检索速度
    query = "如何优化数据库性能？"

    start = time.time()
    results = await engine.search(query, top_k=5)
    elapsed = time.time() - start

    # 断言：检索时间 < 500ms
    assert elapsed < 0.5
    assert len(results) == 5
```

---

## ✅ 验收标准

### 功能验收

- [ ] **索引功能**
  - [ ] 支持 Markdown、TXT、PDF 格式
  - [ ] 支持递归目录索引
  - [ ] 显示索引进度
  - [ ] 增量索引支持
  - [ ] 错误处理和日志记录

- [ ] **检索功能**
  - [ ] 准确检索相关文档
  - [ ] 支持 Top-K 控制
  - [ ] 支持相似度阈值过滤
  - [ ] 支持元数据过滤
  - [ ] 结果排序正确

- [ ] **RAG 生成**
  - [ ] 检索 + 生成流程完整
  - [ ] 上下文正确注入 Prompt
  - [ ] LLM 响应包含检索内容
  - [ ] Token 限制正确处理

- [ ] **路由集成**
  - [ ] 支持 `@rag:search` 语法
  - [ ] 支持 `@rag:index` 语法
  - [ ] 支持 `@rag:stats` 语法
  - [ ] 路由优先级正确
  - [ ] 错误提示清晰

### 性能验收

- [ ] **索引速度**
  - [ ] 1000 个文档索引时间 < 60 秒
  - [ ] 内存占用 < 2GB

- [ ] **检索速度**
  - [ ] 单次检索延迟 < 500ms
  - [ ] 批量检索（10个查询）< 3秒

- [ ] **准确率**
  - [ ] 中文检索 Recall@5 > 80%
  - [ ] 技术文档检索 Recall@5 > 85%

### 质量验收

- [ ] **测试覆盖率**
  - [ ] 单元测试覆盖率 > 80%
  - [ ] 集成测试覆盖核心流程
  - [ ] 所有测试通过

- [ ] **代码质量**
  - [ ] Ruff 检查通过
  - [ ] Mypy 类型检查通过
  - [ ] 代码审查通过

- [ ] **文档完整性**
  - [ ] 用户指南完整
  - [ ] API 文档完整
  - [ ] 配置说明清晰
  - [ ] 示例代码可运行

---

## ⚠️ 风险评估

### 高风险

1. **模型下载失败**
   - **风险**：HuggingFace 下载 BGE 模型失败（国内网络问题）
   - **缓解**：提供镜像下载地址，或手动下载后离线加载
   - **应急**：降级到更小的模型（MiniLM）

2. **内存不足**
   - **风险**：大量文档索引导致内存溢出
   - **缓解**：批量处理，限制批量大小
   - **应急**：分批索引，增加 swap

### 中风险

3. **检索准确率不达标**
   - **风险**：实际检索效果 < 预期（Recall@5 < 80%）
   - **缓解**：调整 chunking 策略，优化 prompt
   - **应急**：升级到 bge-large 模型

4. **性能不达标**
   - **风险**：检索延迟 > 500ms
   - **缓解**：优化批量处理，添加缓存
   - **应急**：降低 Top-K，减少文档库规模

### 低风险

5. **兼容性问题**
   - **风险**：依赖冲突（sentence-transformers vs 其他库）
   - **缓解**：虚拟环境隔离
   - **应急**：锁定版本号

---

## 📅 时间规划

| 阶段 | 任务 | 预计工期 | 开始日期 | 结束日期 |
|------|------|---------|---------|---------|
| 5.1 | 基础设施搭建 | 2天 | Day 1 | Day 2 |
| 5.2 | 文档处理 | 2天 | Day 3 | Day 4 |
| 5.3 | 向量存储与索引 | 2天 | Day 5 | Day 6 |
| 5.4 | 检索系统 | 1.5天 | Day 6 | Day 7 |
| 5.5 | 系统集成 | 1.5天 | Day 8 | Day 9 |
| 5.6 | 测试与文档 | 1天 | Day 9 | Day 10 |
| **总计** | | **10天** | | |

---

## 🎯 下一步行动

**准备好开始实现了！**

### 推荐工作流程：

1. **创建功能分支**
   ```bash
   git checkout -b feature/phase-5-rag-engine
   ```

2. **按阶段实现**
   - 每完成一个阶段提交一次
   - 提交信息格式：`feat(rag): implement stage X.Y - description`

3. **持续测试**
   - 每个模块写完立即测试
   - 保持 80%+ 覆盖率

4. **文档同步**
   - 实现同时更新文档
   - 代码注释充分

5. **定期集成**
   - 每天合并到 main（如果稳定）
   - 或每 2-3 天合并一次

---

**实现计划已完成！准备好开始 Phase 5.1 了吗？** 🚀
