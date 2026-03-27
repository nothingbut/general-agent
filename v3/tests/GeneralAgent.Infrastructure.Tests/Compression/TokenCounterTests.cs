using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Compression;

public class TokenCounterTests
{
    private readonly ITokenCounter _tokenCounter;

    public TokenCounterTests()
    {
        var logger = new NullLogger<TokenCounter>();
        _tokenCounter = new TokenCounter(logger);
    }

    [Fact]
    public void CountTokens_EmptyString_ShouldReturnZero()
    {
        // Act
        var count = _tokenCounter.CountTokens("");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CountTokens_NullString_ShouldReturnZero()
    {
        // Act
        var count = _tokenCounter.CountTokens(null!);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CountTokens_SimpleEnglishText_ShouldReturnCorrectCount()
    {
        // Arrange
        var text = "Hello, world!";

        // Act
        var count = _tokenCounter.CountTokens(text);

        // Assert
        count.Should().BeGreaterThan(0);
        count.Should().BeLessThan(10); // "Hello, world!" 应该在 3-4 tokens
    }

    [Fact]
    public void CountTokens_ChineseText_ShouldReturnCorrectCount()
    {
        // Arrange
        var text = "你好，世界！";

        // Act
        var count = _tokenCounter.CountTokens(text);

        // Assert
        count.Should().BeGreaterThan(0);
        count.Should().BeLessThan(20);
    }

    [Fact]
    public void CountTokens_MixedLanguageText_ShouldReturnCorrectCount()
    {
        // Arrange
        var text = "Hello 你好 World 世界";

        // Act
        var count = _tokenCounter.CountTokens(text);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountTokens_LongText_ShouldHandleCorrectly()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Repeat("word", 100));

        // Act
        var count = _tokenCounter.CountTokens(text);

        // Assert
        count.Should().BeGreaterThanOrEqualTo(100); // 至少 100 个词
        count.Should().BeLessThan(200); // 应该在 100-150 tokens 之间
    }

    [Fact]
    public void CountTokens_CodeSnippet_ShouldReturnCorrectCount()
    {
        // Arrange
        var code = @"
public class Example
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var count = _tokenCounter.CountTokens(code);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountTokens_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var text = "!@#$%^&*()_+-=[]{}|;':\",./<>?";

        // Act
        var count = _tokenCounter.CountTokens(text);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CountMessagesTokens_MultipleMessages_ShouldReturnTotalCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "Hello"),
            Message.CreateAssistant(sessionId, "Hi there"),
            Message.CreateUser(sessionId, "How are you?")
        };

        // Act
        var count = _tokenCounter.CountMessagesTokens(messages);

        // Assert
        count.Should().BeGreaterThan(0);
    }
}
