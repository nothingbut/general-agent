using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class CompletionRequestTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var request = new CompletionRequest
        {
            Model = "llama3.2",
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            }
        };

        // Assert
        request.Model.Should().Be("llama3.2");
        request.Messages.Should().HaveCount(1);
        request.Temperature.Should().Be(0.7); // 默认值
        request.MaxTokens.Should().BeNull();
        request.SystemPrompt.Should().BeNull();
    }

    [Fact]
    public void Create_WithSystemPrompt_ShouldIncludeIt()
    {
        // Arrange & Act
        var request = new CompletionRequest
        {
            Model = "llama3.2",
            Messages = new List<ChatMessage>(),
            SystemPrompt = "You are a helpful assistant"
        };

        // Assert
        request.SystemPrompt.Should().Be("You are a helpful assistant");
    }

    [Fact]
    public void Create_WithCustomTemperature_ShouldOverrideDefault()
    {
        // Arrange & Act
        var request = new CompletionRequest
        {
            Model = "llama3.2",
            Messages = new List<ChatMessage>(),
            Temperature = 1.5
        };

        // Assert
        request.Temperature.Should().Be(1.5);
    }

    [Fact]
    public void Create_WithMaxTokens_ShouldSetIt()
    {
        // Arrange & Act
        var request = new CompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<ChatMessage>(),
            MaxTokens = 500
        };

        // Assert
        request.MaxTokens.Should().Be(500);
    }

    [Fact]
    public void CompletionRequest_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(CompletionRequest);
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMultipleMessages_ShouldPreserveOrder()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "user", Content = "First" },
            new ChatMessage { Role = "assistant", Content = "Response" },
            new ChatMessage { Role = "user", Content = "Second" }
        };

        // Act
        var request = new CompletionRequest
        {
            Model = "mistral",
            Messages = messages
        };

        // Assert
        request.Messages.Should().HaveCount(3);
        request.Messages.ElementAt(0).Content.Should().Be("First");
        request.Messages.ElementAt(1).Content.Should().Be("Response");
        request.Messages.ElementAt(2).Content.Should().Be("Second");
    }

    [Fact]
    public void Create_WithAllProperties_ShouldSetAllValues()
    {
        // Arrange & Act
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "user", Content = "Test" }
        };

        var request = new CompletionRequest
        {
            Model = "claude-3",
            Messages = messages,
            SystemPrompt = "You are a code reviewer",
            Temperature = 0.2,
            MaxTokens = 2000
        };

        // Assert
        request.Model.Should().Be("claude-3");
        request.Messages.Should().BeEquivalentTo(messages);
        request.SystemPrompt.Should().Be("You are a code reviewer");
        request.Temperature.Should().Be(0.2);
        request.MaxTokens.Should().Be(2000);
    }
}
