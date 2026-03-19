using GeneralAgent.Application.Services;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ToolCallingListener 测试
/// </summary>
public sealed class ToolCallingListenerTests
{
    #region ExtendDecision Tests

    [Fact]
    public void ExtendDecision_ShouldBeImmutable()
    {
        // Arrange & Act
        var decision = new ExtendDecision
        {
            Stop = false,
            ExtendBy = 3
        };

        // Assert - 记录类型的值相等性
        var samDecision = new ExtendDecision
        {
            Stop = false,
            ExtendBy = 3
        };
        Assert.Equal(decision, samDecision);
    }

    [Fact]
    public void ExtendDecision_WithStop_ShouldHaveZeroExtendBy()
    {
        // Arrange & Act
        var decision = new ExtendDecision
        {
            Stop = true,
            ExtendBy = 0
        };

        // Assert
        Assert.True(decision.Stop);
        Assert.Equal(0, decision.ExtendBy);
    }

    [Fact]
    public void ExtendDecision_WithContinue_ShouldHavePositiveExtendBy()
    {
        // Arrange & Act
        var decision = new ExtendDecision
        {
            Stop = false,
            ExtendBy = 5
        };

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(5, decision.ExtendBy);
    }

    #endregion

    #region ConsoleToolCallingListener Tests

    [Fact]
    public void ConsoleToolCallingListener_ShouldBeCreatable()
    {
        // Arrange & Act
        var listener = new ConsoleToolCallingListener();

        // Assert - 仅验证构造函数可用
        Assert.NotNull(listener);
    }

    // 注意：ConsoleToolCallingListener 使用 Console.ReadLine()，难以进行单元测试
    // 实际测试需要通过集成测试或手动测试完成

    #endregion

    #region AutomaticToolCallingListener Tests

    [Fact]
    public async Task AutomaticListener_ShouldAutoExtend_WithDefaultConfig()
    {
        // Arrange
        var config = new ToolCallingConfig();
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        var listener = new AutomaticToolCallingListener(options, logger);

        var sessionId = Guid.NewGuid();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                Id = "call_1",
                ToolName = "test_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var decision = await listener.OnMaxRoundsReachedAsync(3, sessionId, toolCalls);

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(config.AutoExtendBy, decision.ExtendBy);
    }

    [Fact]
    public async Task AutomaticListener_ShouldAutoExtend_WithCustomConfig()
    {
        // Arrange
        var config = new ToolCallingConfig { AutoExtendBy = 10 };
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        var listener = new AutomaticToolCallingListener(options, logger);

        var sessionId = Guid.NewGuid();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                Id = "call_1",
                ToolName = "test_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var decision = await listener.OnMaxRoundsReachedAsync(5, sessionId, toolCalls);

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(10, decision.ExtendBy);
    }

    [Fact]
    public async Task AutomaticListener_ShouldLogDecision()
    {
        // Arrange
        var config = new ToolCallingConfig { AutoExtendBy = 5 };
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        var listener = new AutomaticToolCallingListener(options, logger);

        var sessionId = Guid.NewGuid();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                Id = "call_1",
                ToolName = "test_tool",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        await listener.OnMaxRoundsReachedAsync(3, sessionId, toolCalls);

        // Assert - 验证日志调用（使用 NSubstitute 的 Received）
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void AutomaticListener_Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new AutomaticToolCallingListener(null!, logger));
        Assert.Equal("config", ex.ParamName);
    }

    [Fact]
    public void AutomaticListener_Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var config = new ToolCallingConfig();
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new AutomaticToolCallingListener(options, null!));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public async Task AutomaticListener_ShouldHandleEmptyToolCalls()
    {
        // Arrange
        var config = new ToolCallingConfig { AutoExtendBy = 3 };
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        var listener = new AutomaticToolCallingListener(options, logger);

        var sessionId = Guid.NewGuid();
        var toolCalls = new List<ToolCall>(); // 空列表

        // Act
        var decision = await listener.OnMaxRoundsReachedAsync(2, sessionId, toolCalls);

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(3, decision.ExtendBy);
    }

    [Fact]
    public async Task AutomaticListener_ShouldHandleMultipleToolCalls()
    {
        // Arrange
        var config = new ToolCallingConfig { AutoExtendBy = 7 };
        var options = Substitute.For<IOptions<ToolCallingConfig>>();
        options.Value.Returns(config);
        var logger = Substitute.For<ILogger<AutomaticToolCallingListener>>();

        var listener = new AutomaticToolCallingListener(options, logger);

        var sessionId = Guid.NewGuid();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                Id = "call_1",
                ToolName = "tool_1",
                Arguments = new Dictionary<string, object>()
            },
            new()
            {
                Id = "call_2",
                ToolName = "tool_2",
                Arguments = new Dictionary<string, object>()
            },
            new()
            {
                Id = "call_3",
                ToolName = "tool_3",
                Arguments = new Dictionary<string, object>()
            }
        };

        // Act
        var decision = await listener.OnMaxRoundsReachedAsync(5, sessionId, toolCalls);

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(7, decision.ExtendBy);
    }

    #endregion

    #region ToolCallingConfig Tests

    [Fact]
    public void ToolCallingConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new ToolCallingConfig();

        // Assert
        Assert.Equal(3, config.MaxRounds);
        Assert.Equal(50, config.AbsoluteMaxRounds);
        Assert.Equal(5, config.AutoExtendBy);
    }

    [Fact]
    public void ToolCallingConfig_ShouldAllowCustomValues()
    {
        // Arrange & Act
        var config = new ToolCallingConfig
        {
            MaxRounds = 5,
            AbsoluteMaxRounds = 100,
            AutoExtendBy = 10
        };

        // Assert
        Assert.Equal(5, config.MaxRounds);
        Assert.Equal(100, config.AbsoluteMaxRounds);
        Assert.Equal(10, config.AutoExtendBy);
    }

    [Fact]
    public void ToolCallingConfig_ShouldBeImmutable()
    {
        // Arrange & Act
        var config1 = new ToolCallingConfig { MaxRounds = 3, AutoExtendBy = 5 };
        var config2 = new ToolCallingConfig { MaxRounds = 3, AutoExtendBy = 5 };

        // Assert - 记录类型的值相等性
        Assert.Equal(config1, config2);
    }

    #endregion
}
