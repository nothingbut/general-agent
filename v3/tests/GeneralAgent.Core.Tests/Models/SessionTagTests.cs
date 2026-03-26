using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class SessionTagTests
{
    [Fact]
    public void Create_ValidTag_ReturnsTagWithLowercaseName()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tag = "Python";

        // Act
        var result = SessionTag.Create(sessionId, tag);

        // Assert
        result.Tag.Should().Be("python");
        result.SessionId.Should().Be(sessionId);
        result.Source.Should().Be(TagSource.User);
    }

    [Fact]
    public void Create_WithColorAndEmoji_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var result = SessionTag.Create(
            sessionId,
            "bug",
            TagSource.Auto,
            "#EF4444",
            "🐛"
        );

        // Assert
        result.Tag.Should().Be("bug");
        result.Color.Should().Be("#EF4444");
        result.Emoji.Should().Be("🐛");
        result.Source.Should().Be(TagSource.Auto);
    }

    [Fact]
    public void Create_TagWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var result = SessionTag.Create(sessionId, "  work  ");

        // Assert
        result.Tag.Should().Be("work");
    }
}
