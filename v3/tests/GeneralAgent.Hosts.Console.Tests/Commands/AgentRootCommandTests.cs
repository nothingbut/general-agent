using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// AgentRootCommand 测试
/// </summary>
public class AgentRootCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnRootCommand()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("agent", command.Name);
        Assert.Contains("General Agent V3", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveVerboseGlobalOption()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var globalOptions = command.Options.Where(o => o is not null).ToList();
        Assert.Contains(globalOptions, o => o.HasAlias("--verbose") || o.HasAlias("-v"));
    }

    [Fact]
    public void Create_ShouldHaveNewCommand()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var subcommands = command.Subcommands.ToList();
        Assert.Contains(subcommands, c => c.Name == "new");
    }

    [Fact]
    public void Create_ShouldHaveListCommand()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var subcommands = command.Subcommands.ToList();
        Assert.Contains(subcommands, c => c.Name == "list");
    }

    [Fact]
    public void Create_ShouldHaveChatCommand()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var subcommands = command.Subcommands.ToList();
        Assert.Contains(subcommands, c => c.Name == "chat");
    }

    [Fact]
    public void Create_ShouldHaveThreeSubcommands()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var subcommands = command.Subcommands.ToList();
        Assert.Equal(3, subcommands.Count);
    }

    [Fact]
    public void Create_SubcommandNames_ShouldBeCorrect()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        var subcommandNames = command.Subcommands.Select(c => c.Name).ToList();
        Assert.Contains("new", subcommandNames);
        Assert.Contains("list", subcommandNames);
        Assert.Contains("chat", subcommandNames);
    }

    [Fact]
    public void Create_Description_ShouldContainAIAssistant()
    {
        // Act
        var command = AgentRootCommand.Create(ServiceProvider);

        // Assert
        Assert.Contains("AI 对话助手", command.Description);
    }
}
