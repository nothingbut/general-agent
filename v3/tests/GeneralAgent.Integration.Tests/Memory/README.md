# Memory 集成测试

## 概述

本目录包含记忆系统的端到端集成测试，验证：
- 记忆创建和向量双写
- 语义搜索和向量检索
- 记忆更新时的向量同步
- 记忆删除时的向量同步
- 混合检索（关键词 + 语义）

## 前置条件

运行这些测试需要以下服务：

### 1. Ollama 服务

```bash
# 启动 Ollama
ollama serve

# 拉取 embedding 模型
ollama pull nomic-embed-text
```

### 2. Qdrant 向量数据库

```bash
# 使用 Docker 启动 Qdrant
docker run -d --name qdrant \
  -p 6333:6333 \
  -p 6334:6334 \
  qdrant/qdrant
```

## 运行测试

```bash
# 进入项目根目录
cd v3

# 运行所有 Memory E2E 测试
dotnet test tests/GeneralAgent.Integration.Tests \
  --filter "Category=E2E&FullyQualifiedName~MemoryVectorSearch"

# 运行特定测试
dotnet test tests/GeneralAgent.Integration.Tests \
  --filter "FullyQualifiedName~CreateAndSearchMemory_WithVectors"
```

## 测试说明

### 测试 1: CreateAndSearchMemory_WithVectors_FindsRelevantMemories
- 创建 2 个测试记忆（TDD 偏好、编码风格）
- 等待向量索引（500ms）
- 语义搜索"测试驱动开发"
- 验证找到最相关的记忆

### 测试 2: UpdateMemory_UpdatesVector
- 创建记忆
- 更新内容为"人工智能"
- 搜索"人工智能"
- 验证找到更新后的记忆

### 测试 3: DeleteMemory_DeletesVector
- 创建记忆
- 删除记忆
- 搜索该记忆
- 验证未找到（向量也被删除）

### 测试 4: HybridSearch_CombinesKeywordAndSemantic
- 创建多个记忆（Rust、C#）
- 使用混合检索（关键词 30% + 语义 70%）
- 验证综合排序结果

## 故障排查

### 测试被跳过
如果看到 `SkipTestException`，说明服务不可用：

```
Ollama 服务不可用（http://localhost:11434）。请运行: ollama serve
Ollama 模型 'nomic-embed-text' 不存在。请运行: ollama pull nomic-embed-text
Qdrant 服务不可用（http://localhost:6333）。请运行: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### 检查服务状态

```bash
# 检查 Ollama
curl http://localhost:11434/api/tags

# 检查 Qdrant
curl http://localhost:6333/
```

## 测试数据清理

测试使用临时目录和独立的 Qdrant 集合（`memory_e2e_test`），测试完成后会自动清理：
- 删除所有测试记忆的文件和向量
- 删除临时记忆目录
- 不影响其他测试或生产数据

## 性能说明

- 每个测试大约需要 1-3 秒
- 向量索引需要等待 500ms
- 测试是串行执行的（使用 `[Collection("E2E")]`）
