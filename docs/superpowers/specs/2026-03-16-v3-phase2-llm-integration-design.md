# V3 Phase 2: LLM Integration 设计文档

**日期**: 2026-03-16
**版本**: 1.0
**状态**: Approved
**前置条件**: Phase 1 (Core + Storage) 已完成

---

## 目录

- [概述](#概述)
- [设计决策](#设计决策)
- [整体架构](#整体架构)
- [Core 层扩展](#core-层扩展)
- [Infrastructure.LLM 层](#infrastructurellm-层)
- [Application 层](#application-层)
- [Console REPL](#console-repl)
- [测试策略](#测试策略)
- [验收标准](#验收标准)

---

## 概述

### Phase 2 目标

实现 LLM 集成，让 General Agent V3 能够与本地 LLM 平台（Ollama、LM Studio、llama.cpp、OMLX）进行对话。

**核心功能**：
1. 统一的 LLM 客户端接口
2. 支持多个本地模型平台（OpenAI 兼容 API）
3. 非流式和流式响应
4. Application 层业务逻辑（SessionService、ConversationService）
5. 交互式 Console REPL（支持命令和对话）

**交付物**：
- GeneralAgent.Core（新增 LLM 接口和模型）
- GeneralAgent.Infrastructure.LLM（新项目）
- GeneralAgent.Application（新项目）
- GeneralAgent.Hosts.Console（升级为 REPL）
- 完整测试套件（单元测试 + 集成测试）

### 非目标（留待后续）

- Anthropic/OpenAI 等云服务 API（Phase 3 或 4）
- 技能系统集成（Phase 3）
- MCP 协议支持（Phase 4+）
- RAG 检索（Phase 4+）
- TUI 界面（Phase 4+）

---

## 设计决策

### 决策 1: 优先支持本地模型平台

**选择**: Ollama 优先，统一 OpenAI 兼容 API

**理由**：
- 本地模型便于开发测试，无需 API Key
- Ollama、LM Studio、llama.cpp、OMLX 都兼容 OpenAI API 格式
- 统一客户端实现，代码复用率高（90%+）
- 添加新平台只需配置，无需额外代码

**权衡**：
- ✅ 快速开发，易于扩展
- ✅ 配置灵活，支持多平台
- ⚠️ 平台特有功能需要后续扩展

### 决策 2: 多提供商同时配置，运行时选择

**选择**: 配置文件默认提供商 + 命令行参数覆盖

**理由**：
- 日常使用方便（默认提供商）
- 测试灵活（命令行临时切换）
- 高级用户可以按需切换

**实现**：
```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": { "BaseUrl": "http://localhost:11434", ... },
      "LMStudio": { "BaseUrl": "http://localhost:1234", ... }
    }
  }
}
```

```bash
dotnet run --provider=LMStudio
```

### 决策 3: SessionService = CRUD，ConversationService = 对话逻辑

**选择**: 清晰的职责分离

**理由**：
- SessionService 专注数据持久化
- ConversationService 专注业务编排
- 符合单一职责原则
- 便于独立测试

**交互流程**：
```
ConversationService.SendMessageAsync()
  → SessionService.AddMessageAsync() [保存用户消息]
  → ILLMClient.CompleteAsync() [调用 LLM]
  → SessionService.AddMessageAsync() [保存助手消息]
```

### 决策 4: 同时实现流式和非流式，Console 先用非流式

**选择**: 两种模式都实现，分阶段应用

**理由**：
- 接口完整性（`ILLMClient` 定义两个方法）
- 非流式便于初期验证
- 流式为后续 TUI 准备

**实现时序**：
1. Phase 2 前期：实现两种模式，Console 用非流式
2. Phase 2 后期：Console 支持流式显示（可选）
3. Phase 4 TUI：充分利用流式响应

### 决策 5: Mock 为主 + 少量真实调用

**选择**: 单元测试用 Mock，集成测试可选

**理由**：
- 单元测试快速可靠
- 开发时不依赖外部服务
- 集成测试标记为 `[Trait("Category", "Integration")]`
- CI/CD 可选择是否运行集成测试

---

## 整体架构

### 项目依赖关系

```
GeneralAgent.Hosts.Console
    ↓ 依赖
GeneralAgent.Application
    ↓ 依赖 ↓ 依赖
Infrastructure.LLM   Infrastructure (Storage)
    ↓ 依赖          ↓ 依赖
GeneralAgent.Core
```

### 新增项目

**GeneralAgent.Infrastructure.LLM**：
- 职责：LLM 客户端实现
- 依赖：Core、HttpClient、System.Text.Json
- 输出：ILLMClient 实现、ILLMClientFactory

**GeneralAgent.Application**：
- 职责：业务逻辑编排
- 依赖：Core、Infrastructure（Storage + LLM）
- 输出：SessionService、ConversationService

### 目录结构

```
v3/
├── src/
│   ├── GeneralAgent.Core/
│   │   ├── Abstractions/
│   │   │   ├── ISessionRepository.cs (Phase 1)
│   │   │   ├── IMessageRepository.cs (Phase 1)
│   │   │   ├── ILLMClient.cs (Phase 2 新增)
│   │   │   └── ILLMClientFactory.cs (Phase 2 新增)
│   │   ├── Models/
│   │   │   ├── Session.cs (Phase 1)
│   │   │   ├── Message.cs (Phase 1)
│   │   │   ├── CompletionRequest.cs (Phase 2 新增)
│   │   │   ├── CompletionResponse.cs (Phase 2 新增)
│   │   │   ├── StreamChunk.cs (Phase 2 新增)
│   │   │   └── TokenUsage.cs (Phase 2 新增)
│   │   ├── Exceptions/
│   │   │   ├── AgentException.cs (Phase 1)
│   │   │   ├── StorageException.cs (Phase 1)
│   │   │   └── LLMException.cs (Phase 2 新增)
│   │   └── Common/
│   │       ├── Result.cs (Phase 1)
│   │       └── PagedResult.cs (Phase 1)
│   │
│   ├── GeneralAgent.Infrastructure/
│   │   └── Storage/ (Phase 1)
│   │
│   ├── GeneralAgent.Infrastructure.LLM/ (Phase 2 新增)
│   │   ├── OpenAICompatibleClient.cs
│   │   ├── LLMClientFactory.cs
│   │   ├── Models/
│   │   │   ├── OpenAIChatRequest.cs
│   │   │   ├── OpenAIChatResponse.cs
│   │   │   ├── OpenAIMessage.cs
│   │   │   └── OpenAIStreamChunk.cs
│   │   ├── LLMOptions.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── GeneralAgent.Application/ (Phase 2 新增)
│   │   ├── Services/
│   │   │   ├── SessionService.cs
│   │   │   └── ConversationService.cs
│   │   └── DependencyInjection.cs
│   │
│   └── GeneralAgent.Hosts.Console/
│       ├── Program.cs (Phase 2 重写)
│       ├── AgentRepl.cs (Phase 2 新增)
│       └── appsettings.json (Phase 2 扩展)
│
└── tests/
    ├── GeneralAgent.Core.Tests/ (Phase 1，Phase 2 新增 LLM 模型测试)
    ├── GeneralAgent.Infrastructure.Tests/ (Phase 1)
    ├── GeneralAgent.Infrastructure.LLM.Tests/ (Phase 2 新增)
    ├── GeneralAgent.Application.Tests/ (Phase 2 新增)
    └── GeneralAgent.Integration.Tests/ (Phase 2 新增)
```

---

## Core 层扩展

### 新增接口

#### ILLMClient

```csharp
// Core/Abstractions/ILLMClient.cs
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// LLM 客户端接口
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// 提供商名称（如 "Ollama", "LMStudio"）
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 非流式补全
    /// </summary>
    /// <param name="request">补全请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>补全响应</returns>
    /// <exception cref="LLMException">LLM 调用失败</exception>
    Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 流式补全
    /// </summary>
    /// <param name="request">补全请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应块</returns>
    /// <exception cref="LLMException">LLM 调用失败</exception>
    IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken ct = default);
}
```

#### ILLMClientFactory

```csharp
// Core/Abstractions/ILLMClientFactory.cs
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// LLM 客户端工厂，支持多提供商管理
/// </summary>
public interface ILLMClientFactory
{
    /// <summary>
    /// 获取指定提供商的客户端
    /// </summary>
    /// <param name="providerName">提供商名称</param>
    /// <returns>LLM 客户端实例</returns>
    /// <exception cref="LLMException">提供商未配置</exception>
    ILLMClient GetClient(string providerName);

    /// <summary>
    /// 获取所有已配置的提供商名称
    /// </summary>
    /// <returns>提供商名称列表</returns>
    IReadOnlyList<string> GetAvailableProviders();
}
```

### 新增模型

#### CompletionRequest

```csharp
// Core/Models/CompletionRequest.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// LLM 补全请求
/// </summary>
public sealed record CompletionRequest
{
    /// <summary>
    /// 模型名称（如 "llama3.2", "mistral"）
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 对话历史消息
    /// </summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>
    /// 系统提示词（可选）
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 温度参数（0.0-2.0，默认 0.7）
    /// 控制响应的随机性，越高越随机
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// 最大生成 token 数（可选）
    /// </summary>
    public int? MaxTokens { get; init; }
}
```

#### CompletionResponse

```csharp
// Core/Models/CompletionResponse.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// LLM 补全响应
/// </summary>
public sealed record CompletionResponse
{
    /// <summary>
    /// 生成的内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Token 使用统计
    /// </summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>
    /// 实际使用的模型名称
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 响应时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

#### TokenUsage

```csharp
// Core/Models/TokenUsage.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// Token 使用统计
/// </summary>
public sealed record TokenUsage
{
    /// <summary>
    /// 提示词 token 数
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// 生成内容 token 数
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// 总 token 数
    /// </summary>
    public int TotalTokens { get; init; }
}
```

#### StreamChunk

```csharp
// Core/Models/StreamChunk.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// 流式响应块
/// </summary>
public sealed record StreamChunk
{
    /// <summary>
    /// 本次流式返回的内容片段
    /// </summary>
    public required string Delta { get; init; }

    /// <summary>
    /// 是否为流的结束
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Token 使用统计（仅在 IsComplete = true 时有值）
    /// </summary>
    public TokenUsage? Usage { get; init; }
}
```

### 新增异常

#### LLMException

```csharp
// Core/Exceptions/LLMException.cs
namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// LLM 调用异常
/// </summary>
public class LLMException : AgentException
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public LLMErrorType ErrorType { get; }

    public LLMException(
        string message,
        string? providerName = null,
        LLMErrorType errorType = LLMErrorType.Unknown,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        ErrorType = errorType;
    }
}

/// <summary>
/// LLM 错误类型
/// </summary>
public enum LLMErrorType
{
    /// <summary>
    /// 网络连接失败
    /// </summary>
    NetworkError,

    /// <summary>
    /// 请求超时
    /// </summary>
    TimeoutError,

    /// <summary>
    /// 认证失败
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// 模型不存在
    /// </summary>
    ModelNotFound,

    /// <summary>
    /// 速率限制
    /// </summary>
    RateLimitError,

    /// <summary>
    /// 服务器错误
    /// </summary>
    ServerError,

    /// <summary>
    /// 未知错误
    /// </summary>
    Unknown
}
```

---

## Infrastructure.LLM 层

### OpenAICompatibleClient

```csharp
// Infrastructure.LLM/OpenAICompatibleClient.cs
namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// OpenAI 兼容 API 客户端
/// 支持 Ollama、LM Studio、llama.cpp、OMLX 等本地平台
/// </summary>
internal sealed class OpenAICompatibleClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly LLMProviderConfig _config;
    private readonly ILogger<OpenAICompatibleClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderName => _config.Name;

    public OpenAICompatibleClient(
        HttpClient httpClient,
        LLMProviderConfig config,
        ILogger<OpenAICompatibleClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 非流式补全
    /// </summary>
    public async Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug(
                "Starting completion request to {Provider} with model {Model}",
                ProviderName, request.Model);

            // 1. 构建 OpenAI 格式的请求
            var apiRequest = new OpenAIChatRequest
            {
                Model = request.Model,
                Messages = ConvertMessages(request),
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = false
            };

            // 2. 发送 HTTP 请求
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
            {
                Content = JsonContent.Create(apiRequest, options: JsonOptions)
            };

            var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
            httpResponse.EnsureSuccessStatusCode();

            // 3. 解析响应
            var apiResponse = await httpResponse.Content
                .ReadFromJsonAsync<OpenAIChatResponse>(JsonOptions, ct)
                ?? throw new LLMException(
                    "Failed to deserialize response",
                    ProviderName,
                    LLMErrorType.ServerError);

            // 4. 转换为统一格式
            var result = ConvertResponse(apiResponse);

            _logger.LogInformation(
                "Completion successful: {Provider}, tokens: {Tokens}",
                ProviderName, result.Usage.TotalTokens);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error for provider {Provider}", ProviderName);
            throw new LLMException(
                $"Network error connecting to {ProviderName}: {ex.Message}",
                ProviderName,
                LLMErrorType.NetworkError,
                ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout for provider {Provider}", ProviderName);
            throw new LLMException(
                $"Request timeout for {ProviderName} (>{_config.TimeoutSeconds}s)",
                ProviderName,
                LLMErrorType.TimeoutError,
                ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for provider {Provider}", ProviderName);
            throw new LLMException(
                $"Failed to parse response from {ProviderName}: {ex.Message}",
                ProviderName,
                LLMErrorType.ServerError,
                ex);
        }
        catch (Exception ex) when (ex is not LLMException)
        {
            _logger.LogError(ex, "Unexpected error for provider {Provider}", ProviderName);
            throw new LLMException(
                $"Unexpected error: {ex.Message}",
                ProviderName,
                LLMErrorType.Unknown,
                ex);
        }
    }

    /// <summary>
    /// 流式补全
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Starting streaming request to {Provider} with model {Model}",
            ProviderName, request.Model);

        // 1. 构建流式请求
        var apiRequest = new OpenAIChatRequest
        {
            Model = request.Model,
            Messages = ConvertMessages(request),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(apiRequest, options: JsonOptions)
        };

        HttpResponseMessage? httpResponse = null;
        Stream? stream = null;
        StreamReader? reader = null;

        try
        {
            // 2. 发送请求（立即返回，不等待完整响应）
            httpResponse = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            httpResponse.EnsureSuccessStatusCode();

            // 3. 读取 SSE 流
            stream = await httpResponse.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var data = line[6..]; // 去掉 "data: " 前缀

                // OpenAI 格式的流结束标记
                if (data == "[DONE]")
                {
                    yield return new StreamChunk
                    {
                        Delta = "",
                        IsComplete = true
                    };
                    yield break;
                }

                // 解析流式数据块
                var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data, JsonOptions);
                if (chunk?.Choices is { Count: > 0 })
                {
                    var delta = chunk.Choices[0].Delta?.Content;
                    if (delta is not null)
                    {
                        yield return new StreamChunk
                        {
                            Delta = delta,
                            IsComplete = false
                        };
                    }

                    // 检查是否完成
                    if (chunk.Choices[0].FinishReason is not null)
                    {
                        yield return new StreamChunk
                        {
                            Delta = "",
                            IsComplete = true,
                            Usage = chunk.Usage is not null
                                ? new TokenUsage
                                {
                                    PromptTokens = chunk.Usage.PromptTokens,
                                    CompletionTokens = chunk.Usage.CompletionTokens,
                                    TotalTokens = chunk.Usage.TotalTokens
                                }
                                : null
                        };
                        yield break;
                    }
                }
            }
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
            httpResponse?.Dispose();
        }
    }

    /// <summary>
    /// 转换消息格式
    /// </summary>
    private List<OpenAIMessage> ConvertMessages(CompletionRequest request)
    {
        var messages = new List<OpenAIMessage>();

        // 系统提示词
        if (request.SystemPrompt is not null)
        {
            messages.Add(new OpenAIMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        // 对话历史
        foreach (var msg in request.Messages)
        {
            messages.Add(new OpenAIMessage
            {
                Role = msg.Role.ToString().ToLowerInvariant(),
                Content = msg.Content
            });
        }

        return messages;
    }

    /// <summary>
    /// 转换响应格式
    /// </summary>
    private CompletionResponse ConvertResponse(OpenAIChatResponse apiResponse)
    {
        var content = apiResponse.Choices?[0]?.Message?.Content
            ?? throw new LLMException(
                "Invalid response format: missing content",
                ProviderName,
                LLMErrorType.ServerError);

        return new CompletionResponse
        {
            Content = content,
            Model = apiResponse.Model,
            Usage = new TokenUsage
            {
                PromptTokens = apiResponse.Usage?.PromptTokens ?? 0,
                CompletionTokens = apiResponse.Usage?.CompletionTokens ?? 0,
                TotalTokens = apiResponse.Usage?.TotalTokens ?? 0
            }
        };
    }
}
```

### LLMClientFactory

```csharp
// Infrastructure.LLM/LLMClientFactory.cs
namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// LLM 客户端工厂实现
/// </summary>
internal sealed class LLMClientFactory : ILLMClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LLMOptions _options;
    private readonly ILogger<LLMClientFactory> _logger;
    private readonly Dictionary<string, ILLMClient> _clients = new();

    public LLMClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<LLMOptions> options,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<LLMClientFactory>();

        // 预创建所有配置的客户端
        InitializeClients(loggerFactory);
    }

    public ILLMClient GetClient(string providerName)
    {
        if (_clients.TryGetValue(providerName, out var client))
        {
            _logger.LogDebug("Retrieved LLM client for provider: {Provider}", providerName);
            return client;
        }

        var available = string.Join(", ", _clients.Keys);
        throw new LLMException(
            $"Provider '{providerName}' not configured. Available providers: {available}",
            providerName);
    }

    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _clients.Keys.ToList();
    }

    private void InitializeClients(ILoggerFactory loggerFactory)
    {
        foreach (var (name, config) in _options.Providers)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient($"LLM_{name}");
                var logger = loggerFactory.CreateLogger<OpenAICompatibleClient>();

                _clients[name] = new OpenAICompatibleClient(httpClient, config, logger);

                _logger.LogInformation(
                    "Initialized LLM client: {Provider} at {BaseUrl}",
                    name, config.BaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to initialize LLM client for provider: {Provider}",
                    name);
            }
        }

        if (_clients.Count == 0)
        {
            throw new LLMException("No LLM providers configured successfully");
        }
    }
}
```

### 内部 DTO 模型

```csharp
// Infrastructure.LLM/Models/OpenAIChatRequest.cs
namespace GeneralAgent.Infrastructure.LLM.Models;

internal sealed record OpenAIChatRequest
{
    public required string Model { get; init; }
    public required List<OpenAIMessage> Messages { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int? MaxTokens { get; init; }
    public bool Stream { get; init; }
}

internal sealed record OpenAIMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

// Infrastructure.LLM/Models/OpenAIChatResponse.cs
internal sealed record OpenAIChatResponse
{
    public string? Id { get; init; }
    public string? Model { get; init; }
    public List<OpenAIChoice>? Choices { get; init; }
    public OpenAIUsage? Usage { get; init; }
}

internal sealed record OpenAIChoice
{
    public int Index { get; init; }
    public OpenAIMessage? Message { get; init; }
    public OpenAIDelta? Delta { get; init; }
    public string? FinishReason { get; init; }
}

internal sealed record OpenAIDelta
{
    public string? Content { get; init; }
}

internal sealed record OpenAIUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
}

// Infrastructure.LLM/Models/OpenAIStreamChunk.cs
internal sealed record OpenAIStreamChunk
{
    public List<OpenAIChoice>? Choices { get; init; }
    public OpenAIUsage? Usage { get; init; }
}
```

### 配置模型

```csharp
// Infrastructure.LLM/LLMOptions.cs
namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// LLM 配置选项
/// </summary>
public sealed class LLMOptions
{
    /// <summary>
    /// 默认使用的提供商
    /// </summary>
    public string DefaultProvider { get; set; } = "Ollama";

    /// <summary>
    /// 配置的提供商列表
    /// </summary>
    public Dictionary<string, LLMProviderConfig> Providers { get; set; } = new();
}

/// <summary>
/// LLM 提供商配置
/// </summary>
public sealed class LLMProviderConfig
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// API 基础 URL
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// 默认模型名称
    /// </summary>
    public string DefaultModel { get; set; } = "";

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
```

### 依赖注入配置

```csharp
// Infrastructure.LLM/DependencyInjection.cs
namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// Infrastructure.LLM 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 LLM 基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLLMInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));

        // 为每个提供商注册 HttpClient
        var llmOptions = configuration.GetSection("LLM").Get<LLMOptions>();
        if (llmOptions?.Providers is not null)
        {
            foreach (var providerName in llmOptions.Providers.Keys)
            {
                services.AddHttpClient($"LLM_{providerName}");
            }
        }

        // 注册工厂（单例）
        services.AddSingleton<ILLMClientFactory, LLMClientFactory>();

        return services;
    }
}
```

---

## Application 层

### SessionService

```csharp
// Application/Services/SessionService.cs
namespace GeneralAgent.Application.Services;

/// <summary>
/// 会话管理服务
/// 负责会话和消息的 CRUD 操作
/// </summary>
public sealed class SessionService
{
    private readonly ISessionRepository _sessionRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionRepository sessionRepo,
        IMessageRepository messageRepo,
        ILogger<SessionService> logger)
    {
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        string? title = null,
        Guid? parentId = null,
        CancellationToken ct = default)
    {
        var session = Session.Create(title, parentId);
        await _sessionRepo.CreateAsync(session, ct);

        _logger.LogInformation(
            "Created session {SessionId} with type {Type}",
            session.Id, session.Type);

        return session;
    }

    /// <summary>
    /// 获取会话
    /// </summary>
    public async Task<Session?> GetSessionAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await _sessionRepo.GetByIdAsync(sessionId, ct);
    }

    /// <summary>
    /// 列出会话（分页）
    /// </summary>
    public async Task<PagedResult<Session>> ListSessionsAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await _sessionRepo.ListAsync(limit, offset, ct);
    }

    /// <summary>
    /// 搜索会话
    /// </summary>
    public async Task<List<Session>> SearchSessionsAsync(
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        return await _sessionRepo.SearchAsync(query, limit, ct);
    }

    /// <summary>
    /// 获取会话的所有消息
    /// </summary>
    public async Task<List<Message>> GetMessagesAsync(
        Guid sessionId,
        int? limit = null,
        CancellationToken ct = default)
    {
        if (limit.HasValue)
        {
            return await _messageRepo.GetRecentAsync(sessionId, limit.Value, ct);
        }
        return await _messageRepo.GetBySessionAsync(sessionId, ct);
    }

    /// <summary>
    /// 添加消息（内部方法，由 ConversationService 调用）
    /// </summary>
    internal async Task AddMessageAsync(
        Message message,
        CancellationToken ct = default)
    {
        await _messageRepo.CreateAsync(message, ct);

        // 更新会话的 UpdatedAt 时间戳
        var session = await _sessionRepo.GetByIdAsync(message.SessionId, ct);
        if (session is not null)
        {
            var updatedSession = session with { UpdatedAt = DateTime.UtcNow };
            await _sessionRepo.UpdateAsync(updatedSession, ct);
        }
    }

    /// <summary>
    /// 删除会话（级联删除消息）
    /// </summary>
    public async Task DeleteSessionAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        await _sessionRepo.DeleteAsync(sessionId, ct);
        _logger.LogInformation("Deleted session {SessionId}", sessionId);
    }

    /// <summary>
    /// 统计会话的消息数
    /// </summary>
    public async Task<int> CountMessagesAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await _messageRepo.CountAsync(sessionId, ct);
    }
}
```

### ConversationService

```csharp
// Application/Services/ConversationService.cs
namespace GeneralAgent.Application.Services;

/// <summary>
/// 对话编排服务
/// 负责 LLM 调用和完整的对话流程
/// </summary>
public sealed class ConversationService
{
    private readonly SessionService _sessionService;
    private readonly ILLMClientFactory _llmFactory;
    private readonly LLMOptions _llmOptions;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        SessionService sessionService,
        ILLMClientFactory llmFactory,
        IOptions<LLMOptions> llmOptions,
        ILogger<ConversationService> logger)
    {
        _sessionService = sessionService;
        _llmFactory = llmFactory;
        _llmOptions = llmOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 发送消息并获取响应（非流式）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userInput">用户输入</param>
    /// <param name="providerName">LLM 提供商（可选，默认使用配置的默认提供商）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>助手响应内容</returns>
    public async Task<string> SendMessageAsync(
        Guid sessionId,
        string userInput,
        string? providerName = null,
        CancellationToken ct = default)
    {
        // 1. 验证会话存在
        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
        {
            throw new AgentException($"Session {sessionId} not found");
        }

        // 2. 保存用户消息
        var userMessage = Message.CreateUser(sessionId, userInput);
        await _sessionService.AddMessageAsync(userMessage, ct);

        _logger.LogInformation(
            "User message added to session {SessionId}: {Preview}",
            sessionId,
            userInput.Length > 50 ? userInput[..50] + "..." : userInput);

        try
        {
            // 3. 获取 LLM 客户端
            var provider = providerName ?? _llmOptions.DefaultProvider;
            var llmClient = _llmFactory.GetClient(provider);

            // 4. 构建对话上下文
            var messages = await _sessionService.GetMessagesAsync(sessionId, ct: ct);
            var providerConfig = _llmOptions.Providers[provider];

            var request = new CompletionRequest
            {
                Model = providerConfig.DefaultModel,
                Messages = messages,
                Temperature = 0.7
            };

            // 5. 调用 LLM
            _logger.LogInformation(
                "Calling LLM: provider={Provider}, model={Model}, messages={Count}",
                provider, providerConfig.DefaultModel, messages.Count);

            var response = await llmClient.CompleteAsync(request, ct);

            // 6. 保存助手响应
            var assistantMessage = Message.CreateAssistant(sessionId, response.Content);
            await _sessionService.AddMessageAsync(assistantMessage, ct);

            _logger.LogInformation(
                "Assistant response added: session={SessionId}, tokens={Tokens}, length={Length}",
                sessionId, response.Usage.TotalTokens, response.Content.Length);

            return response.Content;
        }
        catch (LLMException ex)
        {
            _logger.LogError(
                ex,
                "LLM call failed: session={SessionId}, provider={Provider}",
                sessionId, providerName);
            throw;
        }
    }

    /// <summary>
    /// 发送消息并流式返回响应
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userInput">用户输入</param>
    /// <param name="providerName">LLM 提供商（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应块</returns>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        Guid sessionId,
        string userInput,
        string? providerName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 验证会话并保存用户消息
        var session = await _sessionService.GetSessionAsync(sessionId, ct);
        if (session is null)
        {
            throw new AgentException($"Session {sessionId} not found");
        }

        var userMessage = Message.CreateUser(sessionId, userInput);
        await _sessionService.AddMessageAsync(userMessage, ct);

        _logger.LogInformation(
            "Starting streaming for session {SessionId}: {Preview}",
            sessionId,
            userInput.Length > 50 ? userInput[..50] + "..." : userInput);

        // 2. 获取 LLM 客户端
        var provider = providerName ?? _llmOptions.DefaultProvider;
        var llmClient = _llmFactory.GetClient(provider);

        // 3. 构建对话上下文
        var messages = await _sessionService.GetMessagesAsync(sessionId, ct: ct);
        var providerConfig = _llmOptions.Providers[provider];

        var request = new CompletionRequest
        {
            Model = providerConfig.DefaultModel,
            Messages = messages,
            Temperature = 0.7
        };

        // 4. 流式调用 LLM
        var fullContent = new StringBuilder();
        TokenUsage? finalUsage = null;

        try
        {
            await foreach (var chunk in llmClient.StreamAsync(request, ct))
            {
                if (!string.IsNullOrEmpty(chunk.Delta))
                {
                    fullContent.Append(chunk.Delta);
                    yield return chunk.Delta;
                }

                if (chunk.IsComplete)
                {
                    finalUsage = chunk.Usage;

                    // 5. 保存完整的助手响应
                    var assistantMessage = Message.CreateAssistant(
                        sessionId,
                        fullContent.ToString());
                    await _sessionService.AddMessageAsync(assistantMessage, ct);

                    _logger.LogInformation(
                        "Streaming completed: session={SessionId}, tokens={Tokens}, length={Length}",
                        sessionId,
                        finalUsage?.TotalTokens ?? 0,
                        fullContent.Length);
                }
            }
        }
        catch (LLMException ex)
        {
            _logger.LogError(
                ex,
                "Streaming failed: session={SessionId}, provider={Provider}",
                sessionId, provider);
            throw;
        }
    }
}
```

### 依赖注入配置

```csharp
// Application/DependencyInjection.cs
namespace GeneralAgent.Application;

/// <summary>
/// Application 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Application 服务
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // 注册应用服务
        services.AddScoped<SessionService>();
        services.AddScoped<ConversationService>();

        return services;
    }
}
```

---

## Console REPL

### Program.cs

```csharp
// Hosts.Console/Program.cs
using GeneralAgent.Application;
using GeneralAgent.Application.Services;
using GeneralAgent.Infrastructure;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// 解析命令行参数
var providerArg = args.FirstOrDefault(a =>
    a.StartsWith("--provider=") || a.StartsWith("-p="));
var provider = providerArg?.Split('=', 2)[1];

// 构建 Host
var builder = Host.CreateApplicationBuilder(args);

// 配置服务
var connectionString = builder.Configuration.GetConnectionString("AgentDb")
    ?? "Data Source=agent.db";

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddLLMInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var host = builder.Build();

// 应用数据库迁移（确保数据库存在）
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// 启动 REPL
var repl = new AgentRepl(
    host.Services.GetRequiredService<SessionService>(),
    host.Services.GetRequiredService<ConversationService>(),
    host.Services.GetRequiredService<ILLMClientFactory>(),
    host.Services.GetRequiredService<ILogger<AgentRepl>>(),
    builder.Configuration);

await repl.RunAsync(provider);

return 0;
```

### AgentRepl

```csharp
// Hosts.Console/AgentRepl.cs
namespace GeneralAgent.Hosts.Console;

/// <summary>
/// Agent REPL (Read-Eval-Print Loop) 实现
/// </summary>
internal sealed class AgentRepl
{
    private readonly SessionService _sessionService;
    private readonly ConversationService _conversationService;
    private readonly ILLMClientFactory _llmFactory;
    private readonly ILogger<AgentRepl> _logger;
    private readonly string _defaultProvider;

    private Session? _currentSession;
    private string? _activeProvider;

    public AgentRepl(
        SessionService sessionService,
        ConversationService conversationService,
        ILLMClientFactory llmFactory,
        ILogger<AgentRepl> logger,
        IConfiguration configuration)
    {
        _sessionService = sessionService;
        _conversationService = conversationService;
        _llmFactory = llmFactory;
        _logger = logger;
        _defaultProvider = configuration["LLM:DefaultProvider"] ?? "Ollama";
    }

    /// <summary>
    /// 运行 REPL 主循环
    /// </summary>
    public async Task RunAsync(string? provider = null)
    {
        _activeProvider = provider ?? _defaultProvider;

        // 显示欢迎信息
        ShowWelcome();

        // 自动创建初始会话
        _currentSession = await _sessionService.CreateSessionAsync("默认会话");
        AnsiConsole.MarkupLine($"[green]✓[/] Session created: {_currentSession.Id}\n");

        // REPL 主循环
        while (true)
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[blue]>[/] ")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // 处理命令
            if (input.StartsWith('/'))
            {
                var shouldExit = await HandleCommandAsync(input);
                if (shouldExit)
                {
                    break;
                }
                continue;
            }

            // 处理普通消息
            await HandleMessageAsync(input);
        }

        AnsiConsole.MarkupLine("\n[grey]Goodbye![/]");
    }

    private void ShowWelcome()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("General Agent V3")
                .Color(Color.Blue));

        var providers = _llmFactory.GetAvailableProviders();
        AnsiConsole.MarkupLine($"[grey]Provider: [green]{_activeProvider}[/][/]");
        AnsiConsole.MarkupLine($"[grey]Available: {string.Join(", ", providers)}[/]");
        AnsiConsole.MarkupLine("[grey]Type [cyan]/help[/] for commands[/]\n");
    }

    private async Task<bool> HandleCommandAsync(string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : null;

        try
        {
            switch (cmd)
            {
                case "/new":
                    await CreateNewSessionAsync(arg);
                    break;

                case "/list":
                    await ListSessionsAsync();
                    break;

                case "/switch":
                    if (arg is null)
                    {
                        AnsiConsole.MarkupLine("[red]Usage: /switch <session-id>[/]");
                    }
                    else
                    {
                        await SwitchSessionAsync(arg);
                    }
                    break;

                case "/provider":
                    if (arg is null)
                    {
                        ShowCurrentProvider();
                    }
                    else
                    {
                        SwitchProvider(arg);
                    }
                    break;

                case "/history":
                    await ShowHistoryAsync();
                    break;

                case "/help":
                    ShowHelp();
                    break;

                case "/exit":
                case "/quit":
                    return true; // 退出 REPL

                default:
                    AnsiConsole.MarkupLine($"[red]Unknown command: {cmd}[/]");
                    AnsiConsole.MarkupLine("[grey]Type /help for available commands[/]");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed: {Command}", command);
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }

        return false;
    }

    private async Task HandleMessageAsync(string input)
    {
        if (_currentSession is null)
        {
            AnsiConsole.MarkupLine("[red]No active session. Use /new to create one.[/]");
            return;
        }

        try
        {
            AnsiConsole.MarkupLine($"[grey]Thinking...[/]");

            var response = await _conversationService.SendMessageAsync(
                _currentSession.Id,
                input,
                _activeProvider);

            // 显示响应
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Assistant:[/]");
            AnsiConsole.MarkupLine(response);
            AnsiConsole.WriteLine();
        }
        catch (LLMException ex)
        {
            _logger.LogError(ex, "LLM call failed");
            AnsiConsole.MarkupLine($"[red]LLM Error ({ex.ErrorType}): {ex.Message}[/]");

            if (ex.ErrorType == LLMErrorType.NetworkError)
            {
                AnsiConsole.MarkupLine($"[grey]Hint: Is {_activeProvider} running?[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message handling failed");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private async Task CreateNewSessionAsync(string? title)
    {
        title ??= $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";
        _currentSession = await _sessionService.CreateSessionAsync(title);

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Created and switched to session: [cyan]{title}[/]");
        AnsiConsole.MarkupLine($"[grey]ID: {_currentSession.Id}[/]");
    }

    private async Task ListSessionsAsync()
    {
        var sessions = await _sessionService.ListSessionsAsync(limit: 10);

        if (sessions.Items.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No sessions found[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("ID")
            .AddColumn("Title")
            .AddColumn("Messages")
            .AddColumn("Updated");

        foreach (var session in sessions.Items)
        {
            var messageCount = await _sessionService.CountMessagesAsync(session.Id);
            var isActive = session.Id == _currentSession?.Id ? "[green]*[/] " : "  ";
            var idDisplay = isActive + session.Id.ToString()[..8] + "...";

            table.AddRow(
                idDisplay,
                session.Title ?? "[grey](no title)[/]",
                messageCount.ToString(),
                session.UpdatedAt.ToString("MM-dd HH:mm"));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Total: {sessions.Total} sessions[/]");
    }

    private async Task SwitchSessionAsync(string sessionIdStr)
    {
        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            // 尝试部分匹配
            var sessions = await _sessionService.ListSessionsAsync(limit: 50);
            var match = sessions.Items.FirstOrDefault(s =>
                s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid session ID format or session not found[/]");
                return;
            }

            sessionId = match.Id;
        }

        var session = await _sessionService.GetSessionAsync(sessionId);
        if (session is null)
        {
            AnsiConsole.MarkupLine("[red]Session not found[/]");
            return;
        }

        _currentSession = session;
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Switched to session: [cyan]{session.Title ?? "(no title)"}[/]");
    }

    private void ShowCurrentProvider()
    {
        AnsiConsole.MarkupLine(
            $"[grey]Current provider: [green]{_activeProvider}[/][/]");

        var available = _llmFactory.GetAvailableProviders();
        AnsiConsole.MarkupLine(
            $"[grey]Available providers: {string.Join(", ", available)}[/]");
    }

    private void SwitchProvider(string provider)
    {
        var available = _llmFactory.GetAvailableProviders();
        if (!available.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]Provider '{provider}' not configured[/]");
            AnsiConsole.MarkupLine(
                $"[grey]Available: {string.Join(", ", available)}[/]");
            return;
        }

        // 大小写不敏感匹配
        _activeProvider = available.First(p =>
            p.Equals(provider, StringComparison.OrdinalIgnoreCase));

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Switched to provider: [cyan]{_activeProvider}[/]");
    }

    private async Task ShowHistoryAsync()
    {
        if (_currentSession is null)
        {
            AnsiConsole.MarkupLine("[red]No active session[/]");
            return;
        }

        var messages = await _sessionService.GetMessagesAsync(_currentSession.Id);

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No messages in this session[/]");
            return;
        }

        AnsiConsole.MarkupLine(
            $"\n[cyan]Session: {_currentSession.Title ?? "Untitled"}[/]");
        AnsiConsole.MarkupLine($"[grey]{messages.Count} messages[/]\n");

        var rule = new Rule().RuleStyle("grey dim");

        foreach (var msg in messages)
        {
            var roleColor = msg.Role == MessageRole.User ? "blue" : "yellow";
            var roleText = msg.Role == MessageRole.User ? "You" : "Assistant";

            AnsiConsole.Write(rule);
            AnsiConsole.MarkupLine($"[{roleColor}]{roleText}[/] [grey dim]{msg.CreatedAt:HH:mm:ss}[/]");
            AnsiConsole.WriteLine(msg.Content);
        }

        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    private void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Command")
            .AddColumn("Description");

        table.AddRow("/new [title]", "Create a new session");
        table.AddRow("/list", "List recent sessions");
        table.AddRow("/switch <id>", "Switch to a session (supports partial ID)");
        table.AddRow("/provider [name]", "Show or switch LLM provider");
        table.AddRow("/history", "Show current session history");
        table.AddRow("/help", "Show this help");
        table.AddRow("/exit, /quit", "Exit the application");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Tip: Just type your message to chat with the AI[/]");
    }
}
```

### appsettings.json 扩展

```json
{
  "ConnectionStrings": {
    "AgentDb": "Data Source=agent.db"
  },
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "Name": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "DefaultModel": "llama3.2",
        "TimeoutSeconds": 120
      },
      "LMStudio": {
        "Name": "LMStudio",
        "BaseUrl": "http://localhost:1234",
        "DefaultModel": "default",
        "TimeoutSeconds": 120
      },
      "LlamaCpp": {
        "Name": "LlamaCpp",
        "BaseUrl": "http://localhost:8080",
        "DefaultModel": "default",
        "TimeoutSeconds": 120
      },
      "OMLX": {
        "Name": "OMLX",
        "BaseUrl": "http://localhost:8000",
        "DefaultModel": "default",
        "TimeoutSeconds": 120
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System.Net.Http": "Warning"
    }
  }
}
```

### 项目文件

```xml
<!-- Hosts.Console/GeneralAgent.Hosts.Console.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\GeneralAgent.Core\GeneralAgent.Core.csproj" />
    <ProjectReference Include="..\GeneralAgent.Infrastructure\GeneralAgent.Infrastructure.csproj" />
    <ProjectReference Include="..\GeneralAgent.Application\GeneralAgent.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Spectre.Console" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

---

## 测试策略

### 测试项目结构

```
v3/tests/
├── GeneralAgent.Core.Tests/
│   └── Models/
│       ├── CompletionRequestTests.cs (新增)
│       └── CompletionResponseTests.cs (新增)
├── GeneralAgent.Infrastructure.LLM.Tests/ (新增)
│   ├── OpenAICompatibleClientTests.cs (Mock)
│   ├── LLMClientFactoryTests.cs
│   └── OllamaIntegrationTests.cs (Integration)
├── GeneralAgent.Application.Tests/ (新增)
│   ├── SessionServiceTests.cs
│   ├── ConversationServiceTests.cs
│   └── Mocks/
│       └── MockLLMClient.cs
└── GeneralAgent.Integration.Tests/ (新增)
    └── EndToEndTests.cs
```

### MockLLMClient 实现

```csharp
// Application.Tests/Mocks/MockLLMClient.cs
namespace GeneralAgent.Application.Tests.Mocks;

/// <summary>
/// Mock LLM 客户端，用于单元测试
/// </summary>
internal sealed class MockLLMClient : ILLMClient
{
    public string ProviderName => "Mock";

    private readonly Queue<CompletionResponse> _responses = new();
    private readonly Queue<List<StreamChunk>> _streamResponses = new();

    /// <summary>
    /// 预设非流式响应
    /// </summary>
    public void EnqueueResponse(string content, int tokens = 100)
    {
        _responses.Enqueue(new CompletionResponse
        {
            Content = content,
            Usage = new TokenUsage
            {
                PromptTokens = 50,
                CompletionTokens = tokens,
                TotalTokens = 50 + tokens
            },
            Model = "mock-model"
        });
    }

    /// <summary>
    /// 预设流式响应
    /// </summary>
    public void EnqueueStreamResponse(params string[] chunks)
    {
        var streamChunks = chunks.Select((c, i) => new StreamChunk
        {
            Delta = c,
            IsComplete = i == chunks.Length - 1,
            Usage = i == chunks.Length - 1
                ? new TokenUsage
                {
                    PromptTokens = 50,
                    CompletionTokens = 50,
                    TotalTokens = 100
                }
                : null
        }).ToList();

        _streamResponses.Enqueue(streamChunks);
    }

    public Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mock response enqueued");
        }
        return Task.FromResult(_responses.Dequeue());
    }

    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_streamResponses.Count == 0)
        {
            throw new InvalidOperationException("No mock stream response enqueued");
        }

        var chunks = _streamResponses.Dequeue();
        foreach (var chunk in chunks)
        {
            await Task.Delay(10, ct); // 模拟网络延迟
            yield return chunk;
        }
    }
}
```

### 单元测试示例

```csharp
// Application.Tests/ConversationServiceTests.cs
namespace GeneralAgent.Application.Tests;

public class ConversationServiceTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ConversationService _conversationService;
    private readonly SessionService _sessionService;
    private readonly MockLLMClient _mockLLM;
    private readonly AgentDbContext _dbContext;

    public ConversationServiceTests()
    {
        var services = new ServiceCollection();

        // 配置内存数据库
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        // 注册 Infrastructure
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        // Mock LLM
        _mockLLM = new MockLLMClient();
        var mockFactory = new Mock<ILLMClientFactory>();
        mockFactory.Setup(f => f.GetClient(It.IsAny<string>())).Returns(_mockLLM);
        mockFactory.Setup(f => f.GetAvailableProviders())
            .Returns(new List<string> { "Mock" });
        services.AddSingleton(mockFactory.Object);

        // Mock 配置
        var config = new Dictionary<string, string>
        {
            ["LLM:DefaultProvider"] = "Mock",
            ["LLM:Providers:Mock:Name"] = "Mock",
            ["LLM:Providers:Mock:DefaultModel"] = "mock-model"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));

        // 注册应用服务
        services.AddLogging();
        services.AddScoped<SessionService>();
        services.AddScoped<ConversationService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AgentDbContext>();
        _sessionService = _serviceProvider.GetRequiredService<SessionService>();
        _conversationService = _serviceProvider.GetRequiredService<ConversationService>();
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.CloseConnectionAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SendMessage_ShouldSaveUserAndAssistantMessages()
    {
        // Arrange
        var session = await _sessionService.CreateSessionAsync("Test Session");
        _mockLLM.EnqueueResponse("Hello! How can I help you?");

        // Act
        var response = await _conversationService.SendMessageAsync(
            session.Id,
            "Hi there");

        // Assert
        Assert.Equal("Hello! How can I help you?", response);

        var messages = await _sessionService.GetMessagesAsync(session.Id);
        Assert.Equal(2, messages.Count);
        Assert.Equal(MessageRole.User, messages[0].Role);
        Assert.Equal("Hi there", messages[0].Content);
        Assert.Equal(MessageRole.Assistant, messages[1].Role);
        Assert.Equal("Hello! How can I help you?", messages[1].Content);
    }

    [Fact]
    public async Task SendMessage_WithInvalidSession_ShouldThrowException()
    {
        // Arrange
        var invalidSessionId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AgentException>(async () =>
            await _conversationService.SendMessageAsync(invalidSessionId, "Test"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task SendMessage_WhenLLMFails_ShouldNotSaveAssistantMessage()
    {
        // Arrange
        var session = await _sessionService.CreateSessionAsync("Test Session");

        var mockFactory = new Mock<ILLMClientFactory>();
        mockFactory.Setup(f => f.GetClient(It.IsAny<string>()))
            .Throws(new LLMException("Connection failed", "Mock", LLMErrorType.NetworkError));

        var failingService = new ConversationService(
            _sessionService,
            mockFactory.Object,
            Options.Create(new LLMOptions
            {
                DefaultProvider = "Mock",
                Providers = new() { ["Mock"] = new LLMProviderConfig { Name = "Mock", DefaultModel = "test" } }
            }),
            Mock.Of<ILogger<ConversationService>>());

        // Act & Assert
        await Assert.ThrowsAsync<LLMException>(async () =>
            await failingService.SendMessageAsync(session.Id, "Test"));

        // 只有用户消息被保存
        var messages = await _sessionService.GetMessagesAsync(session.Id);
        Assert.Single(messages);
        Assert.Equal(MessageRole.User, messages[0].Role);
    }

    [Fact]
    public async Task SendMessageStream_ShouldAccumulateAndSaveFullResponse()
    {
        // Arrange
        var session = await _sessionService.CreateSessionAsync("Test Session");
        _mockLLM.EnqueueStreamResponse("Hello", " there", "!");

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _conversationService.SendMessageStreamAsync(
            session.Id, "Hi"))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(new[] { "Hello", " there", "!" }, chunks);

        var messages = await _sessionService.GetMessagesAsync(session.Id);
        Assert.Equal(2, messages.Count);
        Assert.Equal("Hello there!", messages[1].Content);
    }

    [Fact]
    public async Task SendMessage_ShouldMaintainConversationContext()
    {
        // Arrange
        var session = await _sessionService.CreateSessionAsync("Context Test");
        _mockLLM.EnqueueResponse("Response 1");
        _mockLLM.EnqueueResponse("Response 2");

        // Act - 第一轮对话
        await _conversationService.SendMessageAsync(session.Id, "Message 1");

        // Act - 第二轮对话
        await _conversationService.SendMessageAsync(session.Id, "Message 2");

        // Assert - 消息按顺序保存
        var messages = await _sessionService.GetMessagesAsync(session.Id);
        Assert.Equal(4, messages.Count);
        Assert.Equal("Message 1", messages[0].Content);
        Assert.Equal("Response 1", messages[1].Content);
        Assert.Equal("Message 2", messages[2].Content);
        Assert.Equal("Response 2", messages[3].Content);
    }
}
```

### 集成测试示例

```csharp
// Infrastructure.LLM.Tests/OllamaIntegrationTests.cs
namespace GeneralAgent.Infrastructure.LLM.Tests;

/// <summary>
/// Ollama 集成测试（需要 Ollama 运行）
/// </summary>
[Trait("Category", "Integration")]
public class OllamaIntegrationTests
{
    private readonly ILLMClient _client;

    public OllamaIntegrationTests()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(120)
        };

        var config = new LLMProviderConfig
        {
            Name = "Ollama",
            BaseUrl = "http://localhost:11434",
            DefaultModel = "llama3.2",
            TimeoutSeconds = 120
        };

        _client = new OpenAICompatibleClient(
            httpClient,
            config,
            Mock.Of<ILogger<OpenAICompatibleClient>>());
    }

    [Fact(Skip = "Requires Ollama running locally with llama3.2 model")]
    public async Task CompleteAsync_WithRealOllama_ShouldReturnResponse()
    {
        // Arrange
        var request = new CompletionRequest
        {
            Model = "llama3.2",
            Messages = new[]
            {
                Message.CreateUser(Guid.NewGuid(), "Say 'Hello World' and nothing else")
            },
            Temperature = 0.1
        };

        // Act
        var response = await _client.CompleteAsync(request);

        // Assert
        Assert.NotEmpty(response.Content);
        Assert.Contains("Hello", response.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Usage.TotalTokens > 0);
        Assert.Equal("llama3.2", response.Model);
    }

    [Fact(Skip = "Requires Ollama running locally with llama3.2 model")]
    public async Task StreamAsync_WithRealOllama_ShouldYieldChunks()
    {
        // Arrange
        var request = new CompletionRequest
        {
            Model = "llama3.2",
            Messages = new[]
            {
                Message.CreateUser(Guid.NewGuid(), "Count from 1 to 3, one number per line")
            },
            Temperature = 0.1
        };

        // Act
        var chunks = new List<string>();
        var isComplete = false;
        TokenUsage? usage = null;

        await foreach (var chunk in _client.StreamAsync(request))
        {
            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                chunks.Add(chunk.Delta);
            }

            if (chunk.IsComplete)
            {
                isComplete = true;
                usage = chunk.Usage;
            }
        }

        // Assert
        Assert.NotEmpty(chunks);
        Assert.True(isComplete);
        Assert.NotNull(usage);
        Assert.True(usage.TotalTokens > 0);

        var fullContent = string.Join("", chunks);
        Assert.Contains("1", fullContent);
        Assert.Contains("2", fullContent);
        Assert.Contains("3", fullContent);
    }

    [Fact]
    public async Task CompleteAsync_WithInvalidUrl_ShouldThrowNetworkError()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:99999") // 无效端口
        };

        var config = new LLMProviderConfig
        {
            Name = "Invalid",
            BaseUrl = "http://localhost:99999",
            DefaultModel = "test",
            TimeoutSeconds = 5
        };

        var client = new OpenAICompatibleClient(
            httpClient,
            config,
            Mock.Of<ILogger<OpenAICompatibleClient>>());

        var request = new CompletionRequest
        {
            Model = "test",
            Messages = new[] { Message.CreateUser(Guid.NewGuid(), "Test") }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LLMException>(
            async () => await client.CompleteAsync(request));

        Assert.Equal(LLMErrorType.NetworkError, ex.ErrorType);
        Assert.Contains("Invalid", ex.ProviderName);
    }
}
```

### 测试覆盖率目标

**Core 层**（新增代码）：
- CompletionRequest/Response 模型：100%
- LLMException：80%+

**Infrastructure.LLM 层**：
- OpenAICompatibleClient：80%+
- LLMClientFactory：90%+

**Application 层**：
- SessionService：85%+
- ConversationService：85%+

**整体目标**：Phase 2 新增代码覆盖率 >= 80%

### 测试运行命令

```bash
# 快速单元测试（不需要外部依赖）
dotnet test --filter "Category!=Integration"

# 完整测试（需要 Ollama 运行）
dotnet test

# 仅运行集成测试
dotnet test --filter "Category=Integration"

# 带覆盖率报告
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## 验收标准

### 功能验收

#### 1. 基本对话功能

```bash
dotnet run
> /new "测试对话"
✓ Created and switched to session: 测试对话
> 你好
[Assistant] 你好！有什么我可以帮助你的吗？
> 我刚才说了什么？
[Assistant] 你刚才说"你好"
```

**验证点**：
- ✅ 创建会话成功
- ✅ 用户消息保存到数据库
- ✅ LLM 返回有效响应
- ✅ 助手消息保存到数据库
- ✅ 多轮对话保持上下文

#### 2. 多提供商支持

```bash
dotnet run
> /provider
Current provider: Ollama
Available providers: Ollama, LMStudio, LlamaCpp, OMLX

> /provider LMStudio
✓ Switched to provider: LMStudio

> 你好
[Assistant] (使用 LMStudio 响应)

# 或通过命令行参数
dotnet run --provider=OMLX
```

**验证点**：
- ✅ 显示所有配置的提供商
- ✅ 运行时切换提供商
- ✅ 命令行参数覆盖默认提供商
- ✅ 不同提供商独立工作

#### 3. 会话管理

```bash
> /new "会话 1"
> 第一条消息
> /new "会话 2"
> 第二条消息
> /list
┌────────────┬─────────┬──────────┬──────────────┐
│ ID         │ Title   │ Messages │ Updated      │
├────────────┼─────────┼──────────┼──────────────┤
│ * abc123...│ 会话 2  │ 2        │ 03-16 13:45  │
│   def456...│ 会话 1  │ 2        │ 03-16 13:44  │
└────────────┴─────────┴──────────┴──────────────┘

> /switch def456
✓ Switched to session: 会话 1

> /history
Session: 会话 1
2 messages
─────────────────────────
You 13:44:15
第一条消息
─────────────────────────
Assistant 13:44:17
(LLM 响应)
```

**验证点**：
- ✅ 创建多个会话
- ✅ 列出会话（分页）
- ✅ 切换会话（支持部分 ID 匹配）
- ✅ 查看会话历史
- ✅ 每个会话独立的对话上下文

#### 4. 错误处理

```bash
# Ollama 未运行
> 你好
LLM Error (NetworkError): Network error connecting to Ollama: ...
Hint: Is Ollama running?

# 无效的会话
> /switch invalid-id
Invalid session ID format or session not found

# 未配置的提供商
> /provider UnknownProvider
Provider 'UnknownProvider' not configured
Available: Ollama, LMStudio, LlamaCpp, OMLX
```

**验证点**：
- ✅ 网络错误友好提示
- ✅ 输入验证错误提示
- ✅ 配置错误友好提示
- ✅ 错误日志记录完整

#### 5. 流式响应（可选验证）

```csharp
// 单元测试验证
[Fact]
public async Task SendMessageStream_ShouldYieldChunksInOrder()
{
    var session = await _sessionService.CreateSessionAsync();
    _mockLLM.EnqueueStreamResponse("Hello", " ", "World", "!");

    var chunks = new List<string>();
    await foreach (var chunk in _conversationService.SendMessageStreamAsync(
        session.Id, "Test"))
    {
        chunks.Add(chunk);
    }

    Assert.Equal(new[] { "Hello", " ", "World", "!" }, chunks);
}
```

### 性能验收

**目标**（使用本地模型 llama3.2）：
- 非流式首次响应：< 10 秒
- 流式首字节：< 2 秒
- 数据库操作：< 100ms
- 内存占用（空闲）：< 200MB
- 内存占用（活跃）：< 500MB

### 测试验收

**测试数量目标**：
- Core 层：5+ 个测试（LLM 模型）
- Infrastructure.LLM 层：10+ 个测试（8 单元 + 2 集成）
- Application 层：15+ 个测试（SessionService 8 + ConversationService 7）
- 集成测试：5+ 个测试

**总计**：35+ 个新增测试

**覆盖率目标**：
- Core 层：100%（简单模型）
- Infrastructure.LLM 层：80%+
- Application 层：85%+
- 整体：80%+

### 可运行演示

**场景 1：基本对话**
```bash
cd v3/src/GeneralAgent.Hosts.Console
dotnet run

> 你好，介绍一下自己
[Assistant] 你好！我是一个 AI 助手...
> 我刚才问了什么？
[Assistant] 你刚才让我介绍一下自己。
```

**场景 2：多提供商切换**
```bash
# 使用默认提供商
dotnet run

# 使用指定提供商
dotnet run --provider=LMStudio
dotnet run -p=OMLX

# 运行时切换
> /provider LlamaCpp
✓ Switched to provider: LlamaCpp
> 你好
[Assistant] (使用 LlamaCpp 响应)
```

**场景 3：会话管理**
```bash
> /new "工作会话"
> 帮我写一个 C# 函数
> /new "学习会话"
> 解释一下什么是依赖注入
> /list
(显示两个会话)
> /switch <id>
> /history
(显示会话历史)
```

---

## 数据流

### 端到端流程

```
用户输入消息
    ↓
AgentRepl.HandleMessageAsync()
    ↓
ConversationService.SendMessageAsync()
    ↓
Step 1: 验证会话存在
  └─ SessionService.GetSessionAsync()
      └─ ISessionRepository.GetByIdAsync()
    ↓
Step 2: 保存用户消息
  └─ SessionService.AddMessageAsync()
      ├─ IMessageRepository.CreateAsync()
      └─ ISessionRepository.UpdateAsync() (更新时间戳)
    ↓
Step 3: 获取 LLM 客户端
  └─ ILLMClientFactory.GetClient(providerName)
      └─ 返回 OpenAICompatibleClient 实例
    ↓
Step 4: 构建对话上下文
  └─ SessionService.GetMessagesAsync()
      └─ IMessageRepository.GetBySessionAsync()
          └─ 返回完整消息历史
    ↓
Step 5: 创建补全请求
  └─ new CompletionRequest
      ├─ Model (从配置读取)
      ├─ Messages (完整历史)
      └─ Temperature (0.7)
    ↓
Step 6: 调用 LLM
  └─ OpenAICompatibleClient.CompleteAsync()
      ├─ 构建 OpenAIChatRequest
      ├─ HTTP POST to /v1/chat/completions
      ├─ 解析 OpenAIChatResponse
      └─ 转换为 CompletionResponse
    ↓
Step 7: 保存助手响应
  └─ SessionService.AddMessageAsync()
      ├─ IMessageRepository.CreateAsync()
      └─ ISessionRepository.UpdateAsync() (更新时间戳)
    ↓
Step 8: 返回响应内容
    ↓
AgentRepl 显示响应
```

### 流式数据流

```
用户输入消息
    ↓
ConversationService.SendMessageStreamAsync()
    ↓
Step 1-4: (同上，验证会话、保存消息、获取客户端、构建上下文)
    ↓
Step 5: 流式调用 LLM
  └─ OpenAICompatibleClient.StreamAsync()
      ├─ HTTP POST (ResponseHeadersRead)
      ├─ 读取 SSE 流
      └─ 逐行解析 "data: {...}"
    ↓
Step 6: 逐块返回
  ├─ yield return chunk.Delta (实时返回给调用者)
  └─ 累积完整内容到 StringBuilder
    ↓
Step 7: 流结束时保存助手响应
  └─ SessionService.AddMessageAsync(完整内容)
    ↓
调用者接收所有块
```

---

## 与 Phase 1 的集成

### 复用 Phase 1 成果

**直接复用**：
- ✅ Session 和 Message 模型
- ✅ ISessionRepository 和 IMessageRepository 接口
- ✅ SessionRepository 和 MessageRepository 实现
- ✅ AgentDbContext 和数据库迁移
- ✅ Result 和 PagedResult 类型
- ✅ 异常基类（AgentException、StorageException）

**扩展**：
- ✅ Core.Abstractions：新增 ILLMClient、ILLMClientFactory
- ✅ Core.Models：新增 CompletionRequest、CompletionResponse、StreamChunk
- ✅ Core.Exceptions：新增 LLMException
- ✅ Console 应用：从简单验证升级为 REPL

### 数据库兼容性

**无需修改数据库**：
- Phase 2 不改变 Session 和 Message 的数据结构
- 使用现有的 agent.db 文件
- 无需新的迁移

**数据库使用**：
- ConversationService 通过 SessionService 访问数据库
- SessionService 通过 Repository 接口访问数据库
- 保持 Phase 1 的分层架构

---

## 为 Phase 3 预留的扩展点

### 技能系统集成

ConversationService 预留技能处理接口：
```csharp
public sealed class ConversationService
{
    private readonly ISkillService? _skillService; // Phase 3 注入

    private async Task<string> ProcessSkillInvocationAsync(
        string input,
        CancellationToken ct)
    {
        if (_skillService is null)
        {
            return input; // Phase 2：直接返回
        }

        // Phase 3：检测 @skill 或 /skill 语法并执行
        return await _skillService.ProcessAsync(input, ct);
    }
}
```

### 流式显示增强

Console REPL 预留流式显示：
```csharp
// Phase 2：非流式
var response = await _conversationService.SendMessageAsync(...);
AnsiConsole.MarkupLine(response);

// Phase 3/4：流式（Spectre.Console 实时显示）
await foreach (var chunk in _conversationService.SendMessageStreamAsync(...))
{
    AnsiConsole.Markup(chunk);
}
```

### 配置热重载

DependencyInjection 预留配置刷新：
```csharp
// Phase 2：启动时加载配置
services.Configure<LLMOptions>(configuration.GetSection("LLM"));

// Phase 4：支持热重载
services.AddOptions<LLMOptions>()
    .Bind(configuration.GetSection("LLM"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddSingleton<IOptionsMonitor<LLMOptions>>(/* ... */);
```

---

## 技术细节

### JSON 序列化配置

```csharp
// Infrastructure.LLM/OpenAICompatibleClient.cs
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false
};
```

### SSE (Server-Sent Events) 解析

OpenAI 流式格式：
```
data: {"choices":[{"delta":{"content":"Hello"}}]}

data: {"choices":[{"delta":{"content":" World"}}]}

data: [DONE]
```

解析逻辑：
```csharp
while (!reader.EndOfStream)
{
    var line = await reader.ReadLineAsync(ct);
    if (string.IsNullOrEmpty(line)) continue;
    if (!line.StartsWith("data: ")) continue;

    var data = line[6..];
    if (data == "[DONE]") yield break;

    var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data);
    // 处理 chunk...
}
```

### HttpClient 配置

```csharp
// 每个提供商独立的 HttpClient
services.AddHttpClient($"LLM_{providerName}")
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5)); // 连接池管理
```

### 日志级别

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "GeneralAgent": "Information",
      "GeneralAgent.Infrastructure.LLM": "Debug",
      "Microsoft": "Warning",
      "System.Net.Http": "Warning"
    }
  }
}
```

---

## 实施任务清单

Phase 2 预计包含以下任务（详细步骤见实施计划）：

### Chunk 1: Core 层扩展（3-4 个 Task）
- [ ] 添加 LLM 接口（ILLMClient、ILLMClientFactory）
- [ ] 添加 LLM 模型（CompletionRequest、CompletionResponse、StreamChunk、TokenUsage）
- [ ] 添加 LLMException
- [ ] 为 LLM 模型编写单元测试

### Chunk 2: Infrastructure.LLM 层（4-5 个 Task）
- [ ] 创建 Infrastructure.LLM 项目
- [ ] 实现内部 DTO 模型（OpenAI 格式）
- [ ] 实现 OpenAICompatibleClient（非流式）
- [ ] 实现 OpenAICompatibleClient（流式）
- [ ] 实现 LLMClientFactory
- [ ] 实现依赖注入扩展
- [ ] 编写单元测试和集成测试

### Chunk 3: Application 层（3-4 个 Task）
- [ ] 创建 Application 项目
- [ ] 实现 SessionService（TDD）
- [ ] 实现 ConversationService（非流式，TDD）
- [ ] 实现 ConversationService（流式，TDD）
- [ ] 实现依赖注入扩展

### Chunk 4: Console REPL（2-3 个 Task）
- [ ] 重写 Program.cs（集成所有层）
- [ ] 实现 AgentRepl（命令系统）
- [ ] 更新 appsettings.json
- [ ] 添加 Spectre.Console 依赖

### Chunk 5: 验证和测试（2-3 个 Task）
- [ ] 运行所有单元测试（验证 >= 80% 覆盖率）
- [ ] 运行集成测试（需要 Ollama）
- [ ] 端到端验证（4 个演示场景）
- [ ] 更新文档

**预计总任务数**：15-18 个 Task

---

## 总结

### Phase 2 完成后的系统能力

**核心功能**：
- ✅ 与本地 LLM 平台对话（Ollama、LM Studio、llama.cpp、OMLX）
- ✅ 多轮对话，保持上下文
- ✅ 会话管理（创建、列出、切换、删除）
- ✅ 流式和非流式响应
- ✅ 多提供商支持和运行时切换

**技术实现**：
- ✅ 统一的 LLM 客户端抽象（ILLMClient）
- ✅ OpenAI 兼容 API 客户端（支持 4+ 平台）
- ✅ Application 层业务逻辑（SessionService、ConversationService）
- ✅ 交互式 Console REPL（命令 + 对话）

**质量保证**：
- ✅ 35+ 单元测试和集成测试
- ✅ 80%+ 测试覆盖率
- ✅ Mock 测试快速运行
- ✅ 集成测试可选运行

**用户体验**：
- ✅ 美观的终端 UI（Spectre.Console）
- ✅ 友好的错误提示
- ✅ 完整的命令系统
- ✅ 灵活的配置和切换

### 下一步（Phase 3）

Phase 2 完成后，系统具备基本对话能力，可以继续：
- **Phase 3**: 技能系统（@skill 语法、Markdown 解析）
- **Phase 4**: CLI/TUI（System.CommandLine、完整 TUI）
- **Phase 5**: MCP 协议（工具调用）
- **Phase 6**: RAG 检索（向量数据库）

---

**设计文档版本**: 1.0
**创建日期**: 2026-03-16
**状态**: Approved
**下一步**: 编写实施计划
