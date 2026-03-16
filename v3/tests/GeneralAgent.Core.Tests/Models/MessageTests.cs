using FluentAssertions;
using GeneralAgent.Core.Models;
using System.Text.Json;

namespace GeneralAgent.Core.Tests.Models;

public class MessageTests
{
    [Fact]
    public void CreateUser_ShouldGenerateUniqueId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Hello");

        // Assert
        message.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateUser_ShouldSetUserRole()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Hello");

        // Assert
        message.Role.Should().Be(MessageRole.User);
    }

    [Fact]
    public void CreateUser_ShouldSetContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Test content");

        // Assert
        message.Content.Should().Be("Test content");
        message.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void CreateUser_ShouldSetTimestamp()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var message = Message.CreateUser(sessionId, "Hello");
        var after = DateTime.UtcNow;

        // Assert
        message.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreateAssistant_ShouldSetAssistantRole()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateAssistant(sessionId, "Response");

        // Assert
        message.Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public void CreateAssistant_WithMetadata_ShouldSetMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, JsonElement>
        {
            ["model"] = JsonSerializer.SerializeToElement("claude-3"),
            ["tokens"] = JsonSerializer.SerializeToElement(100)
        };

        // Act
        var message = Message.CreateAssistant(sessionId, "Response", metadata);

        // Assert
        message.Metadata.Should().NotBeNull();
        message.Metadata.Should().ContainKey("model");
        message.Metadata!["model"].GetString().Should().Be("claude-3");
    }

    [Fact]
    public void CreateAssistant_WithoutMetadata_ShouldHaveNullMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateAssistant(sessionId, "Response");

        // Assert
        message.Metadata.Should().BeNull();
    }

    [Fact]
    public void Message_ShouldBeImmutable()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = Message.CreateUser(sessionId, "Test");

        // Act & Assert
        // 以下代码不应编译（验证不可变性）
        // message.Content = "Modified";  // 应该报错
        // message.Role = MessageRole.Assistant;  // 应该报错
    }
}
