using System.Text.Json;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Tests.Services;

public class ToolExecutorTests
{
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _executor;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutorTests()
    {
        _logger = Substitute.For<ILogger<ToolExecutor>>();
        _registry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
        _executor = new ToolExecutor(_registry, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteTool_WhenFound()
    {
        // Arrange
        var mockTool = CreateEchoTool("test_echo");
        _registry.Register(mockTool);

        var arguments = new Dictionary<string, object>
        {
            ["message"] = "Hello World"
        };

        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var result = await _executor.ExecuteAsync("test_echo", arguments, context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Hello World", result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenToolNotFound()
    {
        // Arrange
        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var result = await _executor.ExecuteAsync(
            "non_existent",
            new Dictionary<string, object>(),
            context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("工具不存在", result.Error);
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldExecuteInParallel()
    {
        // Arrange
        var mockTool = CreateEchoTool("test_echo");
        _registry.Register(mockTool);

        var toolCalls = new[]
        {
            new ToolCall
            {
                Id = "call_1",
                FunctionName = "test_echo",
                Arguments = JsonSerializer.Serialize(new { message = "First" })
            },
            new ToolCall
            {
                Id = "call_2",
                FunctionName = "test_echo",
                Arguments = JsonSerializer.Serialize(new { message = "Second" })
            }
        };

        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var results = await _executor.ExecuteManyAsync(toolCalls, context);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.IsError));
        Assert.Contains(results, r => r.Content.Contains("First"));
        Assert.Contains(results, r => r.Content.Contains("Second"));
    }

    private ITool CreateEchoTool(string name)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns("Echo tool");
        tool.ExecuteAsync(
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var args = callInfo.Arg<Dictionary<string, object>>();
                var message = args.GetValueOrDefault("message", "No message");
                return Task.FromResult(Result<string>.Success($"Echo: {message}"));
            });

        return tool;
    }
}
