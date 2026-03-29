using System.Text.Json;
using System.Text.Json.Serialization;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.Embedding.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.Embedding;

/// <summary>
/// Ollama Embedding 客户端实现
/// </summary>
public sealed class OllamaEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OllamaEmbeddingClient> _logger;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderName => "Ollama";

    public int Dimensions => 768;

    public OllamaEmbeddingClient(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options,
        ILogger<OllamaEmbeddingClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 为单个文本生成 Embedding 向量
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            _logger.LogDebug("Generating embedding for text with length: {TextLength}", text.Length);

            var request = new EmbeddingRequest
            {
                Model = _options.Model,
                Prompt = text
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var url = $"{_options.BaseUrl}/api/embeddings";
            var response = await _httpClient.PostAsync(url, content, cts.Token);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent, JsonOptions);

            if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
            {
                throw new EmbeddingException("Ollama API returned empty embedding");
            }

            _logger.LogDebug("Successfully generated embedding with dimensions: {Dimensions}", embeddingResponse.Embedding.Length);
            return embeddingResponse.Embedding;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while generating embedding");
            throw new EmbeddingException($"Failed to call Ollama embedding API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Embedding generation request timed out after {TimeoutSeconds} seconds", _options.TimeoutSeconds);
            throw new EmbeddingException($"Embedding generation request timed out after {_options.TimeoutSeconds} seconds", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Ollama API response");
            throw new EmbeddingException("Invalid response format from Ollama API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during embedding generation");
            throw new EmbeddingException($"Unexpected error during embedding generation: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 批量生成 Embedding 向量（并行处理）
    /// </summary>
    public async Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null)
        {
            throw new ArgumentNullException(nameof(texts));
        }

        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        _logger.LogDebug("Generating embeddings for batch of {Count} texts", texts.Count);

        try
        {
            var tasks = texts.Select(text => GenerateEmbeddingAsync(text, cancellationToken)).ToList();
            var embeddings = await Task.WhenAll(tasks);

            _logger.LogDebug("Successfully generated {Count} embeddings", embeddings.Length);
            return Array.AsReadOnly(embeddings);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Batch embedding generation was cancelled");
            throw new EmbeddingException("Batch embedding generation was cancelled", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch embedding generation");
            throw new EmbeddingException($"Error during batch embedding generation: {ex.Message}", ex);
        }
    }
}
