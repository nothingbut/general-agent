# V3 Phase 2 设计文档：Embedding 向量化和向量数据库集成

**创建日期**: 2026-03-27
**作者**: Claude Sonnet 4.5
**状态**: 设计审查中
**Phase**: V3 Phase 2

---

## 1. 概述

### 1.1 背景

V3 Phase 1 实现了基于文件系统的长期记忆系统，提供了完整的记忆 CRUD 和 LLM 驱动的语义搜索功能。然而，当前实现存在显著的性能瓶颈：

- **时间复杂度**: O(n) - 需要对每个记忆单独调用 LLM 评分
- **响应时间**: 50-100秒（100个记忆时）
- **API 成本**: 100次 LLM 调用/查询
- **扩展性**: 记忆数量增加时性能线性下降

### 1.2 目标

Phase 2 通过集成 Embedding 模型和向量数据库，将记忆搜索性能提升 **1000-10000 倍**：

- **性能提升**: 响应时间从 50-100秒 降至 10-50毫秒
- **成本优化**: API 调用从 100次 降至 1次（节省 99%）
- **扩展性**: 支持百万级记忆的高效检索
- **准确性**: 保持与 LLM 评分相当的语义理解能力

### 1.3 范围

**Phase 2 包含**：
- ✅ 集成 Ollama Embedding 模型（nomic-embed-text）
- ✅ 集成 Qdrant 向量数据库
- ✅ 双写模式（同步更新文件系统和向量数据库）
- ✅ 自动降级策略（Qdrant 不可用时回退到关键词搜索）
- ✅ 现有记忆迁移工具
- ✅ 完整的测试覆盖（单元、集成、端到端）

**Phase 2 不包含**：
- ❌ 多 Embedding 提供商支持（OpenAI, etc.）- 留给后续版本
- ❌ 记忆去重和自动归档 - 留给 Phase 3
- ❌ 记忆关系和知识图谱 - 留给 Phase 4

---

## 2. 技术决策

### 2.1 技术栈

| 组件 | 技术选择 | 理由 |
|------|---------|------|
| **Embedding 模型** | Ollama + nomic-embed-text | • 本地运行，零成本<br>• 768维，支持中文<br>• 与现有 Ollama 技术栈一致<br>• 性能优秀（~10-50ms/query） |
| **向量数据库** | Qdrant | • 高性能 HNSW 索引<br>• V2 项目已使用，有现成代码<br>• Docker 一键部署<br>• .NET SDK 支持完善 |
| **存储策略** | 双写模式 | • 实时一致性<br>• 用户体验好（创建后立即可搜索）<br>• 文件系统为 source of truth<br>• 向量数据库为性能加速层 |
| **降级策略** | 自动降级到关键词搜索 | • 提升系统健壮性<br>• 避免单点故障<br>• 用户体验平滑降级 |
| **实施方式** | 渐进式（3迭代） | • 风险可控<br>• 易于调试<br>• 质量保证 |

### 2.2 架构决策记录 (ADR)

#### ADR-001: 选择 Ollama 而非 OpenAI Embeddings

**决策**: 使用 Ollama + nomic-embed-text 作为 Embedding 模型

**理由**:
- ✅ 与 Phase 1 的 Ollama 技术栈一致，用户已熟悉
- ✅ 零成本，无需 API Key
- ✅ 隐私保护，数据不离开本地
- ✅ 离线可用
- ✅ nomic-embed-text 质量高（768维，支持中文）

**权衡**: OpenAI Embeddings 质量稍高但有成本和隐私问题

**状态**: 已接受

#### ADR-002: 选择 Qdrant 而非 ChromaDB

**决策**: 使用 Qdrant 作为向量数据库

**理由**:
- ✅ V2 项目已使用 Qdrant，有现成代码可参考
- ✅ 性能优秀（HNSW 索引，毫秒级查询）
- ✅ Docker 部署简单
- ✅ .NET SDK (Qdrant.Client) 支持完善
- ✅ 功能丰富（过滤、混合搜索、多租户）

**权衡**: ChromaDB 更轻量但需要 Python 环境且 .NET 集成较弱

**状态**: 已接受

#### ADR-003: 双写模式 vs 延迟生成

**决策**: 采用双写模式（同步更新文件和向量数据库）

**理由**:
- ✅ 数据始终一致，用户体验好
- ✅ 创建记忆后立即可被向量搜索
- ✅ 实现复杂度适中
- ✅ 记忆写入频率不高（~10-50ms 延迟可接受）

**权衡**: 写入稍慢（增加 Embedding 生成时间），但可接受

**状态**: 已接受

#### ADR-004: 自动降级 vs 直接报错

**决策**: Qdrant 不可用时自动降级到关键词搜索

**理由**:
- ✅ 提升系统健壮性，避免单点故障
- ✅ 用户体验好（功能降级但不丢失）
- ✅ 复用 Phase 1 代码，实现成本低
- ✅ 向量搜索是增强功能，不是核心依赖

**权衡**: 降级后性能显著下降（毫秒 → 秒级），但保证可用性

**状态**: 已接受

---

## 3. 系统架构

### 3.1 整体架构

```
┌─────────────────────────────────────────────────────────┐
│  CLI 层 (AgentRepl)                                      │
│  /memory semantic-search, /memory migrate-to-vectors   │
└─────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────┐
│  应用层 (Services)                                       │
│  MemoryExtractionService, MemoryRetrievalService       │
└─────────────────────────────────────────────────────────┘
                           ↓
┌──────────────────────┬──────────────────────────────────┐
│  基础设施层 (New)    │  基础设施层 (Phase 1)           │
│                      │                                  │
│  IEmbeddingClient    │  MemoryRepository               │
│  IVectorRepository   │  MemoryIndexManager             │
│                      │  ILLMClient                     │
└──────────────────────┴──────────────────────────────────┘
                           ↓
┌──────────────────────┬──────────────────────────────────┐
│  外部服务 (New)      │  存储 (Phase 1)                 │
│                      │                                  │
│  Ollama Embedding    │  Markdown 文件系统               │
│  Qdrant Vector DB    │  MEMORY.md 索引                 │
└──────────────────────┴──────────────────────────────────┘
```

### 3.2 项目结构

```
v3/src/
├── GeneralAgent.Core/
│   ├── Models/
│   │   ├── Memory.cs                          (Phase 1)
│   │   ├── VectorSearchResult.cs              (新增)
│   │   └── VectorCollectionStats.cs           (新增)
│   └── Abstractions/
│       ├── IMemoryRepository.cs               (Phase 1)
│       ├── IEmbeddingClient.cs                (新增)
│       └── IVectorRepository.cs               (新增)
│
├── GeneralAgent.Infrastructure.Embedding/      (新项目)
│   ├── OllamaEmbeddingClient.cs
│   ├── EmbeddingOptions.cs
│   ├── DependencyInjection.cs
│   └── GeneralAgent.Infrastructure.Embedding.csproj
│
├── GeneralAgent.Infrastructure.VectorDB/       (新项目)
│   ├── QdrantVectorRepository.cs
│   ├── VectorDBOptions.cs
│   ├── DependencyInjection.cs
│   └── GeneralAgent.Infrastructure.VectorDB.csproj
│
└── GeneralAgent.Infrastructure.Memory/
    ├── Repositories/
    │   └── MemoryRepository.cs                (修改：双写逻辑)
    └── Services/
        └── MemoryRetrievalService.cs          (修改：向量搜索)
```

### 3.3 核心组件说明

#### 新增组件

1. **IEmbeddingClient / OllamaEmbeddingClient**
   - 职责：调用 Ollama API 生成 Embedding 向量
   - 输入：文本字符串
   - 输出：float[768] 向量
   - 特性：支持单个和批量生成

2. **IVectorRepository / QdrantVectorRepository**
   - 职责：向量存储、检索和管理
   - 功能：向量 CRUD、相似度搜索、健康检查
   - 技术：Qdrant.Client SDK + HNSW 索引

#### 修改的组件

1. **MemoryRepository**
   - 新增：双写逻辑（文件 + 向量）
   - 新增：容错处理（向量写入失败不影响记忆创建）

2. **MemoryRetrievalService**
   - 修改：SearchBySemanticAsync 使用向量搜索
   - 新增：健康检查和自动降级逻辑
   - 保留：Phase 1 的关键词搜索作为降级路径

---

## 4. 接口设计

### 4.1 IEmbeddingClient 接口

```csharp
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// Embedding 向量生成客户端接口
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// 提供商名称（如 "Ollama", "OpenAI"）
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 向量维度（如 768, 1536）
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// 为单个文本生成 Embedding 向量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>归一化的向量数组（长度 = Dimensions）</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成 Embedding 向量（优化性能）
    /// </summary>
    /// <param name="texts">输入文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量列表（与输入顺序对应）</returns>
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default);
}
```

### 4.2 IVectorRepository 接口

```csharp
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 向量存储和检索接口
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// 存储或更新向量及其元数据
    /// </summary>
    /// <param name="memoryId">记忆唯一标识</param>
    /// <param name="embedding">向量数据</param>
    /// <param name="metadata">元数据（type, name, created_at 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(
        Guid memoryId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量相似度搜索
    /// </summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topK">返回的结果数量</param>
    /// <param name="filters">过滤条件（可选，如 type=User）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果列表（按相似度降序）</returns>
    Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除向量
    /// </summary>
    /// <param name="memoryId">记忆唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示服务可用</returns>
    Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集合统计信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量数量、维度等统计</returns>
    Task<VectorCollectionStats> GetStatsAsync(
        CancellationToken cancellationToken = default);
}
```

### 4.3 数据模型

```csharp
namespace GeneralAgent.Core.Models;

/// <summary>
/// 向量搜索结果
/// </summary>
public sealed record VectorSearchResult
{
    /// <summary>
    /// 记忆 ID
    /// </summary>
    public Guid MemoryId { get; init; }

    /// <summary>
    /// 相似度评分（0.0-1.0，越高越相似）
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// 元数据（从向量数据库返回）
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// 向量集合统计信息
/// </summary>
public sealed record VectorCollectionStats
{
    /// <summary>
    /// 向量总数
    /// </summary>
    public long VectorCount { get; init; }

    /// <summary>
    /// 向量维度
    /// </summary>
    public int Dimensions { get; init; }

    /// <summary>
    /// 索引类型（如 "HNSW"）
    /// </summary>
    public string IndexType { get; init; } = string.Empty;
}
```

### 4.4 配置模型

```csharp
// EmbeddingOptions.cs
namespace GeneralAgent.Infrastructure.Embedding;

public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>
    /// 提供商名称（当前仅支持 "Ollama"）
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// Ollama 服务地址
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Embedding 模型名称
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

// VectorDBOptions.cs
namespace GeneralAgent.Infrastructure.VectorDB;

public class VectorDBOptions
{
    public const string SectionName = "VectorDB";

    /// <summary>
    /// 提供商名称（当前仅支持 "Qdrant"）
    /// </summary>
    public string Provider { get; set; } = "Qdrant";

    /// <summary>
    /// Qdrant 服务地址
    /// </summary>
    public string Url { get; set; } = "http://localhost:6333";

    /// <summary>
    /// 集合名称
    /// </summary>
    public string CollectionName { get; set; } = "memory_vectors";

    /// <summary>
    /// 是否启用自动降级（Qdrant 不可用时）
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// 健康检查缓存时间（秒）
    /// </summary>
    public int HealthCheckCacheSeconds { get; set; } = 30;
}
```

### 4.5 配置文件（appsettings.json）

```json
{
  "ConnectionStrings": {
    "AgentDb": "Data Source=agent.db"
  },
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Name": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "DefaultModel": "qwen2.5:0.5b",
        "TimeoutSeconds": 120
      }
    }
  },
  "Embedding": {
    "Provider": "Ollama",
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text",
    "TimeoutSeconds": 30
  },
  "VectorDB": {
    "Provider": "Qdrant",
    "Url": "http://localhost:6333",
    "CollectionName": "memory_vectors",
    "EnableFallback": true,
    "HealthCheckCacheSeconds": 30
  },
  "Memory": {
    "MemoryDirectory": "~/.agent/memory"
  }
}
```

---

## 5. 数据流设计

### 5.1 记忆创建流程（双写模式）

```
用户输入 → /memory add user coding_style
                    ↓
    1. 收集输入（name, description, content）
                    ↓
    2. 创建 Memory 实体
       memory = Memory.Create(type, name, description, content)
                    ↓
    3. 写入 Markdown 文件（Source of Truth）
       ~/.agent/memory/user/coding_style.md
                    ↓
    4. 生成 Embedding 向量
       text = $"{name} {description} {content}"
       vector = await _embeddingClient.GenerateEmbeddingAsync(text)
       (768 维 float 数组)
                    ↓
    5. 存入 Qdrant
       metadata = { memory_id, type, name, created_at }
       await _vectorRepository.UpsertAsync(memory.Id, vector, metadata)
                    ↓
    6. 更新 MEMORY.md 索引
       await _indexManager.AddToIndexAsync(memory)
                    ↓
         ✅ 返回成功
```

**容错处理**：

- **文件写入失败** → 回滚整个操作，返回错误
- **Embedding 生成失败** → 记录日志，重试 3 次，失败则允许继续（向量稍后同步）
- **向量写入失败** → 记录日志，记忆已保存，提示用户稍后运行 `/memory sync`

### 5.2 记忆搜索流程（向量搜索 + 自动降级）

```
用户查询 → /memory semantic-search "TDD测试"
                    ↓
    1. 检查 Qdrant 健康状态
       isHealthy = await _vectorRepository.IsHealthyAsync()
       (30秒缓存，避免频繁检查)
                    ↓
         健康？
         ├─ 是 → 🚀 向量搜索（快速路径，10-50ms）
         │         ↓
         │    2. 生成查询向量
         │       queryVector = await _embeddingClient
         │                       .GenerateEmbeddingAsync("TDD测试")
         │         ↓
         │    3. 向量相似度搜索
         │       results = await _vectorRepository
         │                   .SearchAsync(queryVector, topK=5)
         │       (使用 HNSW 索引，O(log n) 复杂度)
         │         ↓
         │    4. 根据 memory_id 加载完整记忆
         │       memories = []
         │       foreach result in results:
         │           memory = await _memoryRepository
         │                      .GetByIdAsync(result.MemoryId)
         │           memories.Add(memory)
         │         ↓
         │       ✅ 返回结果（总耗时 ~10-50ms）
         │
         └─ 否 → 🐢 降级到关键词搜索（慢速路径，1-5秒）
                   ↓
              2. 记录降级日志
                 _logger.LogWarning("Qdrant 不可用，降级到关键词搜索")
                   ↓
              3. 显示用户提示
                 "⚠️ 向量搜索不可用，使用关键词搜索（较慢）"
                 "提示：docker run -p 6333:6333 qdrant/qdrant"
                   ↓
              4. 执行关键词搜索（Phase 1 实现）
                 results = await _memoryRepository.SearchAsync("TDD测试")
                 (文件系统扫描，O(n) 复杂度)
                   ↓
                 ✅ 返回结果（总耗时 ~1-5秒）
```

### 5.3 记忆更新流程

```
更新记忆内容 → memory = memory.WithContent("新内容")
                    ↓
    1. 更新 Markdown 文件
       await _memoryRepository.UpdateAsync(memory)
                    ↓
    2. 重新生成 Embedding（内容变了）
       text = $"{memory.Name} {memory.Description} {memory.Content}"
       newVector = await _embeddingClient.GenerateEmbeddingAsync(text)
                    ↓
    3. 更新 Qdrant 中的向量
       await _vectorRepository.UpsertAsync(memory.Id, newVector, metadata)
       (UpsertAsync 自动处理 insert/update)
                    ↓
    4. 更新 MEMORY.md 索引（如果 description 变了）
       await _indexManager.UpdateIndexAsync(memory)
                    ↓
         ✅ 返回成功
```

### 5.4 记忆删除流程

```
删除记忆 → /memory delete coding_style
                    ↓
    1. 删除 Markdown 文件
       await _memoryRepository.DeleteAsync(memoryId)
                    ↓
    2. 从 Qdrant 删除向量
       await _vectorRepository.DeleteAsync(memoryId)
                    ↓
    3. 从 MEMORY.md 索引中移除
       await _indexManager.RemoveFromIndexAsync(memoryId)
                    ↓
         ✅ 返回成功
```

### 5.5 初始迁移流程（/memory migrate-to-vectors）

```
用户执行 → /memory migrate-to-vectors
                    ↓
    1. 验证 Qdrant 健康状态
       if (!await _vectorRepository.IsHealthyAsync())
           throw new Exception("Qdrant 未运行，请启动：docker run -p 6333:6333 qdrant/qdrant")
                    ↓
    2. 扫描所有现有记忆文件
       allMemories = await _memoryRepository.GetAllAsync()
       total = allMemories.Count
       Console.WriteLine($"开始迁移 {total} 个记忆...")
                    ↓
    3. 分批处理（每批 10 个，避免 Ollama 过载）
       foreach batch in allMemories.Chunk(10):
                    ↓
           3.1 批量生成 Embedding（比单个快 5-10 倍）
               texts = batch.Select(m =>
                   $"{m.Name} {m.Description} {m.Content}").ToList()
               embeddings = await _embeddingClient
                               .GenerateBatchEmbeddingsAsync(texts)
                    ↓
           3.2 批量插入 Qdrant（并行）
               tasks = batch.Zip(embeddings).Select(async pair =>
               {
                   var (memory, embedding) = pair;
                   var metadata = CreateMetadata(memory);
                   await _vectorRepository.UpsertAsync(
                       memory.Id, embedding, metadata);
               })
               await Task.WhenAll(tasks)
                    ↓
           3.3 更新进度
               processed += batch.Length
               progress = processed * 100 / total
               Console.WriteLine($"已迁移 {processed}/{total} ({progress}%)...")
                    ↓
    4. 迁移完成
       Console.WriteLine($"✅ 成功迁移 {total} 个记忆到向量数据库")
       Console.WriteLine($"💾 向量存储位置：{qdrantUrl}/collections/{collectionName}")
```

---

## 6. 核心实现

### 6.1 OllamaEmbeddingClient 实现要点

```csharp
public class OllamaEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OllamaEmbeddingClient> _logger;

    public string ProviderName => "Ollama";
    public int Dimensions => 768;  // nomic-embed-text

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                model = _options.Model,
                prompt = text
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.BaseUrl}/api/embeddings",
                request,
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<EmbeddingResponse>(ct);

            if (result?.Embedding == null || result.Embedding.Length != Dimensions)
            {
                throw new EmbeddingException("Invalid embedding response");
            }

            return result.Embedding;
        }
        catch (HttpRequestException ex)
        {
            throw new EmbeddingException(
                $"Ollama 服务不可用。请确保 Ollama 正在运行：ollama serve\n" +
                $"并已下载模型：ollama pull {_options.Model}\n" +
                $"错误：{ex.Message}",
                ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            throw new EmbeddingException(
                $"Embedding 生成超时（{_options.TimeoutSeconds}秒）",
                ex);
        }
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken ct = default)
    {
        // 并行生成（Ollama API 不支持原生批量，所以并行调用）
        var tasks = texts.Select(text => GenerateEmbeddingAsync(text, ct));
        var embeddings = await Task.WhenAll(tasks);
        return embeddings.ToList();
    }
}
```

### 6.2 QdrantVectorRepository 实现要点

```csharp
public class QdrantVectorRepository : IVectorRepository
{
    private readonly QdrantClient _client;
    private readonly VectorDBOptions _options;
    private readonly ILogger<QdrantVectorRepository> _logger;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private bool _lastHealthStatus = false;

    public async Task UpsertAsync(
        Guid memoryId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken ct = default)
    {
        try
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = memoryId.ToString() },
                Vectors = embedding,
                Payload = metadata.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Value { StringValue = kvp.Value.ToString() })
            };

            await _client.UpsertAsync(
                collectionName: _options.CollectionName,
                points: new[] { point },
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            throw new VectorRepositoryException(
                $"向量写入失败。请检查 Qdrant 服务：{_options.Url}\n" +
                $"错误：{ex.Message}",
                ex);
        }
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        Dictionary<string, object>? filters = null,
        CancellationToken ct = default)
    {
        try
        {
            var searchParams = new SearchPoints
            {
                CollectionName = _options.CollectionName,
                Vector = queryVector,
                Limit = (ulong)topK,
                WithPayload = true
            };

            // 添加过滤条件（如 type=User）
            if (filters != null && filters.Any())
            {
                searchParams.Filter = CreateFilter(filters);
            }

            var response = await _client.SearchAsync(searchParams, ct);

            return response.Result.Select(point => new VectorSearchResult
            {
                MemoryId = Guid.Parse(point.Id.Uuid),
                Score = point.Score,
                Metadata = point.Payload.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value.StringValue)
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new VectorRepositoryException(
                $"向量搜索失败：{ex.Message}",
                ex);
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // 健康检查结果缓存 30 秒（避免频繁检查）
        if (DateTime.UtcNow - _lastHealthCheck <
            TimeSpan.FromSeconds(_options.HealthCheckCacheSeconds))
        {
            return _lastHealthStatus;
        }

        try
        {
            await _client.GetCollectionInfoAsync(_options.CollectionName, ct);
            _lastHealthStatus = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant 健康检查失败");
            _lastHealthStatus = false;
        }

        _lastHealthCheck = DateTime.UtcNow;
        return _lastHealthStatus;
    }
}
```

### 6.3 MemoryRepository 双写逻辑

```csharp
public async Task<Memory> CreateAsync(Memory memory, CancellationToken ct = default)
{
    // 1. 写入 Markdown 文件（Source of Truth）
    await WriteMarkdownFileAsync(memory, ct);

    try
    {
        // 2. 生成 Embedding 向量
        var text = $"{memory.Name} {memory.Description} {memory.Content}";
        var embedding = await _embeddingClient.GenerateEmbeddingAsync(text, ct);

        // 3. 存入向量数据库
        var metadata = new Dictionary<string, object>
        {
            ["memory_id"] = memory.Id.ToString(),
            ["type"] = memory.Type.ToString(),
            ["name"] = memory.Name,
            ["created_at"] = memory.CreatedAt.ToString("O")
        };

        await _vectorRepository.UpsertAsync(memory.Id, embedding, metadata, ct);

        _logger.LogInformation(
            "记忆 '{Name}' 已创建并向量化",
            memory.Name);
    }
    catch (Exception ex)
    {
        // 向量写入失败不影响记忆创建（文件已保存）
        _logger.LogWarning(ex,
            "向量化失败，记忆已保存到文件，稍后可通过 /memory sync 同步。" +
            "记忆 ID: {MemoryId}",
            memory.Id);

        // 可选：将失败的记忆 ID 加入重试队列（Phase 2 暂不实现）
    }

    // 4. 更新 MEMORY.md 索引
    await _indexManager.AddToIndexAsync(memory, ct);

    return memory;
}
```

### 6.4 MemoryRetrievalService 向量搜索 + 降级

```csharp
public async Task<List<Memory>> SearchBySemanticAsync(
    string query,
    int topK = 5,
    MemoryType? typeFilter = null,
    CancellationToken ct = default)
{
    // 检查 Qdrant 健康状态（30秒缓存）
    var isHealthy = await _vectorRepository.IsHealthyAsync(ct);

    if (isHealthy)
    {
        // 🚀 快速路径：向量搜索
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. 生成查询向量
            var queryVector = await _embeddingClient.GenerateEmbeddingAsync(query, ct);

            // 2. 构建过滤条件
            Dictionary<string, object>? filters = null;
            if (typeFilter.HasValue)
            {
                filters = new() { ["type"] = typeFilter.Value.ToString() };
            }

            // 3. 向量相似度搜索
            var vectorResults = await _vectorRepository.SearchAsync(
                queryVector,
                topK,
                filters,
                ct);

            // 4. 加载完整记忆实体
            var memories = new List<Memory>();
            foreach (var result in vectorResults)
            {
                var memory = await _memoryRepository.GetByIdAsync(result.MemoryId, ct);
                if (memory != null)
                {
                    memories.Add(memory);
                }
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "✅ 向量搜索 '{Query}' 返回 {Count} 个结果（耗时 {ElapsedMs}ms）",
                query, memories.Count, stopwatch.ElapsedMilliseconds);

            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "向量搜索失败，降级到关键词搜索");
            // 继续执行降级逻辑
        }
    }

    // 🐢 慢速路径：降级到关键词搜索
    _logger.LogWarning("Qdrant 不可用或搜索失败，降级到关键词搜索");

    // 显示用户提示
    OnFallbackToKeywordSearch?.Invoke(
        "⚠️ 向量搜索不可用，使用关键词搜索（较慢）\n" +
        "提示：启动 Qdrant 以获得更快的搜索速度\n" +
        "  docker run -p 6333:6333 qdrant/qdrant");

    var stopwatch2 = Stopwatch.StartNew();
    var results = await _memoryRepository.SearchAsync(
        query,
        type: typeFilter,
        ct);
    stopwatch2.Stop();

    _logger.LogInformation(
        "⚠️ 关键词搜索 '{Query}' 返回 {Count} 个结果（耗时 {ElapsedMs}ms）",
        query, results.Count, stopwatch2.ElapsedMilliseconds);

    return results;
}
```

---

## 7. 测试策略

### 7.1 测试覆盖率目标

| 模块 | 单元测试 | 集成测试 | 覆盖率目标 |
|------|---------|---------|-----------|
| OllamaEmbeddingClient | ✅ Mock HttpClient | ✅ 真实 Ollama | 85%+ |
| QdrantVectorRepository | ✅ Mock QdrantClient | ✅ 真实 Qdrant | 85%+ |
| MemoryRepository (双写) | ✅ Mock 依赖 | ✅ 文件 + 向量 | 80%+ |
| MemoryRetrievalService | ✅ Mock 依赖 | ✅ E2E 搜索 | 80%+ |

### 7.2 单元测试策略

#### OllamaEmbeddingClient 测试

```csharp
public class OllamaEmbeddingClientTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsVector()
    {
        // Arrange
        var mockHttp = CreateMockHttpMessageHandler();
        var client = new OllamaEmbeddingClient(
            new HttpClient(mockHttp),
            Options.Create(new EmbeddingOptions()),
            NullLogger<OllamaEmbeddingClient>.Instance);

        // Act
        var result = await client.GenerateEmbeddingAsync("test text");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(768);  // nomic-embed-text 维度
        result.All(v => v >= -1 && v <= 1).Should().BeTrue();  // 归一化
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_OllamaDown_ThrowsEmbeddingException()
    {
        // Arrange
        var mockHttp = CreateFailingHttpMessageHandler();
        var client = new OllamaEmbeddingClient(...);

        // Act & Assert
        var act = () => client.GenerateEmbeddingAsync("test");
        await act.Should().ThrowAsync<EmbeddingException>()
            .WithMessage("*Ollama 服务不可用*");
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_MultipleTexts_ReturnsBatch()
    {
        // 测试批量生成
    }
}
```

#### QdrantVectorRepository 测试

```csharp
public class QdrantVectorRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_ValidVector_Succeeds()
    {
        // Arrange
        var mockClient = Substitute.For<QdrantClient>();
        var repo = new QdrantVectorRepository(mockClient, ...);

        // Act
        await repo.UpsertAsync(
            Guid.NewGuid(),
            new float[768],
            new Dictionary<string, object>());

        // Assert
        await mockClient.Received(1).UpsertAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<PointStruct>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ValidVector_ReturnsResults()
    {
        // Arrange & Act & Assert
    }

    [Fact]
    public async Task IsHealthyAsync_QdrantDown_ReturnsFalse()
    {
        // Arrange
        var mockClient = Substitute.For<QdrantClient>();
        mockClient.GetCollectionInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CollectionInfo>(new Exception("Connection refused")));
        var repo = new QdrantVectorRepository(mockClient, ...);

        // Act
        var result = await repo.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }
}
```

### 7.3 集成测试策略

#### Ollama Embedding 集成测试

```csharp
[Collection("Ollama")]  // 需要 Ollama 运行
public class OllamaEmbeddingIntegrationTests
{
    private readonly IEmbeddingClient _client;

    public OllamaEmbeddingIntegrationTests()
    {
        _client = new OllamaEmbeddingClient(
            new HttpClient(),
            Options.Create(new EmbeddingOptions
            {
                BaseUrl = "http://localhost:11434",
                Model = "nomic-embed-text"
            }),
            NullLogger<OllamaEmbeddingClient>.Instance);
    }

    [Fact]
    public async Task GenerateEmbedding_RealOllama_ReturnsValidVector()
    {
        // Act
        var vector = await _client.GenerateEmbeddingAsync("Hello world");

        // Assert
        vector.Should().NotBeNull();
        vector.Should().HaveCount(768);
        vector.All(v => v >= -1 && v <= 1).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEmbedding_ChineseText_WorksCorrectly()
    {
        // Act
        var vector = await _client.GenerateEmbeddingAsync("你好世界");

        // Assert
        vector.Should().NotBeNull();
        vector.Should().HaveCount(768);
    }

    [Fact]
    public async Task GenerateBatchEmbeddings_RealOllama_ReturnsBatch()
    {
        // Arrange
        var texts = new List<string> { "text1", "text2", "text3" };

        // Act
        var vectors = await _client.GenerateBatchEmbeddingsAsync(texts);

        // Assert
        vectors.Should().HaveCount(3);
        vectors.All(v => v.Length == 768).Should().BeTrue();
    }
}
```

#### Qdrant 集成测试

```csharp
[Collection("Qdrant")]  // 需要 Qdrant 运行
public class QdrantVectorRepositoryIntegrationTests
{
    private readonly IVectorRepository _repo;
    private readonly string _testCollectionName = "test_memory_vectors";

    public QdrantVectorRepositoryIntegrationTests()
    {
        var client = new QdrantClient("http://localhost:6333");
        _repo = new QdrantVectorRepository(
            client,
            Options.Create(new VectorDBOptions
            {
                Url = "http://localhost:6333",
                CollectionName = _testCollectionName
            }),
            NullLogger<QdrantVectorRepository>.Instance);

        // 创建测试集合
        client.CreateCollectionAsync(
            _testCollectionName,
            new VectorParams { Size = 768, Distance = Distance.Cosine });
    }

    [Fact]
    public async Task UpsertAndSearch_RealQdrant_WorksEndToEnd()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var vector = GenerateRandomVector(768);
        var metadata = new Dictionary<string, object>
        {
            ["memory_id"] = memoryId.ToString(),
            ["type"] = "User",
            ["name"] = "test_memory"
        };

        // Act - 插入
        await _repo.UpsertAsync(memoryId, vector, metadata);

        // Act - 搜索（用相同向量搜索，应该找到）
        var results = await _repo.SearchAsync(vector, topK: 1);

        // Assert
        results.Should().HaveCount(1);
        results[0].MemoryId.Should().Be(memoryId);
        results[0].Score.Should().BeGreaterThan(0.99);  // 完全相同的向量
    }

    [Fact]
    public async Task SearchAsync_WithFilter_ReturnsFilteredResults()
    {
        // 测试带过滤条件的搜索
    }

    [Fact]
    public async Task IsHealthyAsync_RealQdrant_ReturnsTrue()
    {
        // Act
        var isHealthy = await _repo.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }
}
```

### 7.4 端到端测试

```csharp
[Collection("E2E")]  // 需要 Ollama + Qdrant
public class MemoryVectorSearchE2ETests
{
    private readonly IMemoryRepository _memoryRepo;
    private readonly IMemoryRetrievalService _retrievalService;

    [Fact]
    public async Task CreateAndSearchMemory_WithVectors_FindsRelevantMemories()
    {
        // Arrange - 创建测试记忆
        var memory1 = Memory.Create(
            MemoryType.User,
            "tdd_preference",
            "喜欢使用 TDD 方法",
            "我总是先写测试，再写实现代码。TDD 帮助我写出更健壮的代码。");

        var memory2 = Memory.Create(
            MemoryType.User,
            "coding_style",
            "代码风格偏好",
            "我喜欢函数式编程和不可变数据结构。");

        await _memoryRepo.CreateAsync(memory1);
        await _memoryRepo.CreateAsync(memory2);

        // 等待向量索引完成
        await Task.Delay(500);

        // Act - 语义搜索
        var results = await _retrievalService.SearchBySemanticAsync("测试驱动开发");

        // Assert
        results.Should().NotBeEmpty();
        results[0].Name.Should().Be("tdd_preference");  // 最相关的
        results[0].Description.Should().Contain("TDD");
    }

    [Fact]
    public async Task SearchMemory_QdrantDown_FallsBackToKeywordSearch()
    {
        // Arrange
        var memory = Memory.Create(
            MemoryType.User,
            "test_memory",
            "测试记忆",
            "包含关键词：重要");

        await _memoryRepo.CreateAsync(memory);

        // 停止 Qdrant（模拟故障）
        // ...

        // Act
        var results = await _retrievalService.SearchBySemanticAsync("重要");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(m => m.Name == "test_memory");
        // 验证降级日志
    }
}
```

### 7.5 CI/CD 测试配置

```yaml
# .github/workflows/phase2-tests.yml
name: V3 Phase 2 Tests

on:
  push:
    branches: [main, feature/phase2-*]
  pull_request:
    branches: [main]

jobs:
  unit-tests:
    name: Unit Tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore v3/

      - name: Run unit tests
        run: |
          cd v3
          dotnet test \
            --filter "Category=Unit" \
            --logger "console;verbosity=detailed"

  integration-tests:
    name: Integration Tests
    runs-on: ubuntu-latest
    services:
      qdrant:
        image: qdrant/qdrant:latest
        ports:
          - 6333:6333
        options: >-
          --health-cmd "curl -f http://localhost:6333/collections || exit 1"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 3

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Install Ollama
        run: |
          curl -fsSL https://ollama.ai/install.sh | sh
          ollama serve &
          sleep 5
          ollama pull nomic-embed-text

      - name: Verify services
        run: |
          curl http://localhost:11434/api/tags
          curl http://localhost:6333/collections

      - name: Run integration tests
        run: |
          cd v3
          dotnet test \
            --filter "Category=Integration" \
            --logger "console;verbosity=detailed"

  e2e-tests:
    name: End-to-End Tests
    runs-on: ubuntu-latest
    needs: [unit-tests, integration-tests]
    services:
      qdrant:
        image: qdrant/qdrant:latest
        ports:
          - 6333:6333

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Install Ollama
        run: |
          curl -fsSL https://ollama.ai/install.sh | sh
          ollama serve &
          sleep 5
          ollama pull nomic-embed-text

      - name: Run E2E tests
        run: |
          cd v3
          dotnet test \
            --filter "Category=E2E" \
            --logger "console;verbosity=detailed"

      - name: Generate coverage report
        run: |
          cd v3
          dotnet test \
            --collect:"XPlat Code Coverage" \
            --results-directory ./coverage

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          files: ./v3/coverage/**/coverage.cobertura.xml
```

---

## 8. 错误处理

### 8.1 自定义异常类型

```csharp
namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// Embedding 生成相关异常
/// </summary>
public class EmbeddingException : Exception
{
    public EmbeddingException(string message) : base(message) { }

    public EmbeddingException(string message, Exception inner)
        : base(message, inner) { }
}

/// <summary>
/// 向量数据库相关异常
/// </summary>
public class VectorRepositoryException : Exception
{
    public VectorRepositoryException(string message) : base(message) { }

    public VectorRepositoryException(string message, Exception inner)
        : base(message, inner) { }
}
```

### 8.2 错误处理策略

#### Ollama 错误处理

| 错误类型 | 处理策略 | 用户提示 |
|---------|---------|---------|
| 连接失败 | 抛出 EmbeddingException | "Ollama 服务不可用。请确保 Ollama 正在运行：`ollama serve`" |
| 超时 | 抛出 EmbeddingException | "Embedding 生成超时（30秒）" |
| 模型未下载 | 抛出 EmbeddingException | "模型未下载。请运行：`ollama pull nomic-embed-text`" |
| 无效响应 | 抛出 EmbeddingException | "Embedding 响应无效（维度不匹配）" |

#### Qdrant 错误处理

| 错误类型 | 处理策略 | 用户提示 |
|---------|---------|---------|
| 连接失败（健康检查） | 返回 false | （静默处理，触发降级） |
| 连接失败（写入） | 记录日志，允许继续 | "向量写入失败，记忆已保存，稍后可通过 `/memory sync` 同步" |
| 连接失败（搜索） | 自动降级 | "⚠️ 向量搜索不可用，使用关键词搜索（较慢）" |
| 集合不存在 | 自动创建集合 | （静默处理） |

#### 双写逻辑错误处理

```
创建记忆流程：
  1. 写入文件 ─┬─ 成功 → 继续
              └─ 失败 → 抛出异常，整个操作回滚

  2. 生成向量 ─┬─ 成功 → 继续
              └─ 失败 → 记录日志，允许继续（文件已保存）

  3. 写入向量 ─┬─ 成功 → 完成 ✓
              └─ 失败 → 记录日志，提示用户稍后同步
```

**原则**：
- **文件写入失败**：严重错误，整个操作失败
- **向量写入失败**：非致命错误，记忆已保存，可稍后同步
- **用户体验**：优先保证记忆不丢失，向量可以后续补全

---

## 9. 性能优化

### 9.1 批量处理优化

#### 批量 Embedding 生成

```csharp
// 单个生成（慢）
foreach (var memory in memories)
{
    var vector = await _embeddingClient.GenerateEmbeddingAsync(memory.Content);
    // 100 个记忆 = 100 次网络调用
}

// 批量生成（快 5-10 倍）
var texts = memories.Select(m => m.Content).ToList();
var vectors = await _embeddingClient.GenerateBatchEmbeddingsAsync(texts);
// 100 个记忆 = 10-20 次网络调用（批量处理，每批 10 个）
```

#### 批量向量写入

```csharp
// 并行写入
var tasks = memories.Zip(vectors).Select(async pair =>
{
    var (memory, vector) = pair;
    await _vectorRepository.UpsertAsync(memory.Id, vector, metadata);
});
await Task.WhenAll(tasks);  // 并行执行，速度提升 3-5 倍
```

### 9.2 连接池和缓存

#### HttpClient 连接池

```csharp
// DI 配置（Program.cs 或 DependencyInjection.cs）
services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));  // 连接重用 5 分钟
```

#### Qdrant 客户端单例

```csharp
// 单例模式（连接重用）
services.AddSingleton<QdrantClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<VectorDBOptions>>().Value;
    return new QdrantClient(options.Url);
});
```

#### 健康检查结果缓存

```csharp
// 避免每次搜索都检查 Qdrant 健康状态
private DateTime _lastHealthCheck = DateTime.MinValue;
private bool _lastHealthStatus = false;
private readonly TimeSpan _cacheTime = TimeSpan.FromSeconds(30);

public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
{
    if (DateTime.UtcNow - _lastHealthCheck < _cacheTime)
    {
        return _lastHealthStatus;  // 返回缓存结果（30秒内）
    }

    _lastHealthStatus = await CheckHealthAsync(ct);
    _lastHealthCheck = DateTime.UtcNow;
    return _lastHealthStatus;
}
```

### 9.3 性能指标

| 操作 | Phase 1 (LLM) | Phase 2 (向量) | 提升倍数 |
|------|---------------|----------------|---------|
| **生成 Embedding** | N/A | ~10-50ms | - |
| **单次记忆搜索 (10个)** | ~5-10秒 | ~10-50ms | 100-1000x |
| **单次记忆搜索 (100个)** | ~50-100秒 | ~10-50ms | 1000-10000x |
| **批量迁移 (100个)** | N/A | ~5-10秒 | - |
| **API 调用成本** | 100次/查询 | 1次/查询 | 节省 99% |

---

## 10. 部署指南

### 10.1 本地开发环境部署

#### 前置要求

- .NET 10.0 SDK
- Docker（用于 Qdrant）
- Ollama（本地 LLM 和 Embedding）

#### 步骤

```bash
# 1. 启动 Qdrant
docker run -d --name qdrant \
  -p 6333:6333 \
  -v ~/.agent/qdrant:/qdrant/storage \
  qdrant/qdrant

# 2. 验证 Qdrant
curl http://localhost:6333/collections
# 应返回: {"result":{"collections":[]}}

# 3. 启动 Ollama（如果未运行）
ollama serve

# 4. 下载 Embedding 模型
ollama pull nomic-embed-text

# 5. 验证 Ollama
curl http://localhost:11434/api/tags
# 应看到 nomic-embed-text 在模型列表中

# 6. 运行 V3 应用
cd v3/src/GeneralAgent.Hosts.Console
dotnet run

# 7. 在 REPL 中测试
> /memory add user test_memory
> /memory semantic-search "test"
```

### 10.2 Docker Compose 部署（推荐生产环境）

#### docker-compose.yml

```yaml
version: '3.8'

services:
  qdrant:
    image: qdrant/qdrant:latest
    container_name: agent-qdrant
    ports:
      - "6333:6333"
    volumes:
      - qdrant_data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/collections"]
      interval: 10s
      timeout: 5s
      retries: 3
    restart: unless-stopped

  agent-v3:
    build:
      context: .
      dockerfile: v3/Dockerfile
    container_name: agent-v3
    depends_on:
      qdrant:
        condition: service_healthy
    environment:
      - VectorDB__Url=http://qdrant:6333
      - Embedding__BaseUrl=http://host.docker.internal:11434  # Ollama 在宿主机
      - Memory__MemoryDirectory=/root/.agent/memory
    volumes:
      - agent_memory:/root/.agent/memory
    stdin_open: true
    tty: true
    restart: unless-stopped

volumes:
  qdrant_data:
    driver: local
  agent_memory:
    driver: local
```

#### Dockerfile

```dockerfile
# v3/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制项目文件
COPY v3/*.sln .
COPY v3/src/**/*.csproj ./
RUN for file in $(ls *.csproj); do \
      mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; \
    done

# 还原依赖
RUN dotnet restore

# 复制源代码
COPY v3/src/ ./src/

# 构建
RUN dotnet publish src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj \
    -c Release \
    -o /app/publish

# 运行时镜像
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GeneralAgent.Hosts.Console.dll"]
```

#### 启动和管理

```bash
# 启动所有服务
docker-compose up -d

# 查看日志
docker-compose logs -f agent-v3

# 进入 agent-v3 容器（交互式 REPL）
docker attach agent-v3

# 停止服务
docker-compose down

# 停止并删除数据
docker-compose down -v
```

### 10.3 生产环境注意事项

#### 资源配置

| 组件 | 最小配置 | 推荐配置 | 说明 |
|------|---------|---------|------|
| Qdrant | 256MB 内存 | 512MB-1GB | ~1KB/向量，100个记忆约 100KB |
| Ollama | 2GB 内存 | 4GB+ | nomic-embed-text 模型约 200MB |
| Agent V3 | 100MB 内存 | 256MB | .NET runtime + 应用 |

#### 数据持久化

```bash
# Qdrant 数据位置
~/.agent/qdrant/storage/
  └── collections/
      └── memory_vectors/

# 备份 Qdrant 数据
tar -czf qdrant-backup-$(date +%Y%m%d).tar.gz ~/.agent/qdrant/

# 恢复 Qdrant 数据
tar -xzf qdrant-backup-20260327.tar.gz -C ~/
```

#### 监控指标

监控以下关键指标：

1. **Qdrant 健康状态**
   ```bash
   curl http://localhost:6333/collections/memory_vectors
   ```

2. **Embedding 生成延迟**
   - 目标：< 50ms
   - 警告：> 100ms

3. **向量搜索延迟**
   - 目标：< 50ms
   - 警告：> 100ms

4. **降级事件频率**
   - 目标：< 1% 查询
   - 警告：> 5% 查询

5. **向量数据库大小**
   - 预估：~1KB/记忆

---

## 11. 迁移指南

### 11.1 Phase 1 → Phase 2 迁移

#### 迁移前准备

```bash
# 1. 备份现有数据
cp -r ~/.agent/memory ~/.agent/memory.backup.$(date +%Y%m%d)
echo "备份完成: ~/.agent/memory.backup.$(date +%Y%m%d)"

# 2. 验证备份
ls -lh ~/.agent/memory.backup.$(date +%Y%m%d)

# 3. 记录当前记忆数量
find ~/.agent/memory -name "*.md" -not -name "MEMORY.md" | wc -l
```

#### 迁移步骤

```bash
# Step 1: 更新代码到 Phase 2
git fetch origin
git checkout feature/phase2-vector-search

# Step 2: 安装依赖
cd v3
dotnet restore

# Step 3: 更新配置（如果需要）
# 编辑 src/GeneralAgent.Hosts.Console/appsettings.json
# 确保 Embedding 和 VectorDB 配置正确

# Step 4: 启动 Qdrant
docker run -d --name qdrant \
  -p 6333:6333 \
  -v ~/.agent/qdrant:/qdrant/storage \
  qdrant/qdrant

# 等待 Qdrant 启动（约 5 秒）
sleep 5

# 验证 Qdrant
curl http://localhost:6333/collections
# 应返回空集合列表

# Step 5: 验证 Ollama 和 Embedding 模型
ollama list | grep nomic-embed-text
# 如果没有，下载模型：
ollama pull nomic-embed-text

# Step 6: 运行应用
dotnet run --project src/GeneralAgent.Hosts.Console

# Step 7: 在 REPL 中迁移现有记忆
> /memory migrate-to-vectors
```

#### 迁移输出示例

```
开始迁移现有记忆到向量数据库...
✓ Qdrant 健康检查通过
✓ Ollama Embedding 模型可用
✓ 扫描到 50 个现有记忆

迁移进度:
[##########--------------------] 20% (10/50)
[####################----------] 40% (20/50)
[##############################] 60% (30/50)
[########################################] 80% (40/50)
[##################################################] 100% (50/50)

✅ 迁移完成！
  • 总计: 50 个记忆
  • 成功: 50 个
  • 失败: 0 个
  • 耗时: 15.2 秒
  • 向量存储: http://localhost:6333/collections/memory_vectors

提示: 现在可以使用 /memory semantic-search 进行高速语义搜索
```

#### 验证迁移结果

```bash
# 在 REPL 中

# 1. 检查向量数量
> /memory stats
记忆统计:
  • 文件系统: 50 个记忆
  • 向量数据库: 50 个向量
  • 一致性: ✓ 100%

# 2. 测试向量搜索
> /memory semantic-search "测试"
✅ 找到 3 个相关记忆（向量搜索，耗时 ~15ms）

1. tdd_preference (相似度: 0.92)
   描述: 喜欢使用 TDD 方法
   类型: User

2. unit_testing (相似度: 0.85)
   描述: 单元测试最佳实践
   类型: Knowledge

3. quality_assurance (相似度: 0.78)
   描述: 代码质量保证流程
   类型: Project

# 3. 测试自动降级（可选）
# 在另一个终端停止 Qdrant
$ docker stop qdrant

# 在 REPL 中再次搜索
> /memory semantic-search "测试"
⚠️ 向量搜索不可用，使用关键词搜索（较慢）
提示: 启动 Qdrant: docker start qdrant
✅ 找到 2 个相关记忆（关键词搜索，耗时 ~2s）

# 重新启动 Qdrant
$ docker start qdrant
```

### 11.2 回滚计划（如果出问题）

```bash
# 1. 停止当前应用
# Ctrl+C 或关闭终端

# 2. 停止 Qdrant
docker stop qdrant
docker rm qdrant

# 3. 恢复备份
rm -rf ~/.agent/memory
mv ~/.agent/memory.backup.YYYYMMDD ~/.agent/memory

# 4. 切换回 Phase 1
git checkout feature/phase1-complete

# 5. 重新构建和运行
cd v3
dotnet build
dotnet run --project src/GeneralAgent.Hosts.Console

# 6. 验证功能正常
# 在 REPL 中测试记忆搜索（使用 LLM 评分）
```

### 11.3 常见迁移问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| "Qdrant 服务不可用" | Qdrant 未启动 | `docker start qdrant` 或检查端口占用 |
| "Embedding 模型未下载" | nomic-embed-text 不存在 | `ollama pull nomic-embed-text` |
| "迁移进度停滞" | Ollama 过载或超时 | 减小批量大小（修改代码）或重启 Ollama |
| "部分记忆迁移失败" | 记忆内容格式问题 | 查看日志，手动修复问题记忆后重新迁移 |
| "向量搜索无结果" | 索引未完成或集合名错误 | 检查配置，重新运行迁移 |

---

## 12. 验收标准

### 12.1 功能性验收

| # | 验收项 | 测试方法 | 状态 |
|---|--------|---------|------|
| 1 | Ollama 集成工作正常 | 手动测试：`/memory add user test` 创建记忆 | ☐ |
| 2 | Qdrant 集成工作正常 | 检查 Qdrant 中是否有向量 | ☐ |
| 3 | 双写逻辑正常 | 创建记忆后，文件和向量都存在 | ☐ |
| 4 | 向量搜索返回相关结果 | 搜索 "测试" 返回 TDD 相关记忆 | ☐ |
| 5 | 更新记忆同步更新向量 | 更新记忆内容，重新搜索能找到 | ☐ |
| 6 | 删除记忆同步删除向量 | 删除记忆后，向量也被删除 | ☐ |
| 7 | 自动降级工作正常 | 停止 Qdrant，搜索降级到关键词 | ☐ |
| 8 | 迁移命令工作正常 | `/memory migrate-to-vectors` 成功迁移所有记忆 | ☐ |
| 9 | 健康检查工作正常 | Qdrant 停止时 IsHealthyAsync 返回 false | ☐ |
| 10 | 错误提示清晰友好 | Qdrant 不可用时显示启动提示 | ☐ |

### 12.2 性能验收

| # | 验收项 | 目标值 | 实际值 | 状态 |
|---|--------|--------|--------|------|
| 1 | Embedding 生成时间 | < 50ms | - | ☐ |
| 2 | 向量搜索时间（10个记忆） | < 50ms | - | ☐ |
| 3 | 向量搜索时间（100个记忆） | < 100ms | - | ☐ |
| 4 | 相比 Phase 1 性能提升 | > 100x | - | ☐ |
| 5 | API 调用成本节省 | > 95% | - | ☐ |
| 6 | 批量迁移速度（100个记忆） | < 30秒 | - | ☐ |

### 12.3 质量验收

| # | 验收项 | 目标值 | 实际值 | 状态 |
|---|--------|--------|--------|------|
| 1 | 单元测试覆盖率 | > 80% | - | ☐ |
| 2 | 集成测试通过率 | 100% | - | ☐ |
| 3 | E2E 测试通过率 | 100% | - | ☐ |
| 4 | 代码审查完成 | - | - | ☐ |
| 5 | 文档完整性 | 100% | - | ☐ |

### 12.4 文档验收

| # | 验收项 | 状态 |
|---|--------|------|
| 1 | 设计文档完整 | ☐ |
| 2 | API 文档完整 | ☐ |
| 3 | 部署指南完整 | ☐ |
| 4 | 迁移指南完整 | ☐ |
| 5 | 用户手册更新 | ☐ |
| 6 | CHANGELOG 更新 | ☐ |

---

## 13. 实施计划

### 13.1 渐进式实施（3 迭代）

#### 迭代 1：Embedding 基础设施（1-2天）

**任务列表**：
- [ ] 创建 `IEmbeddingClient` 接口（Core）
- [ ] 实现 `OllamaEmbeddingClient`（Infrastructure.Embedding）
- [ ] 添加 `EmbeddingOptions` 配置
- [ ] 实现 DI 注册
- [ ] 编写单元测试（Mock HttpClient）
- [ ] 编写集成测试（需要 Ollama 和 nomic-embed-text）
- [ ] 更新 appsettings.json

**验收标准**：
- ✅ 可以调用 Ollama API 生成 Embedding 向量
- ✅ 向量维度为 768
- ✅ 支持中文文本
- ✅ 单元测试覆盖率 > 85%
- ✅ 集成测试通过

**交付物**：
- `GeneralAgent.Core/Abstractions/IEmbeddingClient.cs`
- `GeneralAgent.Infrastructure.Embedding/` 项目
- `tests/.../OllamaEmbeddingClientTests.cs`
- `tests/.../OllamaEmbeddingIntegrationTests.cs`

---

#### 迭代 2：Qdrant 集成和向量存储（1-2天）

**任务列表**：
- [ ] 添加 Qdrant.Client NuGet 包
- [ ] 创建 `IVectorRepository` 接口（Core）
- [ ] 创建 `VectorSearchResult` 和 `VectorCollectionStats` 模型（Core）
- [ ] 实现 `QdrantVectorRepository`（Infrastructure.VectorDB）
- [ ] 添加 `VectorDBOptions` 配置
- [ ] 实现向量 CRUD 操作（Upsert, Search, Delete）
- [ ] 实现健康检查和统计功能
- [ ] 实现健康检查结果缓存（30秒）
- [ ] 编写单元测试（Mock QdrantClient）
- [ ] 编写集成测试（需要 Qdrant Docker）
- [ ] 更新 appsettings.json

**验收标准**：
- ✅ 可以在 Qdrant 中存储向量
- ✅ 可以执行向量相似度搜索
- ✅ 可以删除向量
- ✅ 健康检查工作正常
- ✅ 单元测试覆盖率 > 85%
- ✅ 集成测试通过

**交付物**：
- `GeneralAgent.Core/Abstractions/IVectorRepository.cs`
- `GeneralAgent.Core/Models/VectorSearchResult.cs`
- `GeneralAgent.Core/Models/VectorCollectionStats.cs`
- `GeneralAgent.Infrastructure.VectorDB/` 项目
- `tests/.../QdrantVectorRepositoryTests.cs`
- `tests/.../QdrantVectorRepositoryIntegrationTests.cs`

---

#### 迭代 3：记忆系统集成（1-2天）

**任务列表**：
- [ ] 修改 `MemoryRepository.CreateAsync` 添加双写逻辑
- [ ] 修改 `MemoryRepository.UpdateAsync` 更新向量
- [ ] 修改 `MemoryRepository.DeleteAsync` 删除向量
- [ ] 添加容错处理（向量写入失败时的处理）
- [ ] 修改 `MemoryRetrievalService.SearchBySemanticAsync` 使用向量搜索
- [ ] 实现自动降级逻辑（Qdrant 不可用时）
- [ ] 添加降级提示和日志
- [ ] 实现 `/memory migrate-to-vectors` 命令
- [ ] 实现 `/memory sync` 命令（同步失败的向量）
- [ ] 更新 `/memory help` 命令
- [ ] 编写端到端测试
- [ ] 更新用户文档（CLI_GUIDE.md, CLI_REFERENCE.md）

**验收标准**：
- ✅ 创建记忆时自动生成并存储向量
- ✅ 更新记忆时自动更新向量
- ✅ 删除记忆时自动删除向量
- ✅ 语义搜索使用向量搜索（响应时间 < 100ms）
- ✅ Qdrant 不可用时自动降级到关键词搜索
- ✅ 降级时显示友好提示
- ✅ 迁移命令成功迁移所有现有记忆
- ✅ 端到端测试通过
- ✅ 文档更新完成

**交付物**：
- 修改后的 `MemoryRepository.cs`
- 修改后的 `MemoryRetrievalService.cs`
- 修改后的 `AgentRepl.cs`
- `tests/.../MemoryVectorSearchE2ETests.cs`
- 更新的 `v3/docs/CLI_GUIDE.md`
- 更新的 `v3/docs/CLI_REFERENCE.md`

---

### 13.2 时间表

```
Week 1:
  Mon-Tue:  迭代 1 - Embedding 基础设施
  Wed-Thu:  迭代 2 - Qdrant 集成
  Fri:      迭代 3 - 记忆系统集成（Day 1）

Week 2:
  Mon:      迭代 3 - 记忆系统集成（Day 2）
  Tue:      端到端测试 + 文档更新
  Wed:      代码审查 + 修复问题
  Thu:      Phase 1 → Phase 2 迁移测试
  Fri:      最终验收 + 发布准备
```

**总计**: 7-10 个工作日（含测试、文档和 buffer）

---

## 14. 风险和缓解措施

### 14.1 技术风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|---------|
| Qdrant 性能不达标 | 高 | 低 | 预先进行性能测试，有降级策略 |
| Ollama Embedding 质量不佳 | 中 | 低 | 预留切换到 OpenAI Embeddings 的接口 |
| 双写逻辑复杂，容易出 bug | 中 | 中 | 完善的单元测试和集成测试 |
| 向量数据与文件数据不一致 | 高 | 中 | 提供 `/memory sync` 命令手动同步 |
| Qdrant 依赖导致部署复杂 | 低 | 中 | 提供 Docker Compose 一键部署 |

### 14.2 业务风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|---------|
| 用户不熟悉 Docker 部署 | 中 | 高 | 提供详细的部署文档和视频教程 |
| 迁移过程中数据丢失 | 高 | 低 | 强制备份，提供回滚方案 |
| 性能提升不明显 | 中 | 低 | 预先测试，确保性能提升 100+ 倍 |
| Phase 2 引入新 bug | 中 | 中 | 完善的测试覆盖，渐进式发布 |

### 14.3 缓解策略

1. **完善的测试**：单元测试覆盖率 > 80%，集成测试和 E2E 测试覆盖所有关键路径
2. **渐进式实施**：分 3 个迭代，每个迭代独立可测试
3. **自动降级**：Qdrant 不可用时自动回退到 Phase 1 的关键词搜索
4. **备份和回滚**：强制备份，提供详细的回滚指南
5. **详细文档**：部署指南、迁移指南、故障排查文档

---

## 15. 未来扩展（Phase 3+）

### 15.1 多 Embedding 提供商支持

支持用户在 Ollama 和 OpenAI Embeddings 之间切换：

```json
{
  "Embedding": {
    "Provider": "OpenAI",  // 或 "Ollama"
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "Model": "text-embedding-3-small"
  }
}
```

**注意**：切换提供商需要重新生成所有向量（不兼容）

### 15.2 记忆去重

自动检测和合并相似记忆：

```
检测到 2 个相似记忆（相似度 0.95）：
  1. tdd_preference: "我喜欢使用 TDD"
  2. testing_approach: "我总是先写测试"

是否合并？[y/n]
```

### 15.3 记忆过期和归档

根据时效性自动归档旧记忆：

```csharp
public class Memory
{
    public DateTime? ExpiresAt { get; init; }  // 过期时间
    public bool IsArchived { get; init; }      // 是否归档
}
```

### 15.4 记忆关系和知识图谱

支持记忆之间的引用关系：

```csharp
public class MemoryRelation
{
    public Guid FromMemoryId { get; init; }
    public Guid ToMemoryId { get; init; }
    public string RelationType { get; init; }  // "related", "contradicts", "updates"
}
```

### 15.5 向量数据库切换

支持切换到其他向量数据库（Milvus, ChromaDB）：

```csharp
public interface IVectorRepository
{
    // 统一接口，多个实现
}

// 实现
public class MilvusVectorRepository : IVectorRepository { }
public class ChromaDBVectorRepository : IVectorRepository { }
```

---

## 16. 附录

### 16.1 依赖的 NuGet 包

| 包名 | 版本 | 用途 |
|------|------|------|
| Qdrant.Client | 最新稳定版 | Qdrant 客户端 SDK |
| Microsoft.Extensions.Http | 10.0.x | HttpClient 工厂和连接池 |
| Microsoft.Extensions.Options | 10.0.x | 配置绑定 |
| FluentAssertions | 最新版 | 测试断言 |
| NSubstitute | 最新版 | Mock 框架 |

### 16.2 参考资料

- [Ollama API 文档](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Qdrant 文档](https://qdrant.tech/documentation/)
- [nomic-embed-text 模型](https://ollama.com/library/nomic-embed-text)
- [HNSW 算法论文](https://arxiv.org/abs/1603.09320)
- [V2 Rust 版本 Qdrant 集成代码](../../v2/crates/agent-rag/)

### 16.3 术语表

| 术语 | 说明 |
|------|------|
| **Embedding** | 向量嵌入，将文本转换为高维向量表示 |
| **ANN** | Approximate Nearest Neighbor，近似最近邻搜索 |
| **HNSW** | Hierarchical Navigable Small World，高效的 ANN 算法 |
| **Cosine Similarity** | 余弦相似度，衡量向量之间的相似程度 |
| **双写** | 同时写入两个存储系统（文件 + 向量数据库） |
| **降级** | 当主要功能不可用时，回退到备用功能 |
| **Source of Truth** | 数据的权威来源（本设计中为 Markdown 文件） |

---

**文档版本**: 1.0
**最后更新**: 2026-03-27
**审查状态**: 待审查
**下一步**: 用户审查设计文档 → 调用 writing-plans 技能创建实施计划
