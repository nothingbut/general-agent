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
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GeneralAgent.Infrastructure.LLM.Tests;

/// <summary>
/// OpenAICompatibleClient 单元测试
/// </summary>
public sealed class OpenAICompatibleClientTests
{
    private readonly ILogger<OpenAICompatibleClient> _loggerMock;
    private readonly LLMProviderConfig _config;
    private readonly IOptions<LLMProviderConfig> _options;

    public OpenAICompatibleClientTests()
    {
        _loggerMock = Substitute.For<ILogger<OpenAICompatibleClient>>();
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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Network error"));
        var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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

        var handler = new DelayingHttpMessageHandler(TimeSpan.FromSeconds(5));
        var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleClient(httpClient, shortTimeoutOptions, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

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
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var providerName = client.ProviderName;

        // Assert
        providerName.Should().Be("TestProvider");
    }

    [Fact]
    public async Task StreamAsync_成功流式调用_返回多个chunks()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""role"":""assistant"",""content"":""你好""},""finish_reason"":null}]}

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""世界""},""finish_reason"":null}]}

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""！""},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(3);
        chunks[0].Delta.Should().Be("你好");
        chunks[0].IsComplete.Should().BeFalse();
        chunks[1].Delta.Should().Be("世界");
        chunks[1].IsComplete.Should().BeFalse();
        chunks[2].Delta.Should().Be("！");
        chunks[2].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_单个chunk_成功返回()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Hello""},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Delta.Should().Be("Hello");
        chunks[0].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_空内容chunk_被跳过()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""role"":""assistant""},""finish_reason"":null}]}

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Hello""},""finish_reason"":null}]}

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(2);
        chunks[0].Delta.Should().Be("Hello");
        chunks[0].IsComplete.Should().BeFalse();
        chunks[1].Delta.Should().BeEmpty();
        chunks[1].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_Role字段_在第一个chunk中处理()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""role"":""assistant"",""content"":""Hi""},""finish_reason"":null}]}

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":"" there""},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(2);
        chunks[0].Delta.Should().Be("Hi");
        chunks[1].Delta.Should().Be(" there");
        chunks[1].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_DONE标记_正确结束流()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Test""},""finish_reason"":null}]}

data: [DONE]

data: {""id"":""test-2"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Should not appear""},""finish_reason"":null}]}

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Delta.Should().Be("Test");
    }

    [Fact]
    public async Task StreamAsync_HTTP错误_抛出LLMException()
    {
        // Arrange
        var request = CreateValidRequest();
        var httpClient = CreateMockStreamingHttpClient(
            content: "{\"error\": \"Unauthorized\"}",
            statusCode: HttpStatusCode.Unauthorized);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(request))
            {
                // 不应该执行到这里
            }
        });

        exception.ErrorType.Should().Be(LLMErrorType.AuthenticationError);
        exception.ProviderName.Should().Be("TestProvider");
    }

    [Fact]
    public async Task StreamAsync_网络错误_抛出NetworkError异常()
    {
        // Arrange
        var request = CreateValidRequest();
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Network error"));
        var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(request))
            {
                // 不应该执行到这里
            }
        });

        exception.ErrorType.Should().Be(LLMErrorType.NetworkError);
        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task StreamAsync_超时_抛出TimeoutError异常()
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

        var handler = new DelayingHttpMessageHandler(TimeSpan.FromSeconds(5));
        var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleClient(httpClient, shortTimeoutOptions, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(request))
            {
                // 不应该执行到这里
            }
        });

        exception.ErrorType.Should().Be(LLMErrorType.TimeoutError);
        exception.Message.Should().Contain("超时");
    }

    [Fact]
    public async Task StreamAsync_JSON解析错误_记录警告并跳过chunk()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @"data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Good""},""finish_reason"":null}]}

data: Invalid JSON {]

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""OK""},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(2);
        chunks[0].Delta.Should().Be("Good");
        chunks[1].Delta.Should().Be("OK");
        chunks[1].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_注释行和空行_正确忽略()
    {
        // Arrange
        var request = CreateValidRequest();
        var sseContent = @": This is a comment

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":""Hello""},""finish_reason"":null}]}

: Another comment

data: {""id"":""test-1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""test-model"",""choices"":[{""index"":0,""delta"":{""content"":"" World""},""finish_reason"":""stop""}]}

data: [DONE]

";
        var httpClient = CreateMockStreamingHttpClient(sseContent, HttpStatusCode.OK);
        var client = new OpenAICompatibleClient(httpClient, _options, _loggerMock);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(2);
        chunks[0].Delta.Should().Be("Hello");
        chunks[1].Delta.Should().Be(" World");
        chunks[1].IsComplete.Should().BeTrue();
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
        var client = new OpenAICompatibleClient(httpClient, realOptions, _loggerMock);

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
        var handler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });

        return new HttpClient(handler);
    }

    private static HttpClient CreateMockStreamingHttpClient(
        string content,
        HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        });

        return new HttpClient(handler);
    }

    // Helper classes for HttpMessageHandler mocking
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    private class DelayingHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayingHttpMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
