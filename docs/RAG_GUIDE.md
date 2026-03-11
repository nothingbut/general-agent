# RAG 模块使用指南

## 快速开始

### 1. 安装依赖
```bash
pip install chromadb sentence-transformers pypdf tiktoken
```

### 2. 基础使用
```python
from src.rag import RAGEngine

# 创建引擎（使用默认配置）
engine = RAGEngine()

# 索引文档
await engine.index_documents("docs/")

# 查询
result = await engine.query("如何使用 Python？", top_k=5)
print(result['context'])
```

## 核心功能

### 文档索引
```python
# 索引目录
stats = await engine.index_documents(
    path="docs/",
    recursive=True,
    show_progress=True
)

# 更新单个文档
await engine.update_document("docs/readme.md")

# 删除文档
await engine.delete_document("docs/old.md")
```

### 检索
```python
# 基础检索
results = await engine.retrieve("查询文本", top_k=5)

# 带过滤
results = await engine.retrieve(
    query="Python",
    filters={"category": "programming"}
)
```

### RAG 查询
```python
result = await engine.query(
    query="什么是容器技术？",
    top_k=3,
    max_context_tokens=2000
)

print(result['context'])    # 格式化上下文
print(result['stats'])      # 统计信息
```

## 配置

配置文件：`config/rag_config.yaml`

```yaml
rag:
  enabled: true
  vector_store:
    type: chromadb
    path: data/rag/vector_db
  embedding:
    provider: bge
    model: BAAI/bge-base-zh-v1.5
  retrieval:
    top_k: 5
    similarity_threshold: 0.7
```

## API 参考

### RAGEngine

**index_documents(path, recursive, show_progress)**
- 索引文档目录
- 返回：统计信息

**retrieve(query, top_k, filters)**
- 检索相关文档
- 返回：RetrievalResult 列表

**query(query, top_k, max_context_tokens)**
- 完整 RAG 查询
- 返回：{query, results, context, stats}

更多详情见源码注释。
