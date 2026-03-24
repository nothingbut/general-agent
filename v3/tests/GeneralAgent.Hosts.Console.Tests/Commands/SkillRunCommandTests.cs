using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// SkillRunCommand 测试
/// </summary>
public class SkillRunCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("run", command.Name);
        Assert.Equal("执行指定的技能", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveSkillNameArgument()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var arguments = command.Arguments.ToList();
        Assert.True(arguments.Count >= 1, "应该至少有一个参数（skill-name）");
        Assert.Equal("skill-name", arguments[0].Name);
    }

    [Fact]
    public void Create_ShouldHaveSessionOption()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--session") || o.HasAlias("-s"));
    }

    [Fact]
    public void Create_ShouldHaveProviderOption()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--provider") || o.HasAlias("-p"));
    }

    [Fact]
    public void Create_ShouldHaveStreamOption()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--stream"));
    }

    [Fact]
    public void Create_StreamOption_ShouldDefaultToTrue()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var streamOption = command.Options.FirstOrDefault(o => o.HasAlias("--stream"));
        Assert.NotNull(streamOption);

        // 验证默认值是 true
        var optionType = streamOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(streamOption, null);
            Assert.Equal(true, defaultValue);
        }
    }

    [Fact]
    public void Create_SessionOption_ShouldBeOptional()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var sessionOption = command.Options.FirstOrDefault(o => o.HasAlias("--session"));
        Assert.NotNull(sessionOption);
        // 选项类型应该是 nullable string
        Assert.Equal(typeof(string), sessionOption.ValueType);
    }

    [Fact]
    public void Create_ProviderOption_ShouldBeOptional()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        var providerOption = command.Options.FirstOrDefault(o => o.HasAlias("--provider"));
        Assert.NotNull(providerOption);
        // 选项类型应该是 nullable string
        Assert.Equal(typeof(string), providerOption.ValueType);
    }

    [Fact]
    public void Create_ShouldHaveThreeOptions()
    {
        // Act
        var command = SkillRunCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(3, command.Options.Count);
    }
}
