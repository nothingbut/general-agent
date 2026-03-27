using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory.Services;
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
