# V3 (C#) 架构设计文档

**日期**: 2026-03-16
**版本**: 1.0
**状态**: Draft

---

## 目录

- [概述](#概述)
- [设计目标](#设计目标)
- [技术栈选择](#技术栈选择)
- [架构方案](#架构方案)
- [详细设计](#详细设计)
  - [核心层 (GeneralAgent.Core)](#核心层-generalagentcore)
  - [基础设施层 (GeneralAgent.Infrastructure)](#基础设施层-generalagentinfrastructure)
  - [应用层 (GeneralAgent.Application)](#应用层-generalagentapplication)
  - [宿主层 (GeneralAgent.Hosts)](#宿主层-generalaggenthosts)
- [验收测试策略](#验收测试策略)
- [与 V2 的对应关系](#与-v2-的对应关系)
- [实施路线图](#实施路线图)

---

## 概述

General Agent V3 是使用 C# 和 .NET 9 开发的 AI Agent 系统，是继 Python 版本和 Rust V2 版本之后的第三个实现版本。

### 为什么开发 V3?

**场景定位**:
- **60%**: 桌面应用场景 - TUI/CLI 工具，用于开发者和高级用户
- **40%**: 后台服务场景 - Windows Service/systemd daemon，自动化工作流

**版本互补关系**:
- **Python**: 快速原型、脚本自动化
- **Rust (V2)**: 高性能、系统编程、生产环境 CLI/TUI
- **C# (V3)**: 企业应用、Windows 集成、桌面应用

### 核心原则

1. **与 V2 概念一致** - 保持相同的模块划分和架构理念
2. **充分利用 .NET 生态** - Generic Host、DI、Options Pattern
3. **AOT 就绪** - 设计时考虑 Native AOT 限制
4. **增量交付** - 每个阶段可独立验收
5. **渐进式复杂度** - 简单场景不臃肿，复杂场景可扩展

---

## 设计目标

### 功能目标

1. **完整的对话管理** - 会话创建、消息存储、上下文管理
2. **多 LLM 支持** - Anthropic Claude、OpenAI、Ollama
3. **技能系统** - 加载和执行 Markdown 技能文件
4. **流式响应** - 支持流式和非流式两种模式
5. **后台服务** - 支持 Windows Service 和 Linux systemd

### 质量目标

1. **类型安全** - 利用 C# 的类型系统在编译时捕获错误
2. **高性能** - 支持 Native AOT，启动快、内存占用低
3. **可测试性** - 接口抽象，依赖注入，易于 Mock
4. **可维护性** - 清晰的代码组织，完整的文档
5. **测试覆盖** - 每个阶段 80%+ 测试覆盖率

---

## 技术栈选择

### 核心框架

**选择**: .NET 9 + C# 12+

**理由**:
- 最新性能优化（较 .NET 8 提升显著）
- 更好的 Native AOT 支持
- C# 13 新特性（更简洁的语法）
- 支持到 2025-11（足够时间验证稳定性）

**AOT 策略**: 渐进式
1. **Phase 1**: 标准 .NET 9 应用，确保功能完整
2. **Phase 2**: 逐步启用 AOT，验证兼容性
3. **Phase 3**: 发布 AOT 版本（CLI/TUI）和标准版本（GUI 备用）

### 数据持久化

**选择**: Entity Framework Core 9 + SQLite（标准模式）/ Dapper（AOT 模式）

**理由**:
- EF Core 是 .NET 标准 ORM，与 V2 的 SQLx 概念一致
- SQLite 轻量级，与 V2 保持一致
- 支持迁移和查询优化

**AOT 兼容性说明**:

EF Core 对 AOT 的支持仍然有限：
- ❌ 不支持: 动态 LINQ、Expression.Compile()
- ⚠️ 部分支持: 基本 LINQ 查询（需要 Compiled Models）
- ✅ 完全支持: 预编译查询、原始 SQL

**推荐策略**:
1. **Phase 1-3**: 使用标准 EF Core，不启用 AOT
2. **Phase 4** (AOT 优化时):
   - **选项 A**: 切换到 Dapper（轻量级 ORM，完全兼容 AOT）
   - **选项 B**: 使用 EF Core Compiled Models + 原始 SQL
   - **选项 C**: CLI/TUI 启用 AOT，Worker 保持标准模式

### LLM 客户端

**选择**: HttpClient + System.Text.Json (Source Generator)

**理由**:
- .NET 标准 HTTP 客户端
- Source Generator 模式完全兼容 AOT
- 高性能 JSON 序列化
- 与 V2 的 reqwest 概念一致

### CLI 框架

**选择**: System.CommandLine + Spectre.Console

**理由**:
- System.CommandLine: .NET 官方 CLI 框架
- Spectre.Console: 美观的终端 UI，丰富的组件
- 两者均支持现代化的命令行体验
- 对应 V2 的 clap + ratatui

### 依赖注入

**选择**: Microsoft.Extensions.DependencyInjection

**理由**:
- .NET 官方 DI 容器
- 与 ASP.NET Core 一致
- 支持生命周期管理（Singleton、Scoped、Transient）
- 完全兼容 AOT

### 测试框架

**选择**: xUnit + FluentAssertions + Moq

**理由**:
- xUnit: 现代、轻量、社区推荐
- FluentAssertions: 可读性强的断言库
- Moq: 强大的 Mock 框架

---

## 架构方案

### 方案选择: 混合架构（V2 分层 + .NET Generic Host）

我们评估了三个方案：
- **方案 A**: 直接移植 V2 架构（1:1 映射）
- **方案 B**: .NET 标准架构（Clean Architecture）
- **方案 C**: 混合架构（推荐）✅

**选择方案 C 的原因**:
1. 保持与 V2 概念一致（降低维护者认知负担）
2. 充分利用 .NET Generic Host（DI、配置、日志统一）
3. 支持 TUI 和后台服务两种场景
4. AOT 友好（避免过度反射）
5. 渐进式复杂度（简单场景不臃肿）

### 层次架构

```
┌────────────────────────────────────────────────────────┐
│  Hosts Layer (宿主层)                                   │
│  - CLI: TUI 命令行（System.CommandLine + Spectre）     │
│  - Worker: 后台服务（BackgroundService）                │
│  - Console: 简单测试工具                                │
└─────────────────────┬──────────────────────────────────┘
                      │ 依赖
┌─────────────────────┴──────────────────────────────────┐
│  Application Layer (应用层)                             │
│  - SessionService: 会话管理                             │
│  - ConversationService: 对话流程编排                    │
│  - SkillService: 技能管理                               │
│  - (后期) WorkflowEngine, SubagentManager              │
└──────────┬──────────────┬──────────────┬───────────────┘
           │              │              │ 依赖
┌──────────┴──────────────┴──────────────┴───────────────┐
│  Infrastructure Layer (基础设施层)                      │
│  - Storage: EF Core + SQLite                           │
│  - LLM: Anthropic, OpenAI, Ollama 客户端                │
│  - Skills: 技能加载和执行                               │
│  - (后期) MCP, RAG                                     │
└─────────────────────┬──────────────────────────────────┘
                      │ 依赖
┌─────────────────────┴──────────────────────────────────┐
│  Core Layer (核心层)                                    │
│  - Abstractions: 接口定义（ISessionRepository, etc.）   │
│  - Models: 领域模型（Session, Message, etc.）          │
│  - Exceptions: 自定义异常                               │
└────────────────────────────────────────────────────────┘
```

### 依赖规则

1. **向下依赖**: 上层依赖下层，下层不依赖上层
2. **依赖抽象**: 所有跨层依赖通过接口（而非具体实现）
3. **核心纯净**: Core 层无外部依赖（纯 POCO）
4. **单一职责**: 每层只负责一个关注点

---

## 详细设计

### 核心层 (GeneralAgent.Core)

#### 职责
定义系统的核心概念和抽象，**无外部依赖**（纯净的领域层）。

#### 目录结构
```
GeneralAgent.Core/
├── Abstractions/              # 接口定义
│   ├── ISessionRepository.cs
│   ├── IMessageRepository.cs
│   ├── ILLMClient.cs
│   └── ISkillRegistry.cs
├── Models/                    # 领域模型
│   ├── Session.cs
│   ├── Message.cs
│   ├── MessageRole.cs
│   └── CompletionRequest.cs
├── Exceptions/                # 自定义异常
│   ├── AgentException.cs
│   ├── StorageException.cs
│   └── LLMException.cs
└── Common/                    # 通用类型
    ├── Result.cs              # Result<T> 模式
    └── PagedResult.cs
```

#### 关键接口

```csharp
// ISessionRepository.cs - 对应 V2 的 SessionRepository trait
public interface ISessionRepository
{
    Task<Session> CreateAsync(Session session, CancellationToken ct = default);
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Session>> ListAsync(int limit, int offset, CancellationToken ct = default);
    Task<List<Session>> SearchAsync(string query, int limit, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// ILLMClient.cs - 对应 V2 的 LLMClient trait
public interface ILLMClient
{
    string ProviderName { get; }

    Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default);

    IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken ct = default);

    Task<List<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
}
```

#### 领域模型

```csharp
// Session.cs - 使用 record 实现不可变性
public sealed record Session
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public SessionType Type { get; init; } = SessionType.Normal;
    public Guid? ParentId { get; init; }  // Subagent 场景
    public SessionStatus Status { get; init; } = SessionStatus.Active;

    // 工厂方法
    public static Session Create(string? title = null, Guid? parentId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ParentId = parentId,
            Type = parentId.HasValue ? SessionType.Subagent : SessionType.Normal
        };

    // 不可变更新方法（返回新实例）
    public Session WithTitle(string? title)
        => this with { Title = title, UpdatedAt = DateTime.UtcNow };

    public Session WithStatus(SessionStatus status)
        => this with { Status = status, UpdatedAt = DateTime.UtcNow };
}

// Message.cs - 使用 record 实现不可变性
public sealed record Message
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static Message CreateUser(Guid sessionId, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

    public static Message CreateAssistant(Guid sessionId, string content, Dictionary<string, object>? metadata = null)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata
        };
}
```

#### Result 模式（函数式错误处理）

```csharp
// Result.cs - 避免异常用于控制流
public readonly record struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(string error) => Error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

#### 错误处理策略

**何时使用 Result<T> vs 异常**：

| 场景 | 使用 Result<T> | 使用异常 |
|------|---------------|---------|
| 业务规则验证失败 | ✅ | ❌ |
| 可预期的错误（如未找到） | ✅ | ❌ |
| 基础设施故障（数据库、网络） | ❌ | ✅ |
| 编程错误（null reference） | ❌ | ✅ |

**示例**：

```csharp
// ✅ 使用 Result - 业务逻辑错误
public Result<Session> ValidateSession(Session session)
{
    if (string.IsNullOrEmpty(session.Title))
        return Result<Session>.Failure("会话标题不能为空");
    return Result<Session>.Success(session);
}

// ✅ 使用异常 - 基础设施错误
public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    // EF Core 异常（DbUpdateException 等）向上抛出
    return await _context.Sessions
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.Id == id, ct);
}

// ✅ 应用层统一处理
public async Task<Result<string>> SendMessageAsync(
    SendMessageRequest request,
    CancellationToken ct = default)
{
    try
    {
        var response = await _llmClient.CompleteAsync(/* ... */);
        return Result<string>.Success(response.Content);
    }
    catch (StorageException ex)
    {
        _logger.LogError(ex, "存储错误");
        return Result<string>.Failure($"存储错误: {ex.Message}");
    }
    catch (LLMException ex)
    {
        _logger.LogError(ex, "LLM 错误");
        return Result<string>.Failure($"LLM 错误: {ex.Message}");
    }
}
```

---

### 基础设施层 (GeneralAgent.Infrastructure)

#### 职责
实现 Core 层定义的接口，处理具体技术细节。

#### 目录结构
```
GeneralAgent.Infrastructure/
├── Storage/                   # 数据持久化
│   ├── AgentDbContext.cs
│   ├── Configurations/
│   ├── Repositories/
│   └── Migrations/
├── LLM/                       # LLM 客户端
│   ├── Anthropic/
│   ├── OpenAI/
│   └── Ollama/
├── Skills/                    # 技能系统
│   ├── SkillLoader.cs
│   ├── SkillRegistry.cs
│   └── SkillExecutor.cs
└── DependencyInjection.cs     # 服务注册
```

#### Storage - EF Core 实现

```csharp
// AgentDbContext.cs
public sealed class AgentDbContext : DbContext
{
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();

    public AgentDbContext(DbContextOptions<AgentDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);
    }
}

// Repositories/SessionRepository.cs
internal sealed class SessionRepository : ISessionRepository
{
    private readonly AgentDbContext _context;

    public SessionRepository(AgentDbContext context) => _context = context;

    public async Task<Session> CreateAsync(Session session, CancellationToken ct = default)
    {
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return session;
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    // ... 其他方法
}
```

#### LLM - Anthropic 客户端

```csharp
// LLM/Anthropic/AnthropicClient.cs
internal sealed class AnthropicClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ProviderName => "Anthropic";

    public async Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken ct = default)
    {
        // 1. 构建 API 请求
        var apiRequest = BuildRequest(request);

        // 2. 发送 HTTP 请求
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(apiRequest, options: _jsonOptions)
        };
        httpRequest.Headers.Add("x-api-key", _config.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        // 3. 解析响应
        var apiResponse = await response.Content
            .ReadFromJsonAsync<AnthropicResponse>(_jsonOptions, ct);

        // 4. 转换为统一格式
        return ConvertResponse(apiResponse);
    }

    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 流式实现（SSE）
        // ...
    }
}

// AOT 友好的 JSON 序列化上下文
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicStreamChunk))]
internal partial class AnthropicJsonContext : JsonSerializerContext { }
```

#### Skills - 技能系统

```csharp
// Skills/SkillLoader.cs
public sealed class SkillLoader
{
    public async Task<List<Skill>> LoadAllAsync(CancellationToken ct = default)
    {
        var skills = new List<Skill>();
        var skillFiles = Directory.GetFiles(_basePath, "*.md", SearchOption.AllDirectories);

        foreach (var file in skillFiles)
        {
            var skill = await LoadSkillAsync(file, ct);
            skills.Add(skill);
        }

        return skills;
    }

    private async Task<Skill> LoadSkillAsync(string filePath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var (frontmatter, body) = MarkdownParser.Parse(content);
        var metadata = YamlParser.Deserialize<SkillMetadata>(frontmatter);

        return new Skill
        {
            Name = metadata.Name,
            Namespace = metadata.Namespace ?? "default",
            Description = metadata.Description,
            Parameters = metadata.Parameters ?? [],
            Template = body
        };
    }
}

// Skills/SkillExecutor.cs
public sealed class SkillExecutor
{
    public string Execute(Skill skill, Dictionary<string, object> parameters)
    {
        ValidateParameters(skill, parameters);
        var template = Template.Parse(skill.Template);
        return template.Render(parameters);
    }
}
```

#### 依赖注入

```csharp
// DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddGeneralAgentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Storage
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        // LLM - 根据配置选择提供商
        var llmProvider = configuration["LLM:Provider"] ?? "anthropic";
        switch (llmProvider.ToLowerInvariant())
        {
            case "anthropic":
                services.Configure<AnthropicConfig>(
                    configuration.GetSection("LLM:Anthropic"));
                services.AddHttpClient<ILLMClient, AnthropicClient>();
                break;
            case "ollama":
                services.Configure<OllamaConfig>(
                    configuration.GetSection("LLM:Ollama"));
                services.AddHttpClient<ILLMClient, OllamaClient>();
                break;
        }

        // Skills
        var skillsPath = configuration["Skills:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "skills");
        services.AddSingleton<SkillLoader>(sp =>
            new SkillLoader(skillsPath, sp.GetRequiredService<ILogger<SkillLoader>>()));
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<SkillExecutor>();

        return services;
    }
}
```

---

### 应用层 (GeneralAgent.Application)

#### 职责
编排业务逻辑，协调基础设施层的各个组件，实现完整的用例。

#### 目录结构
```
GeneralAgent.Application/
├── Services/
│   ├── SessionService.cs
│   ├── ConversationService.cs
│   └── SkillService.cs
├── DTOs/
│   ├── SendMessageRequest.cs
│   └── ConversationContext.cs
└── DependencyInjection.cs
```

#### SessionService - 会话管理

```csharp
// Services/SessionService.cs - 对应 V2 的 SessionManager
public sealed class SessionService
{
    private readonly ISessionRepository _sessionRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<SessionService> _logger;

    public async Task<Session> CreateSessionAsync(
        string? title = null,
        Guid? parentId = null,
        CancellationToken ct = default)
    {
        var session = Session.Create(title, parentId);
        await _sessionRepo.CreateAsync(session, ct);
        _logger.LogInformation("Created session {SessionId}", session.Id);
        return session;
    }

    public async Task<PagedResult<Session>> ListSessionsAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await _sessionRepo.ListAsync(limit, offset, ct);
    }

    public async Task AddMessageAsync(Message message, CancellationToken ct = default)
    {
        await _messageRepo.CreateAsync(message, ct);

        // 更新会话的 UpdatedAt
        var session = await _sessionRepo.GetByIdAsync(message.SessionId, ct);
        if (session is not null)
        {
            session.UpdatedAt = DateTime.UtcNow;
            await _sessionRepo.UpdateAsync(session, ct);
        }
    }

    // ... 其他方法
}
```

#### ConversationService - 对话流程

```csharp
// Services/ConversationService.cs - 对应 V2 的 ConversationFlow
public sealed class ConversationService
{
    private readonly SessionService _sessionService;
    private readonly ILLMClient _llmClient;
    private readonly SkillService _skillService;
    private readonly ConversationOptions _options;

    public async Task<string> SendMessageAsync(
        SendMessageRequest request,
        CancellationToken ct = default)
    {
        // 1. 检测并处理技能调用
        var processedContent = await ProcessSkillInvocationAsync(request.Content, ct);

        // 2. 保存用户消息
        var userMessage = Message.CreateUser(request.SessionId, processedContent);
        await _sessionService.AddMessageAsync(userMessage, ct);

        // 3. 构建上下文
        var context = await BuildContextAsync(request.SessionId, ct);

        // 4. 调用 LLM
        var llmRequest = new CompletionRequest
        {
            Messages = context.History.Select(m => new MessageDto
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            SystemPrompt = context.SystemPrompt
        };
        var response = await _llmClient.CompleteAsync(llmRequest, ct);

        // 5. 保存助手响应
        var assistantMessage = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            Role = MessageRole.Assistant,
            Content = response.Content,
            CreatedAt = DateTime.UtcNow
        };
        await _sessionService.AddMessageAsync(assistantMessage, ct);

        return response.Content;
    }

    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 流式实现
        // ...
    }
}
```

#### SubagentService - 并行任务编排

```csharp
// Services/SubagentService.cs
public sealed class SubagentService
{
    private readonly SessionService _sessionService;
    private readonly ConversationService _conversationService;
    private readonly ILogger<SubagentService> _logger;

    public SubagentService(
        SessionService sessionService,
        ConversationService conversationService,
        ILogger<SubagentService> logger)
    {
        _sessionService = sessionService;
        _conversationService = conversationService;
        _logger = logger;
    }

    // 启动并行子任务
    public async Task<List<SubagentTask>> StartParallelTasksAsync(
        Guid parentSessionId,
        List<string> taskDescriptions,
        CancellationToken ct = default)
    {
        var tasks = new List<SubagentTask>();

        foreach (var description in taskDescriptions)
        {
            // 创建子会话
            var subSession = await _sessionService.CreateSessionAsync(
                title: description,
                parentId: parentSessionId,
                ct: ct);

            // 更新父会话状态
            var parentSession = await _sessionService.LoadSessionAsync(parentSessionId, ct);
            await _sessionService.UpdateSessionAsync(
                parentSession.WithStatus(SessionStatus.Running),
                ct);

            // 启动异步任务
            var task = Task.Run(async () =>
            {
                try
                {
                    var result = await _conversationService.SendMessageAsync(new()
                    {
                        SessionId = subSession.Id,
                        Content = description
                    }, ct);

                    // 更新子会话为完成状态
                    var completedSubSession = await _sessionService.LoadSessionAsync(subSession.Id, ct);
                    await _sessionService.UpdateSessionAsync(
                        completedSubSession.WithStatus(SessionStatus.Completed),
                        ct);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Subagent task failed: {Description}", description);

                    // 更新子会话为失败状态
                    var failedSubSession = await _sessionService.LoadSessionAsync(subSession.Id, ct);
                    await _sessionService.UpdateSessionAsync(
                        failedSubSession.WithStatus(SessionStatus.Failed),
                        ct);

                    throw;
                }
            }, ct);

            tasks.Add(new SubagentTask
            {
                SessionId = subSession.Id,
                Description = description,
                Task = task
            });
        }

        return tasks;
    }

    // 获取子代理状态
    public async Task<List<SubagentStatus>> GetSubagentStatusAsync(
        Guid parentSessionId,
        CancellationToken ct = default)
    {
        // 查询所有子会话
        var allSessions = await _sessionService.ListSessionsAsync(limit: 1000, offset: 0, ct);
        var subSessions = allSessions.Items
            .Where(s => s.ParentId == parentSessionId)
            .ToList();

        return subSessions.Select(s => new SubagentStatus
        {
            SessionId = s.Id,
            Title = s.Title ?? "(untitled)",
            Status = s.Status,
            LastUpdate = s.UpdatedAt
        }).ToList();
    }

    // 等待所有子任务完成
    public async Task<List<string>> WaitForAllTasksAsync(
        List<SubagentTask> tasks,
        CancellationToken ct = default)
    {
        var results = await Task.WhenAll(tasks.Select(t => t.Task));
        return results.ToList();
    }
}

// DTOs/SubagentTask.cs
public sealed class SubagentTask
{
    public required Guid SessionId { get; init; }
    public required string Description { get; init; }
    public required Task<string> Task { get; init; }
}

// DTOs/SubagentStatus.cs
public sealed class SubagentStatus
{
    public required Guid SessionId { get; init; }
    public required string Title { get; init; }
    public required SessionStatus Status { get; init; }
    public required DateTime LastUpdate { get; init; }
}
```

#### 性能监控和可观测性

```csharp
// Application/Telemetry/AgentMetrics.cs
public sealed class AgentMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _messagesCounter;
    private readonly Histogram<double> _responseDuration;
    private readonly Counter<long> _errorsCounter;

    public AgentMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("GeneralAgent");
        _messagesCounter = _meter.CreateCounter<long>("messages_total");
        _responseDuration = _meter.CreateHistogram<double>("llm_response_duration_seconds");
        _errorsCounter = _meter.CreateCounter<long>("errors_total");
    }

    public void RecordMessage(string role)
        => _messagesCounter.Add(1, new KeyValuePair<string, object?>("role", role));

    public void RecordResponseDuration(TimeSpan duration, string provider)
        => _responseDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("provider", provider));

    public void RecordError(string errorType)
        => _errorsCounter.Add(1, new KeyValuePair<string, object?>("type", errorType));
}

// 集成到 ConversationService
public async Task<string> SendMessageAsync(SendMessageRequest request, CancellationToken ct)
{
    using var activity = ActivitySource.StartActivity("SendMessage");
    var sw = Stopwatch.StartNew();

    try
    {
        // ... 业务逻辑
        _metrics.RecordMessage("user");
        _metrics.RecordMessage("assistant");
        return response.Content;
    }
    catch (Exception ex)
    {
        _metrics.RecordError(ex.GetType().Name);
        throw;
    }
    finally
    {
        sw.Stop();
        _metrics.RecordResponseDuration(sw.Elapsed, _llmClient.ProviderName);
    }
}
```

---

### 宿主层 (GeneralAgent.Hosts)

#### 职责
提供用户交互界面，初始化应用程序。

#### 目录结构
```
GeneralAgent.Hosts/
├── CLI/                       # TUI 命令行（优先）
│   ├── Program.cs
│   ├── Commands/
│   └── appsettings.json
├── Worker/                    # 后台服务
│   ├── Program.cs
│   ├── AgentWorker.cs
│   └── appsettings.json
└── Console/                   # 简单测试工具
    └── Program.cs
```

#### CLI 宿主

```csharp
// CLI/Program.cs
var builder = Host.CreateApplicationBuilder(args);

// 配置服务
builder.Services.AddGeneralAgentCore();
builder.Services.AddGeneralAgentInfrastructure(builder.Configuration);
builder.Services.AddGeneralAgentApplication(builder.Configuration);

var host = builder.Build();

// 确保数据库已创建
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // 初始化技能系统
    var skillService = scope.ServiceProvider.GetRequiredService<SkillService>();
    var skillLoader = scope.ServiceProvider.GetRequiredService<SkillLoader>();
    await skillService.InitializeAsync(skillLoader);
}

// 构建命令行界面（System.CommandLine）
var rootCommand = new RootCommand("General Agent CLI");

var newCommand = new Command("new", "Create a new session");
var chatCommand = new Command("chat", "Start a conversation");
var listCommand = new Command("list", "List sessions");

rootCommand.AddCommand(newCommand);
rootCommand.AddCommand(chatCommand);
rootCommand.AddCommand(listCommand);

return await rootCommand.InvokeAsync(args);
```

#### Worker 宿主

```csharp
// Worker/Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGeneralAgentCore();
builder.Services.AddGeneralAgentInfrastructure(builder.Configuration);
builder.Services.AddGeneralAgentApplication(builder.Configuration);

// 注册后台服务
builder.Services.AddHostedService<AgentWorker>();

// Windows Service 支持
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "General Agent Service";
    });
}

var host = builder.Build();
await host.RunAsync();

// Worker/AgentWorker.cs
public sealed class AgentWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWorkAsync(stoppingToken);
        }
    }
}
```

---

## 验收测试策略

### 测试金字塔

```
        /\
       /  \  E2E Tests (10%)
      /____\
     /      \  Integration Tests (30%)
    /________\
   /          \  Unit Tests (60%)
  /__________\
```

### Phase 1: Core + Storage

**目标**: 会话和消息的 CRUD 操作

**验收标准**:
```csharp
[Fact]
public async Task CreateSession_ShouldPersistToDatabase()
{
    var session = await _sessionService.CreateSessionAsync("Test");
    var loaded = await _sessionService.LoadSessionAsync(session.Id);
    Assert.Equal(session.Id, loaded.Id);
}

[Fact]
public async Task AddMessage_ShouldUpdateSessionTimestamp()
{
    var session = await _sessionService.CreateSessionAsync();
    var message = Message.CreateUser(session.Id, "Hello");
    await _sessionService.AddMessageAsync(message);

    var messages = await _sessionService.GetMessagesAsync(session.Id);
    Assert.Single(messages);
}

[Fact]
public async Task ListSessions_ShouldReturnPagedResults()
{
    for (int i = 0; i < 15; i++)
        await _sessionService.CreateSessionAsync($"Session {i}");

    var result = await _sessionService.ListSessionsAsync(limit: 10, offset: 0);
    Assert.Equal(10, result.Items.Count);
    Assert.Equal(15, result.Total);
}
```

**可运行演示**:
```bash
dotnet run --project Console
# 输出: 成功创建会话、添加消息、查询列表
```

### Phase 2: LLM Integration

**目标**: 与 Anthropic/Ollama 通信

**验收标准**:
```csharp
[Fact]
public async Task CompleteAsync_ShouldReturnResponse()
{
    var request = new CompletionRequest
    {
        Messages = [new MessageDto { Role = MessageRole.User, Content = "Say hi" }]
    };

    var response = await _llmClient.CompleteAsync(request);
    Assert.NotEmpty(response.Content);
    Assert.True(response.Usage.TotalTokens > 0);
}

[Fact]
public async Task StreamAsync_ShouldYieldChunks()
{
    var chunks = new List<string>();
    await foreach (var chunk in _llmClient.StreamAsync(request))
    {
        chunks.Add(chunk.Delta);
    }
    Assert.NotEmpty(chunks);
}

[Fact]
public async Task SendMessage_ShouldPersistBothMessages()
{
    var session = await _sessionService.CreateSessionAsync();
    var response = await _conversationService.SendMessageAsync(new()
    {
        SessionId = session.Id,
        Content = "Hello"
    });

    var messages = await _sessionService.GetMessagesAsync(session.Id);
    Assert.Equal(2, messages.Count); // User + Assistant
}
```

**可运行演示**:
```bash
dotnet run --project Console
# 输入: Hello
# 输出: (LLM 实际响应)
```

### Phase 3: Skills System

**目标**: 加载和执行技能

**验收标准**:
```csharp
[Fact]
public async Task LoadSkills_ShouldParseMarkdownFiles()
{
    var skills = await _skillLoader.LoadAllAsync();
    Assert.NotEmpty(skills);
    Assert.Contains(skills, s => s.Name == "greeting");
}

[Fact]
public void ExecuteSkill_ShouldRenderTemplate()
{
    var skill = _registry.Get("greeting");
    var result = _executor.Execute(skill, new() { ["user_name"] = "Alice" });
    Assert.Contains("Alice", result);
}

[Fact]
public async Task SendMessage_WithSkillInvocation_ShouldExecuteSkill()
{
    var response = await _conversationService.SendMessageAsync(new()
    {
        SessionId = _sessionId,
        Content = "@greeting user_name='Bob'"
    });

    var messages = await _sessionService.GetMessagesAsync(_sessionId);
    Assert.Contains("Bob", messages[0].Content);
}
```

**可运行演示**:
```bash
dotnet run --project CLI -- chat <session-id>
# 输入: @greeting user_name='Alice'
# 输出: (技能渲染结果 + LLM 响应)
```

### 测试基础设施

```csharp
// 使用内存数据库 + Mock LLM
public class ConversationServiceTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AgentDbContext _dbContext;

    public ConversationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite("Data Source=:memory:"));
        services.AddSingleton<ILLMClient>(new MockLLMClient());
        services.AddGeneralAgentApplication(new ConfigurationBuilder().Build());
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AgentDbContext>();
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    // 测试方法...
}
```

---

## 与 V2 的对应关系

| V2 (Rust) | V3 (C#) | 说明 |
|-----------|---------|------|
| `agent-core` | `GeneralAgent.Core` | 核心抽象和模型 |
| `agent-storage` | `GeneralAgent.Infrastructure.Storage` | EF Core 替代 SQLx |
| `agent-llm` | `GeneralAgent.Infrastructure.LLM` | HttpClient 替代 reqwest |
| `agent-skills` | `GeneralAgent.Infrastructure.Skills` | 保持 Markdown 格式 |
| `agent-mcp` | `GeneralAgent.Infrastructure.MCP` | JSON-RPC 实现 |
| `agent-rag` | `GeneralAgent.Infrastructure.RAG` | 向量检索 |
| `agent-workflow` | `GeneralAgent.Application` | 业务逻辑编排 |
| `agent-cli` | `GeneralAgent.Hosts.CLI` | System.CommandLine |
| `agent-tui` | `GeneralAgent.Hosts.CLI` | Spectre.Console |

### 概念映射

| V2 概念 | V3 实现 |
|---------|---------|
| `trait` | `interface` |
| `Arc<dyn Trait>` | `IServiceProvider` + DI |
| `async fn` | `async Task` / `async Task<T>` |
| `Result<T, E>` | `Result<T>` / 异常 |
| `tokio::spawn` | `Task.Run` / `BackgroundService` |
| `sqlx` | `Entity Framework Core` |
| `reqwest` | `HttpClient` |
| `serde` | `System.Text.Json` |

---

## 实施路线图

### Week 1-2: Phase 1 - Core + Storage

**目标**: 基础数据层完成

**任务**:
1. 创建项目结构和解决方案
2. 实现 Core 层（接口、模型、异常）
3. 实现 Storage 层（EF Core、Repository）
4. 编写单元测试（80%+ 覆盖率）
5. 创建简单 Console 应用验证

**交付物**:
- `GeneralAgent.Core` 项目
- `GeneralAgent.Infrastructure` 项目（Storage 部分）
- `GeneralAgent.Hosts.Console` 项目
- 单元测试项目
- README 和架构文档

### Week 3-4: Phase 2 - LLM Integration

**目标**: LLM 客户端完成

**任务**:
1. 实现 Anthropic 客户端（非流式 + 流式）
2. 实现 Ollama 客户端
3. 实现 ConversationService
4. 编写集成测试
5. 更新 Console 应用支持对话

**交付物**:
- `GeneralAgent.Infrastructure.LLM` 实现
- `GeneralAgent.Application` 项目
- 集成测试
- 可对话的 Console 应用

### Week 5-6: Phase 3 - Skills System

**目标**: 技能系统完成

**任务**:
1. 实现 SkillLoader（Markdown 解析）
2. 实现 SkillRegistry 和 SkillExecutor
3. 集成到 ConversationService
4. 创建示例技能文件
5. 编写端到端测试

**交付物**:
- `GeneralAgent.Infrastructure.Skills` 实现
- 示例技能文件
- 端到端测试
- 技能系统文档

### Week 7-8: Phase 4 - CLI/TUI

**目标**: 命令行界面完成

**任务**:
1. 实现 System.CommandLine 命令
2. 集成 Spectre.Console（美化输出）
3. 实现流式显示
4. 添加配置文件支持
5. 用户体验优化

**交付物**:
- `GeneralAgent.Hosts.CLI` 项目
- 完整的 CLI 命令
- 用户文档

### Week 9-10: Phase 5 - Worker Service

**目标**: 后台服务完成

**任务**:
1. 实现 BackgroundService
2. Windows Service 支持
3. systemd 支持
4. 定时任务调度
5. 监控和日志

**交付物**:
- `GeneralAgent.Hosts.Worker` 项目
- 部署文档
- 监控指南

### Phase 6-8: MCP, RAG, Workflow（后续）

按相同模式继续开发剩余功能模块。

---

## 总结

### 核心价值

1. **与 V2 保持一致** - 降低维护认知负担
2. **充分利用 .NET 生态** - Generic Host、DI、Options
3. **支持双重场景** - TUI (60%) + Worker (40%)
4. **AOT 就绪** - 渐进式 AOT 支持
5. **增量交付** - 每阶段可独立验收

### 技术亮点

1. **.NET 9** - 最新性能和 AOT 支持
2. **EF Core 9** - 标准 ORM，Compiled Models
3. **Source Generator** - AOT 友好的序列化
4. **System.CommandLine** - 现代 CLI 框架
5. **Spectre.Console** - 美观的 TUI

### 下一步

1. ✅ **设计文档审核** - 本文档
2. 🔄 **实施计划编写** - 详细的开发任务分解
3. ⏳ **Phase 1 开发** - Core + Storage 实现
4. ⏳ **持续迭代** - 按路线图逐步交付

---

**文档版本**: 1.0
**最后更新**: 2026-03-16
**状态**: Draft - 待审核
