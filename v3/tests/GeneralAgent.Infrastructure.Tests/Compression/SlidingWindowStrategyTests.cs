using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Compression.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Compression;

public class SlidingWindowStrategyTests
{
    private readonly SlidingWindowStrategy _strategy;

    public SlidingWindowStrategyTests()
    {
        var logger = new NullLogger<SlidingWindowStrategy>();
        var tokenCounter = new TokenCounter(new NullLogger<TokenCounter>());
        _strategy = new SlidingWindowStrategy(tokenCounter, logger);
    }

    [Fact]
    public async Task CompressAsync_WithFewerMessagesThanWindow_ShouldKeepAll()
    {
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Message 1"),
            Message.CreateAssistant(sessionId, "Response 1"),
            Message.CreateUser(sessionId, "Message 2")
        };

        var options = new CompressionOptions { WindowSize = 10 };
        var result = await _strategy.CompressAsync(messages, options);

        result.Should().NotBeNull();
        result.CompressedMessages.Should().HaveCount(3);
        result.Stats.OriginalMessageCount.Should().Be(3);
    }

    [Fact]
    public async Task CompressAsync_WithMoreMessagesThanWindow_ShouldKeepOnlyRecentMessages()
    {
        var sessionId = Guid.NewGuid();
        var messages = Enumerable.Range(0, 20)
            .Select(i => Message.CreateUser(sessionId, $"Message {i}"))
            .ToList();

        var options = new CompressionOptions { WindowSize = 10 };
        var result = await _strategy.CompressAsync(messages, options);

        result.Should().NotBeNull();
        result.CompressedMessages.Should().HaveCount(10);
        result.Stats.OriginalMessageCount.Should().Be(20);
    }

    [Fact]
    public async Task CompressAsync_EmptyMessageList_ShouldReturnEmptyResult()
    {
        var messages = new List<Message>();
        var result = await _strategy.CompressAsync(messages);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().BeEmpty();
    }
}
