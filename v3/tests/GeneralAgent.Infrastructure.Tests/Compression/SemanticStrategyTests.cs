using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Compression.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Compression;

public class SemanticStrategyTests
{
    private readonly SemanticStrategy _strategy;

    public SemanticStrategyTests()
    {
        var logger = new NullLogger<SemanticStrategy>();
        var tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());
        // 创建无LLM的策略（使用规则摘要）
        _strategy = new SemanticStrategy(tokenCounter, logger);
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
        result.Stats.StrategyUsed.Should().Be("semantic");
    }

    [Fact]
    public async Task CompressAsync_WithFewMessages_ShouldKeepRecentMessages()
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
            PreserveRecentCount = 5
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(3); // 所有消息都保留
    }

    [Fact]
    public async Task CompressAsync_WithManyMessages_ShouldGenerateRuleBasedSummary()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions
        {
            PreserveRecentCount = 5,
            EnableLlmSummary = false
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(6); // 1摘要 + 5近期

        // 第一条应该是规则生成的摘要
        result.CompressedMessages.First().Role.Should().Be(MessageRole.System);
        result.CompressedMessages.First().Content.Should().Contain("[会话摘要]");

        // 元数据
        result.Metadata.Should().ContainKey("llm_summary_used");
        result.Metadata["llm_summary_used"].Should().Be(false);
        result.Metadata.Should().ContainKey("old_messages_summarized");
        result.Metadata["old_messages_summarized"].Should().Be(15);
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
            PreserveRecentCount = 5
        };

        // Act
        var estimatedTokens = _strategy.EstimateCompressedTokens(messages, options);

        // Assert
        estimatedTokens.Should().BeGreaterThan(0);

        // 估算应该包含：5条近期消息 + 约200 tokens的摘要
        var tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());
        var recentMessages = messages.TakeLast(5).ToList();
        var recentTokens = tokenCounter.CountMessagesTokens(recentMessages);
        estimatedTokens.Should().BeGreaterThanOrEqualTo(recentTokens);
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
            MinMessagesForCompression = 10
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
            MinMessagesForCompression = 10
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
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i} with some content"))
            .ToList();

        var options = new CompressionOptions
        {
            PreserveRecentCount = 5
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Stats.Should().NotBeNull();
        result.Stats.OriginalMessageCount.Should().Be(20);
        result.Stats.CompressedMessageCount.Should().Be(6); // 1摘要 + 5近期
        result.Stats.OriginalTokens.Should().BeGreaterThan(0);
        result.Stats.CompressedTokens.Should().BeGreaterThan(0);
        result.Stats.CompressedTokens.Should().BeLessThan(result.Stats.OriginalTokens);
        result.Stats.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.Stats.CompressionRatio.Should().BeLessThan(1.0);
    }

    [Fact]
    public async Task CompressAsync_WithOnlyRecentMessages_ShouldNotGenerateSummary()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Message 1"),
            Message.CreateUser(sessionId, "Message 2"),
            Message.CreateUser(sessionId, "Message 3")
        };

        var options = new CompressionOptions
        {
            PreserveRecentCount = 10 // 大于消息数量
        };

        // Act
        var result = await _strategy.CompressAsync(messages, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(3); // 所有消息都保留
        result.CompressedMessages.Should().NotContain(m => m.Content.Contains("[会话摘要]"));
    }
}
