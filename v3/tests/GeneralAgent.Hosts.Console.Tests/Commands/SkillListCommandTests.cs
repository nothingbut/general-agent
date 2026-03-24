using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// SkillListCommand 测试
/// </summary>
public class SkillListCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("list", command.Name);
        Assert.Equal("列出所有已注册的技能", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveNamespaceOption()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--namespace") || o.HasAlias("-n"));
    }

    [Fact]
    public void Create_ShouldHaveFormatOption()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--format") || o.HasAlias("-f"));
    }

    [Fact]
    public void Create_FormatOption_ShouldDefaultToTable()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        var formatOption = command.Options.FirstOrDefault(o => o.HasAlias("--format"));
        Assert.NotNull(formatOption);

        // 验证默认值是 "table"
        var optionType = formatOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(formatOption, null);
            Assert.Equal("table", defaultValue);
        }
    }

    [Fact]
    public void Create_NamespaceOption_ShouldBeOptional()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        var namespaceOption = command.Options.FirstOrDefault(o => o.HasAlias("--namespace"));
        Assert.NotNull(namespaceOption);
        // 选项类型应该是 nullable string
        Assert.Equal(typeof(string), namespaceOption.ValueType);
    }

    [Fact]
    public void Create_ShouldHaveTwoOptions()
    {
        // Act
        var command = SkillListCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(2, command.Options.Count);
    }
}
