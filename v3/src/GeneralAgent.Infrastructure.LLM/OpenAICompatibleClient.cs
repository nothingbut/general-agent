using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// OpenAI 兼容 API 客户端（支持 Ollama, LMStudio, llama.cpp, OMLX）
/// </summary>
public sealed class OpenAICompatibleClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly LLMProviderConfig _config;
    private readonly ILogger<OpenAICompatibleClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAICompatibleClient(
        HttpClient httpClient,
        IOptions<LLMProviderConfig> options,
        ILogger<OpenAICompatibleClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    /// <inheritdoc/>
    public string ProviderName => _config.Name;

    /// <inheritdoc/>
    public async Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // 将 Domain Model 转换为 DTO
            var openAIRequest = ToOpenAIRequest(request);

            // 构建 API URL
            var apiUrl = $"{_config.BaseUrl.TrimEnd('/')}/v1/chat/completions";

            _logger.LogDebug(
                "发送补全请求到 {Provider} ({Url})，模型: {Model}",
                ProviderName,
                apiUrl,
                request.Model);

            // 创建带超时的 CancellationTokenSource
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            // 发送 HTTP 请求
            var response = await _httpClient.PostAsJsonAsync(
                apiUrl,
                openAIRequest,
                _jsonOptions,
                timeoutCts.Token);

            // 处理 HTTP 错误
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, ct);
            }

            // 解析响应
            var openAIResponse = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(
                _jsonOptions,
                ct);

            if (openAIResponse is null)
            {
                throw new LLMException(
                    "LLM 响应为空",
                    ProviderName,
                    LLMErrorType.Unknown);
            }

            // 验证响应数据完整性
            if (openAIResponse.Choices.Count == 0)
            {
                throw new LLMException(
                    "LLM 响应中没有生成的内容 (Choices 为空)",
                    ProviderName,
                    LLMErrorType.Unknown);
            }

            // 将 DTO 转换回 Domain Model
            var completionResponse = ToCompletionResponse(openAIResponse);

            _logger.LogDebug(
                "收到补全响应，生成内容长度: {Length}，使用 token: {Tokens}",
                completionResponse.Content.Length,
                completionResponse.Usage.TotalTokens);

            return completionResponse;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户主动取消
            _logger.LogWarning("补全请求被用户取消");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // 超时
            _logger.LogError("补全请求超时 (超过 {Timeout} 秒)", _config.TimeoutSeconds);
            throw new LLMException(
                $"LLM 请求超时（超过 {_config.TimeoutSeconds} 秒）",
                ProviderName,
                LLMErrorType.TimeoutError,
                ex);
        }
        catch (HttpRequestException ex)
        {
            // 网络错误
            _logger.LogError(ex, "补全请求网络错误: {Message}", ex.Message);
            throw new LLMException(
                $"LLM 请求网络错误: {ex.Message}",
                ProviderName,
                LLMErrorType.NetworkError,
                ex);
        }
        catch (JsonException ex)
        {
            // JSON 解析错误
            _logger.LogError(ex, "LLM 响应 JSON 解析失败: {Message}", ex.Message);
            throw new LLMException(
                $"LLM 响应格式错误: {ex.Message}",
                ProviderName,
                LLMErrorType.Unknown,
                ex);
        }
        catch (LLMException)
        {
            // 直接抛出 LLMException
            throw;
        }
        catch (Exception ex)
        {
            // 其他未知错误
            _logger.LogError(ex, "补全请求未知错误: {Message}", ex.Message);
            throw new LLMException(
                $"LLM 请求失败: {ex.Message}",
                ProviderName,
                LLMErrorType.Unknown,
                ex);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        // Task 8 实现
        throw new NotImplementedException("流式补全将在 Task 8 中实现");
    }

    /// <summary>
    /// 将 CompletionRequest 转换为 OpenAIChatRequest
    /// </summary>
    private OpenAIChatRequest ToOpenAIRequest(CompletionRequest request)
    {
        var messages = new List<OpenAIChatMessage>();

        // 如果有系统提示词，添加为第一条消息
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new OpenAIChatMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        // 添加对话历史消息
        foreach (var msg in request.Messages)
        {
            messages.Add(new OpenAIChatMessage
            {
                Role = msg.Role,
                Content = msg.Content
            });
        }

        return new OpenAIChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = false
        };
    }

    /// <summary>
    /// 将 OpenAIChatResponse 转换为 CompletionResponse
    /// </summary>
    private CompletionResponse ToCompletionResponse(OpenAIChatResponse response)
    {
        var firstChoice = response.Choices[0];

        return new CompletionResponse
        {
            Content = firstChoice.Message.Content,
            Model = response.Model,
            Usage = new TokenUsage
            {
                PromptTokens = response.Usage.PromptTokens,
                CompletionTokens = response.Usage.CompletionTokens
            },
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 处理 HTTP 错误响应
    /// </summary>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var statusCode = response.StatusCode;
        var content = await response.Content.ReadAsStringAsync(ct);

        _logger.LogError(
            "LLM 请求失败，状态码: {StatusCode}，响应: {Content}",
            (int)statusCode,
            content);

        var errorType = statusCode switch
        {
            HttpStatusCode.Unauthorized => LLMErrorType.AuthenticationError,
            HttpStatusCode.NotFound => LLMErrorType.ModelNotFound,
            HttpStatusCode.TooManyRequests => LLMErrorType.RateLimitError,
            HttpStatusCode.BadRequest
            or HttpStatusCode.Forbidden
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotAcceptable
            or HttpStatusCode.Conflict
            or HttpStatusCode.Gone
            or HttpStatusCode.UnprocessableEntity => LLMErrorType.Unknown,
            _ when (int)statusCode >= 500 => LLMErrorType.ServerError,
            _ => LLMErrorType.Unknown
        };

        var errorMessage = $"LLM 请求失败 (HTTP {(int)statusCode})";
        if (!string.IsNullOrWhiteSpace(content))
        {
            errorMessage += $": {content}";
        }

        throw new LLMException(errorMessage, ProviderName, errorType);
    }
}
