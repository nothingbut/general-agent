using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// SkillInfoCommand 测试
/// </summary>
public class SkillInfoCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = SkillInfoCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("info", command.Name);
        Assert.Equal("显示技能的详细信息", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveSkillNameArgument()
    {
        // Act
        var command = SkillInfoCommand.Create(ServiceProvider);

        // Assert
        var arguments = command.Arguments.ToList();
        Assert.Single(arguments);
        Assert.Equal("skill-name", arguments[0].Name);
    }

    [Fact]
    public void Create_SkillNameArgument_ShouldBeRequired()
    {
        // Act
        var command = SkillInfoCommand.Create(ServiceProvider);

        // Assert
        var argument = command.Arguments.FirstOrDefault();
        Assert.NotNull(argument);
        Assert.Equal(typeof(string), argument.ValueType);
    }

    [Fact]
    public void Create_ShouldHaveNoOptions()
    {
        // Act
        var command = SkillInfoCommand.Create(ServiceProvider);

        // Assert
        Assert.Empty(command.Options);
    }
}
