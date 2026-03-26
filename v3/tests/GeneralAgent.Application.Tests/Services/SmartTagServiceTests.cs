using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// 智能标签建议服务测试
/// </summary>
public sealed class SmartTagServiceTests
{
    private readonly ILLMClient _mockLlmClient;
    private readonly ISessionTagRepository _mockTagRepository;
    private readonly ILogger<SmartTagService> _mockLogger;
    private readonly SmartTagService _service;

    public SmartTagServiceTests()
    {
        _mockLlmClient = Substitute.For<ILLMClient>();
        _mockTagRepository = Substitute.For<ISessionTagRepository>();
        _mockLogger = Substitute.For<ILogger<SmartTagService>>();
        _service = new SmartTagService(
            _mockLlmClient,
            _mockTagRepository,
            _mockLogger
        );
    }

    [Fact]
    public async Task SuggestFromTitleAsync_ValidTitle_ReturnsSuggestions()
    {
        // Arrange
        var title = "讨论 C# 异步编程最佳实践";
        var llmResponse = @"{
            ""tags"": [
                {""name"": ""csharp"", ""emoji"": ""#️⃣"", ""color"": ""#239120""},
                {""name"": ""async"", ""emoji"": ""⚡"", ""color"": ""#F59E0B""},
                {""name"": ""best-practices"", ""emoji"": ""✨"", ""color"": ""#8B5CF6""}
            ]
        }";

        var completionResponse = new CompletionResponse
        {
            Content = llmResponse,
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
            Timestamp = DateTime.UtcNow
        };

        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).Returns(completionResponse);

        // Act
        var result = await _service.SuggestFromTitleAsync(title);

        // Assert
        result.Should().HaveCount(3);
        result[0].Tag.Should().Be("csharp");
        result[0].Emoji.Should().Be("#️⃣");
        result[0].Color.Should().Be("#239120");
        result[1].Tag.Should().Be("async");
        result[2].Tag.Should().Be("best-practices");

        // 验证 LLM 被调用
        await _mockLlmClient.Received(1).CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task SuggestFromTitleAsync_LLMTimeout_ReturnsEmpty()
    {
        // Arrange
        var title = "测试标题";

        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _service.SuggestFromTitleAsync(title);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestFromContentAsync_ValidContent_ReturnsSuggestions()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = new List<Message>
        {
            Message.CreateUser(sessionId, "如何在 .NET 中使用依赖注入？"),
            Message.CreateAssistant(sessionId, "依赖注入是 .NET Core 的核心特性...")
        };

        var llmResponse = @"{
            ""tags"": [
                {""name"": ""dotnet"", ""emoji"": ""🟣"", ""color"": ""#512BD4""},
                {""name"": ""dependency-injection"", ""emoji"": ""💉"", ""color"": ""#10B981""},
                {""name"": ""architecture"", ""emoji"": ""🏗️"", ""color"": ""#6366F1""}
            ]
        }";

        var completionResponse = new CompletionResponse
        {
            Content = llmResponse,
            Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 30 },
            Timestamp = DateTime.UtcNow
        };

        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).Returns(completionResponse);

        // Act
        var result = await _service.SuggestFromContentAsync(sessionId, messages);

        // Assert
        result.Should().HaveCount(3);
        result[0].Tag.Should().Be("dotnet");
        result[0].Emoji.Should().Be("🟣");
        result[1].Tag.Should().Be("dependency-injection");
        result[2].Tag.Should().Be("architecture");

        // 验证 LLM 被调用
        await _mockLlmClient.Received(1).CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ApplySuggestionsAsync_ValidSuggestions_AddsToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var suggestions = new List<TagSuggestion>
        {
            new("python", "🐍", "#3776AB"),
            new("bug", "🐛", "#EF4444")
        };
        _mockTagRepository
            .GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new List<SessionTag>());

        // Act
        await _service.ApplySuggestionsAsync(sessionId, suggestions);

        // Assert
        await _mockTagRepository.Received(1).AddAsync(
            Arg.Is<SessionTag>(t => t.Tag == "python" && t.Source == TagSource.Auto && t.Emoji == "🐍" && t.Color == "#3776AB"),
            Arg.Any<CancellationToken>()
        );
        await _mockTagRepository.Received(1).AddAsync(
            Arg.Is<SessionTag>(t => t.Tag == "bug" && t.Source == TagSource.Auto && t.Emoji == "🐛" && t.Color == "#EF4444"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ApplySuggestionsAsync_DuplicateTag_SkipsExisting()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var existingTags = new List<SessionTag>
        {
            SessionTag.Create(sessionId, "python", TagSource.User)
        };
        var suggestions = new List<TagSuggestion>
        {
            new("python", "🐍", "#3776AB"),
            new("bug", "🐛", "#EF4444")
        };
        _mockTagRepository
            .GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingTags);

        // Act
        await _service.ApplySuggestionsAsync(sessionId, suggestions);

        // Assert
        await _mockTagRepository.DidNotReceive().AddAsync(
            Arg.Is<SessionTag>(t => t.Tag == "python"),
            Arg.Any<CancellationToken>()
        );
        await _mockTagRepository.Received(1).AddAsync(
            Arg.Is<SessionTag>(t => t.Tag == "bug"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ApplySuggestionsAsync_ExceedsMaxTags_OnlyAddsUpToLimit()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var existingTags = new List<SessionTag>
        {
            SessionTag.Create(sessionId, "tag1"),
            SessionTag.Create(sessionId, "tag2"),
            SessionTag.Create(sessionId, "tag3")
        };
        var suggestions = new List<TagSuggestion>
        {
            new("new1", "🆕", "#000000"),
            new("new2", "🆕", "#111111"),
            new("new3", "🆕", "#222222")
        };
        _mockTagRepository
            .GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(existingTags);

        // Act
        await _service.ApplySuggestionsAsync(sessionId, suggestions);

        // Assert
        await _mockTagRepository.Received(2).AddAsync(
            Arg.Any<SessionTag>(),
            Arg.Any<CancellationToken>()
        );
    }
}
