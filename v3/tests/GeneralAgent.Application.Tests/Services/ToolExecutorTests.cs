using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ToolExecutor 单元测试
/// </summary>
public sealed class ToolExecutorTests
{
    private readonly ToolRegistry _registry;
    private readonly ILogger<ToolExecutor> _mockLogger;
    private readonly ToolExecutor _executor;
    private readonly ToolExecutionContext _context;

    public ToolExecutorTests()
    {
        var registryLogger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(registryLogger);
        _mockLogger = Substitute.For<ILogger<ToolExecutor>>();
        _executor = new ToolExecutor(_registry, _mockLogger);

        _context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenToolExecutesSuccessfully()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Success result"));

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(call, _context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Success result");
        result.Call.Should().Be(call);
        result.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenToolNotFound()
    {
        // Arrange
        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "non_existent_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(call, _context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("工具未找到");
        result.ErrorMessage.Should().Contain("non_existent_tool");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenToolReturnsError()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure("Tool execution failed"));

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(call, _context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Tool execution failed");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenToolThrowsException()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns<Result<string>>(_ => throw new InvalidOperationException("Unexpected error"));

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(call, _context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("工具执行失败");
        result.ErrorMessage.Should().Contain("Unexpected error");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenOperationIsCancelled()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(1000, ct);
                return Result<string>.Success("Should not reach here");
            });

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _executor.ExecuteAsync(call, _context, ct: cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("已取消");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenTimeout()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(5000, ct);
                return Result<string>.Success("Should not reach here");
            });

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var result = await _executor.ExecuteAsync(call, _context, timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("超时");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowArgumentNullException_WhenCallIsNull()
    {
        // Act & Assert
        var action = async () => await _executor.ExecuteAsync(null!, _context);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act & Assert
        var action = async () => await _executor.ExecuteAsync(call, null!);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectArgumentsToTool()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Success"));

        _registry.Register(tool);

        var arguments = new Dictionary<string, object>
        {
            ["param1"] = "value1",
            ["param2"] = 42
        };

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = arguments
        };

        // Act
        await _executor.ExecuteAsync(call, _context);

        // Assert
        await tool.Received(1).ExecuteAsync(
            Arg.Is<IReadOnlyDictionary<string, object>>(args =>
                args["param1"].Equals("value1") &&
                args["param2"].Equals(42)),
            Arg.Is<ToolExecutionContext>(ctx => ctx.SessionId == _context.SessionId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExecuteManyAsync Tests

    [Fact]
    public async Task ExecuteManyAsync_ShouldExecuteAllTools()
    {
        // Arrange
        var tool1 = CreateMockTool("tool1", "Tool 1");
        var tool2 = CreateMockTool("tool2", "Tool 2");
        var tool3 = CreateMockTool("tool3", "Tool 3");

        tool1.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Result 1"));

        tool2.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Result 2"));

        tool3.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Result 3"));

        _registry.Register(tool1);
        _registry.Register(tool2);
        _registry.Register(tool3);

        var calls = new List<ToolCall>
        {
            new() { Id = "1", ToolName = "tool1", Arguments = new Dictionary<string, object>() },
            new() { Id = "2", ToolName = "tool2", Arguments = new Dictionary<string, object>() },
            new() { Id = "3", ToolName = "tool3", Arguments = new Dictionary<string, object>() }
        };

        // Act
        var results = await _executor.ExecuteManyAsync(calls, _context);

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results[0].Content.Should().Be("Result 1");
        results[1].Content.Should().Be("Result 2");
        results[2].Content.Should().Be("Result 3");
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldHandleMixedSuccessAndFailure()
    {
        // Arrange
        var tool1 = CreateMockTool("tool1", "Tool 1");
        var tool2 = CreateMockTool("tool2", "Tool 2");

        tool1.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Success"));

        tool2.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure("Failure"));

        _registry.Register(tool1);
        _registry.Register(tool2);

        var calls = new List<ToolCall>
        {
            new() { Id = "1", ToolName = "tool1", Arguments = new Dictionary<string, object>() },
            new() { Id = "2", ToolName = "tool2", Arguments = new Dictionary<string, object>() }
        };

        // Act
        var results = await _executor.ExecuteManyAsync(calls, _context);

        // Assert
        results.Should().HaveCount(2);
        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldReturnEmptyList_WhenNoCallsProvided()
    {
        // Arrange
        var calls = new List<ToolCall>();

        // Act
        var results = await _executor.ExecuteManyAsync(calls, _context);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldThrowArgumentNullException_WhenCallsIsNull()
    {
        // Act & Assert
        var action = async () => await _executor.ExecuteManyAsync(null!, _context);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var calls = new List<ToolCall>
        {
            new() { Id = "1", ToolName = "tool1", Arguments = new Dictionary<string, object>() }
        };

        // Act & Assert
        var action = async () => await _executor.ExecuteManyAsync(calls, null!);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldExecuteInParallel()
    {
        // Arrange
        var executionOrder = new List<int>();
        var lockObject = new object();

        var tool1 = CreateMockTool("tool1", "Tool 1");
        var tool2 = CreateMockTool("tool2", "Tool 2");

        tool1.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(100);
                lock (lockObject) { executionOrder.Add(1); }
                return Result<string>.Success("Result 1");
            });

        tool2.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                lock (lockObject) { executionOrder.Add(2); }
                return Result<string>.Success("Result 2");
            });

        _registry.Register(tool1);
        _registry.Register(tool2);

        var calls = new List<ToolCall>
        {
            new() { Id = "1", ToolName = "tool1", Arguments = new Dictionary<string, object>() },
            new() { Id = "2", ToolName = "tool2", Arguments = new Dictionary<string, object>() }
        };

        // Act
        await _executor.ExecuteManyAsync(calls, _context);

        // Assert
        // tool2 应该先完成（50ms < 100ms）
        executionOrder.Should().Equal(2, 1);
    }

    #endregion

    #region ExecuteStreamAsync Tests

    [Fact]
    public async Task ExecuteStreamAsync_ShouldYieldChunksFromTool()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test Tool");
        tool.ExecuteStreamAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable("Chunk 1", "Chunk 2", "Chunk 3"));

        _registry.Register(tool);

        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _executor.ExecuteStreamAsync(call, _context))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().Equal("Chunk 1", "Chunk 2", "Chunk 3");
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldYieldError_WhenToolNotFound()
    {
        // Arrange
        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "non_existent_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _executor.ExecuteStreamAsync(call, _context))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Should().Contain("错误");
        chunks[0].Should().Contain("工具未找到");
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldThrowArgumentNullException_WhenCallIsNull()
    {
        // Act & Assert
        var action = async () =>
        {
            await foreach (var _ in _executor.ExecuteStreamAsync(null!, _context))
            {
            }
        };

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var call = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            ToolName = "test_tool",
            Arguments = new Dictionary<string, object>()
        };

        // Act & Assert
        var action = async () =>
        {
            await foreach (var _ in _executor.ExecuteStreamAsync(call, null!))
            {
            }
        };

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    private static ITool CreateMockTool(string name, string description)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        return tool;
    }

    private static async IAsyncEnumerable<string> AsyncEnumerable(params string[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    #endregion
}
