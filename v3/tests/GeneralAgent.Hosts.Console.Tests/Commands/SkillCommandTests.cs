using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// SkillCommand 测试
/// </summary>
public class SkillCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = SkillCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("skill", command.Name);
        Assert.Equal("技能管理命令", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveListSubCommand()
    {
        // Act
        var command = SkillCommand.Create(ServiceProvider);

        // Assert
        var subCommands = command.Subcommands.ToList();
        Assert.Contains(subCommands, c => c.Name == "list");
    }

    [Fact]
    public void Create_ShouldHaveInfoSubCommand()
    {
        // Act
        var command = SkillCommand.Create(ServiceProvider);

        // Assert
        var subCommands = command.Subcommands.ToList();
        Assert.Contains(subCommands, c => c.Name == "info");
    }

    [Fact]
    public void Create_ShouldHaveRunSubCommand()
    {
        // Act
        var command = SkillCommand.Create(ServiceProvider);

        // Assert
        var subCommands = command.Subcommands.ToList();
        Assert.Contains(subCommands, c => c.Name == "run");
    }

    [Fact]
    public void Create_ShouldHaveThreeSubCommands()
    {
        // Act
        var command = SkillCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(3, command.Subcommands.Count);
    }
}
