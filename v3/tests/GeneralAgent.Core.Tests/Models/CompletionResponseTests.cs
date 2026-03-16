using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class CompletionResponseTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 10,
            CompletionTokens = 20
        };

        var timestamp = DateTime.UtcNow;

        var response = new CompletionResponse
        {
            Content = "Hello, how can I help?",
            Usage = usage,
            Timestamp = timestamp
        };

        // Assert
        response.Content.Should().Be("Hello, how can I help?");
        response.Usage.TotalTokens.Should().Be(30); // 自动计算
        response.Timestamp.Should().Be(timestamp);
        response.Model.Should().BeNull();
    }

    [Fact]
    public void Create_WithModel_ShouldIncludeIt()
    {
        // Arrange & Act
        var response = new CompletionResponse
        {
            Content = "Test",
            Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 10 },
            Model = "llama3.2",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        response.Model.Should().Be("llama3.2");
    }

    [Fact]
    public void Timestamp_MustBeExplicitlySet()
    {
        // Arrange
        var timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var response = new CompletionResponse
        {
            Content = "Test",
            Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 10 },
            Timestamp = timestamp
        };

        // Assert
        response.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void CompletionResponse_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(CompletionResponse);
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Create_WithDifferentTimestamps_ShouldPreserveAccuracy()
    {
        // Arrange
        var timestamp1 = new DateTime(2025, 1, 1, 10, 30, 45, 123, DateTimeKind.Utc);
        var timestamp2 = new DateTime(2025, 1, 1, 14, 15, 30, 456, DateTimeKind.Utc);

        // Act
        var response1 = new CompletionResponse
        {
            Content = "Response 1",
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
            Timestamp = timestamp1
        };

        var response2 = new CompletionResponse
        {
            Content = "Response 2",
            Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 25 },
            Timestamp = timestamp2
        };

        // Assert
        response1.Timestamp.Should().Be(timestamp1);
        response2.Timestamp.Should().Be(timestamp2);
        response1.Timestamp.Should().BeBefore(response2.Timestamp);
    }
}

public class TokenUsageTests
{
    [Fact]
    public void Create_WithValues_ShouldSucceed()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 100,
            CompletionTokens = 200
        };

        // Assert
        usage.PromptTokens.Should().Be(100);
        usage.CompletionTokens.Should().Be(200);
        usage.TotalTokens.Should().Be(300); // 自动计算
    }

    [Fact]
    public void TotalTokens_IsComputed()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 50,
            CompletionTokens = 75
        };

        // Assert
        usage.TotalTokens.Should().Be(125);
    }

    [Fact]
    public void TotalTokens_WithZeroValues_ShouldBeZero()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 0,
            CompletionTokens = 0
        };

        // Assert
        usage.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void TotalTokens_WithLargeValues_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 100000,
            CompletionTokens = 50000
        };

        // Assert
        usage.TotalTokens.Should().Be(150000);
    }

    [Fact]
    public void TokenUsage_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(TokenUsage);
        type.IsSealed.Should().BeTrue();
    }
}

public class StreamChunkTests
{
    [Fact]
    public void Create_IntermediateChunk_ShouldNotBeComplete()
    {
        // Arrange & Act
        var chunk = new StreamChunk
        {
            Delta = "Hello",
            IsComplete = false
        };

        // Assert
        chunk.Delta.Should().Be("Hello");
        chunk.IsComplete.Should().BeFalse();
        chunk.Usage.Should().BeNull();
    }

    [Fact]
    public void Create_FinalChunk_ShouldBeComplete()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            PromptTokens = 10,
            CompletionTokens = 20
        };

        var chunk = new StreamChunk
        {
            Delta = "",
            IsComplete = true,
            Usage = usage
        };

        // Assert
        chunk.Delta.Should().BeEmpty();
        chunk.IsComplete.Should().BeTrue();
        chunk.Usage.Should().NotBeNull();
        chunk.Usage!.TotalTokens.Should().Be(30); // 自动计算
    }

    [Fact]
    public void Create_MultipleIntermediateChunks_ShouldAccumulate()
    {
        // Arrange & Act
        var chunk1 = new StreamChunk { Delta = "Hello", IsComplete = false };
        var chunk2 = new StreamChunk { Delta = " ", IsComplete = false };
        var chunk3 = new StreamChunk { Delta = "world", IsComplete = false };
        var chunk4 = new StreamChunk
        {
            Delta = "",
            IsComplete = true,
            Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 15 }
        };

        // Assert
        chunk1.IsComplete.Should().BeFalse();
        chunk2.IsComplete.Should().BeFalse();
        chunk3.IsComplete.Should().BeFalse();
        chunk4.IsComplete.Should().BeTrue();
        chunk4.Usage?.TotalTokens.Should().Be(20);
    }

    [Fact]
    public void StreamChunk_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(StreamChunk);
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Create_ChunkWithEmptyDelta_ShouldBeAllowed()
    {
        // Arrange & Act
        var chunk = new StreamChunk
        {
            Delta = "",
            IsComplete = false
        };

        // Assert
        chunk.Delta.Should().BeEmpty();
        chunk.IsComplete.Should().BeFalse();
    }
}

public class ChatMessageTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "user",
            Content = "Hello, how are you?"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hello, how are you?");
    }

    [Fact]
    public void Create_WithAssistantRole_ShouldSucceed()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "assistant",
            Content = "I'm doing well, thank you!"
        };

        // Assert
        message.Role.Should().Be("assistant");
        message.Content.Should().Be("I'm doing well, thank you!");
    }

    [Fact]
    public void Create_WithSystemRole_ShouldSucceed()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "system",
            Content = "You are a helpful AI assistant."
        };

        // Assert
        message.Role.Should().Be("system");
        message.Content.Should().Be("You are a helpful AI assistant.");
    }

    [Fact]
    public void ChatMessage_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(ChatMessage);
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Create_WithLongContent_ShouldPreserve()
    {
        // Arrange
        var longContent = new string('a', 10000);

        // Act
        var message = new ChatMessage
        {
            Role = "user",
            Content = longContent
        };

        // Assert
        message.Content.Should().HaveLength(10000);
        message.Content.Should().Be(longContent);
    }
}
