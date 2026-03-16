using System.Text.Json;
using FluentAssertions;
using GeneralAgent.Infrastructure.LLM.DTOs;

namespace GeneralAgent.Infrastructure.LLM.Tests.DTOs;

public class OpenAIChatMessageTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var message = new OpenAIChatMessage
        {
            Role = "user",
            Content = "Hello, how are you?"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hello, how are you?");
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var message = new OpenAIChatMessage
        {
            Role = "assistant",
            Content = "I'm doing well!"
        };

        // Act
        var json = JsonSerializer.Serialize(message);

        // Assert
        json.Should().Contain("\"role\":");
        json.Should().Contain("\"content\":");
        json.Should().NotContain("\"Role\":");
        json.Should().NotContain("\"Content\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{ ""role"": ""system"", ""content"": ""You are helpful."" }";

        // Act
        var message = JsonSerializer.Deserialize<OpenAIChatMessage>(json);

        // Assert
        message.Should().NotBeNull();
        message!.Role.Should().Be("system");
        message.Content.Should().Be("You are helpful.");
    }

    [Fact]
    public void OpenAIChatMessage_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(OpenAIChatMessage);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIChatRequestTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var request = new OpenAIChatRequest
        {
            Model = "llama3.2",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Assert
        request.Model.Should().Be("llama3.2");
        request.Messages.Should().HaveCount(1);
        request.Stream.Should().BeFalse();
    }

    [Fact]
    public void Create_WithOptionalProperties_ShouldSetThem()
    {
        // Arrange & Act
        var request = new OpenAIChatRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<OpenAIChatMessage>(),
            Temperature = 0.8,
            MaxTokens = 100,
            TopP = 0.9,
            N = 1,
            Stop = new List<string> { "[END]" }
        };

        // Assert
        request.Temperature.Should().Be(0.8);
        request.MaxTokens.Should().Be(100);
        request.TopP.Should().Be(0.9);
        request.N.Should().Be(1);
        request.Stop.Should().ContainSingle().Which.Should().Be("[END]");
    }

    [Fact]
    public void Create_WithStreamTrue_ShouldSetStream()
    {
        // Arrange & Act
        var request = new OpenAIChatRequest
        {
            Model = "llama",
            Messages = new List<OpenAIChatMessage>(),
            Stream = true
        };

        // Assert
        request.Stream.Should().BeTrue();
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var request = new OpenAIChatRequest
        {
            Model = "test",
            Messages = new List<OpenAIChatMessage>(),
            MaxTokens = 50,
            TopP = 0.95
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"model\":");
        json.Should().Contain("\"messages\":");
        json.Should().Contain("\"max_tokens\":");
        json.Should().Contain("\"top_p\":");
        json.Should().NotContain("\"MaxTokens\":");
        json.Should().NotContain("\"TopP\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{
            ""model"": ""llama3.2"",
            ""messages"": [{""role"": ""user"", ""content"": ""Hi""}],
            ""temperature"": 0.7,
            ""max_tokens"": 100,
            ""stream"": false,
            ""top_p"": 0.9,
            ""n"": 1,
            ""stop"": [""[END]""]
        }";

        // Act
        var request = JsonSerializer.Deserialize<OpenAIChatRequest>(json);

        // Assert
        request.Should().NotBeNull();
        request!.Model.Should().Be("llama3.2");
        request.Temperature.Should().Be(0.7);
        request.MaxTokens.Should().Be(100);
        request.Stream.Should().BeFalse();
        request.TopP.Should().Be(0.9);
        request.N.Should().Be(1);
        request.Stop.Should().ContainSingle().Which.Should().Be("[END]");
    }

    [Fact]
    public void OpenAIChatRequest_IsImmutable()
    {
        // Record 类型应该是不可变的
        var type = typeof(OpenAIChatRequest);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIUsageTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var usage = new OpenAIUsage
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            TotalTokens = 30
        };

        // Assert
        usage.PromptTokens.Should().Be(10);
        usage.CompletionTokens.Should().Be(20);
        usage.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var usage = new OpenAIUsage
        {
            PromptTokens = 5,
            CompletionTokens = 10,
            TotalTokens = 15
        };

        // Act
        var json = JsonSerializer.Serialize(usage);

        // Assert
        json.Should().Contain("\"prompt_tokens\":");
        json.Should().Contain("\"completion_tokens\":");
        json.Should().Contain("\"total_tokens\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{ ""prompt_tokens"": 100, ""completion_tokens"": 50, ""total_tokens"": 150 }";

        // Act
        var usage = JsonSerializer.Deserialize<OpenAIUsage>(json);

        // Assert
        usage.Should().NotBeNull();
        usage!.PromptTokens.Should().Be(100);
        usage.CompletionTokens.Should().Be(50);
        usage.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void OpenAIUsage_IsImmutable()
    {
        var type = typeof(OpenAIUsage);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIChoiceTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var choice = new OpenAIChoice
        {
            Index = 0,
            Message = new OpenAIChatMessage
            {
                Role = "assistant",
                Content = "Hello!"
            }
        };

        // Assert
        choice.Index.Should().Be(0);
        choice.Message.Role.Should().Be("assistant");
        choice.Message.Content.Should().Be("Hello!");
        choice.FinishReason.Should().BeNull();
    }

    [Fact]
    public void Create_WithFinishReason_ShouldSetIt()
    {
        // Arrange & Act
        var choice = new OpenAIChoice
        {
            Index = 0,
            Message = new OpenAIChatMessage { Role = "assistant", Content = "Done" },
            FinishReason = "stop"
        };

        // Assert
        choice.FinishReason.Should().Be("stop");
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var choice = new OpenAIChoice
        {
            Index = 0,
            Message = new OpenAIChatMessage { Role = "user", Content = "test" },
            FinishReason = "stop"
        };

        // Act
        var json = JsonSerializer.Serialize(choice);

        // Assert
        json.Should().Contain("\"index\":");
        json.Should().Contain("\"message\":");
        json.Should().Contain("\"finish_reason\":");
    }

    [Fact]
    public void OpenAIChoice_IsImmutable()
    {
        var type = typeof(OpenAIChoice);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIChatResponseTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var response = new OpenAIChatResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = 1234567890,
            Model = "llama3.2",
            Choices = new List<OpenAIChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new OpenAIChatMessage { Role = "assistant", Content = "Response" }
                }
            },
            Usage = new OpenAIUsage
            {
                PromptTokens = 10,
                CompletionTokens = 20,
                TotalTokens = 30
            }
        };

        // Assert
        response.Id.Should().Be("chatcmpl-123");
        response.Object.Should().Be("chat.completion");
        response.Created.Should().Be(1234567890);
        response.Model.Should().Be("llama3.2");
        response.Choices.Should().HaveCount(1);
        response.Usage.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var response = new OpenAIChatResponse
        {
            Id = "id",
            Object = "chat.completion",
            Created = 123,
            Model = "model",
            Choices = new List<OpenAIChoice>(),
            Usage = new OpenAIUsage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2 }
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"object\":");
        json.Should().Contain("\"created\":");
        json.Should().Contain("\"model\":");
        json.Should().Contain("\"choices\":");
        json.Should().Contain("\"usage\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{
            ""id"": ""chatcmpl-456"",
            ""object"": ""chat.completion"",
            ""created"": 9876543210,
            ""model"": ""gpt-3.5-turbo"",
            ""choices"": [{
                ""index"": 0,
                ""message"": {""role"": ""assistant"", ""content"": ""Test""},
                ""finish_reason"": ""stop""
            }],
            ""usage"": {
                ""prompt_tokens"": 50,
                ""completion_tokens"": 100,
                ""total_tokens"": 150
            }
        }";

        // Act
        var response = JsonSerializer.Deserialize<OpenAIChatResponse>(json);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().Be("chatcmpl-456");
        response.Object.Should().Be("chat.completion");
        response.Created.Should().Be(9876543210);
        response.Model.Should().Be("gpt-3.5-turbo");
        response.Choices.Should().HaveCount(1);
        response.Usage.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void OpenAIChatResponse_IsImmutable()
    {
        var type = typeof(OpenAIChatResponse);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIDeltaTests
{
    [Fact]
    public void Create_WithContentOnly_ShouldSucceed()
    {
        // Arrange & Act
        var delta = new OpenAIDelta
        {
            Content = "Hello"
        };

        // Assert
        delta.Content.Should().Be("Hello");
        delta.Role.Should().BeNull();
    }

    [Fact]
    public void Create_WithRoleOnly_ShouldSucceed()
    {
        // Arrange & Act
        var delta = new OpenAIDelta
        {
            Role = "assistant"
        };

        // Assert
        delta.Role.Should().Be("assistant");
        delta.Content.Should().BeNull();
    }

    [Fact]
    public void Create_WithBothRoleAndContent_ShouldSetBoth()
    {
        // Arrange & Act
        var delta = new OpenAIDelta
        {
            Role = "assistant",
            Content = "Hello"
        };

        // Assert
        delta.Role.Should().Be("assistant");
        delta.Content.Should().Be("Hello");
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var delta = new OpenAIDelta { Role = "assistant", Content = "test" };

        // Act
        var json = JsonSerializer.Serialize(delta);

        // Assert
        json.Should().Contain("\"role\":");
        json.Should().Contain("\"content\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{ ""role"": ""assistant"", ""content"": ""Hello world"" }";

        // Act
        var delta = JsonSerializer.Deserialize<OpenAIDelta>(json);

        // Assert
        delta.Should().NotBeNull();
        delta!.Role.Should().Be("assistant");
        delta.Content.Should().Be("Hello world");
    }

    [Fact]
    public void OpenAIDelta_IsImmutable()
    {
        var type = typeof(OpenAIDelta);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIStreamChoiceTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var choice = new OpenAIStreamChoice
        {
            Index = 0,
            Delta = new OpenAIDelta { Content = "Hello" }
        };

        // Assert
        choice.Index.Should().Be(0);
        choice.Delta.Content.Should().Be("Hello");
        choice.FinishReason.Should().BeNull();
    }

    [Fact]
    public void Create_WithFinishReason_ShouldSetIt()
    {
        // Arrange & Act
        var choice = new OpenAIStreamChoice
        {
            Index = 0,
            Delta = new OpenAIDelta(),
            FinishReason = "stop"
        };

        // Assert
        choice.FinishReason.Should().Be("stop");
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var choice = new OpenAIStreamChoice
        {
            Index = 0,
            Delta = new OpenAIDelta { Content = "test" },
            FinishReason = "length"
        };

        // Act
        var json = JsonSerializer.Serialize(choice);

        // Assert
        json.Should().Contain("\"index\":");
        json.Should().Contain("\"delta\":");
        json.Should().Contain("\"finish_reason\":");
    }

    [Fact]
    public void OpenAIStreamChoice_IsImmutable()
    {
        var type = typeof(OpenAIStreamChoice);
        type.IsSealed.Should().BeTrue();
    }
}

public class OpenAIStreamChunkTests
{
    [Fact]
    public void Create_WithRequiredProperties_ShouldSucceed()
    {
        // Arrange & Act
        var chunk = new OpenAIStreamChunk
        {
            Id = "chatcmpl-789",
            Object = "chat.completion.chunk",
            Created = 1234567890,
            Model = "llama3.2",
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new OpenAIDelta { Content = "Hello" }
                }
            }
        };

        // Assert
        chunk.Id.Should().Be("chatcmpl-789");
        chunk.Object.Should().Be("chat.completion.chunk");
        chunk.Created.Should().Be(1234567890);
        chunk.Model.Should().Be("llama3.2");
        chunk.Choices.Should().HaveCount(1);
    }

    [Fact]
    public void Serialize_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var chunk = new OpenAIStreamChunk
        {
            Id = "id",
            Object = "chat.completion.chunk",
            Created = 123,
            Model = "model",
            Choices = new List<OpenAIStreamChoice>()
        };

        // Act
        var json = JsonSerializer.Serialize(chunk);

        // Assert
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"object\":");
        json.Should().Contain("\"created\":");
        json.Should().Contain("\"model\":");
        json.Should().Contain("\"choices\":");
    }

    [Fact]
    public void Deserialize_ShouldMapSnakeCaseToProperties()
    {
        // Arrange
        var json = @"{
            ""id"": ""chatcmpl-999"",
            ""object"": ""chat.completion.chunk"",
            ""created"": 9999999999,
            ""model"": ""ollama:latest"",
            ""choices"": [{
                ""index"": 0,
                ""delta"": {""role"": ""assistant"", ""content"": ""Hello""},
                ""finish_reason"": null
            }]
        }";

        // Act
        var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(json);

        // Assert
        chunk.Should().NotBeNull();
        chunk!.Id.Should().Be("chatcmpl-999");
        chunk.Object.Should().Be("chat.completion.chunk");
        chunk.Created.Should().Be(9999999999);
        chunk.Model.Should().Be("ollama:latest");
        chunk.Choices.Should().HaveCount(1);
    }

    [Fact]
    public void OpenAIStreamChunk_IsImmutable()
    {
        var type = typeof(OpenAIStreamChunk);
        type.IsSealed.Should().BeTrue();
    }
}
