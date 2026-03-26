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
/// 自然语言查询服务测试
/// </summary>
public sealed class NaturalLanguageQueryServiceTests
{
    private readonly ILLMClient _mockLlmClient;
    private readonly ISearchQueryCache _mockCache;
    private readonly ILogger<NaturalLanguageQueryService> _mockLogger;
    private readonly NaturalLanguageQueryService _service;

    public NaturalLanguageQueryServiceTests()
    {
        _mockLlmClient = Substitute.For<ILLMClient>();
        _mockCache = Substitute.For<ISearchQueryCache>();
        _mockLogger = Substitute.For<ILogger<NaturalLanguageQueryService>>();
        _service = new NaturalLanguageQueryService(
            _mockLlmClient,
            _mockCache,
            _mockLogger
        );
    }

    [Fact]
    public async Task ParseQueryAsync_CacheHit_SkipsLLMCall()
    {
        // Arrange
        var naturalQuery = "test query";
        var cachedQuery = new SearchQuery
        {
            NaturalQuery = naturalQuery,
            Criteria = new SearchCriteria { Keywords = new List<string> { "test" } }
        };
        _mockCache.Get(naturalQuery).Returns(cachedQuery);

        // Act
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.NaturalQuery.Should().Be(naturalQuery);
        result.Criteria.Keywords.Should().Contain("test");

        // 验证 LLM 没有被调用
        await _mockLlmClient.DidNotReceive().CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ParseQueryAsync_CacheMiss_CallsLLM()
    {
        // Arrange
        var naturalQuery = "查找昨天关于 Python 的讨论";
        var llmResponse = @"{
            ""keywords"": [""Python""],
            ""startDate"": ""2026-03-24T00:00:00Z""
        }";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

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
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.NaturalQuery.Should().Be(naturalQuery);
        result.Criteria.Keywords.Should().Contain("Python");
        result.Criteria.StartDate.Should().NotBeNull();

        // 验证 LLM 被调用
        await _mockLlmClient.Received(1).CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        );

        // 验证缓存被更新
        _mockCache.Received(1).Set(naturalQuery, Arg.Any<SearchQuery>());
    }

    [Fact]
    public async Task ParseQueryAsync_LLMTimeout_FallsBackToKeywordSearch()
    {
        // Arrange
        var naturalQuery = "test query";
        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);
        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.NaturalQuery.Should().Be(naturalQuery);
        result.Type.Should().Be(SearchType.Keyword);
        result.Criteria.Keywords.Should().Contain(naturalQuery);
    }

    [Fact]
    public async Task ParseQueryAsync_InvalidJSON_FallsBackToKeywordSearch()
    {
        // Arrange
        var naturalQuery = "test query";
        var invalidJson = "not a valid json";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

        var completionResponse = new CompletionResponse
        {
            Content = invalidJson,
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
            Timestamp = DateTime.UtcNow
        };

        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).Returns(completionResponse);

        // Act
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.Type.Should().Be(SearchType.Keyword);
        result.Criteria.Keywords.Should().Contain(naturalQuery);
    }

    [Fact]
    public async Task ParseQueryAsync_ComplexQuery_ParsesAllFields()
    {
        // Arrange
        var naturalQuery = "查找 user 角色的关于 'bug fix' 和 Python 的消息";
        var llmResponse = @"{
            ""keywords"": [""Python""],
            ""exactPhrases"": [""bug fix""],
            ""role"": ""User"",
            ""regexPattern"": ""\\bPython\\b"",
            ""startDate"": ""2026-03-20T00:00:00Z"",
            ""endDate"": ""2026-03-26T23:59:59Z""
        }";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

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
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.Criteria.Keywords.Should().Contain("Python");
        result.Criteria.ExactPhrases.Should().Contain("bug fix");
        result.Criteria.Role.Should().Be(MessageRole.User);
        result.Criteria.RegexPattern.Should().Be("\\bPython\\b");
        result.Criteria.StartDate.Should().NotBeNull();
        result.Criteria.EndDate.Should().NotBeNull();
        result.Type.Should().Be(SearchType.Regex);
    }

    [Fact]
    public async Task ParseQueryAsync_MarkdownCodeBlock_CleansProperly()
    {
        // Arrange
        var naturalQuery = "test query";
        var markdownResponse = @"```json
{
    ""keywords"": [""test""]
}
```";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

        var completionResponse = new CompletionResponse
        {
            Content = markdownResponse,
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
            Timestamp = DateTime.UtcNow
        };

        _mockLlmClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>()
        ).Returns(completionResponse);

        // Act
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.Criteria.Keywords.Should().Contain("test");
    }

    [Fact]
    public async Task ParseQueryAsync_NullFields_HandlesGracefully()
    {
        // Arrange
        var naturalQuery = "simple query";
        var llmResponse = @"{
            ""keywords"": [""simple""],
            ""exactPhrases"": null,
            ""role"": null,
            ""regexPattern"": null,
            ""startDate"": null,
            ""endDate"": null
        }";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

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
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.Criteria.Keywords.Should().Contain("simple");
        result.Criteria.ExactPhrases.Should().BeEmpty();
        result.Criteria.Role.Should().BeNull();
        result.Criteria.RegexPattern.Should().BeNull();
        result.Type.Should().Be(SearchType.Keyword);
    }

    [Fact]
    public async Task ParseQueryAsync_EmptyKeywords_HandlesGracefully()
    {
        // Arrange
        var naturalQuery = "test";
        var llmResponse = @"{
            ""keywords"": [],
            ""exactPhrases"": [""exact phrase""]
        }";

        _mockCache.Get(naturalQuery).Returns((SearchQuery?)null);

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
        var result = await _service.ParseQueryAsync(naturalQuery);

        // Assert
        result.Criteria.Keywords.Should().BeEmpty();
        result.Criteria.ExactPhrases.Should().Contain("exact phrase");
        result.Type.Should().Be(SearchType.Exact);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task ParseQueryAsync_EmptyOrWhitespace_ThrowsArgumentException(string query)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ParseQueryAsync(query));
    }
}
