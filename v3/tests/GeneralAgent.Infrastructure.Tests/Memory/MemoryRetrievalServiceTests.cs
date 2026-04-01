using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using FluentAssertions;

namespace GeneralAgent.Infrastructure.Tests.Memory;

/// <summary>
/// 记忆检索服务测试
/// </summary>
public sealed class MemoryRetrievalServiceTests
{
    private readonly ILLMClientFactory _mockLlmFactory;
    private readonly ILLMClient _mockLlmClient;
    private readonly IMemoryRepository _mockRepository;
    private readonly MemoryRetrievalService _service;

    public MemoryRetrievalServiceTests()
    {
        _mockLlmFactory = Substitute.For<ILLMClientFactory>();
        _mockLlmClient = Substitute.For<ILLMClient>();
        _mockRepository = Substitute.For<IMemoryRepository>();

        _mockLlmFactory.GetClient(Arg.Any<string>()).Returns(_mockLlmClient);

        _service = new MemoryRetrievalService(
            _mockLlmFactory,
            _mockRepository,
            NullLogger<MemoryRetrievalService>.Instance);
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldReturnRelevantMemories()
    {
        // Arrange
        var query = "测试驱动开发";
        var memories = new List<Core.Models.Memory>
        {
            Core.Models.Memory.Create(MemoryType.Feedback, "tdd_preference", "TDD 偏好", "用户喜欢 TDD"),
            Core.Models.Memory.Create(MemoryType.Knowledge, "unit_testing", "单元测试", "单元测试最佳实践"),
            Core.Models.Memory.Create(MemoryType.User, "user_name", "用户名", "张三")
        };

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(memories);

        // Mock LLM 返回相关性评分
        var callCount = 0;
        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var scores = new[] { 0.9, 0.7, 0.1 }; // tdd_preference, unit_testing, user_name
                var score = scores[callCount++];
                return new CompletionResponse
                {
                    Content = $"{{ \"score\": {score}, \"reason\": \"测试\" }}",
                    Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                    Timestamp = DateTime.UtcNow
                };
            });

        // Act
        var results = await _service.SearchBySemanticAsync(query, topK: 5);

        // Assert
        results.Should().HaveCount(2); // user_name 被过滤掉（< 0.3）
        results[0].Name.Should().Be("tdd_preference");
        results[1].Name.Should().Be("unit_testing");
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldReturnEmpty_WhenQueryIsEmpty()
    {
        // Act
        var results = await _service.SearchBySemanticAsync("");

        // Assert
        results.Should().BeEmpty();
        await _mockRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldFilterByType()
    {
        // Arrange
        var query = "测试";
        var memories = new List<Core.Models.Memory>
        {
            Core.Models.Memory.Create(MemoryType.User, "user_info", "用户信息", "内容")
        };

        _mockRepository.GetByTypeAsync(MemoryType.User, Arg.Any<CancellationToken>())
            .Returns(memories);

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await _service.SearchBySemanticAsync(query, typeFilter: MemoryType.User);

        // Assert
        results.Should().HaveCount(1);
        results[0].Type.Should().Be(MemoryType.User);
        await _mockRepository.Received(1).GetByTypeAsync(MemoryType.User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldRespectTopK()
    {
        // Arrange
        var query = "测试";
        var memories = Enumerable.Range(1, 10)
            .Select(i => Core.Models.Memory.Create(MemoryType.User, $"memory_{i}", $"记忆{i}", $"内容{i}"))
            .ToList();

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(memories);

        var callIndex = 0;
        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var score = 1.0 - (callIndex++ * 0.05); // 递减评分
                return new CompletionResponse
                {
                    Content = $"{{ \"score\": {score}, \"reason\": \"测试\" }}",
                    Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                    Timestamp = DateTime.UtcNow
                };
            });

        // Act
        var results = await _service.SearchBySemanticAsync(query, topK: 3);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRelevantMemoriesAsync_ShouldReturnTopRelevantMemories()
    {
        // Arrange
        var context = "用户正在实现 TDD 功能";
        var memories = new List<Core.Models.Memory>
        {
            Core.Models.Memory.Create(MemoryType.Feedback, "tdd_pref", "TDD 偏好", "用户喜欢 TDD"),
            Core.Models.Memory.Create(MemoryType.Knowledge, "testing", "测试", "测试最佳实践")
        };

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(memories);

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await _service.GetRelevantMemoriesAsync(context, topK: 3);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task HybridSearchAsync_ShouldThrowException_WhenWeightsInvalid()
    {
        // Arrange
        var query = "测试";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.HybridSearchAsync(query, keywordWeight: 0.4, semanticWeight: 0.7));
    }

    [Fact]
    public async Task CalculateImportanceScoreAsync_ShouldReturnScore()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "user_role",
            "用户职业",
            "C# 开发者");

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.85, "reason": "核心用户信息" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var score = await _service.CalculateImportanceScoreAsync(memory);

        // Assert
        score.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public async Task CalculateImportanceScoreAsync_ShouldReturnDefaultScore_WhenLlmFails()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "test",
            "测试",
            "内容");

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Throws(new LLMException("LLM 失败"));

        // Act
        var score = await _service.CalculateImportanceScoreAsync(memory);

        // Assert
        score.Should().Be(0.5); // 默认中等评分
    }

    [Fact]
    public async Task CalculateImportanceScoreAsync_ShouldClampScore()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "test",
            "测试",
            "内容");

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 1.5, "reason": "超出范围" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var score = await _service.CalculateImportanceScoreAsync(memory);

        // Assert
        score.Should().Be(1.0); // 限制在 1.0
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldHandleLlmException_Gracefully()
    {
        // Arrange
        var query = "测试";
        var memories = new List<Core.Models.Memory>
        {
            Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容")
        };

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(memories);

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Throws(new LLMException("LLM 失败"));

        // Act
        var results = await _service.SearchBySemanticAsync(query);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldFilterLowRelevanceMemories()
    {
        // Arrange
        var query = "测试";
        var memories = new List<Core.Models.Memory>
        {
            Core.Models.Memory.Create(MemoryType.User, "high", "高相关", "内容"),
            Core.Models.Memory.Create(MemoryType.User, "low", "低相关", "内容")
        };

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(memories);

        var callCount = 0;
        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var score = callCount++ == 0 ? 0.8 : 0.2; // 第一个高，第二个低
                return new CompletionResponse
                {
                    Content = $"{{ \"score\": {score}, \"reason\": \"测试\" }}",
                    Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                    Timestamp = DateTime.UtcNow
                };
            });

        // Act
        var results = await _service.SearchBySemanticAsync(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("high");
    }
}

/// <summary>
/// 向量搜索测试（快速路径）
/// </summary>
public sealed class MemoryRetrievalServiceVectorSearchTests
{
    private readonly ILLMClientFactory _mockLlmFactory;
    private readonly ILLMClient _mockLlmClient;
    private readonly IMemoryRepository _mockRepository;
    private readonly IEmbeddingClient _mockEmbeddingClient;
    private readonly IVectorRepository _mockVectorRepository;
    private readonly ILogger<MemoryRetrievalService> _mockLogger;

    public MemoryRetrievalServiceVectorSearchTests()
    {
        _mockLlmFactory = Substitute.For<ILLMClientFactory>();
        _mockLlmClient = Substitute.For<ILLMClient>();
        _mockRepository = Substitute.For<IMemoryRepository>();
        _mockEmbeddingClient = Substitute.For<IEmbeddingClient>();
        _mockVectorRepository = Substitute.For<IVectorRepository>();
        _mockLogger = Substitute.For<ILogger<MemoryRetrievalService>>();

        _mockLlmFactory.GetClient(Arg.Any<string>()).Returns(_mockLlmClient);
    }

    private MemoryRetrievalService CreateServiceWithVectorSupport()
    {
        return new MemoryRetrievalService(
            _mockLlmFactory,
            _mockRepository,
            _mockLogger,
            _mockEmbeddingClient,
            _mockVectorRepository);
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldUseVectorSearch_WhenHealthy()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试驱动开发";
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };

        var memory1 = Core.Models.Memory.Create(MemoryType.Feedback, "tdd_pref", "TDD 偏好", "用户喜欢 TDD");
        var memory2 = Core.Models.Memory.Create(MemoryType.Knowledge, "testing", "测试知识", "单元测试最佳实践");

        // Mock 健康检查
        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Mock Embedding 生成
        _mockEmbeddingClient.GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>())
            .Returns(queryVector);

        // Mock 向量搜索结果
        var vectorResults = new List<VectorSearchResult>
        {
            new() { MemoryId = memory1.Id, Score = 0.95, Metadata = new() },
            new() { MemoryId = memory2.Id, Score = 0.88, Metadata = new() }
        };

        _mockVectorRepository.SearchAsync(
                queryVector,
                Arg.Any<int>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(vectorResults);

        // Mock 记忆加载
        _mockRepository.GetByIdAsync(memory1.Id, Arg.Any<CancellationToken>())
            .Returns(memory1);
        _mockRepository.GetByIdAsync(memory2.Id, Arg.Any<CancellationToken>())
            .Returns(memory2);

        // Act
        var results = await service.SearchBySemanticAsync(query, topK: 5);

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("tdd_pref");
        results[1].Name.Should().Be("testing");

        // 验证调用链
        await _mockVectorRepository.Received(1).IsHealthyAsync(Arg.Any<CancellationToken>());
        await _mockEmbeddingClient.Received(1).GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>());
        await _mockVectorRepository.Received(1).SearchAsync(
            queryVector,
            5,
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());

        // 验证性能日志
        _mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("向量搜索") && o.ToString()!.Contains("返回 2 个结果")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        // 不应降级到 LLM 评分
        await _mockRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
        await _mockLlmClient.DidNotReceive().CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldApplyTypeFilter_InVectorSearch()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "用户信息";
        var queryVector = new float[] { 0.1f, 0.2f };

        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        _mockEmbeddingClient.GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>())
            .Returns(queryVector);

        _mockVectorRepository.SearchAsync(
                Arg.Any<float[]>(),
                Arg.Any<int>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchResult>());

        // Act
        await service.SearchBySemanticAsync(query, topK: 5, typeFilter: MemoryType.User);

        // Assert - 验证 filters 参数包含 type
        await _mockVectorRepository.Received(1).SearchAsync(
            queryVector,
            5,
            Arg.Is<Dictionary<string, object>>(filters =>
                filters != null &&
                filters.ContainsKey("type") &&
                filters["type"].ToString() == "User"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldPassNullFilters_WhenNoTypeFilter()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";
        var queryVector = new float[] { 0.1f };

        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        _mockEmbeddingClient.GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>())
            .Returns(queryVector);

        _mockVectorRepository.SearchAsync(
                Arg.Any<float[]>(),
                Arg.Any<int>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchResult>());

        // Act
        await service.SearchBySemanticAsync(query);

        // Assert
        await _mockVectorRepository.Received(1).SearchAsync(
            queryVector,
            5,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldFallbackToLLM_WhenHealthCheckFails()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";
        var memory = Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容");

        string? fallbackMessage = null;
        service.OnFallbackToLLMScoring += msg => fallbackMessage = msg;

        // Mock 健康检查失败
        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Mock LLM 评分路径
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory> { memory });

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await service.SearchBySemanticAsync(query);

        // Assert
        results.Should().HaveCount(1);

        // 验证降级通知事件
        fallbackMessage.Should().NotBeNullOrEmpty();
        fallbackMessage.Should().Contain("向量搜索不可用");
        fallbackMessage.Should().Contain("LLM 评分");
        fallbackMessage.Should().Contain("Qdrant");

        // 验证降级警告日志
        _mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("向量数据库不可用")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        // 验证使用 LLM 评分路径
        await _mockRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await _mockLlmClient.Received(1).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());

        // 不应调用向量搜索
        await _mockEmbeddingClient.DidNotReceive().GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockVectorRepository.DidNotReceive().SearchAsync(
            Arg.Any<float[]>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldFallbackToLLM_WhenVectorSearchThrows()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";
        var memory = Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容");

        string? fallbackMessage = null;
        service.OnFallbackToLLMScoring += msg => fallbackMessage = msg;

        // Mock 健康检查通过
        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Mock Embedding 成功
        _mockEmbeddingClient.GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f });

        // Mock 向量搜索抛异常
        _mockVectorRepository.SearchAsync(
                Arg.Any<float[]>(),
                Arg.Any<int>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("向量数据库连接失败"));

        // Mock LLM 评分路径
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory> { memory });

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.7, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await service.SearchBySemanticAsync(query);

        // Assert
        results.Should().HaveCount(1);

        // 验证降级通知事件
        fallbackMessage.Should().NotBeNullOrEmpty();
        fallbackMessage.Should().Contain("向量搜索不可用");

        // 验证异常日志
        _mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("向量搜索失败")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

        // 验证使用 LLM 评分路径
        await _mockRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await _mockLlmClient.Received(1).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldNotFallback_WhenEmbeddingClientIsMissing()
    {
        // Arrange - 只注入 VectorRepository，不注入 EmbeddingClient
        var serviceWithoutEmbedding = new MemoryRetrievalService(
            _mockLlmFactory,
            _mockRepository,
            _mockLogger,
            embeddingClient: null,
            _mockVectorRepository);

        var query = "测试";
        var memory = Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容");

        // Mock LLM 评分路径
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory> { memory });

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await serviceWithoutEmbedding.SearchBySemanticAsync(query);

        // Assert
        results.Should().HaveCount(1);

        // 不应尝试健康检查
        await _mockVectorRepository.DidNotReceive().IsHealthyAsync(Arg.Any<CancellationToken>());

        // 直接使用 LLM 评分
        await _mockRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldNotFallback_WhenVectorRepositoryIsMissing()
    {
        // Arrange - 只注入 EmbeddingClient，不注入 VectorRepository
        var serviceWithoutVector = new MemoryRetrievalService(
            _mockLlmFactory,
            _mockRepository,
            _mockLogger,
            _mockEmbeddingClient,
            vectorRepository: null);

        var query = "测试";
        var memory = Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容");

        // Mock LLM 评分路径
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory> { memory });

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await serviceWithoutVector.SearchBySemanticAsync(query);

        // Assert
        results.Should().HaveCount(1);

        // 不应尝试生成 Embedding
        await _mockEmbeddingClient.DidNotReceive().GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // 直接使用 LLM 评分
        await _mockRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchBySemanticAsync_ShouldHandleMissingMemories_InVectorResults()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";
        var queryVector = new float[] { 0.1f };

        var memory1 = Core.Models.Memory.Create(MemoryType.User, "exists", "存在", "内容");
        var missingId = Guid.NewGuid();

        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        _mockEmbeddingClient.GenerateEmbeddingAsync(query, Arg.Any<CancellationToken>())
            .Returns(queryVector);

        // 向量搜索返回 2 个结果，但其中一个记忆不存在
        var vectorResults = new List<VectorSearchResult>
        {
            new() { MemoryId = memory1.Id, Score = 0.9, Metadata = new() },
            new() { MemoryId = missingId, Score = 0.8, Metadata = new() }
        };

        _mockVectorRepository.SearchAsync(
                Arg.Any<float[]>(),
                Arg.Any<int>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(vectorResults);

        _mockRepository.GetByIdAsync(memory1.Id, Arg.Any<CancellationToken>())
            .Returns(memory1);
        _mockRepository.GetByIdAsync(missingId, Arg.Any<CancellationToken>())
            .Returns((Core.Models.Memory?)null); // 记忆不存在

        // Act
        var results = await service.SearchBySemanticAsync(query);

        // Assert - 只返回存在的记忆
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(memory1.Id);
    }

    [Fact]
    public async Task SearchBySemanticAsync_WithTypeFilter_ShouldFallbackCorrectly()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";
        var memory = Core.Models.Memory.Create(MemoryType.User, "test", "测试", "内容");

        // 健康检查失败，触发降级
        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Mock LLM 评分路径（按类型过滤）
        _mockRepository.GetByTypeAsync(MemoryType.User, Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory> { memory });

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = """{ "score": 0.8, "reason": "测试" }""",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var results = await service.SearchBySemanticAsync(query, typeFilter: MemoryType.User);

        // Assert
        results.Should().HaveCount(1);

        // 验证使用按类型过滤的仓储方法
        await _mockRepository.Received(1).GetByTypeAsync(MemoryType.User, Arg.Any<CancellationToken>());
        await _mockRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnFallbackToLLMScoring_ShouldContainHelpfulMessage()
    {
        // Arrange
        var service = CreateServiceWithVectorSupport();
        var query = "测试";

        string? capturedMessage = null;
        service.OnFallbackToLLMScoring += msg => capturedMessage = msg;

        _mockVectorRepository.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Core.Models.Memory>());

        // Act
        await service.SearchBySemanticAsync(query);

        // Assert
        capturedMessage.Should().NotBeNullOrEmpty();
        capturedMessage.Should().Contain("⚠️");
        capturedMessage.Should().Contain("向量搜索不可用");
        capturedMessage.Should().Contain("LLM 评分");
        capturedMessage.Should().Contain("较慢");
        capturedMessage.Should().Contain("50-100秒");
        capturedMessage.Should().Contain("Qdrant");
        capturedMessage.Should().Contain("docker run");
        capturedMessage.Should().Contain("10-50ms");
        capturedMessage.Should().Contain("1000-10000 倍");
    }
}
