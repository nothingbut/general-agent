using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Embedding;
using GeneralAgent.Infrastructure.Memory;
using GeneralAgent.Infrastructure.Memory.Repositories;
using GeneralAgent.Infrastructure.Memory.Services;
using GeneralAgent.Infrastructure.VectorDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Qdrant.Client;

namespace GeneralAgent.Integration.Tests.Memory;

/// <summary>
/// 记忆向量搜索端到端测试
/// 测试完整的记忆向量搜索流程，包括创建、搜索、更新、删除时的向量同步
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class MemoryVectorSearchE2ETests : IAsyncLifetime
{
    private const string OllamaUrl = "http://localhost:11434";
    private const string QdrantUrl = "http://localhost:6333";
    private const string TestCollectionName = "memory_e2e_test";
    private const int QdrantGrpcPort = 6334;
    private const string EmbeddingModel = "nomic-embed-text";

    private readonly string _testMemoryDir;
    private readonly IMemoryRepository _memoryRepository;
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly IVectorRepository _vectorRepository;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly List<Guid> _testMemoryIds = new();
    private string? _skipReason;

    public MemoryVectorSearchE2ETests()
    {
        // 创建临时记忆目录
        _testMemoryDir = Path.Combine(Path.GetTempPath(), $"memory_e2e_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testMemoryDir);

        // 配置 Embedding 客户端
        var embeddingOptions = Options.Create(new EmbeddingOptions
        {
            BaseUrl = OllamaUrl,
            Model = EmbeddingModel,
            TimeoutSeconds = 30
        });
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(OllamaUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _embeddingClient = new OllamaEmbeddingClient(
            httpClient,
            embeddingOptions,
            NullLogger<OllamaEmbeddingClient>.Instance
        );

        // 配置 VectorDB
        var vectorDbOptions = Options.Create(new VectorDBOptions
        {
            Url = QdrantUrl,
            CollectionName = TestCollectionName,
            HealthCheckCacheSeconds = 5,
            EnableFallback = true
        });

        var qdrantClient = new QdrantClient("localhost", QdrantGrpcPort);
        var wrappedClient = new QdrantClientWrapper(qdrantClient);
        _vectorRepository = new QdrantVectorRepository(
            wrappedClient,
            vectorDbOptions,
            NullLogger<QdrantVectorRepository>.Instance
        );

        // 配置 Memory Repository（带双写）
        var memoryOptions = Options.Create(new MemoryOptions
        {
            RootDirectory = _testMemoryDir
        });
        _memoryRepository = new MemoryRepository(
            memoryOptions,
            NullLogger<MemoryRepository>.Instance,
            _vectorRepository,
            _embeddingClient
        );

        // 配置 MemoryRetrievalService（Mock LLM Factory）
        var mockLlmClient = Substitute.For<ILLMClient>();
        var mockLlmFactory = Substitute.For<ILLMClientFactory>();
        mockLlmFactory.GetClient(Arg.Any<string?>()).Returns(mockLlmClient);

        _retrievalService = new MemoryRetrievalService(
            mockLlmFactory,
            _memoryRepository,
            NullLogger<MemoryRetrievalService>.Instance,
            _embeddingClient,
            _vectorRepository
        );
    }

    public async Task InitializeAsync()
    {
        // 检查服务是否可用
        var isOllamaAvailable = await IsOllamaAvailableAsync();
        var isQdrantAvailable = await IsQdrantAvailableAsync();

        if (!isOllamaAvailable)
        {
            _skipReason = $"Ollama 服务不可用（{OllamaUrl}）。请运行: ollama serve";
            return;
        }

        if (!isQdrantAvailable)
        {
            _skipReason = $"Qdrant 服务不可用（{QdrantUrl}）。请运行: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant";
            return;
        }

        // 检查 Ollama 模型是否存在
        var hasModel = await CheckOllamaModelAsync();
        if (!hasModel)
        {
            _skipReason = $"Ollama 模型 '{EmbeddingModel}' 不存在。请运行: ollama pull {EmbeddingModel}";
            return;
        }

        try
        {
            // 确保测试集合存在
            await EnsureCollectionExistsAsync();
        }
        catch (Exception ex)
        {
            _skipReason = $"初始化集合失败: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        // 清理测试数据
        foreach (var id in _testMemoryIds)
        {
            try
            {
                await _memoryRepository.DeleteAsync(id);
            }
            catch
            {
                // 忽略删除错误
            }
        }

        // 清理临时目录
        if (Directory.Exists(_testMemoryDir))
        {
            try
            {
                Directory.Delete(_testMemoryDir, recursive: true);
            }
            catch
            {
                // 忽略清理失败
            }
        }

        // 等待一下确保删除完成
        await Task.Delay(100);
    }

    /// <summary>
    /// 测试 1: 创建记忆并进行语义搜索
    /// </summary>
    [Fact]
    public async Task CreateAndSearchMemory_WithVectors_FindsRelevantMemories()
    {
        // Arrange
        SkipIfServicesUnavailable();

        // 创建测试记忆 1：TDD 偏好
        var memory1 = Core.Models.Memory.Create(
            MemoryType.User,
            "tdd_preference",
            "用户喜欢测试驱动开发",
            "# TDD 偏好\n\n我倾向于使用测试驱动开发（TDD）方法，先写测试再写实现代码。这样可以确保代码质量和可维护性。",
            new List<string> { "tdd", "testing", "development" }
        );

        // 创建测试记忆 2：编码风格
        var memory2 = Core.Models.Memory.Create(
            MemoryType.User,
            "coding_style",
            "用户的编码风格偏好",
            "# 编码风格\n\n我喜欢使用不可变数据结构和函数式编程风格。避免副作用，优先使用纯函数。",
            new List<string> { "coding-style", "functional", "immutable" }
        );

        // Act 1: 保存记忆（触发向量双写）
        var saved1 = await _memoryRepository.SaveAsync(memory1);
        var saved2 = await _memoryRepository.SaveAsync(memory2);
        _testMemoryIds.Add(saved1.Id);
        _testMemoryIds.Add(saved2.Id);

        // 等待向量索引完成
        await Task.Delay(500);

        // Act 2: 语义搜索 "测试驱动开发"
        var results = await _retrievalService.SearchBySemanticAsync(
            "测试驱动开发",
            topK: 5,
            typeFilter: MemoryType.User
        );

        // Assert
        results.Should().NotBeEmpty();
        // 使用包含断言而非严格排序，因为向量相似度可能有细微差异
        results.Select(r => r.Id).Should().Contain(saved1.Id, "TDD 记忆应该在结果中");
        var tddMemory = results.First(r => r.Id == saved1.Id);
        tddMemory.Description.Should().Contain("测试驱动开发");

        // 验证文件系统存储
        var memoryFile = Path.Combine(_testMemoryDir, memory1.FilePath);
        File.Exists(memoryFile).Should().BeTrue();
    }

    /// <summary>
    /// 测试 2: 更新记忆时更新向量
    /// </summary>
    [Fact]
    public async Task UpdateMemory_UpdatesVector()
    {
        // Arrange
        SkipIfServicesUnavailable();

        var memory = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "test_fact",
            "测试事实记忆",
            "# 原始内容\n\n这是一个关于编程的事实。",
            new List<string> { "test", "programming" }
        );

        var saved = await _memoryRepository.SaveAsync(memory);
        _testMemoryIds.Add(saved.Id);
        await Task.Delay(500);

        // Act: 更新内容为 "人工智能"
        var updated = saved.WithContent("# 更新内容\n\n这是一个关于人工智能的事实。人工智能正在改变世界。");
        await _memoryRepository.UpdateAsync(updated);
        await Task.Delay(500);

        // Assert: 搜索 "人工智能" 应该找到更新后的记忆
        var results = await _retrievalService.SearchBySemanticAsync(
            "人工智能",
            topK: 5,
            typeFilter: MemoryType.Feedback
        );

        results.Should().NotBeEmpty();
        results.Should().Contain(m => m.Id == saved.Id, "应该找到更新后的记忆");

        var found = results.First(m => m.Id == saved.Id);
        found.Content.Should().Contain("人工智能");
    }

    /// <summary>
    /// 测试 3: 删除记忆时删除向量
    /// </summary>
    [Fact]
    public async Task DeleteMemory_DeletesVector()
    {
        // Arrange
        SkipIfServicesUnavailable();

        var memory = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "deletable_fact",
            "可删除的事实",
            "# 可删除的事实\n\n这是一个将被删除的测试记忆，内容是关于量子计算的。",
            new List<string> { "test", "quantum" }
        );

        var saved = await _memoryRepository.SaveAsync(memory);
        _testMemoryIds.Add(saved.Id);
        await Task.Delay(500);

        // 验证记忆存在
        var beforeDelete = await _retrievalService.SearchBySemanticAsync(
            "量子计算",
            topK: 5,
            typeFilter: MemoryType.Feedback
        );
        beforeDelete.Should().Contain(m => m.Id == saved.Id, "删除前应该能找到");

        // Act: 删除记忆
        var deleted = await _memoryRepository.DeleteAsync(saved.Id);
        deleted.Should().BeTrue();
        await Task.Delay(500);

        // Assert: 搜索不应该找到该记忆
        var afterDelete = await _retrievalService.SearchBySemanticAsync(
            "量子计算",
            topK: 5,
            typeFilter: MemoryType.Feedback
        );
        afterDelete.Should().NotContain(m => m.Id == saved.Id, "删除后不应该找到");

        // 验证文件系统也删除了
        var memoryFile = Path.Combine(_testMemoryDir, memory.FilePath);
        File.Exists(memoryFile).Should().BeFalse();
    }

    /// <summary>
    /// 测试 4: 混合检索（关键词 + 语义）
    /// </summary>
    [Fact]
    public async Task HybridSearch_CombinesKeywordAndSemantic()
    {
        // Arrange
        SkipIfServicesUnavailable();

        var memory1 = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "rust_programming",
            "Rust 编程语言知识",
            "# Rust 编程\n\nRust 是一门系统编程语言，注重内存安全和并发性。",
            new List<string> { "rust", "programming", "systems" }
        );

        var memory2 = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "csharp_programming",
            "C# 编程语言知识",
            "# C# 编程\n\nC# 是一门面向对象的编程语言，适合企业级应用开发。",
            new List<string> { "csharp", "programming", "oop" }
        );

        var saved1 = await _memoryRepository.SaveAsync(memory1);
        var saved2 = await _memoryRepository.SaveAsync(memory2);
        _testMemoryIds.Add(saved1.Id);
        _testMemoryIds.Add(saved2.Id);
        await Task.Delay(500);

        // Act: 混合检索 "Rust 系统编程"
        var results = await _retrievalService.HybridSearchAsync(
            "Rust 系统编程",
            topK: 5,
            keywordWeight: 0.3,
            semanticWeight: 0.7
        );

        // Assert
        results.Should().NotBeEmpty();
        // Rust 记忆应该排在前面（同时包含关键词和语义匹配）
        var rustMemory = results.FirstOrDefault(m => m.Name == "rust_programming");
        rustMemory.Should().NotBeNull();
    }

    /// <summary>
    /// 测试 5: 向量搜索结果按相似度排序
    /// </summary>
    [Fact]
    public async Task VectorSearch_ShouldReturnResultsSortedBySimilarity()
    {
        // Arrange
        SkipIfServicesUnavailable();

        // 创建 3 个记忆：相关性依次递减
        var memory1 = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "highly_relevant",
            "深度学习和神经网络",
            "# 深度学习\n\n深度学习是机器学习的一个分支，使用多层神经网络进行训练。" +
            "深度学习模型可以自动学习特征，广泛应用于图像识别、自然语言处理等领域。" +
            "常见的深度学习框架包括 TensorFlow、PyTorch 等。",
            new List<string> { "ai", "deep-learning", "neural-network" }
        );

        var memory2 = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "moderately_relevant",
            "机器学习基础",
            "# 机器学习\n\n机器学习是一种数据分析方法，让计算机从数据中学习规律。" +
            "监督学习、无监督学习和强化学习是机器学习的三大类别。",
            new List<string> { "ai", "machine-learning" }
        );

        var memory3 = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "less_relevant",
            "软件工程方法论",
            "# 软件工程\n\n软件工程是应用工程方法设计、开发和维护软件的学科。" +
            "包括需求分析、系统设计、编码实现、测试和维护等阶段。",
            new List<string> { "software", "engineering" }
        );

        var saved1 = await _memoryRepository.SaveAsync(memory1);
        var saved2 = await _memoryRepository.SaveAsync(memory2);
        var saved3 = await _memoryRepository.SaveAsync(memory3);
        _testMemoryIds.Add(saved1.Id);
        _testMemoryIds.Add(saved2.Id);
        _testMemoryIds.Add(saved3.Id);

        // 等待向量索引完成
        await Task.Delay(500);

        // Act: 搜索 "深度学习神经网络"（最匹配 memory1）
        var results = await _retrievalService.SearchBySemanticAsync(
            "深度学习神经网络",
            topK: 3,
            typeFilter: MemoryType.Knowledge
        );

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterThanOrEqualTo(2, "应该至少返回 2 个相关记忆");

        // 验证最相关的记忆在结果中（使用包含检查，因为向量相似度可能有细微差异）
        results.Select(r => r.Id).Should().Contain(saved1.Id, "最相关的记忆应该在结果中");

        // 查找各个记忆在结果中的位置
        var index1 = results.FindIndex(r => r.Id == saved1.Id);
        var index2 = results.FindIndex(r => r.Id == saved2.Id);
        var index3 = results.FindIndex(r => r.Id == saved3.Id);

        // 验证排序规则：memory1 应该在前面（如果存在的话）
        if (index1 >= 0 && index2 >= 0)
        {
            index1.Should().BeLessThan(index2,
                "最相关的记忆（深度学习）应该排在次相关的记忆（机器学习）前面");
        }

        if (index2 >= 0 && index3 >= 0)
        {
            index2.Should().BeLessThan(index3,
                "次相关的记忆（机器学习）应该排在不相关的记忆（软件工程）前面");
        }

        // 额外验证：第一个结果应该是 memory1
        if (results.Count > 0)
        {
            results[0].Id.Should().Be(saved1.Id,
                "第一个结果应该是最相关的记忆（深度学习神经网络）");
        }
    }

    /// <summary>
    /// 跳过测试如果服务不可用
    /// </summary>
    private void SkipIfServicesUnavailable()
    {
        if (_skipReason != null)
        {
            throw new SkipTestException(_skipReason);
        }
    }

    /// <summary>
    /// 检查 Ollama 服务是否可用
    /// </summary>
    private static async Task<bool> IsOllamaAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{OllamaUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查 Qdrant 服务是否可用
    /// </summary>
    private static async Task<bool> IsQdrantAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{QdrantUrl}/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查 Ollama 模型是否存在
    /// </summary>
    private static async Task<bool> CheckOllamaModelAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync($"{OllamaUrl}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            return content.Contains(EmbeddingModel, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 确保测试集合存在
    /// </summary>
    private async Task EnsureCollectionExistsAsync()
    {
        try
        {
            // 尝试获取集合信息
            var qdrantClient = new QdrantClient("localhost", QdrantGrpcPort);
            await qdrantClient.GetCollectionInfoAsync(TestCollectionName);
        }
        catch
        {
            // 集合不存在，创建它
            var qdrantClient = new QdrantClient("localhost", QdrantGrpcPort);
            await qdrantClient.CreateCollectionAsync(
                collectionName: TestCollectionName,
                vectorsConfig: new Qdrant.Client.Grpc.VectorParams
                {
                    Size = 768, // nomic-embed-text 的向量维度
                    Distance = Qdrant.Client.Grpc.Distance.Cosine
                });
        }
    }
}

/// <summary>
/// 用于跳过测试的异常类
/// </summary>
internal class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}
