# V3 Phase 2: Embedding 向量化和向量数据库集成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通过集成 Embedding 模型和向量数据库，将记忆搜索性能从 50-100秒 提升到 10-50毫秒（1000-10000倍提升）

**Architecture:** 采用双写模式（文件系统 + 向量数据库），Qdrant 不可用时自动降级到关键词搜索。Markdown 文件为 source of truth，向量数据库为性能加速层。

**Tech Stack:** .NET 10, Ollama + nomic-embed-text (768维), Qdrant Vector DB, xUnit, FluentAssertions

---

## 完整计划文档

**注意**：完整的详细实施计划（包含所有代码和步骤）请参考设计文档的 Section 13。本计划提供执行路径和关键实施细节。

由于完整计划包含 15+ 个任务，每个任务 5-10 个步骤，总计超过 150 个详细步骤，完整版本过长。

**推荐方式**：按迭代执行，每个迭代完成后进行验收测试。

---

## 迭代 1: Embedding 基础设施

### Task 1: Embedding 核心接口和异常

**Files:**
- Create: `v3/src/GeneralAgent.Core/Exceptions/EmbeddingException.cs`
- Create: `v3/src/GeneralAgent.Core/Abstractions/IEmbeddingClient.cs`

**参考**: 设计文档 Section 4.1 (IEmbeddingClient 接口定义)

**实施指南**:
- 创建 EmbeddingException 继承自 Exception，提供两个构造函数
- 创建 IEmbeddingClient 接口，包含：
  - `string ProviderName { get; }` - 提供商名称
  - `int Dimensions { get; }` - 向量维度
  - `Task<float[]> GenerateEmbeddingAsync(string, CancellationToken)` - 单个生成
  - `Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string>, CancellationToken)` - 批量生成

**验收**:
- `dotnet build src/GeneralAgent.Core` 成功
- 提交消息: "feat(core): 添加 Embedding 接口和异常类型"

---

### Task 2: Embedding 基础设施项目

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/GeneralAgent.Infrastructure.Embedding.csproj`
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/EmbeddingOptions.cs`

**参考**: 设计文档 Section 4.4 (配置模型)

**实施指南**:
- 创建 .csproj 文件，目标框架 net10.0，引用 Core 项目和以下包：
  - Microsoft.Extensions.Http (10.0.0)
  - Microsoft.Extensions.Logging.Abstractions (10.0.0)
  - Microsoft.Extensions.Options (10.0.0)
- 创建 EmbeddingOptions 类，包含字段：Provider, BaseUrl, Model, TimeoutSeconds
- SectionName 常量设为 "Embedding"

**验收**:
- `dotnet build src/GeneralAgent.Infrastructure.Embedding` 成功
- 提交消息: "feat(embedding): 创建 Embedding 基础设施项目"

---

### Task 3: OllamaEmbeddingClient 实现

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/DTOs/EmbeddingRequest.cs`
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/DTOs/EmbeddingResponse.cs`
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/OllamaEmbeddingClient.cs`

**参考**: 设计文档 Section 6.1 (OllamaEmbeddingClient 实现要点)

**实施指南**:
- 创建 DTO 类：EmbeddingRequest (Model, Prompt), EmbeddingResponse (Embedding)
- 实现 OllamaEmbeddingClient:
  - 构造函数注入 HttpClient, IOptions<EmbeddingOptions>, ILogger
  - ProviderName = "Ollama", Dimensions = 768
  - GenerateEmbeddingAsync: 调用 `/api/embeddings`，处理错误（HttpRequestException, TaskCanceledException）
  - GenerateBatchEmbeddingsAsync: 并行调用 GenerateEmbeddingAsync

**验收**:
- `dotnet build src/GeneralAgent.Infrastructure.Embedding` 成功
- 提交消息: "feat(embedding): 实现 OllamaEmbeddingClient"

---

### Task 4: Embedding DI 注册

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.Embedding/DependencyInjection.cs`

**实施指南**:
- 创建静态类 DependencyInjection
- 添加扩展方法 AddEmbedding(IServiceCollection, IConfiguration)
- 使用 Configure<EmbeddingOptions> 绑定配置
- 使用 AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient> 配置 HTTP 客户端，连接池生命周期 5 分钟

**验收**:
- `dotnet build src/GeneralAgent.Infrastructure.Embedding` 成功
- 提交消息: "feat(embedding): 添加 DI 注册扩展"

---

### Task 5: Embedding 单元测试

**Files:**
- Create: `v3/tests/GeneralAgent.Infrastructure.Embedding.Tests/GeneralAgent.Infrastructure.Embedding.Tests.csproj`
- Create: `v3/tests/GeneralAgent.Infrastructure.Embedding.Tests/OllamaEmbeddingClientTests.cs`

**参考**: 设计文档 Section 7.2 (单元测试策略)

**实施指南**:
- 创建测试项目，引用：xunit, FluentAssertions, NSubstitute, coverlet.collector
- 创建 MockHttpMessageHandler 辅助类
- 编写测试：
  - GenerateEmbeddingAsync_EmptyText_ThrowsArgumentException
  - GenerateEmbeddingAsync_ValidText_ReturnsVector (768 维)
  - GenerateEmbeddingAsync_OllamaDown_ThrowsEmbeddingException
  - GenerateBatchEmbeddingsAsync_MultipleTexts_ReturnsBatch

**验收**:
- `dotnet test tests/GeneralAgent.Infrastructure.Embedding.Tests` - 4 passed
- 提交消息: "test(embedding): 添加 OllamaEmbeddingClient 单元测试"

---

### Task 6: Embedding 集成测试

**Files:**
- Create: `v3/tests/GeneralAgent.Integration.Tests/Embedding/OllamaEmbeddingIntegrationTests.cs`

**参考**: 设计文档 Section 7.3 (集成测试策略)

**前置条件**: Ollama 服务运行在 http://localhost:11434，nomic-embed-text 模型已下载

**实施指南**:
- 创建测试类，标记 [Collection("Ollama")] 和 [Trait("Category", "Integration")]
- 编写测试：
  - GenerateEmbedding_RealOllama_ReturnsValidVector
  - GenerateEmbedding_ChineseText_WorksCorrectly
  - GenerateBatchEmbeddings_RealOllama_ReturnsBatch
  - GenerateEmbedding_LongText_WorksCorrectly

**验收**:
- `dotnet test tests/GeneralAgent.Integration.Tests --filter "Category=Integration&FullyQualifiedName~Embedding"` - all passed
- 提交消息: "test(embedding): 添加 Ollama 集成测试"

---

### Task 7: 更新 appsettings.json

**Files:**
- Modify: `v3/src/GeneralAgent.Hosts.Console/appsettings.json`

**参考**: 设计文档 Section 4.5 (配置文件)

**实施指南**:
- 在 appsettings.json 中添加 "Embedding" 配置节：
  - Provider: "Ollama"
  - BaseUrl: "http://localhost:11434"
  - Model: "nomic-embed-text"
  - TimeoutSeconds: 30

**验收**:
- 配置文件格式正确（JSON 有效）
- 提交消息: "feat(config): 添加 Embedding 配置到 appsettings.json"

---

**迭代 1 验收标准**：
- ✅ 可以生成 768 维向量
- ✅ 支持中文和英文文本
- ✅ 单元测试覆盖率 > 85%
- ✅ 集成测试通过（需要 Ollama 运行）

---

## 迭代 2: Qdrant 集成和向量存储

### Task 8: VectorDB 核心模型和接口

**Files:**
- Create: `v3/src/GeneralAgent.Core/Exceptions/VectorRepositoryException.cs`
- Create: `v3/src/GeneralAgent.Core/Models/VectorSearchResult.cs`
- Create: `v3/src/GeneralAgent.Core/Models/VectorCollectionStats.cs`
- Create: `v3/src/GeneralAgent.Core/Abstractions/IVectorRepository.cs`

**参考**: 设计文档 Section 4.2-4.3

**实施指南**: 创建异常类、两个 record 模型（VectorSearchResult 包含 MemoryId/Score/Metadata，VectorCollectionStats 包含 VectorCount/Dimensions/IndexType）、IVectorRepository 接口（UpsertAsync, SearchAsync, DeleteAsync, IsHealthyAsync, GetStatsAsync）

**验收**: `dotnet build src/GeneralAgent.Core` 成功，提交消息: "feat(core): 添加向量数据库模型和接口"

---

### Task 9: VectorDB 基础设施项目

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.VectorDB/GeneralAgent.Infrastructure.VectorDB.csproj`
- Create: `v3/src/GeneralAgent.Infrastructure.VectorDB/VectorDBOptions.cs`

**参考**: 设计文档 Section 4.4

**实施指南**: 创建项目文件（引用 Core 和 Qdrant.Client 1.14.0），创建 VectorDBOptions（Provider, Url, CollectionName, EnableFallback, HealthCheckCacheSeconds）

**验收**: `dotnet build src/GeneralAgent.Infrastructure.VectorDB` 成功，提交: "feat(vectordb): 创建 VectorDB 基础设施项目"

---

### Task 10: QdrantVectorRepository 实现

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.VectorDB/QdrantVectorRepository.cs`

**参考**: 设计文档 Section 6.2

**实施指南**: 实现 IVectorRepository，构造函数注入 QdrantClient/VectorDBOptions/ILogger，实现 UpsertAsync（PointStruct）、SearchAsync（SearchPoints）、DeleteAsync、IsHealthyAsync（带 30 秒缓存）、GetStatsAsync

**验收**: `dotnet build` 成功，提交: "feat(vectordb): 实现 QdrantVectorRepository"

---

### Task 11: VectorDB DI 注册

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.VectorDB/DependencyInjection.cs`

**实施指南**: 创建 AddVectorDB 扩展方法，Configure<VectorDBOptions>，AddSingleton<QdrantClient>，AddSingleton<IVectorRepository, QdrantVectorRepository>

**验收**: `dotnet build` 成功，提交: "feat(vectordb): 添加 DI 注册扩展"

---

### Task 12: VectorDB 单元测试

**Files:**
- Create: `v3/tests/GeneralAgent.Infrastructure.VectorDB.Tests/` (项目和测试类)

**参考**: 设计文档 Section 7.2

**实施指南**: 创建测试项目，Mock QdrantClient，测试 UpsertAsync/SearchAsync/DeleteAsync/IsHealthyAsync

**验收**: `dotnet test` - all passed，提交: "test(vectordb): 添加 QdrantVectorRepository 单元测试"

---

### Task 13: VectorDB 集成测试

**Files:**
- Create: `v3/tests/GeneralAgent.Integration.Tests/VectorDB/QdrantVectorRepositoryIntegrationTests.cs`

**参考**: 设计文档 Section 7.3

**前置条件**: `docker run -d -p 6333:6333 qdrant/qdrant`

**实施指南**: 测试真实 Qdrant，测试 UpsertAndSearch/SearchWithFilter/IsHealthy

**验收**: 集成测试通过，提交: "test(vectordb): 添加 Qdrant 集成测试"

---

### Task 14: 更新 appsettings.json (VectorDB)

**Files:**
- Modify: `v3/src/GeneralAgent.Hosts.Console/appsettings.json`

**实施指南**: 添加 "VectorDB" 配置节（Provider: "Qdrant", Url, CollectionName, EnableFallback: true, HealthCheckCacheSeconds: 30）

**验收**: JSON 有效，提交: "feat(config): 添加 VectorDB 配置"

---

**迭代 2 验收标准**：
- ✅ 可以在 Qdrant 中存储和检索向量
- ✅ 相似度搜索返回正确结果
- ✅ 健康检查工作正常（带缓存）
- ✅ 单元测试覆盖率 > 85%
- ✅ 集成测试通过

**关键实现代码示例**（QdrantVectorRepository.cs）：

```csharp
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
            $"向量写入失败：{ex.Message}", ex);
    }
}

public async Task<List<VectorSearchResult>> SearchAsync(
    float[] queryVector,
    int topK = 5,
    Dictionary<string, object>? filters = null,
    CancellationToken ct = default)
{
    var searchParams = new SearchPoints
    {
        CollectionName = _options.CollectionName,
        Vector = queryVector,
        Limit = (ulong)topK,
        WithPayload = true
    };

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

public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
{
    // 健康检查结果缓存 30 秒
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
```

**详细代码请参考设计文档 Section 4（接口设计）和 Section 6（核心实现）。**

**验收标准**：
- ✅ 可以在 Qdrant 中存储和检索向量
- ✅ 相似度搜索返回正确结果
- ✅ 健康检查工作正常（带缓存）
- ✅ 单元测试覆盖率 > 85%
- ✅ 集成测试通过（需要 Qdrant 运行）

---

## 迭代 3: 记忆系统集成

### Task 16: MemoryRepository 双写逻辑

**Files:**
- Modify: `v3/src/GeneralAgent.Infrastructure.Memory/Repositories/MemoryRepository.cs`

**目标**：在 CreateAsync/UpdateAsync/DeleteAsync 中添加向量同步逻辑

- [ ] **步骤 16.1: 修改 MemoryRepository 构造函数，注入依赖**

```csharp
public class MemoryRepository : IMemoryRepository
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorRepository _vectorRepository;
    private readonly MemoryIndexManager _indexManager;
    private readonly ILogger<MemoryRepository> _logger;
    // ... 其他字段

    public MemoryRepository(
        IOptions<MemoryOptions> options,
        MemoryIndexManager indexManager,
        IEmbeddingClient embeddingClient,
        IVectorRepository vectorRepository,
        ILogger<MemoryRepository> logger)
    {
        _options = options.Value;
        _indexManager = indexManager;
        _embeddingClient = embeddingClient;
        _vectorRepository = vectorRepository;
        _logger = logger;
        // ... 初始化
    }
}
```

- [ ] **步骤 16.2: 修改 CreateAsync 添加双写逻辑**

```csharp
public async Task<Memory> CreateAsync(Memory memory, CancellationToken ct = default)
{
    // 1. 写入 Markdown 文件（Source of Truth）
    var filePath = GetFilePath(memory.Type, memory.Name);
    await WriteMarkdownFileAsync(memory, filePath, ct);

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
            "记忆 '{Name}' 已创建并向量化", memory.Name);
    }
    catch (Exception ex)
    {
        // 向量写入失败不影响记忆创建（文件已保存）
        _logger.LogWarning(ex,
            "向量化失败，记忆已保存到文件。记忆 ID: {MemoryId}",
            memory.Id);
    }

    // 4. 更新 MEMORY.md 索引
    await _indexManager.AddToIndexAsync(memory, ct);

    return memory;
}
```

- [ ] **步骤 16.3: 修改 UpdateAsync 添加向量更新逻辑**

```csharp
public async Task<Memory> UpdateAsync(Memory memory, CancellationToken ct = default)
{
    // 1. 更新 Markdown 文件
    var filePath = GetFilePath(memory.Type, memory.Name);
    await WriteMarkdownFileAsync(memory, filePath, ct);

    try
    {
        // 2. 重新生成 Embedding（内容变了）
        var text = $"{memory.Name} {memory.Description} {memory.Content}";
        var newEmbedding = await _embeddingClient.GenerateEmbeddingAsync(text, ct);

        // 3. 更新 Qdrant 中的向量
        var metadata = new Dictionary<string, object>
        {
            ["memory_id"] = memory.Id.ToString(),
            ["type"] = memory.Type.ToString(),
            ["name"] = memory.Name,
            ["updated_at"] = DateTime.UtcNow.ToString("O")
        };

        await _vectorRepository.UpsertAsync(memory.Id, newEmbedding, metadata, ct);

        _logger.LogInformation(
            "记忆 '{Name}' 已更新并重新向量化", memory.Name);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "向量更新失败，记忆文件已更新。记忆 ID: {MemoryId}",
            memory.Id);
    }

    // 4. 更新 MEMORY.md 索引（如果 description 变了）
    await _indexManager.UpdateIndexAsync(memory, ct);

    return memory;
}
```

- [ ] **步骤 16.4: 修改 DeleteAsync 添加向量删除逻辑**

```csharp
public async Task DeleteAsync(Guid memoryId, CancellationToken ct = default)
{
    var memory = await GetByIdAsync(memoryId, ct)
        ?? throw new InvalidOperationException($"记忆不存在: {memoryId}");

    // 1. 删除 Markdown 文件
    var filePath = GetFilePath(memory.Type, memory.Name);
    if (File.Exists(filePath))
    {
        File.Delete(filePath);
    }

    try
    {
        // 2. 从 Qdrant 删除向量
        await _vectorRepository.DeleteAsync(memoryId, ct);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "向量删除失败，记忆文件已删除。记忆 ID: {MemoryId}",
            memoryId);
    }

    // 3. 从 MEMORY.md 索引中移除
    await _indexManager.RemoveFromIndexAsync(memoryId, ct);

    _logger.LogInformation("记忆已删除: {MemoryId}", memoryId);
}
```

- [ ] **步骤 16.5: 运行现有 Memory 测试确保没有破坏**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Memory.Tests --filter "FullyQualifiedName~MemoryRepository"
```

预期：所有测试通过（或根据需要修复）

- [ ] **步骤 16.6: 提交**

```bash
git add v3/src/GeneralAgent.Infrastructure.Memory/Repositories/MemoryRepository.cs
git commit -m "feat(memory): 在 MemoryRepository 中添加双写逻辑

- CreateAsync: 创建记忆后自动生成并存储向量
- UpdateAsync: 更新记忆时重新生成向量
- DeleteAsync: 删除记忆时同步删除向量
- 向量操作失败不影响文件操作（容错处理）"
```

---

### Task 17: MemoryRetrievalService 向量搜索和自动降级

**Files:**
- Modify: `v3/src/GeneralAgent.Infrastructure.Memory/Services/MemoryRetrievalService.cs`

**目标**：使用向量搜索，Qdrant 不可用时自动降级到关键词搜索

- [ ] **步骤 17.1: 修改 MemoryRetrievalService 构造函数，注入依赖**

```csharp
public class MemoryRetrievalService
{
    private readonly IMemoryRepository _memoryRepository;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorRepository _vectorRepository;
    private readonly ILogger<MemoryRetrievalService> _logger;

    public MemoryRetrievalService(
        IMemoryRepository memoryRepository,
        IEmbeddingClient embeddingClient,
        IVectorRepository vectorRepository,
        ILogger<MemoryRetrievalService> logger)
    {
        _memoryRepository = memoryRepository;
        _embeddingClient = embeddingClient;
        _vectorRepository = vectorRepository;
        _logger = logger;
    }

    // 用于通知用户降级（可选）
    public event Action<string>? OnFallbackToKeywordSearch;
}
```

- [ ] **步骤 17.2: 修改 SearchBySemanticAsync 使用向量搜索**

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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

    var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();

    // 使用 Phase 1 的关键词搜索（已有实现）
    var results = await SearchByKeywordAsync(query, type: typeFilter, ct);

    stopwatch2.Stop();

    _logger.LogInformation(
        "⚠️ 关键词搜索 '{Query}' 返回 {Count} 个结果（耗时 {ElapsedMs}ms）",
        query, results.Count, stopwatch2.ElapsedMilliseconds);

    return results;
}

// 关键词搜索（Phase 1 已实现，这里保留作为降级路径）
private async Task<List<Memory>> SearchByKeywordAsync(
    string query,
    MemoryType? type = null,
    CancellationToken ct = default)
{
    // Phase 1 的文件系统扫描实现
    // ... 现有代码
}
```

- [ ] **步骤 17.3: 运行测试**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Memory.Tests --filter "FullyQualifiedName~MemoryRetrievalService"
```

预期：现有测试通过

- [ ] **步骤 17.4: 提交**

```bash
git add v3/src/GeneralAgent.Infrastructure.Memory/Services/MemoryRetrievalService.cs
git commit -m "feat(memory): 实现向量搜索和自动降级

- SearchBySemanticAsync 使用向量搜索（快速路径）
- Qdrant 不可用时自动降级到关键词搜索
- 添加性能日志（毫秒级 vs 秒级）
- 添加降级通知事件"
```

---

### Task 18: REPL 命令 - 迁移现有记忆到向量数据库

**Files:**
- Modify: `v3/src/GeneralAgent.Hosts.Console/Repl/AgentRepl.cs`

**目标**：添加 `/memory migrate-to-vectors` 命令

- [ ] **步骤 18.1: 在 AgentRepl 中添加迁移命令处理**

在 `AgentRepl.cs` 的命令处理部分添加：

```csharp
private async Task HandleMemoryMigrateToVectorsAsync(CancellationToken ct)
{
    _console.WriteLine("开始迁移现有记忆到向量数据库...");

    try
    {
        // 1. 验证 Qdrant 健康状态
        var isHealthy = await _vectorRepository.IsHealthyAsync(ct);
        if (!isHealthy)
        {
            _console.WriteError(
                "❌ Qdrant 未运行，请启动：\n" +
                "  docker run -d -p 6333:6333 qdrant/qdrant");
            return;
        }

        _console.WriteLine("✓ Qdrant 健康检查通过");

        // 2. 扫描所有现有记忆文件
        var allMemories = await _memoryRepository.GetAllAsync(ct);
        var total = allMemories.Count;

        _console.WriteLine($"✓ 扫描到 {total} 个现有记忆");

        if (total == 0)
        {
            _console.WriteLine("没有需要迁移的记忆");
            return;
        }

        // 3. 分批处理（每批 10 个，避免 Ollama 过载）
        var processed = 0;
        var failed = 0;
        var batchSize = 10;

        foreach (var batch in allMemories.Chunk(batchSize))
        {
            try
            {
                // 3.1 批量生成 Embedding
                var texts = batch.Select(m =>
                    $"{m.Name} {m.Description} {m.Content}").ToList();

                var embeddings = await _embeddingClient
                    .GenerateBatchEmbeddingsAsync(texts, ct);

                // 3.2 批量插入 Qdrant（并行）
                var tasks = batch.Zip(embeddings).Select(async pair =>
                {
                    var (memory, embedding) = pair;
                    var metadata = new Dictionary<string, object>
                    {
                        ["memory_id"] = memory.Id.ToString(),
                        ["type"] = memory.Type.ToString(),
                        ["name"] = memory.Name,
                        ["created_at"] = memory.CreatedAt.ToString("O")
                    };
                    await _vectorRepository.UpsertAsync(
                        memory.Id, embedding, metadata, ct);
                });

                await Task.WhenAll(tasks);

                processed += batch.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量迁移失败");
                failed += batch.Length;
            }

            // 3.3 更新进度
            var progress = processed * 100 / total;
            _console.WriteLine($"已迁移 {processed}/{total} ({progress}%)...");
        }

        // 4. 迁移完成
        _console.WriteLine();
        _console.WriteLine("✅ 迁移完成！");
        _console.WriteLine($"  • 总计: {total} 个记忆");
        _console.WriteLine($"  • 成功: {processed} 个");
        _console.WriteLine($"  • 失败: {failed} 个");
        _console.WriteLine($"  • 向量存储: {_vectorDBOptions.Url}/collections/{_vectorDBOptions.CollectionName}");
        _console.WriteLine();
        _console.WriteLine("提示: 现在可以使用 /memory semantic-search 进行高速语义搜索");
    }
    catch (Exception ex)
    {
        _console.WriteError($"❌ 迁移失败：{ex.Message}");
        _logger.LogError(ex, "记忆迁移失败");
    }
}
```

- [ ] **步骤 18.2: 注册命令**

在 `AgentRepl.cs` 的命令分发部分添加：

```csharp
private async Task ProcessCommandAsync(string commandLine, CancellationToken ct)
{
    var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var command = parts[0].ToLowerInvariant();

    switch (command)
    {
        // ... 现有命令

        case "/memory":
            if (parts.Length > 1 && parts[1] == "migrate-to-vectors")
            {
                await HandleMemoryMigrateToVectorsAsync(ct);
            }
            else
            {
                // ... 现有 memory 命令处理
            }
            break;
    }
}
```

- [ ] **步骤 18.3: 更新帮助文本**

```csharp
private void ShowMemoryHelp()
{
    _console.WriteLine("Memory 命令:");
    _console.WriteLine("  /memory add <type> <name>           - 创建新记忆");
    _console.WriteLine("  /memory list [type]                 - 列出记忆");
    _console.WriteLine("  /memory search <query>              - 搜索记忆");
    _console.WriteLine("  /memory semantic-search <query>     - 语义搜索（向量搜索）");
    _console.WriteLine("  /memory migrate-to-vectors          - 迁移现有记忆到向量数据库");
    _console.WriteLine("  /memory help                        - 显示此帮助");
}
```

- [ ] **步骤 18.4: 测试迁移命令（手动）**

```bash
cd v3/src/GeneralAgent.Hosts.Console
dotnet run

# 在 REPL 中
> /memory migrate-to-vectors
```

预期：显示迁移进度和结果

- [ ] **步骤 18.5: 提交**

```bash
git add v3/src/GeneralAgent.Hosts.Console/Repl/AgentRepl.cs
git commit -m "feat(repl): 添加 /memory migrate-to-vectors 命令

- 批量迁移现有记忆到向量数据库
- 显示迁移进度（百分比）
- Qdrant 健康检查
- 更新帮助文本"
```

---

### Task 19: 端到端测试

**Files:**
- Create: `v3/tests/GeneralAgent.Integration.Tests/Memory/MemoryVectorSearchE2ETests.cs`

**目标**：测试完整的创建-搜索流程，包括自动降级

- [ ] **步骤 19.1: 创建 E2E 测试类**

```csharp
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory.Repositories;
using GeneralAgent.Infrastructure.Memory.Services;

namespace GeneralAgent.Integration.Tests.Memory;

/// <summary>
/// 记忆向量搜索端到端测试
/// 需要：
/// 1. Ollama 服务运行在 http://localhost:11434
/// 2. nomic-embed-text 模型已下载
/// 3. Qdrant 服务运行在 http://localhost:6333
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class MemoryVectorSearchE2ETests : IAsyncLifetime
{
    private readonly IMemoryRepository _memoryRepo;
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly List<Guid> _createdMemoryIds = new();

    public MemoryVectorSearchE2ETests()
    {
        // 设置真实服务（使用 TestFixture 或手动配置）
        // ... 初始化代码
    }

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

        _createdMemoryIds.Add(memory1.Id);
        _createdMemoryIds.Add(memory2.Id);

        // 等待向量索引完成
        await Task.Delay(500);

        // Act - 语义搜索
        var results = await _retrievalService.SearchBySemanticAsync("测试驱动开发");

        // Assert
        results.Should().NotBeEmpty();
        results[0].Name.Should().Be("tdd_preference"); // 最相关的
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
        _createdMemoryIds.Add(memory.Id);

        // 停止 Qdrant（模拟故障）
        // 注意：实际测试中可能需要用 Mock 来模拟故障

        // Act
        var results = await _retrievalService.SearchBySemanticAsync("重要");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(m => m.Name == "test_memory");
        // 验证降级日志（需要捕获日志或事件）
    }

    [Fact]
    public async Task UpdateMemory_UpdatesVector()
    {
        // Arrange
        var memory = Memory.Create(
            MemoryType.User,
            "update_test",
            "原始描述",
            "原始内容");

        await _memoryRepo.CreateAsync(memory);
        _createdMemoryIds.Add(memory.Id);

        await Task.Delay(500);

        // Act - 更新记忆
        var updatedMemory = memory with
        {
            Content = "新内容：人工智能"
        };

        await _memoryRepo.UpdateAsync(updatedMemory);
        await Task.Delay(500);

        // 搜索新内容
        var results = await _retrievalService.SearchBySemanticAsync("人工智能");

        // Assert
        results.Should().Contain(m => m.Name == "update_test");
    }

    [Fact]
    public async Task DeleteMemory_DeletesVector()
    {
        // Arrange
        var memory = Memory.Create(
            MemoryType.User,
            "delete_test",
            "待删除",
            "内容");

        await _memoryRepo.CreateAsync(memory);
        await Task.Delay(500);

        // Act - 删除记忆
        await _memoryRepo.DeleteAsync(memory.Id);
        await Task.Delay(500);

        // 尝试搜索（应该找不到）
        var results = await _retrievalService.SearchBySemanticAsync("待删除");

        // Assert
        results.Should().NotContain(m => m.Name == "delete_test");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // 清理测试数据
        foreach (var id in _createdMemoryIds)
        {
            try
            {
                await _memoryRepo.DeleteAsync(id);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }
}
```

- [ ] **步骤 19.2: 运行 E2E 测试**

```bash
# 确保服务运行
ollama serve &
docker run -d -p 6333:6333 qdrant/qdrant

# 运行测试
cd v3
dotnet test tests/GeneralAgent.Integration.Tests --filter "Category=E2E&FullyQualifiedName~MemoryVectorSearch"
```

预期：所有测试通过

- [ ] **步骤 19.3: 提交**

```bash
git add v3/tests/GeneralAgent.Integration.Tests/Memory/MemoryVectorSearchE2ETests.cs
git commit -m "test(memory): 添加记忆向量搜索 E2E 测试

- 测试创建和搜索记忆（向量搜索）
- 测试自动降级（Qdrant 不可用时）
- 测试更新记忆时向量同步
- 测试删除记忆时向量同步
- 标记为 E2E 测试类别"
```

---

### Task 20: 更新文档

**Files:**
- Modify: `v3/docs/CLI_GUIDE.md`
- Modify: `v3/docs/CLI_REFERENCE.md`

**目标**：更新用户文档，添加向量搜索和迁移说明

- [ ] **步骤 20.1: 更新 CLI_GUIDE.md**

在 CLI_GUIDE.md 的记忆管理部分添加：

```markdown
### 向量搜索（Vector Search）

Phase 2 引入了向量搜索功能，将语义搜索性能提升 1000-10000 倍（从 50-100秒 降至 10-50毫秒）。

#### 前置要求

1. 启动 Qdrant 向量数据库：
   ```bash
   docker run -d --name qdrant -p 6333:6333 qdrant/qdrant
   ```

2. 确保 Ollama 运行并下载 Embedding 模型：
   ```bash
   ollama pull nomic-embed-text
   ```

#### 使用向量搜索

```bash
# 语义搜索（自动使用向量搜索）
> /memory semantic-search "TDD测试"

✅ 找到 3 个相关记忆（向量搜索，耗时 ~15ms）

1. tdd_preference (相似度: 0.92)
   描述: 喜欢使用 TDD 方法
   类型: User

2. unit_testing (相似度: 0.85)
   描述: 单元测试最佳实践
   类型: Knowledge
```

#### 迁移现有记忆

如果你已经在 Phase 1 创建了记忆，需要迁移到向量数据库：

```bash
> /memory migrate-to-vectors

开始迁移现有记忆到向量数据库...
✓ Qdrant 健康检查通过
✓ 扫描到 50 个现有记忆
已迁移 10/50 (20%)...
已迁移 20/50 (40%)...
...
✅ 迁移完成！
  • 总计: 50 个记忆
  • 成功: 50 个
  • 失败: 0 个
```

#### 自动降级

如果 Qdrant 不可用，系统会自动降级到关键词搜索（较慢但仍可用）：

```bash
> /memory semantic-search "测试"

⚠️ 向量搜索不可用，使用关键词搜索（较慢）
提示：启动 Qdrant: docker run -p 6333:6333 qdrant/qdrant

⚠️ 找到 2 个相关记忆（关键词搜索，耗时 ~2s）
```
```

- [ ] **步骤 20.2: 更新 CLI_REFERENCE.md**

在 CLI_REFERENCE.md 添加新命令：

```markdown
### Memory 命令

#### `/memory migrate-to-vectors`

迁移现有记忆到向量数据库。

**用法**：
```bash
/memory migrate-to-vectors
```

**前置要求**：
- Qdrant 服务运行在 http://localhost:6333
- Ollama 服务运行在 http://localhost:11434
- nomic-embed-text 模型已下载

**行为**：
1. 验证 Qdrant 健康状态
2. 扫描所有现有记忆文件
3. 分批生成 Embedding 向量（每批 10 个）
4. 批量插入 Qdrant
5. 显示迁移进度和结果

**注意事项**：
- 迁移过程不会修改文件系统中的记忆
- 向量数据库仅用于加速搜索
- 迁移失败不会影响现有记忆
```

- [ ] **步骤 20.3: 添加部署指南**

在文档中添加 `v3/docs/DEPLOYMENT_PHASE2.md`:

```markdown
# V3 Phase 2 部署指南

## 本地开发环境

### 1. 启动 Qdrant

```bash
docker run -d --name qdrant \
  -p 6333:6333 \
  -v ~/.agent/qdrant:/qdrant/storage \
  qdrant/qdrant
```

### 2. 验证 Qdrant

```bash
curl http://localhost:6333/collections
# 应返回: {"result":{"collections":[]}}
```

### 3. 启动 Ollama（如果未运行）

```bash
ollama serve
```

### 4. 下载 Embedding 模型

```bash
ollama pull nomic-embed-text
```

### 5. 运行应用

```bash
cd v3/src/GeneralAgent.Hosts.Console
dotnet run
```

### 6. 迁移现有记忆

```bash
> /memory migrate-to-vectors
```

## Docker Compose 部署

请参考设计文档 Section 10.2。
```

- [ ] **步骤 20.4: 提交**

```bash
git add v3/docs/CLI_GUIDE.md
git add v3/docs/CLI_REFERENCE.md
git add v3/docs/DEPLOYMENT_PHASE2.md
git commit -m "docs: 更新文档添加 Phase 2 向量搜索功能

- CLI_GUIDE: 添加向量搜索使用说明
- CLI_REFERENCE: 添加 migrate-to-vectors 命令文档
- DEPLOYMENT_PHASE2: 添加部署指南"
```

---

## 验收和测试

### 最终验收清单

执行以下命令验证所有功能：

```bash
# 1. 运行所有单元测试
cd v3
dotnet test --filter "Category=Unit"

# 2. 运行集成测试（需要 Ollama + Qdrant）
ollama serve &
docker run -d -p 6333:6333 qdrant/qdrant
dotnet test --filter "Category=Integration"

# 3. 运行 E2E 测试
dotnet test --filter "Category=E2E"

# 4. 检查测试覆盖率
dotnet test --collect:"XPlat Code Coverage"

# 5. 手动功能测试
cd v3/src/GeneralAgent.Hosts.Console
dotnet run

# 在 REPL 中：
> /memory add user test_memory
> /memory semantic-search "test"
> /memory migrate-to-vectors
```

### 性能基准测试

执行以下命令测试性能提升：

```bash
# 创建 100 个测试记忆
for i in {1..100}; do
  echo "Memory $i content" | dotnet run -- /memory add user "memory_$i"
done

# 测试向量搜索性能
time dotnet run -- /memory semantic-search "Memory 50"
# 预期: < 100ms

# 停止 Qdrant 测试降级
docker stop qdrant
time dotnet run -- /memory semantic-search "Memory 50"
# 预期: 1-5秒（降级到关键词搜索）

# 重启 Qdrant
docker start qdrant
```

---

## 总结

本计划覆盖了 V3 Phase 2 的所有关键实施任务：

### 迭代 1: Embedding 基础设施 (Task 1-8)
- ✅ IEmbeddingClient 接口和异常
- ✅ OllamaEmbeddingClient 实现
- ✅ 单元测试和集成测试
- ✅ 配置和 DI 注册

### 迭代 2: Qdrant 集成 (Task 9-15)
- ✅ IVectorRepository 接口和模型
- ✅ QdrantVectorRepository 实现
- ✅ 健康检查和缓存
- ✅ 单元测试和集成测试
- ✅ 配置和 DI 注册

### 迭代 3: 记忆系统集成 (Task 16-20)
- ✅ MemoryRepository 双写逻辑
- ✅ MemoryRetrievalService 向量搜索和自动降级
- ✅ REPL 迁移命令
- ✅ 端到端测试
- ✅ 文档更新

### 预期成果

- **性能提升**: 1000-10000倍（50-100秒 → 10-50毫秒）
- **成本优化**: API 调用减少 99%（100次 → 1次）
- **测试覆盖率**: 80%+
- **系统健壮性**: 自动降级，无单点故障

---

**详细实施代码和步骤请参考设计文档**：
- Section 4: 接口设计（完整接口定义）
- Section 5: 数据流设计（详细流程图）
- Section 6: 核心实现（完整代码示例）
- Section 7: 测试策略（测试用例）
- Section 10: 部署指南（Docker Compose）
- Section 11: 迁移指南（详细步骤）
