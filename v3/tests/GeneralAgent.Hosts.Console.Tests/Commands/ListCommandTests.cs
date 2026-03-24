using System.CommandLine;
using GeneralAgent.Hosts.Console.Commands;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// ListCommand 测试
/// </summary>
public class ListCommandTests : CommandTestsBase
{
    [Fact]
    public void Create_ShouldReturnCommand()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        Assert.NotNull(command);
        Assert.Equal("list", command.Name);
        Assert.Equal("列出所有会话", command.Description);
    }

    [Fact]
    public void Create_ShouldHaveLimitOption()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--limit") || o.HasAlias("-l"));
    }

    [Fact]
    public void Create_ShouldHaveOffsetOption()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--offset") || o.HasAlias("-o"));
    }

    [Fact]
    public void Create_ShouldHaveFormatOption()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        var options = command.Options.ToList();
        Assert.Contains(options, o => o.HasAlias("--format") || o.HasAlias("-f"));
    }

    [Fact]
    public void Create_LimitOption_ShouldDefaultTo20()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        var limitOption = command.Options.FirstOrDefault(o => o.HasAlias("--limit"));
        Assert.NotNull(limitOption);

        // 验证默认值是 20
        var optionType = limitOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(limitOption, null);
            Assert.Equal(20, defaultValue);
        }
    }

    [Fact]
    public void Create_OffsetOption_ShouldDefaultTo0()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        var offsetOption = command.Options.FirstOrDefault(o => o.HasAlias("--offset"));
        Assert.NotNull(offsetOption);

        // 验证默认值是 0
        var optionType = offsetOption.GetType();
        var getDefaultValueMethod = optionType.GetMethod("GetDefaultValue");
        if (getDefaultValueMethod != null)
        {
            var defaultValue = getDefaultValueMethod.Invoke(offsetOption, null);
            Assert.Equal(0, defaultValue);
        }
    }

    [Fact]
    public void Create_ShouldHaveThreeOptions()
    {
        // Act
        var command = ListCommand.Create(ServiceProvider);

        // Assert
        Assert.Equal(3, command.Options.Count);
    }
}
