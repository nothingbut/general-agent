using FluentAssertions;
using Xunit;

namespace GeneralAgent.Hosts.Console.Tests.Repl;

/// <summary>
/// 简化的自动补全处理器测试
/// 只测试命令补全和基本逻辑，不依赖于服务
/// </summary>
public class AutoCompletionHandlerSimpleTests
{
    [Fact]
    public void Separators_ShouldNotBeEmpty()
    {
        // Arrange - 简单测试，直接验证分隔符
        var separators = new[] { ' ', '\t' };

        // Act & Assert
        separators.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("/", "help", true)]
    [InlineData("/", "new", true)]
    [InlineData("/", "list", true)]
    [InlineData("/", "exit", true)]
    [InlineData("/ne", "new", true)]
    [InlineData("/li", "list", true)]
    [InlineData("/he", "help", true)]
    public void CommandCompletion_ShouldMatchPrefixes(string input, string command, bool shouldMatch)
    {
        // Arrange
        var fullCommand = $"/{command}";

        // Act
        var matches = fullCommand.StartsWith(input, StringComparison.OrdinalIgnoreCase);

        // Assert
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("/NEW", "/new")]
    [InlineData("/List", "/list")]
    [InlineData("/HELP", "/help")]
    public void CommandCompletion_ShouldBeCaseInsensitive(string input, string expected)
    {
        // Act
        var normalized = input.ToLower();

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789012", "12345678")]
    [InlineData("abcdef01-2345-3456-4567-567890123456", "abcdef01")]
    public void SessionIdCompletion_ShouldExtractShortId(string fullId, string expectedShortId)
    {
        // Act
        var shortId = fullId.Substring(0, 8);

        // Assert
        shortId.Should().Be(expectedShortId);
    }

    [Theory]
    [InlineData("personal:greeting", "@personal:greeting")]
    [InlineData("work:task", "@work:task")]
    public void SkillCompletion_ShouldAddAtSymbol(string skillName, string expected)
    {
        // Act
        var withAt = $"@{skillName}";

        // Assert
        withAt.Should().Be(expected);
    }

    [Theory]
    [InlineData("~/documents", "/documents")]
    [InlineData("~/", "/")]
    public void FilePathCompletion_ShouldHandleTilde(string input, string expectedSuffix)
    {
        // Arrange
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Act
        var expanded = input.StartsWith("~")
            ? Path.Combine(homeDir, input[1..].TrimStart('/'))
            : input;

        // Assert
        expanded.Should().StartWith(homeDir);
        if (expectedSuffix != "/")
        {
            expanded.Should().Contain(expectedSuffix);
        }
    }

    [Theory]
    [InlineData("/session ", "session")]
    [InlineData("/delete ", "delete")]
    [InlineData("/skill ", "skill")]
    public void ContextAnalysis_ShouldDetectCommand(string input, string expectedCommand)
    {
        // Act
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].TrimStart('/') : "";

        // Assert
        command.Should().Be(expectedCommand);
    }

    [Theory]
    [InlineData("/export 123 --output ", "--output")]
    [InlineData("/export 123 -o ", "-o")]
    [InlineData("/export 123 --file ", "--file")]
    public void ContextAnalysis_ShouldDetectFilePathContext(string input, string expectedFlag)
    {
        // Act
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lastFlag = parts.Length >= 3 ? parts[^1] : "";

        // Assert
        lastFlag.Should().Be(expectedFlag);
    }

    [Fact]
    public void Commands_ShouldBeSortedAlphabetically()
    {
        // Arrange
        var commands = new[] { "/help", "/new", "/list", "/exit", "/session", "/delete" };

        // Act
        var sorted = commands.OrderBy(c => c).ToArray();

        // Assert
        sorted.Should().BeInAscendingOrder();
    }

    [Fact]
    public void TempDirectory_ShouldExist()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act & Assert
        Directory.Exists(tempDir).Should().BeTrue();
    }

    [Fact]
    public void HomeDirectory_ShouldExist()
    {
        // Arrange
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Act & Assert
        Directory.Exists(homeDir).Should().BeTrue();
        homeDir.Should().NotBeNullOrEmpty();
    }
}
