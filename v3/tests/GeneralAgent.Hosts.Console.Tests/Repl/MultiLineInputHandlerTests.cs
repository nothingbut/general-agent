using FluentAssertions;
using GeneralAgent.Hosts.Console.Repl;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GeneralAgent.Hosts.Console.Tests.Repl;

public class MultiLineInputHandlerTests
{
    private readonly ILogger<MultiLineInputHandler> _logger;
    private readonly MultiLineInputHandler _handler;

    public MultiLineInputHandlerTests()
    {
        _logger = Substitute.For<ILogger<MultiLineInputHandler>>();
        _handler = new MultiLineInputHandler(_logger);
    }

    [Fact]
    public void Constructor_ShouldCreateHandler()
    {
        // Assert
        _handler.Should().NotBeNull();
    }

    [Theory]
    [InlineData("\"\"\"")]
    [InlineData("  \"\"\"  ")]
    [InlineData("\t\"\"\"\t")]
    public void IsMultiLineStart_ShouldDetectStartMarker(string input)
    {
        // Act
        var result = _handler.IsMultiLineStart(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("\"\"")]
    [InlineData("\" \" \"")]
    public void IsMultiLineStart_ShouldNotDetectNonMarkers(string input)
    {
        // Act
        var result = _handler.IsMultiLineStart(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("\"\"\"")]
    [InlineData("  \"\"\"  ")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsMultiLineEnd_ShouldDetectEndMarkers(string input)
    {
        // Act
        var result = _handler.IsMultiLineEnd(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("some text")]
    [InlineData("\"\"")]
    public void IsMultiLineEnd_ShouldNotDetectNonMarkers(string input)
    {
        // Act
        var result = _handler.IsMultiLineEnd(input);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CollectMultiLineInput_ShouldCollectLines()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "line1", "line2", "line3", "" });
        var readFunc = new Func<string, string>(prompt => inputs.Dequeue());

        // Act
        var result = _handler.CollectMultiLineInput(readFunc);

        // Assert
        result.Should().Contain("line1");
        result.Should().Contain("line2");
        result.Should().Contain("line3");
    }

    [Fact]
    public void CollectMultiLineInput_ShouldEndWithTripleQuotes()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "line1", "line2", "\"\"\"" });
        var readFunc = new Func<string, string>(prompt => inputs.Dequeue());

        // Act
        var result = _handler.CollectMultiLineInput(readFunc);

        // Assert
        result.Should().Contain("line1");
        result.Should().Contain("line2");
        result.Should().NotContain("\"\"\"");
    }

    [Fact]
    public void CollectMultiLineInput_ShouldEndWithEmptyLine()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "line1", "line2", "" });
        var readFunc = new Func<string, string>(prompt => inputs.Dequeue());

        // Act
        var result = _handler.CollectMultiLineInput(readFunc);

        // Assert
        result.Should().Contain("line1");
        result.Should().Contain("line2");
    }

    [Fact]
    public void ProcessInput_ShouldReturnSingleLineForNonMultiLine()
    {
        // Arrange
        var input = "hello world";
        var readFunc = new Func<string, string>(prompt => "");

        // Act
        var result = _handler.ProcessInput(input, readFunc);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void ProcessInput_ShouldCollectMultiLineForMarker()
    {
        // Arrange
        var input = "\"\"\"";
        var inputs = new Queue<string>(new[] { "line1", "line2", "" });
        var readFunc = new Func<string, string>(prompt => inputs.Dequeue());

        // Act
        var result = _handler.ProcessInput(input, readFunc);

        // Assert
        result.Should().Contain("line1");
        result.Should().Contain("line2");
    }

    [Fact]
    public void FormatMultiLineDisplay_ShouldHandleEmptyInput()
    {
        // Act
        var result = _handler.FormatMultiLineDisplay("");

        // Assert
        result.Should().Be("[空输入]");
    }

    [Fact]
    public void FormatMultiLineDisplay_ShouldReturnFullTextForFewLines()
    {
        // Arrange
        var input = "line1\nline2\nline3";

        // Act
        var result = _handler.FormatMultiLineDisplay(input, maxLines: 5);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void FormatMultiLineDisplay_ShouldTruncateForManyLines()
    {
        // Arrange
        var input = "line1\nline2\nline3\nline4\nline5\nline6\nline7";

        // Act
        var result = _handler.FormatMultiLineDisplay(input, maxLines: 3);

        // Assert
        result.Should().Contain("line1");
        result.Should().Contain("line2");
        result.Should().Contain("line3");
        result.Should().Contain("...");
        result.Should().Contain("7 行");
    }

    [Fact]
    public void GetInputStats_ShouldReturnZeroForEmptyInput()
    {
        // Act
        var stats = _handler.GetInputStats("");

        // Assert
        stats.TotalLines.Should().Be(0);
        stats.NonEmptyLines.Should().Be(0);
        stats.TotalChars.Should().Be(0);
    }

    [Fact]
    public void GetInputStats_ShouldCountLines()
    {
        // Arrange
        var input = "line1\nline2\nline3";

        // Act
        var stats = _handler.GetInputStats(input);

        // Assert
        stats.TotalLines.Should().Be(3);
        stats.NonEmptyLines.Should().Be(3);
    }

    [Fact]
    public void GetInputStats_ShouldCountNonEmptyLines()
    {
        // Arrange
        var input = "line1\n\nline2\n\nline3";

        // Act
        var stats = _handler.GetInputStats(input);

        // Assert
        stats.TotalLines.Should().Be(5);
        stats.NonEmptyLines.Should().Be(3);
    }

    [Fact]
    public void GetInputStats_ShouldCountCharacters()
    {
        // Arrange
        var input = "hello";

        // Act
        var stats = _handler.GetInputStats(input);

        // Assert
        stats.TotalChars.Should().Be(5);
    }

    [Fact]
    public void InputStats_FormatShouldDisplayCorrectly()
    {
        // Arrange
        var stats = new InputStats(5, 3, 100);

        // Act
        var formatted = stats.Format();

        // Assert
        formatted.Should().Contain("3 行");
        formatted.Should().Contain("100 字符");
    }

    [Fact]
    public void CollectMultiLineInput_ShouldUseCorrectPrompt()
    {
        // Arrange
        var capturedPrompts = new List<string>();
        var inputs = new Queue<string>(new[] { "line1", "" });
        var readFunc = new Func<string, string>(prompt =>
        {
            capturedPrompts.Add(prompt);
            return inputs.Dequeue();
        });

        // Act
        _handler.CollectMultiLineInput(readFunc);

        // Assert
        capturedPrompts.Should().AllBe("... ");
    }

    [Fact]
    public void CollectMultiLineInput_ShouldHandleSingleLine()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "" });
        var readFunc = new Func<string, string>(prompt => inputs.Dequeue());

        // Act
        var result = _handler.CollectMultiLineInput(readFunc);

        // Assert
        result.Should().BeEmpty();
    }
}
