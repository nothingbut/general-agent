using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// NewCommand 测试
/// </summary>
public class NewCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("new", command.Name);
        Assert.Equal("创建新会话", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveTitleOption()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--title") || o.HasAlias("-t"));
    }

    [Fact]
    public void Create_ShouldHaveFormatOption()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--format") || o.HasAlias("-f"));
    }

    [Fact]
    public void Create_FormatOption_ShouldDefaultToText()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        var formatOption = command.Options.FirstOrDefault(o => o.HasAlias("--format"));
        Assert.NotNull(formatOption);

        // 验证默认值是 "text"
        var optionType = formatOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(formatOption, null);
            Assert.Equal("text", defaultValue);
        }
    }

    [Fact]
    public void Create_TitleOption_ShouldBeOptional()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        var titleOption = command.Options.FirstOrDefault(o => o.HasAlias("--title"));
        Assert.NotNull(titleOption);
        // 选项类型应该是 nullable string
        Assert.Equal(typeof(string), titleOption.ValueType);
    }

    [Fact]
    public void Create_ShouldHaveTwoOptions()
    {
        // Act
        var command = NewCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(2, command.Options.Count);
    }
}
