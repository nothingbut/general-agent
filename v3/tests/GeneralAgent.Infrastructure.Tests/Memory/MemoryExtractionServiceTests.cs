using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using FluentAssertions;

namespace GeneralAgent.Infrastructure.Tests.Memory;

/// <summary>
/// 记忆提取服务测试
/// </summary>
public sealed class MemoryExtractionServiceTests
{
    private readonly ILLMClientFactory _mockLlmFactory;
    private readonly ILLMClient _mockLlmClient;
    private readonly IMemoryRepository _mockRepository;
    private readonly MemoryExtractionService _service;

    public MemoryExtractionServiceTests()
    {
        _mockLlmFactory = Substitute.For<ILLMClientFactory>();
        _mockLlmClient = Substitute.For<ILLMClient>();
        _mockRepository = Substitute.For<IMemoryRepository>();

        _mockLlmFactory.GetClient(Arg.Any<string>()).Returns(_mockLlmClient);

        _service = new MemoryExtractionService(
            _mockLlmFactory,
            _mockRepository,
            NullLogger<MemoryExtractionService>.Instance);
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldReturnSuggestions_WhenLlmReturnsValidJson()
    {
        // Arrange
        var message = "我是一名 C# 开发者，喜欢使用 TDD 方法";
        var llmResponse = """
            {
              "suggestions": [
                {
                  "type": "User",
                  "name": "user_role",
                  "description": "用户职业",
                  "content": "C# 开发者",
                  "confidence": 0.9,
                  "tags": ["programming", "csharp"],
                  "rationale": "用户明确说明了职业"
                },
                {
                  "type": "Feedback",
                  "name": "coding_preference",
                  "description": "编码偏好",
                  "content": "喜欢使用 TDD 方法",
                  "confidence": 0.85,
                  "tags": ["tdd", "testing"],
                  "rationale": "用户表达了编码方法偏好"
                }
              ]
            }
            """;

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = llmResponse,
                Usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 200 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(message);

        // Assert
        suggestions.Should().HaveCount(2);

        var firstSuggestion = suggestions[0];
        firstSuggestion.Type.Should().Be(MemoryType.User);
        firstSuggestion.Name.Should().Be("user_role");
        firstSuggestion.Description.Should().Be("用户职业");
        firstSuggestion.Content.Should().Be("C# 开发者");
        firstSuggestion.Confidence.Should().Be(0.9);
        firstSuggestion.Tags.Should().BeEquivalentTo(new[] { "programming", "csharp" });

        var secondSuggestion = suggestions[1];
        secondSuggestion.Type.Should().Be(MemoryType.Feedback);
        secondSuggestion.Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldReturnEmpty_WhenMessageIsEmpty()
    {
        // Arrange
        var emptyMessage = "";

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(emptyMessage);

        // Assert
        suggestions.Should().BeEmpty();
        await _mockLlmClient.DidNotReceive().CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldReturnEmpty_WhenLlmReturnsNoSuggestions()
    {
        // Arrange
        var message = "你好";
        var llmResponse = """
            {
              "suggestions": []
            }
            """;

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = llmResponse,
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(message);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldHandleInvalidJson_Gracefully()
    {
        // Arrange
        var message = "测试消息";
        var llmResponse = "这不是有效的 JSON";

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = llmResponse,
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(message);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldFilterInvalidType()
    {
        // Arrange
        var message = "测试消息";
        var llmResponse = """
            {
              "suggestions": [
                {
                  "type": "InvalidType",
                  "name": "test",
                  "description": "test",
                  "content": "test",
                  "confidence": 0.9,
                  "tags": []
                }
              ]
            }
            """;

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = llmResponse,
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 50 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(message);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldHandleLlmException_Gracefully()
    {
        // Arrange
        var message = "测试消息";

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Throws(new LLMException("LLM 调用失败"));

        // Act
        var suggestions = await _service.ExtractFromMessageAsync(message);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMemoryFromSuggestionAsync_ShouldCreateMemory_WhenSuggestionIsValid()
    {
        // Arrange
        var suggestion = new MemorySuggestion
        {
            Type = MemoryType.User,
            Name = "user_role",
            Description = "用户职业",
            Content = "C# 开发者",
            Confidence = 0.9,
            Tags = new[] { "programming", "csharp" }
        };

        _mockRepository.NameExistsAsync(Arg.Any<string>(), Arg.Any<MemoryType>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _mockRepository.SaveAsync(Arg.Any<Core.Models.Memory>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<Core.Models.Memory>()));

        // Act
        var memory = await _service.CreateMemoryFromSuggestionAsync(suggestion);

        // Assert
        memory.Should().NotBeNull();
        memory!.Name.Should().Be("user_role");
        memory.Type.Should().Be(MemoryType.User);
        memory.Description.Should().Be("用户职业");
        memory.Content.Should().Be("C# 开发者");
        memory.Tags.Should().BeEquivalentTo(new[] { "programming", "csharp" });

        await _mockRepository.Received(1).SaveAsync(
            Arg.Is<Core.Models.Memory>(m => m.Name == "user_role"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateMemoryFromSuggestionAsync_ShouldReturnNull_WhenConfidenceTooLow()
    {
        // Arrange
        var suggestion = new MemorySuggestion
        {
            Type = MemoryType.User,
            Name = "low_confidence",
            Description = "低置信度记忆",
            Content = "测试内容",
            Confidence = 0.5, // 低于 0.6 阈值
            Tags = Array.Empty<string>()
        };

        // Act
        var memory = await _service.CreateMemoryFromSuggestionAsync(suggestion);

        // Assert
        memory.Should().BeNull();
        await _mockRepository.DidNotReceive().SaveAsync(
            Arg.Any<Core.Models.Memory>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateMemoryFromSuggestionAsync_ShouldReturnNull_WhenNameExists()
    {
        // Arrange
        var suggestion = new MemorySuggestion
        {
            Type = MemoryType.User,
            Name = "existing_name",
            Description = "已存在的记忆",
            Content = "测试内容",
            Confidence = 0.9,
            Tags = Array.Empty<string>()
        };

        _mockRepository.NameExistsAsync("existing_name", MemoryType.User, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var memory = await _service.CreateMemoryFromSuggestionAsync(suggestion);

        // Assert
        memory.Should().BeNull();
        await _mockRepository.DidNotReceive().SaveAsync(
            Arg.Any<Core.Models.Memory>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractFromConversationAsync_ShouldExtractFromLastUserMessage()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>
        {
            new() { Role = "user", Content = "你好" },
            new() { Role = "assistant", Content = "你好！有什么可以帮助你的吗？" },
            new() { Role = "user", Content = "我是一名 Python 开发者" }
        };

        var llmResponse = """
            {
              "suggestions": [
                {
                  "type": "User",
                  "name": "user_language",
                  "description": "编程语言",
                  "content": "Python 开发者",
                  "confidence": 0.9,
                  "tags": ["python", "programming"]
                }
              ]
            }
            """;

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = llmResponse,
                Usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 100 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var suggestions = await _service.ExtractFromConversationAsync(conversationHistory);

        // Assert
        suggestions.Should().HaveCount(1);
        suggestions[0].Name.Should().Be("user_language");
    }

    [Fact]
    public async Task ExtractFromConversationAsync_ShouldReturnEmpty_WhenNoUserMessages()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>
        {
            new() { Role = "assistant", Content = "你好！" },
            new() { Role = "system", Content = "系统消息" }
        };

        // Act
        var suggestions = await _service.ExtractFromConversationAsync(conversationHistory);

        // Assert
        suggestions.Should().BeEmpty();
        await _mockLlmClient.DidNotReceive().CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractFromConversationAsync_ShouldReturnEmpty_WhenHistoryIsEmpty()
    {
        // Arrange
        var emptyHistory = new List<ChatMessage>();

        // Act
        var suggestions = await _service.ExtractFromConversationAsync(emptyHistory);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessageAsync_ShouldIncludeContext_WhenProvided()
    {
        // Arrange
        var message = "我喜欢用 TDD";
        var context = "用户之前提到他是 C# 开发者";
        CompletionRequest? capturedRequest = null;

        _mockLlmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<CompletionRequest>();
                return new CompletionResponse
                {
                    Content = """{"suggestions": []}""",
                    Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 20 },
                    Timestamp = DateTime.UtcNow
                };
            });

        // Act
        await _service.ExtractFromMessageAsync(message, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Messages[0].Content.Should().Contain(context);
    }
}
