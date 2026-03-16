using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
            using var response = await _httpClient.PostAsJsonAsync(
                apiUrl,
                openAIRequest,
                _jsonOptions,
                timeoutCts.Token);

            // 处理 HTTP 错误
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, timeoutCts.Token);
            }

            // 解析响应
            var openAIResponse = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(
                _jsonOptions,
                timeoutCts.Token);

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
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 创建带超时的 CancellationTokenSource（用于整个流的超时控制）
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

        // 将 Domain Model 转换为 DTO（Stream = true）
        var openAIRequest = ToOpenAIRequest(request, stream: true);

        // 构建 API URL
        var apiUrl = $"{_config.BaseUrl.TrimEnd('/')}/v1/chat/completions";

        _logger.LogDebug(
            "发送流式补全请求到 {Provider} ({Url})，模型: {Model}",
            ProviderName,
            apiUrl,
            request.Model);

        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;

        try
        {
            // 发送 HTTP 请求
            response = await _httpClient.PostAsJsonAsync(
                apiUrl,
                openAIRequest,
                _jsonOptions,
                timeoutCts.Token);

            // 处理 HTTP 错误
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, timeoutCts.Token);
            }

            // 读取响应流
            stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            reader = new StreamReader(stream);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户主动取消
            _logger.LogWarning("流式补全请求被用户取消");
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // 超时
            _logger.LogError("流式补全请求超时 (超过 {Timeout} 秒)", _config.TimeoutSeconds);
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            throw new LLMException(
                $"LLM 流式请求超时（超过 {_config.TimeoutSeconds} 秒）",
                ProviderName,
                LLMErrorType.TimeoutError,
                ex);
        }
        catch (HttpRequestException ex)
        {
            // 网络错误
            _logger.LogError(ex, "流式补全请求网络错误: {Message}", ex.Message);
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            throw new LLMException(
                $"LLM 流式请求网络错误: {ex.Message}",
                ProviderName,
                LLMErrorType.NetworkError,
                ex);
        }
        catch (LLMException)
        {
            // 直接抛出 LLMException
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            // 其他未知错误
            _logger.LogError(ex, "流式补全请求未知错误: {Message}", ex.Message);
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            throw new LLMException(
                $"LLM 流式请求失败: {ex.Message}",
                ProviderName,
                LLMErrorType.Unknown,
                ex);
        }

        // 逐行解析 SSE 事件（在 try-catch 之外使用 yield）
        await foreach (var chunk in ParseSseStreamAsync(reader, timeoutCts.Token).ConfigureAwait(false))
        {
            yield return chunk;
        }

        _logger.LogDebug("流式补全完成");

        // 清理资源
        reader?.Dispose();
        stream?.Dispose();
        response?.Dispose();
    }

    /// <summary>
    /// 将 CompletionRequest 转换为 OpenAIChatRequest
    /// </summary>
    /// <param name="request">补全请求</param>
    /// <param name="stream">是否启用流式传输</param>
    private OpenAIChatRequest ToOpenAIRequest(CompletionRequest request, bool stream = false)
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
            Stream = stream
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
    private async Task HandleHttpErrorAsync(HttpResponseMessage response, CancellationToken timeoutToken)
    {
        var statusCode = response.StatusCode;
        var content = await response.Content.ReadAsStringAsync(timeoutToken);

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

    /// <summary>
    /// 解析 SSE 流并转换为 StreamChunk
    /// </summary>
    private async IAsyncEnumerable<StreamChunk> ParseSseStreamAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            // 跳过空行和注释行
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
            {
                continue;
            }

            // 解析 SSE 事件（格式：data: {...}）
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6); // 移除 "data: " 前缀

                // 检测流结束标记
                if (data.Trim() == "[DONE]")
                {
                    _logger.LogDebug("收到流结束标记 [DONE]");
                    yield break;
                }

                // 解析 JSON
                OpenAIStreamChunk? streamChunk;
                try
                {
                    streamChunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data, _jsonOptions);
                }
                catch (JsonException ex)
                {
                    // JSON 解析错误：记录警告并跳过该 chunk（不中断流）
                    _logger.LogWarning(ex, "无法解析流式响应 chunk，跳过: {Data}", data);
                    continue;
                }

                if (streamChunk is null)
                {
                    _logger.LogWarning("流式响应 chunk 为空，跳过");
                    continue;
                }

                // 转换为 StreamChunk
                var chunk = ToStreamChunk(streamChunk);
                if (chunk is not null)
                {
                    yield return chunk;
                }
            }
        }
    }

    /// <summary>
    /// 将 OpenAIStreamChunk DTO 转换为 StreamChunk Domain Model
    /// </summary>
    /// <param name="streamChunk">OpenAI 流式响应块</param>
    /// <returns>StreamChunk 或 null（如果内容为空）</returns>
    private StreamChunk? ToStreamChunk(OpenAIStreamChunk streamChunk)
    {
        // 验证 Choices 不为空
        if (streamChunk.Choices.Count == 0)
        {
            _logger.LogWarning("流式响应 chunk 的 Choices 为空，跳过");
            return null;
        }

        var firstChoice = streamChunk.Choices[0];
        var delta = firstChoice.Delta;

        // 跳过空内容的 chunk
        if (string.IsNullOrEmpty(delta.Content))
        {
            // 如果有 finish_reason，说明流结束
            if (!string.IsNullOrEmpty(firstChoice.FinishReason))
            {
                return new StreamChunk
                {
                    Delta = string.Empty,
                    IsComplete = true,
                    Usage = null // 流式响应通常不提供 token 使用统计
                };
            }

            // 空内容且未结束，跳过
            return null;
        }

        // 检查是否为流的结束
        var isComplete = !string.IsNullOrEmpty(firstChoice.FinishReason);

        return new StreamChunk
        {
            Delta = delta.Content,
            IsComplete = isComplete,
            Usage = null // 流式响应通常不提供 token 使用统计
        };
    }
}
