using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Compression.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Compression;

public class HierarchicalStrategyTests
{
    private readonly HierarchicalStrategy _strategy;

    public HierarchicalStrategyTests()
    {
        var logger = new NullLogger<HierarchicalStrategy>();
        var tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());
        _strategy = new HierarchicalStrategy(tokenCounter, logger);
    }

    [Fact]
    public async Task CompressAsync_WithEmptyMessages_ShouldReturnEmptyResult()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var result = await _strategy.CompressAsync(messages);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().BeEmpty();
        result.Stats.StrategyUsed.Should().Be("hierarchical");
    }

    [Fact]
    public async Task CompressAsync_WithFewMessages_ShouldKeepAllMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Message 1"),
            Message.CreateAssistant(sessionId, "Response 1"),
            Message.CreateUser(sessionId, "Message 2")
        };

        var options = new CompressionOptions
        {
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(3);
        result.Stats.OriginalMessageCount.Should().Be(3);
    }

    [Fact]
    public async Task CompressAsync_WithManyMessages_ShouldCompressIntoLayers()
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
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCountLessThan(30);

        // 应该包含：1条摘要 + 5条中期 + 5条近期 = 11条
        result.CompressedMessages.Should().HaveCount(11);

        // 第一条应该是摘要（系统消息）
        result.CompressedMessages.First().Role.Should().Be(MessageRole.System);
        result.CompressedMessages.First().Content.Should().Contain("[历史摘要]");

        // 元数据应该包含分层信息
        result.Metadata.Should().ContainKey("recent_messages_count");
        result.Metadata.Should().ContainKey("middle_messages_count");
        result.Metadata.Should().ContainKey("old_messages_count");
        result.Metadata.Should().ContainKey("summary_generated");
    }

    [Fact]
    public void EstimateCompressedTokens_ShouldReturnReasonableEstimate()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var estimatedTokens = _strategy.EstimateCompressedTokens(messages, options);

        // Assert
        estimatedTokens.Should().BeGreaterThan(0);

        // 估算值应该小于原始消息的token数量
        var tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());
        var originalTokens = tokenCounter.CountMessagesTokens(messages);
        estimatedTokens.Should().BeLessThan(originalTokens);
    }

    [Fact]
    public void IsApplicable_WithEnoughMessages_ShouldReturnTrue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var isApplicable = _strategy.IsApplicable(messages, options);

        // Assert
        isApplicable.Should().BeTrue();
    }

    [Fact]
    public void IsApplicable_WithTooFewMessages_ShouldReturnFalse()
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
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var isApplicable = _strategy.IsApplicable(messages, options);

        // Assert
        isApplicable.Should().BeFalse();
    }

    [Fact]
    public async Task CompressAsync_ShouldCalculateStatsCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 25)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            HierarchicalRecentCount = 5,
            HierarchicalMiddleCount = 5
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.Should().NotBeNull();
        result.Stats.OriginalMessageCount.Should().Be(25);
        result.Stats.CompressedMessageCount.Should().Be(11); // 1摘要 + 5中期 + 5近期
        result.Stats.OriginalTokens.Should().BeGreaterThan(0);
        result.Stats.CompressedTokens.Should().BeGreaterThan(0);
        result.Stats.CompressedTokens.Should().BeLessThan(result.Stats.OriginalTokens);
        result.Stats.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.Stats.CompressionRatio.Should().BeLessThan(1.0);
    }
}
