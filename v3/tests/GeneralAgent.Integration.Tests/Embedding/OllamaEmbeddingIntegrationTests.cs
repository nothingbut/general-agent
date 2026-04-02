using FluentAssertions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.Embedding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Integration.Tests.Embedding;

/// <summary>
/// Ollama Embedding 集成测试
/// 实际调用真实的 Ollama 服务，验证与服务的交互
/// </summary>
[Collection("Ollama")]
[Trait("Category", "Integration")]
public sealed class OllamaEmbeddingIntegrationTests : IAsyncLifetime
{
    private readonly string _ollamaBaseUrl = "http://localhost:11434";
    private readonly string _embeddingModel = "nomic-embed-text";
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    private IServiceProvider _serviceProvider = null!;
    private OllamaEmbeddingClient _client = null!;
    private string? _skipReason = null;

    public async Task InitializeAsync()
    {
        // 检查 Ollama 是否可用
        var isOllamaAvailable = await IsOllamaAvailableAsync();

        if (!isOllamaAvailable)
        {
            _skipReason = "Ollama 服务不可用（http://localhost:11434）";
            return;
        }

        try
        {
            // 验证 nomic-embed-text 模型是否已下载
            if (!await IsModelAvailableAsync(_embeddingModel))
            {
                _skipReason = $"Ollama 模型 '{_embeddingModel}' 未下载。运行: ollama pull {_embeddingModel}";
                return;
            }

            // 设置依赖注入
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // 手动注册 Embedding 相关服务
            // 注意：EmbeddingOptions 使用 init 访问器，不能通过 Configure 修改
            // 直接注册配置对象
            var embeddingOptions = new EmbeddingOptions
            {
                Provider = "Ollama",
                BaseUrl = _ollamaBaseUrl,
                Model = _embeddingModel,
                TimeoutSeconds = 60
            };
            services.AddSingleton(Options.Create(embeddingOptions));

            services.AddHttpClient<OllamaEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(_ollamaBaseUrl);
                client.Timeout = _timeout;
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            _serviceProvider = services.BuildServiceProvider();

            var options = _serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>();
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();
            var logger = _serviceProvider.GetRequiredService<ILogger<OllamaEmbeddingClient>>();

            _client = new OllamaEmbeddingClient(httpClient, options, logger);
        }
        catch (Exception ex)
        {
            _skipReason = $"初始化失败: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// 检查 Ollama 服务是否可用
    /// </summary>
    private async Task<bool> IsOllamaAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{_ollamaBaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查特定模型是否已在 Ollama 中可用
    /// </summary>
    private async Task<bool> IsModelAvailableAsync(string modelName)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{_ollamaBaseUrl}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            return content.Contains(modelName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 跳过测试如果 Ollama 不可用
    /// </summary>
    private void SkipIfOllamaUnavailable()
    {
        if (_skipReason != null)
        {
            throw new SkipTestException(_skipReason);
        }
    }

    /// <summary>
    /// 测试：生成单个文本的 Embedding
    /// </summary>
    [Fact]
    public async Task GenerateEmbedding_RealOllama_ReturnsValidVector()
    {
        // Arrange
        SkipIfOllamaUnavailable();
        var text = "Hello, world!";

        // Act
        using var cts = new CancellationTokenSource(_timeout);
        var embedding = await _client.GenerateEmbeddingAsync(text, cts.Token);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768); // nomic-embed-text 返回 768 维向量

        // 验证向量不全为零
        embedding.Any(v => v != 0f).Should().BeTrue("向量不应该全为零");
    }

    /// <summary>
    /// 测试：中文文本 Embedding 生成
    /// </summary>
    [Fact]
    public async Task GenerateEmbedding_ChineseText_WorksCorrectly()
    {
        // Arrange
        SkipIfOllamaUnavailable();
        var chineseText = "你好，世界！这是一个测试。";

        // Act
        using var cts = new CancellationTokenSource(_timeout);
        var embedding = await _client.GenerateEmbeddingAsync(chineseText, cts.Token);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768);
        embedding.Any(v => v != 0f).Should().BeTrue("中文向量不应该全为零");

        // 验证中英文的 Embedding 不同
        var englishText = "Hello, world! This is a test.";
        var englishEmbedding = await _client.GenerateEmbeddingAsync(englishText, cts.Token);

        embedding.Should().NotEqual(englishEmbedding);
    }

    /// <summary>
    /// 测试：批量生成 Embedding
    /// </summary>
    [Fact]
    public async Task GenerateBatchEmbeddings_RealOllama_ReturnsBatch()
    {
        // Arrange
        SkipIfOllamaUnavailable();
        var texts = new List<string>
        {
            "First document",
            "Second document",
            "Third document"
        };

        // Act
        using var cts = new CancellationTokenSource(_timeout);
        var embeddings = await _client.GenerateBatchEmbeddingsAsync(texts, cts.Token);

        // Assert
        embeddings.Should().NotBeNull();
        embeddings.Count.Should().Be(3);

        // 验证每个向量的有效性
        foreach (var embedding in embeddings)
        {
            embedding.Should().NotBeNull();
            embedding.Length.Should().Be(768);
            embedding.Any(v => v != 0f).Should().BeTrue("每个向量不应该全为零");
        }

        // 验证不同文本产生不同的向量
        embeddings[0].Should().NotEqual(embeddings[1]);
        embeddings[1].Should().NotEqual(embeddings[2]);
    }

    /// <summary>
    /// 测试：长文本 Embedding 生成
    /// 长文本定义为超过 1000 个字符的文本
    /// </summary>
    [Fact]
    public async Task GenerateEmbedding_LongText_WorksCorrectly()
    {
        // Arrange
        SkipIfOllamaUnavailable();
        var longText = string.Concat(Enumerable.Repeat(
            "This is a long text for testing embedding generation. " +
            "It contains multiple sentences to ensure that the embedding client " +
            "can handle longer inputs correctly. ",
            25)); // 生成约 2500+ 字符的文本

        // Act
        using var cts = new CancellationTokenSource(_timeout);
        var embedding = await _client.GenerateEmbeddingAsync(longText, cts.Token);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768);
        embedding.Any(v => v != 0f).Should().BeTrue("长文本向量不应该全为零");

        // 验证长文本的向量与短文本不同
        var shortText = "Short text";
        var shortEmbedding = await _client.GenerateEmbeddingAsync(shortText, cts.Token);

        embedding.Should().NotEqual(shortEmbedding);
    }

}

/// <summary>
/// 用于跳过测试的异常类
/// </summary>
internal class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}
