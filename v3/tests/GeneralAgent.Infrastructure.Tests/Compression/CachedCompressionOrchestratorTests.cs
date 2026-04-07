using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Tests.Compression;

/// <summary>
/// CachedCompressionOrchestrator 单元测试
/// </summary>
public sealed class CachedCompressionOrchestratorTests
{
    private readonly ICompressionOrchestrator _mockInner;
    private readonly IMemoryCache _cache;
    private readonly CachedCompressionOrchestrator _orchestrator;

    public CachedCompressionOrchestratorTests()
    {
        _mockInner = Substitute.For<ICompressionOrchestrator>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _orchestrator = new CachedCompressionOrchestrator(
            _mockInner,
            _cache,
            NullLogger<CachedCompressionOrchestrator>.Instance,
            cacheDuration: TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task CompressAsync_FirstCall_ShouldCallInner()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" },
            new() { Role = MessageRole.Assistant, Content = "Hi" }
        };

        var expectedResult = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats { OriginalMessageCount = 2, CompressedMessageCount = 2 }
        };

        _mockInner.CompressAsync(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _orchestrator.CompressAsync(messages);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(2);

        // 验证调用了内部服务
        await _mockInner.Received(1).CompressAsync(
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressAsync_SecondCallWithSameMessages_ShouldUseCacheAndNotCallInner()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" },
            new() { Role = MessageRole.Assistant, Content = "Hi" }
        };

        var expectedResult = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats { OriginalMessageCount = 2, CompressedMessageCount = 2 }
        };

        _mockInner.CompressAsync(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act - 第一次调用
        var result1 = await _orchestrator.CompressAsync(messages);

        // Act - 第二次调用（相同消息）
        var result2 = await _orchestrator.CompressAsync(messages);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result2.CompressedMessages.Should().HaveCount(2);

        // 验证只调用了一次内部服务（第二次使用缓存）
        await _mockInner.Received(1).CompressAsync(
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressAsync_WithDifferentMessages_ShouldCallInnerAgain()
    {
        // Arrange
        var messages1 = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        var messages2 = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Goodbye" }
        };

        var result1 = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages1,
            Stats = new CompressionStats()
        };

        var result2 = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages2,
            Stats = new CompressionStats()
        };

        _mockInner.CompressAsync(messages1, Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result1);

        _mockInner.CompressAsync(messages2, Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result2);

        // Act
        await _orchestrator.CompressAsync(messages1);
        await _orchestrator.CompressAsync(messages2);

        // Assert - 验证调用了两次（因为消息不同）
        await _mockInner.Received(1).CompressAsync(messages1, Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>());
        await _mockInner.Received(1).CompressAsync(messages2, Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressAsync_WithDifferentOptions_ShouldCallInnerAgain()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        var options1 = new CompressionOptions { Strategy = "sliding_window" };
        var options2 = new CompressionOptions { Strategy = "semantic" };

        var result = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats()
        };

        _mockInner.CompressAsync(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _orchestrator.CompressAsync(messages, options1);
        await _orchestrator.CompressAsync(messages, options2);

        // Assert - 验证调用了两次（因为选项不同）
        await _mockInner.Received(2).CompressAsync(
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressAsync_WhenInnerFails_ShouldNotCacheResult()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        var failedResult = new CompressionResult
        {
            Success = false,
            ErrorMessage = "Compression failed",
            CompressedMessages = new List<Message>()
        };

        _mockInner.CompressAsync(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(failedResult);

        // Act - 第一次调用（失败）
        var result1 = await _orchestrator.CompressAsync(messages);

        // Act - 第二次调用（应该重试）
        var result2 = await _orchestrator.CompressAsync(messages);

        // Assert
        result1.Success.Should().BeFalse();
        result2.Success.Should().BeFalse();

        // 验证调用了两次（因为失败结果不缓存）
        await _mockInner.Received(2).CompressAsync(
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressWithStrategyAsync_ShouldRespectStrategy()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        var strategyName = "hierarchical";
        var result = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats()
        };

        _mockInner.CompressWithStrategyAsync(strategyName, Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var actualResult = await _orchestrator.CompressWithStrategyAsync(strategyName, messages);

        // Assert
        actualResult.Should().NotBeNull();
        actualResult.Success.Should().BeTrue();

        // 验证调用了内部服务的指定策略方法
        await _mockInner.Received(1).CompressWithStrategyAsync(
            strategyName,
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressWithStrategyAsync_SecondCall_ShouldUseCache()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        var strategyName = "semantic";
        var result = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats()
        };

        _mockInner.CompressWithStrategyAsync(strategyName, Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act - 两次调用
        await _orchestrator.CompressWithStrategyAsync(strategyName, messages);
        await _orchestrator.CompressWithStrategyAsync(strategyName, messages);

        // Assert - 验证只调用了一次
        await _mockInner.Received(1).CompressWithStrategyAsync(
            strategyName,
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetAvailableStrategies_ShouldForwardToInner()
    {
        // Arrange
        var strategies = new List<string> { "sliding_window", "hierarchical", "semantic" };
        _mockInner.GetAvailableStrategies().Returns(strategies);

        // Act
        var result = _orchestrator.GetAvailableStrategies();

        // Assert
        result.Should().BeEquivalentTo(strategies);
        _mockInner.Received(1).GetAvailableStrategies();
    }

    [Fact]
    public void RecommendStrategy_ShouldForwardToInner()
    {
        // Arrange
        var messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "Hello" }
        };

        _mockInner.RecommendStrategy(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>())
            .Returns("sliding_window");

        // Act
        var result = _orchestrator.RecommendStrategy(messages);

        // Assert
        result.Should().Be("sliding_window");
        _mockInner.Received(1).RecommendStrategy(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>());
    }

    [Fact]
    public async Task CompressAsync_WithEmptyMessages_ShouldStillCache()
    {
        // Arrange
        var messages = new List<Message>();
        var result = new CompressionResult
        {
            Success = true,
            CompressedMessages = messages,
            Stats = new CompressionStats()
        };

        _mockInner.CompressAsync(Arg.Any<List<Message>>(), Arg.Any<CompressionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _orchestrator.CompressAsync(messages);
        await _orchestrator.CompressAsync(messages);

        // Assert - 验证只调用了一次（缓存生效）
        await _mockInner.Received(1).CompressAsync(
            Arg.Any<List<Message>>(),
            Arg.Any<CompressionOptions>(),
            Arg.Any<CancellationToken>());
    }
}
