using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM;
using GeneralAgent.Infrastructure.LLM.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace GeneralAgent.Infrastructure.LLM.Tests;

/// <summary>
/// OpenAICompatibleClient 单元测试
/// </summary>
public sealed class OpenAICompatibleClientTests
{
    private readonly Mock<ILogger<OpenAICompatibleClient>> _loggerMock;
    private readonly LLMProviderConfig _config;
    private readonly IOptions<LLMProviderConfig> _options;

    public OpenAICompatibleClientTests()
    {
        _loggerMock = new Mock<ILogger<OpenAICompatibleClient>>();
        _config = new LLMProviderConfig
        {
            Name = "TestProvider",
            BaseUrl = "http://localhost:11434",
            DefaultModel = "test-model",
            TimeoutSeconds = 120
        };
        _options = Options.Create(_config);
    }

    [Fact]
    public async Task CompleteAsync_成功调用_返回正确响应()
    {
        // Arrange
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };

        var mockResponse = new OpenAIChatResponse
        {
            Id = "test-id",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "test-model",
            Choices = new[]
            {
                new OpenAIChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage
                    {
                        Role = "assistant",
                        Content = "Hi there!"
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new OpenAIUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act
        var response = await client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().Be("Hi there!");
        response.Model.Should().Be("test-model");
        response.Usage.PromptTokens.Should().Be(10);
        response.Usage.CompletionTokens.Should().Be(5);
        response.Usage.TotalTokens.Should().Be(15);
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteAsync_带SystemPrompt_正确构建消息列表()
    {
        // Arrange
        var request = new CompletionRequest
        {
            Model = "test-model",
            SystemPrompt = "You are a helpful assistant.",
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            }
        };

        var mockResponse = CreateValidMockResponse();
        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act
        var response = await client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_HTTP401_抛出AuthenticationError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "{\"error\": \"Unauthorized\"}",
            statusCode: HttpStatusCode.Unauthorized);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.AuthenticationError);
        exception.ProviderName.Should().Be("TestProvider");
        exception.Message.Should().Contain("401");
    }

    [Fact]
    public async Task CompleteAsync_HTTP404_抛出ModelNotFound异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "{\"error\": \"Model not found\"}",
            statusCode: HttpStatusCode.NotFound);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.ModelNotFound);
        exception.ProviderName.Should().Be("TestProvider");
    }

    [Fact]
    public async Task CompleteAsync_HTTP429_抛出RateLimitError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "{\"error\": \"Rate limit exceeded\"}",
            statusCode: HttpStatusCode.TooManyRequests);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.RateLimitError);
    }

    [Fact]
    public async Task CompleteAsync_HTTP500_抛出ServerError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "{\"error\": \"Internal server error\"}",
            statusCode: HttpStatusCode.InternalServerError);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.ServerError);
        exception.Message.Should().Contain("500");
    }

    [Fact]
    public async Task CompleteAsync_HTTP400_抛出Unknown异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "{\"error\": \"Bad request\"}",
            statusCode: HttpStatusCode.BadRequest);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.Unknown);
    }

    [Fact]
    public async Task CompleteAsync_网络错误_抛出NetworkError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.NetworkError);
        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CompleteAsync_超时_抛出TimeoutError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var shortTimeoutConfig = new LLMProviderConfig
        {
            Name = "TestProvider",
            BaseUrl = "http://localhost:11434",
            DefaultModel = "test-model",
            TimeoutSeconds = 1
        };
        var shortTimeoutOptions = Options.Create(shortTimeoutConfig);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new OpenAICompatibleClient(httpClient, shortTimeoutOptions, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.TimeoutError);
        exception.Message.Should().Contain("超时");
    }

    [Fact]
    public async Task CompleteAsync_JSON解析错误_抛出Unknown异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "Invalid JSON {]",
            statusCode: HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.Unknown);
        exception.Message.Should().Contain("格式错误");
        exception.InnerException.Should().BeOfType<JsonException>();
    }

    [Fact]
    public async Task CompleteAsync_空响应_抛出Unknown异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockHttpClient(
            content: "null",
            statusCode: HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.Unknown);
        exception.Message.Should().Contain("响应为空");
    }

    [Fact]
    public async Task CompleteAsync_Choices为空_抛出Unknown异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var mockResponse = new OpenAIChatResponse
        {
            Id = "test-id",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "test-model",
            Choices = Array.Empty<OpenAIChoice>(),
            Usage = new OpenAIUsage
            {
                PromptTokens = 10,
                CompletionTokens = 0,
                TotalTokens = 10
            }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(
            () => client.CompleteAsync(request));

        exception.ErrorType.Should().Be(LLMErrorType.Unknown);
        exception.Message.Should().Contain("Choices 为空");
    }

    [Fact]
    public void ProviderName_返回配置中的提供商名称()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);

        // Act
        var providerName = client.ProviderName;

        // Assert
        providerName.Should().Be("TestProvider");
    }

    [Fact]
    public async Task StreamAsync_抛出NotImplementedException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock.Object);
        var request = CreateValidRequest();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(request))
            {
                // 不应该执行到这里
            }
        });
    }

    // 集成测试（需要本地 Ollama 服务）
    [Fact(Skip = "需要本地 Ollama 服务，设置环境变量 TEST_OLLAMA_ENABLED=true 启用")]
    public async Task CompleteAsync_真实Ollama调用_成功()
    {
        // 仅在环境变量设置时运行
        if (Environment.GetEnvironmentVariable("TEST_OLLAMA_ENABLED") != "true")
        {
            return;
        }

        // Arrange
        var realConfig = new LLMProviderConfig
        {
            Name = "Ollama",
            BaseUrl = "http://localhost:11434",
            DefaultModel = "qwen2.5:0.5b",
            TimeoutSeconds = 60
        };
        var realOptions = Options.Create(realConfig);
        var httpClient = new HttpClient();
        var client = new OpenAICompatibleClient(httpClient, realOptions, _loggerMock.Object);

        var request = new CompletionRequest
        {
            Model = "qwen2.5:0.5b",
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = "Say 'Hello'" }
            }
        };

        // Act
        var response = await client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().NotBeEmpty();
        response.Usage.TotalTokens.Should().BeGreaterThan(0);
    }

    // 辅助方法

    private static CompletionRequest CreateValidRequest()
    {
        return new CompletionRequest
        {
            Model = "test-model",
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            }
        };
    }

    private static OpenAIChatResponse CreateValidMockResponse()
    {
        return new OpenAIChatResponse
        {
            Id = "test-id",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "test-model",
            Choices = new[]
            {
                new OpenAIChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage
                    {
                        Role = "assistant",
                        Content = "Test response"
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new OpenAIUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        };
    }

    private static HttpClient CreateMockHttpClient(
        OpenAIChatResponse response,
        HttpStatusCode statusCode)
    {
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        return CreateMockHttpClient(json, statusCode);
    }

    private static HttpClient CreateMockHttpClient(
        string content,
        HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }
}
