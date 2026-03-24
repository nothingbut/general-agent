using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// ChatCommand 测试
/// </summary>
public class ChatCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("chat", command.Name);
        Assert.Equal("在指定会话中发送消息", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveSessionIdArgument()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var arguments = command.Arguments.ToList();
        Assert.NotEmpty(arguments);
        Assert.Contains(arguments, a => a.Name == "session-id");
    }

    [Fact]
    public void Create_ShouldHaveMessageArgument()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var arguments = command.Arguments.ToList();
        Assert.Contains(arguments, a => a.Name == "message");
    }

    [Fact]
    public void Create_ShouldHaveProviderOption()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--provider") || o.HasAlias("-p"));
    }

    [Fact]
    public void Create_ShouldHaveStreamOption()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--stream") || o.HasAlias("-s"));
    }

    [Fact]
    public void Create_StreamOption_ShouldDefaultToTrue()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var streamOption = command.Options.FirstOrDefault(o => o.HasAlias("--stream"));
        Assert.NotNull(streamOption);

        // 验证默认值是 true
        var optionType = streamOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(streamOption, null);
            Assert.True((bool)defaultValue!);
        }
    }

    [Fact]
    public void Create_ShouldHaveTwoArguments()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        var arguments = command.Arguments.ToList();
        Assert.Equal(2, arguments.Count);
    }

    [Fact]
    public void Create_ShouldHaveTwoOptions()
    {
        // Act
        var command = ChatCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(2, command.Options.Count);
    }
}
