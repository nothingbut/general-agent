using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.Embedding.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Embedding.Tests;

/// <summary>
/// OllamaEmbeddingClient 单元测试
/// </summary>
public sealed class OllamaEmbeddingClientTests
{
    private readonly ILogger<OllamaEmbeddingClient> _loggerMock;
    private readonly EmbeddingOptions _options;
    private readonly IOptions<EmbeddingOptions> _optionsWrapper;

    public OllamaEmbeddingClientTests()
    {
        _loggerMock = Substitute.For<ILogger<OllamaEmbeddingClient>>();
        _options = new EmbeddingOptions
        {
            Provider = "Ollama",
            BaseUrl = "http://localhost:11434",
            Model = "nomic-embed-text",
            TimeoutSeconds = 30
        };
        _optionsWrapper = Options.Create(_options);
    }

    /// <summary>
    /// 测试：空字符串输入抛出 ArgumentException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await client.GenerateEmbeddingAsync("", CancellationToken.None));
        exception.ParamName.Should().Be("text");
    }

    /// <summary>
    /// 测试：Null 字符串输入抛出 ArgumentException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_NullText_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await client.GenerateEmbeddingAsync(null!, CancellationToken.None));
        exception.ParamName.Should().Be("text");
    }

    /// <summary>
    /// 测试：有效文本返回 768 维向量
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsVector()
    {
        // Arrange
        var vector = Enumerable.Range(0, 768).Select(i => (float)i / 1000).ToArray();
        var response = new EmbeddingResponse
        {
            Embedding = vector
        };

        var httpClient = CreateMockHttpClient(response, HttpStatusCode.OK);
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act
        var result = await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(768);
        result.Should().Equal(vector);
        client.Dimensions.Should().Be(768);
        client.ProviderName.Should().Be("Ollama");
    }

    /// <summary>
    /// 测试：Ollama 返回 500 错误抛出 EmbeddingException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_OllamaDown_ThrowsEmbeddingException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain")
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None));
        exception.Message.Should().Contain("Failed to call Ollama embedding API");
    }

    /// <summary>
    /// 测试：网络错误抛出 EmbeddingException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_NetworkError_ThrowsEmbeddingException()
    {
        // Arrange
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler(
            new HttpRequestException("Network error")));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None));
        exception.Message.Should().Contain("Failed to call Ollama embedding API");
    }

    /// <summary>
    /// 测试：无效 JSON 响应抛出 EmbeddingException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_InvalidJsonResponse_ThrowsEmbeddingException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None));
        exception.Message.Should().Contain("Invalid response format");
    }

    /// <summary>
    /// 测试：空向量数组响应抛出 EmbeddingException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyEmbeddingResponse_ThrowsEmbeddingException()
    {
        // Arrange
        var response = new EmbeddingResponse
        {
            Embedding = Array.Empty<float>()
        };

        var httpClient = CreateMockHttpClient(response, HttpStatusCode.OK);
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None));
        exception.Message.Should().Contain("empty embedding");
    }

    /// <summary>
    /// 测试：Null 向量数组响应抛出 EmbeddingException
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_NullEmbeddingResponse_ThrowsEmbeddingException()
    {
        // Arrange
        var response = new EmbeddingResponse
        {
            Embedding = null!
        };

        var httpClient = CreateMockHttpClient(response, HttpStatusCode.OK);
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateEmbeddingAsync("Hello world", CancellationToken.None));
        exception.Message.Should().Contain("empty embedding");
    }

    /// <summary>
    /// 测试：批量生成 Embedding - 多个文本返回多个向量
    /// </summary>
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_MultipleTexts_ReturnsBatch()
    {
        // Arrange
        var vector1 = Enumerable.Range(0, 768).Select(i => (float)i / 1000).ToArray();
        var vector2 = Enumerable.Range(0, 768).Select(i => (float)(i + 1) / 1000).ToArray();
        var vector3 = Enumerable.Range(0, 768).Select(i => (float)(i + 2) / 1000).ToArray();

        var response1 = new EmbeddingResponse { Embedding = vector1 };
        var response2 = new EmbeddingResponse { Embedding = vector2 };
        var response3 = new EmbeddingResponse { Embedding = vector3 };

        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateHttpResponseMessage(response1),
            CreateHttpResponseMessage(response2),
            CreateHttpResponseMessage(response3)
        });

        var httpClient = new HttpClient(new QueuedMockHttpMessageHandler(responses));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        var texts = new List<string> { "Text 1", "Text 2", "Text 3" };

        // Act
        var results = await client.GenerateBatchEmbeddingsAsync(texts, CancellationToken.None);

        // Assert
        results.Should().NotBeNull();
        results.Count.Should().Be(3);
        results[0].Should().Equal(vector1);
        results[1].Should().Equal(vector2);
        results[2].Should().Equal(vector3);
    }

    /// <summary>
    /// 测试：批量生成 Embedding - 空列表返回空数组
    /// </summary>
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_EmptyTexts_ReturnsEmptyArray()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act
        var result = await client.GenerateBatchEmbeddingsAsync(Array.Empty<string>(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试：批量生成 Embedding - Null 输入抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_NullTexts_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK
        }));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await client.GenerateBatchEmbeddingsAsync(null!, CancellationToken.None));
        exception.ParamName.Should().Be("texts");
    }

    /// <summary>
    /// 测试：批量生成 Embedding - 任一文本失败导致异常
    /// </summary>
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_OneTextFails_ThrowsEmbeddingException()
    {
        // Arrange
        var vector1 = Enumerable.Range(0, 768).Select(i => (float)i / 1000).ToArray();
        var response1 = new EmbeddingResponse { Embedding = vector1 };

        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateHttpResponseMessage(response1),
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server Error", Encoding.UTF8, "text/plain")
            }
        });

        var httpClient = new HttpClient(new QueuedMockHttpMessageHandler(responses));
        var client = new OllamaEmbeddingClient(httpClient, _optionsWrapper, _loggerMock);

        var texts = new List<string> { "Text 1", "Text 2" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EmbeddingException>(
            async () => await client.GenerateBatchEmbeddingsAsync(texts, CancellationToken.None));
        exception.Message.Should().Contain("Failed to call Ollama embedding API");
    }

    /// <summary>
    /// 测试：Null HttpClient 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new OllamaEmbeddingClient(null!, _optionsWrapper, _loggerMock));
        exception.ParamName.Should().Be("httpClient");
    }

    /// <summary>
    /// 测试：Null Options 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage()));

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new OllamaEmbeddingClient(httpClient, null!, _loggerMock));
        exception.ParamName.Should().Be("options");
    }

    /// <summary>
    /// 测试：Null Logger 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage()));

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new OllamaEmbeddingClient(httpClient, _optionsWrapper, null!));
        exception.ParamName.Should().Be("logger");
    }

    // ==================== Helper Methods ====================

    private static HttpClient CreateMockHttpClient(
        EmbeddingResponse response,
        HttpStatusCode statusCode)
    {
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        return new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
    }

    private static HttpResponseMessage CreateHttpResponseMessage(EmbeddingResponse response)
    {
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    // ==================== Mock Classes ====================

    /// <summary>
    /// 模拟 HttpMessageHandler - 返回固定响应
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// 模拟 HttpMessageHandler - 按顺序返回不同响应
    /// </summary>
    private class QueuedMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueuedMockHttpMessageHandler(Queue<HttpResponseMessage> responses)
        {
            _responses = responses ?? throw new ArgumentNullException(nameof(responses));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No more mock responses available");
            }

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// 模拟 HttpMessageHandler - 抛出异常
    /// </summary>
    private class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
