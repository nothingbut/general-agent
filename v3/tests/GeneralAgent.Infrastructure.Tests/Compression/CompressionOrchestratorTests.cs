using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Compression.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Compression;

public class CompressionOrchestratorTests
{
    private readonly CompressionOrchestrator _orchestrator;
    private readonly ITokenCounter _tokenCounter;

    public CompressionOrchestratorTests()
    {
        _tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());

        var strategies = new List<GeneralAgent.Infrastructure.Compression.ICompressionStrategy>
        {
            new SlidingWindowStrategy(_tokenCounter, new NullLogger<SlidingWindowStrategy>()),
            new HierarchicalStrategy(_tokenCounter, new NullLogger<HierarchicalStrategy>()),
            new SemanticStrategy(_tokenCounter, new NullLogger<SemanticStrategy>())
        };

        var logger = new NullLogger<CompressionOrchestrator>();
        _orchestrator = new CompressionOrchestrator(strategies, _tokenCounter, logger);
    }

    [Fact]
    public async Task CompressAsync_WithFewerMessagesThanThreshold_ShouldSkipCompression()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Message 1"),
            Message.CreateUser(sessionId, "Message 2")
        };

        var options = new CompressionOptions
        {
            MinMessagesForCompression = 5
        };

        // Act
        var result = await _orchestrator.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(2); // 保持原样
        result.Stats.StrategyUsed.Should().Be("none");
    }

    [Fact]
    public async Task CompressAsync_WithAutoStrategy_ShouldRecommendCorrectStrategy()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 15)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            MinMessagesForCompression = 10
            // Strategy 为空，应该自动推荐
        };

        // Act
        var result = await _orchestrator.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.StrategyUsed.Should().NotBeNullOrEmpty();
        result.CompressedMessages.Should().HaveCountLessThanOrEqualTo(15);
    }

    [Fact]
    public async Task CompressWithStrategyAsync_WithSlidingWindow_ShouldUseSlidingWindowStrategy()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            WindowSize = 10
        };

        // Act
        var result = await _orchestrator.CompressWithStrategyAsync("sliding_window", messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.StrategyUsed.Should().Be("sliding_window");
        result.CompressedMessages.Should().HaveCount(10);
    }

    [Fact]
    public async Task CompressWithStrategyAsync_WithHierarchical_ShouldUseHierarchicalStrategy()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 30)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var result = await _orchestrator.CompressWithStrategyAsync("hierarchical", messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.StrategyUsed.Should().Be("hierarchical");
        result.CompressedMessages.Should().HaveCount(11); // 1摘要 + 5中期 + 5近期
    }

    [Fact]
    public async Task CompressWithStrategyAsync_WithSemantic_ShouldUseSemanticStrategy()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 25)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            PreserveRecentCount = 5,
            EnableLlmSummary = false // 使用规则摘要
        };

        // Act
        var result = await _orchestrator.CompressWithStrategyAsync("semantic", messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.StrategyUsed.Should().Be("semantic");
        result.CompressedMessages.Should().HaveCount(6); // 1摘要 + 5近期
    }

    [Fact]
    public async Task CompressWithStrategyAsync_WithInvalidStrategy_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Message 1")
        };

        // Act
        var result = await _orchestrator.CompressWithStrategyAsync("invalid_strategy", messages);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未找到压缩策略");
        result.ErrorMessage.Should().Contain("invalid_strategy");
    }

    [Fact]
    public void GetAvailableStrategies_ShouldReturnAllRegisteredStrategies()
    {
        // Act
        var strategies = _orchestrator.GetAvailableStrategies();

        // Assert
        strategies.Should().NotBeEmpty();
        strategies.Should().Contain("sliding_window");
        strategies.Should().Contain("hierarchical");
        strategies.Should().Contain("semantic");
        strategies.Should().HaveCount(3);
    }

    [Fact]
    public void RecommendStrategy_WithFewMessages_ShouldRecommendSlidingWindow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 15)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        // Act
        var recommended = _orchestrator.RecommendStrategy(messages);

        // Assert
        recommended.Should().Be("sliding_window");
    }

    [Fact]
    public void RecommendStrategy_WithMediumMessages_ShouldRecommendHierarchical()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 35)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        // Act
        var recommended = _orchestrator.RecommendStrategy(messages);

        // Assert
        recommended.Should().Be("hierarchical");
    }

    [Fact]
    public void RecommendStrategy_WithEmptyMessages_ShouldReturnDefault()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var recommended = _orchestrator.RecommendStrategy(messages);

        // Assert
        recommended.Should().Be("sliding_window");
    }

    [Fact]
    public async Task CompressAsync_ShouldLogCompressionStats()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 25)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            MinMessagesForCompression = 10,
            Strategy = "sliding_window",
            WindowSize = 10
        };

        // Act
        var result = await _orchestrator.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.Should().NotBeNull();
        result.Stats.OriginalMessageCount.Should().Be(25);
        result.Stats.CompressedMessageCount.Should().Be(10);
        result.Stats.OriginalTokens.Should().BeGreaterThan(0);
        result.Stats.CompressedTokens.Should().BeGreaterThan(0);
        result.Stats.CompressionRatio.Should().BeLessThan(1.0);
        result.Stats.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CompressAsync_WithExplicitStrategy_ShouldUseSpecifiedStrategy()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 25)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            Strategy = "hierarchical",
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var result = await _orchestrator.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.StrategyUsed.Should().Be("hierarchical");
    }

    [Fact]
    public async Task CompressAsync_MultipleCompressions_ShouldBeConsistent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            Strategy = "sliding_window",
            WindowSize = 10
        };

        // Act - 执行两次压缩
        var result1 = await _orchestrator.CompressAsync(messages, options);
        var result2 = await _orchestrator.CompressAsync(messages, options);

        // Assert - 两次结果应该一致
        result1.CompressedMessages.Should().HaveCount(result2.CompressedMessages.Count);
        result1.Stats.CompressedTokens.Should().Be(result2.Stats.CompressedTokens);
    }
}
