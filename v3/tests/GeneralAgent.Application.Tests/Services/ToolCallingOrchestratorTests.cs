using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json.Nodes;
using Xunit;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ToolCallingOrchestrator 测试
/// </summary>
public sealed class ToolCallingOrchestratorTests
{
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _toolExecutor;
    private readonly ILLMClient _mockClient;
    private readonly IToolCallingListener _mockListener;
    private readonly IToolSerializer _serializer;
    private readonly ToolCallingOrchestrator _orchestrator;

    public ToolCallingOrchestratorTests()
    {
        // 创建真实的 ToolRegistry 和 ToolExecutor
        _registry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
        _toolExecutor = new ToolExecutor(_registry, Substitute.For<ILogger<ToolExecutor>>());

        // Mock 外部依赖
        _mockClient = Substitute.For<ILLMClient>();
        _mockListener = Substitute.For<IToolCallingListener>();
        _serializer = Substitute.For<IToolSerializer>();

        // 配置序列化器返回空数组（默认行为）
        _serializer.SerializeTools(Arg.Any<IEnumerable<ToolDefinition>>())
            .Returns(new JsonArray());

        // 创建 Orchestrator
        var config = Options.Create(new ToolCallingConfig
        {
            MaxRounds = 3,
            AbsoluteMaxRounds = 20,
            Enabled = true
        });
        _orchestrator = new ToolCallingOrchestrator(
            _toolExecutor,
            _registry,
            _mockClient,
            _mockListener,
            _serializer,
            config,
            Substitute.For<ILogger<ToolCallingOrchestrator>>());
    }

    #region 测试 1: 无工具调用 - LLM 直接返回响应

    [Fact]
    public async Task ExecuteAsync_NoToolCalls_ShouldReturnDirectResponse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        // Mock LLM 返回没有工具调用的响应
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Hello! How can I help you?",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("Hello! How can I help you?", result.FinalResponse);
        Assert.Equal(0, result.TotalRounds);
        Assert.Equal(0, result.TotalToolCalls);
        Assert.False(result.Truncated);
        Assert.Null(result.TruncationReason);
        Assert.Single(result.Messages); // 用户消息
    }

    #endregion

    #region 测试 2: 单轮工具调用

    [Fact]
    public async Task ExecuteAsync_SingleRound_ShouldExecuteToolAndReturnFinalResponse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "What's the weather?" }
        };

        // 注册一个测试工具
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("get_weather");
        mockTool.Description.Returns("Get weather");
        mockTool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "get_weather",
            Description = "Get weather",
            InputSchema = new JsonObject()
        });
        mockTool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("Sunny, 25°C"));
        _registry.Register(mockTool);

        // Mock LLM 第一次调用返回工具调用
        // Mock LLM 第二次调用返回最终响应
        var callCount = 0;
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // 第一次调用：返回工具调用
                    return new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new()
                            {
                                Id = "call_1",
                                ToolName = "get_weather",
                                Arguments = new Dictionary<string, object> { ["location"] = "Beijing" }
                            }
                        }
                    };
                }
                else
                {
                    // 第二次调用：返回最终响应
                    return new CompletionResponse
                    {
                        Content = "The weather is sunny, 25°C.",
                        Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = null
                    };
                }
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("The weather is sunny, 25°C.", result.FinalResponse);
        Assert.Equal(1, result.TotalRounds);
        Assert.Equal(1, result.TotalToolCalls);
        Assert.False(result.Truncated);
        Assert.Null(result.TruncationReason);

        // 验证 LLM 被调用了 2 次
        await _mockClient.Received(2).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region 测试 3: 多轮工具调用

    [Fact]
    public async Task ExecuteAsync_MultipleRounds_ShouldExecuteMultipleToolCalls()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "What's the weather and time?" }
        };

        // 注册两个工具
        var weatherTool = Substitute.For<ITool>();
        weatherTool.Name.Returns("get_weather");
        weatherTool.Description.Returns("Get weather");
        weatherTool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "get_weather",
            Description = "Get weather",
            InputSchema = new JsonObject()
        });
        weatherTool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("Sunny"));
        _registry.Register(weatherTool);

        var timeTool = Substitute.For<ITool>();
        timeTool.Name.Returns("get_time");
        timeTool.Description.Returns("Get time");
        timeTool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "get_time",
            Description = "Get time",
            InputSchema = new JsonObject()
        });
        timeTool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("10:00 AM"));
        _registry.Register(timeTool);

        // Mock LLM 返回
        var callCount = 0;
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new() { Id = "call_1", ToolName = "get_weather", Arguments = new Dictionary<string, object>() }
                        }
                    };
                }
                else if (callCount == 2)
                {
                    return new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new() { Id = "call_2", ToolName = "get_time", Arguments = new Dictionary<string, object>() }
                        }
                    };
                }
                else
                {
                    return new CompletionResponse
                    {
                        Content = "It's sunny and 10:00 AM.",
                        Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = null
                    };
                }
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("It's sunny and 10:00 AM.", result.FinalResponse);
        Assert.Equal(2, result.TotalRounds);
        Assert.Equal(2, result.TotalToolCalls);
        Assert.False(result.Truncated);
    }

    #endregion

    #region 测试 4: 达到最大轮数 - 用户选择停止

    [Fact]
    public async Task ExecuteAsync_MaxRoundsReached_UserStops_ShouldTruncate()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Keep calling tools" }
        };

        // 注册工具
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test_tool");
        tool.Description.Returns("Test");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test",
            InputSchema = new JsonObject()
        });
        tool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("result"));
        _registry.Register(tool);

        // Mock LLM 总是返回工具调用
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "test_tool", Arguments = new Dictionary<string, object>() }
                }
            });

        // Mock 用户选择停止
        _mockListener.OnMaxRoundsReachedAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ToolCall>>(), Arg.Any<CancellationToken>())
            .Returns(new ExtendDecision { Stop = true, ExtendBy = 0 });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal(3, result.TotalRounds);
        Assert.Equal(3, result.TotalToolCalls);
        Assert.True(result.Truncated);
        Assert.NotNull(result.TruncationReason);
        Assert.Contains("用户选择停止", result.TruncationReason);
    }

    #endregion

    #region 测试 5: 达到最大轮数 - 用户选择延长

    [Fact]
    public async Task ExecuteAsync_MaxRoundsReached_UserExtends_ShouldContinue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Question" }
        };

        // 注册工具
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test_tool");
        tool.Description.Returns("Test");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test",
            InputSchema = new JsonObject()
        });
        tool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("result"));
        _registry.Register(tool);

        // Mock LLM 返回
        var callCount = 0;
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount <= 4)
                {
                    // 前 4 次返回工具调用
                    return new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new() { Id = $"call_{callCount}", ToolName = "test_tool", Arguments = new Dictionary<string, object>() }
                        }
                    };
                }
                else
                {
                    // 第 5 次返回最终响应
                    return new CompletionResponse
                    {
                        Content = "Final answer",
                        Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = null
                    };
                }
            });

        // Mock 用户选择延长 2 轮
        _mockListener.OnMaxRoundsReachedAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ToolCall>>(), Arg.Any<CancellationToken>())
            .Returns(new ExtendDecision { Stop = false, ExtendBy = 2 });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("Final answer", result.FinalResponse);
        Assert.Equal(4, result.TotalRounds);
        Assert.Equal(4, result.TotalToolCalls);
        Assert.False(result.Truncated);
    }

    #endregion

    #region 测试 6: 达到绝对最大轮数

    [Fact]
    public async Task ExecuteAsync_AbsoluteMaxRoundsReached_ShouldTruncate()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Question" }
        };

        // 注册工具
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("test_tool");
        tool.Description.Returns("Test");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test",
            InputSchema = new JsonObject()
        });
        tool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Success("result"));
        _registry.Register(tool);

        // Mock LLM 总是返回工具调用
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "test_tool", Arguments = new Dictionary<string, object>() }
                }
            });

        // Mock 用户总是选择延长
        _mockListener.OnMaxRoundsReachedAsync(Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<ToolCall>>(), Arg.Any<CancellationToken>())
            .Returns(new ExtendDecision { Stop = false, ExtendBy = 10 });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal(20, result.TotalRounds); // AbsoluteMaxRounds
        Assert.True(result.Truncated);
        Assert.NotNull(result.TruncationReason);
        Assert.Contains("达到绝对最大轮数", result.TruncationReason);
    }

    #endregion

    #region 测试 7: Tool Calling 禁用

    [Fact]
    public async Task ExecuteAsync_ToolCallingDisabled_ShouldCallLLMDirectly()
    {
        // Arrange
        var config = Options.Create(new ToolCallingConfig { Enabled = false });
        var orchestrator = new ToolCallingOrchestrator(
            _toolExecutor,
            _registry,
            _mockClient,
            _mockListener,
            _serializer,
            config,
            Substitute.For<ILogger<ToolCallingOrchestrator>>());

        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Hello!",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        // Act
        var result = await orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("Hello!", result.FinalResponse);
        Assert.Equal(0, result.TotalRounds);
        Assert.Equal(0, result.TotalToolCalls);
        Assert.False(result.Truncated);

        // 验证 LLM 被调用，但没有传递工具列表
        await _mockClient.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Tools == null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 测试 8: 没有注册工具

    [Fact]
    public async Task ExecuteAsync_NoToolsRegistered_ShouldCallLLMWithoutTools()
    {
        // Arrange - 不注册任何工具
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Hello!",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("Hello!", result.FinalResponse);
        Assert.Equal(0, result.TotalRounds);
        Assert.Equal(0, result.TotalToolCalls);

        // 验证序列化器未被调用（因为没有工具可序列化）
        _serializer.DidNotReceive().SerializeTools(Arg.Any<IEnumerable<ToolDefinition>>());
    }

    #endregion

    #region 测试 9: 工具执行失败

    [Fact]
    public async Task ExecuteAsync_ToolExecutionFails_ShouldContinueWithErrorMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Question" }
        };

        // 注册一个会失败的工具
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("failing_tool");
        tool.Description.Returns("Failing tool");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "failing_tool",
            Description = "Failing tool",
            InputSchema = new JsonObject()
        });
        tool.ExecuteAsync(Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Core.Common.Result<string>.Failure("Tool failed"));
        _registry.Register(tool);

        // Mock LLM 返回
        var callCount = 0;
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new() { Id = "call_1", ToolName = "failing_tool", Arguments = new Dictionary<string, object>() }
                        }
                    };
                }
                else
                {
                    return new CompletionResponse
                    {
                        Content = "I encountered an error.",
                        Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = null
                    };
                }
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(sessionId, history, null);

        // Assert
        Assert.Equal("I encountered an error.", result.FinalResponse);
        Assert.Equal(1, result.TotalRounds);
        Assert.Equal(1, result.TotalToolCalls);
        Assert.False(result.Truncated);
    }

    #endregion
}
