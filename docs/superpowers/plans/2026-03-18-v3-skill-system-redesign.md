# V3 Skill 系统重新设计 - 实施计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重新设计并实现 V3 的 skill 系统，支持 LLM 集成、隐式调用（Function Calling）、上下文感知，并建立统一的工具抽象为 MCP/RAG 集成铺路。

**Architecture:** 采用统一的 ITool 接口抽象所有工具类型，ToolRegistry 管理注册，ToolExecutor 执行单个工具，ToolCallingOrchestrator 管理 Tool Calling 循环，ConversationService 协调整体流程。职责清晰分离，遵循开放-封闭原则。

**Tech Stack:** .NET 9, C# 12+, Scriban (模板引擎), xUnit (测试), NSubstitute (Mock), EF Core 9 + SQLite

**Related Documents:**
- 设计规范: `docs/superpowers/specs/2026-03-18-v3-skill-system-redesign.md`
- 对比分析: `docs/analysis/skill-system-comparison.md`

---

## Phase 3.1: 核心抽象（2天）

### Task 1: 定义 ITool 接口和核心模型

**Files:**
- Create: `v3/src/GeneralAgent.Core/Abstractions/ITool.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ToolDefinition.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ToolExecutionContext.cs`
- Create: `v3/tests/GeneralAgent.Core.Tests/Abstractions/IToolTests.cs`

- [ ] **步骤 1: 编写 ITool 接口测试（定义契约）**

创建测试文件验证接口契约：

```csharp
// v3/tests/GeneralAgent.Core.Tests/Abstractions/IToolTests.cs
namespace GeneralAgent.Core.Tests.Abstractions;

public class IToolTests
{
    [Fact]
    public void ITool_ShouldHaveRequiredProperties()
    {
        // Arrange
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("test_tool");
        mockTool.Description.Returns("Test tool description");

        // Assert
        Assert.NotNull(mockTool.Name);
        Assert.NotNull(mockTool.Description);
    }

    [Fact]
    public async Task ITool_ExecuteAsync_ShouldReturnResult()
    {
        // Arrange
        var mockTool = Substitute.For<ITool>();
        mockTool.ExecuteAsync(
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("output"));

        // Act
        var result = await mockTool.ExecuteAsync(
            new Dictionary<string, object>(),
            new ToolExecutionContext { SessionId = Guid.NewGuid() },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("output", result.Value);
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/Abstractions/IToolTests.cs
```

预期: FAIL - ITool 类型未定义

- [ ] **步骤 3: 实现 ITool 接口**

```csharp
// v3/src/GeneralAgent.Core/Abstractions/ITool.cs
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 统一的工具接口
/// 所有工具（Skill、MCP、RAG 等）都必须实现此接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称（唯一标识）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（用于 LLM 理解工具用途）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取工具定义（供 LLM Function Calling 使用）
    /// </summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// 执行工具（非流式）
    /// </summary>
    Task<Result<string>> ExecuteAsync(
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// 执行工具（流式）
    /// </summary>
    IAsyncEnumerable<string> ExecuteStreamAsync(
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);
}
```

- [ ] **步骤 4: 实现 ToolDefinition 模型**

```csharp
// v3/src/GeneralAgent.Core/Models/ToolDefinition.cs
using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具定义（LLM Function Calling 格式）
/// </summary>
public sealed record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject InputSchema { get; init; }
}
```

- [ ] **步骤 5: 实现 ToolExecutionContext 模型**

```csharp
// v3/src/GeneralAgent.Core/Models/ToolExecutionContext.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具执行上下文
/// </summary>
public sealed record ToolExecutionContext
{
    public required Guid SessionId { get; init; }
    public string? ProviderName { get; init; }
    public IReadOnlyList<Message>? HistoryMessages { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

- [ ] **步骤 6: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Core.Tests/Abstractions/IToolTests.cs
```

预期: PASS

- [ ] **步骤 7: 提交**

```bash
git add src/GeneralAgent.Core/Abstractions/ITool.cs \
        src/GeneralAgent.Core/Models/ToolDefinition.cs \
        src/GeneralAgent.Core/Models/ToolExecutionContext.cs \
        tests/GeneralAgent.Core.Tests/Abstractions/IToolTests.cs
git commit -m "feat(v3): 定义 ITool 接口和核心模型

- 添加 ITool 统一工具接口
- 添加 ToolDefinition 用于 Function Calling
- 添加 ToolExecutionContext 执行上下文
- 添加接口契约测试"
```

---

### Task 2: 实现 ToolRegistry

**Files:**
- Create: `v3/src/GeneralAgent.Application/Services/ToolRegistry.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/ToolRegistryTests.cs`

- [ ] **步骤 1: 编写 ToolRegistry 测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/ToolRegistryTests.cs
namespace GeneralAgent.Application.Tests.Services;

public class ToolRegistryTests
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _logger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_logger);
    }

    [Fact]
    public void Register_ShouldAddTool()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test description");

        // Act
        _registry.Register(tool);

        // Assert
        var retrieved = _registry.GetTool("test_tool");
        Assert.NotNull(retrieved);
        Assert.Equal("test_tool", retrieved.Name);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void Register_ShouldOverwriteExistingTool()
    {
        // Arrange
        var tool1 = CreateMockTool("tool", "Description 1");
        var tool2 = CreateMockTool("tool", "Description 2");

        // Act
        _registry.Register(tool1);
        _registry.Register(tool2);

        // Assert
        var retrieved = _registry.GetTool("tool");
        Assert.Equal("Description 2", retrieved.Description);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void GetTool_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var tool = _registry.GetTool("non_existent");

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void GetAllTools_ShouldReturnAllRegisteredTools()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Tool 1"));
        _registry.Register(CreateMockTool("tool2", "Tool 2"));
        _registry.Register(CreateMockTool("tool3", "Tool 3"));

        // Act
        var tools = _registry.GetAllTools();

        // Assert
        Assert.Equal(3, tools.Count);
    }

    [Fact]
    public void GetToolsByNamespace_ShouldFilterCorrectly()
    {
        // Arrange
        _registry.Register(CreateMockTool("personal:greeting", "Greeting"));
        _registry.Register(CreateMockTool("personal:reminder", "Reminder"));
        _registry.Register(CreateMockTool("productivity:task", "Task"));

        // Act
        var personalTools = _registry.GetToolsByNamespace("personal");

        // Assert
        Assert.Equal(2, personalTools.Count);
        Assert.All(personalTools, t => Assert.StartsWith("personal:", t.Name));
    }

    [Fact]
    public void Unregister_ShouldRemoveTool()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool", "Test"));

        // Act
        var removed = _registry.Unregister("tool");

        // Assert
        Assert.True(removed);
        Assert.Null(_registry.GetTool("tool"));
        Assert.Equal(0, _registry.Count);
    }

    [Fact]
    public void Clear_ShouldRemoveAllTools()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Test 1"));
        _registry.Register(CreateMockTool("tool2", "Test 2"));

        // Act
        _registry.Clear();

        // Assert
        Assert.Equal(0, _registry.Count);
        Assert.Empty(_registry.GetAllTools());
    }

    private ITool CreateMockTool(string name, string description)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        return tool;
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolRegistryTests.cs
```

预期: FAIL - ToolRegistry 类型未定义

- [ ] **步骤 3: 实现 ToolRegistry**

```csharp
// v3/src/GeneralAgent.Application/Services/ToolRegistry.cs
namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具注册表
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;
    private readonly object _lock = new();

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        lock (_lock)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                _logger.LogWarning("工具 {ToolName} 已存在，将被覆盖", tool.Name);
            }

            _tools[tool.Name] = tool;
            _logger.LogDebug("注册工具: {ToolName} - {Description}",
                tool.Name, tool.Description);
        }
    }

    public void RegisterMany(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public ITool? GetTool(string name)
    {
        lock (_lock)
        {
            return _tools.GetValueOrDefault(name);
        }
    }

    public IReadOnlyList<ITool> GetAllTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList();
        }
    }

    public IReadOnlyList<ITool> GetToolsByNamespace(string namespaceName)
    {
        lock (_lock)
        {
            return _tools.Values
                .Where(t => t.Name.StartsWith($"{namespaceName}:"))
                .ToList();
        }
    }

    public bool Unregister(string name)
    {
        lock (_lock)
        {
            if (_tools.Remove(name))
            {
                _logger.LogDebug("取消注册工具: {ToolName}", name);
                return true;
            }
            return false;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _tools.Clear();
            _logger.LogInformation("清空所有工具");
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _tools.Count;
            }
        }
    }
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolRegistryTests.cs
```

预期: PASS - 所有测试通过

- [ ] **步骤 5: 提交**

```bash
git add src/GeneralAgent.Application/Services/ToolRegistry.cs \
        tests/GeneralAgent.Application.Tests/Services/ToolRegistryTests.cs
git commit -m "feat(v3): 实现 ToolRegistry 工具注册表

- 支持注册、查找、列举工具
- 线程安全（使用 lock）
- 支持命名空间过滤
- 单元测试覆盖率 100%"
```

---

### Task 3: 实现 ToolExecutor

**Files:**
- Create: `v3/src/GeneralAgent.Application/Services/ToolExecutor.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/ToolExecutorTests.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ToolCallResult.cs`

- [ ] **步骤 1: 添加 ToolCallResult 模型**

```csharp
// v3/src/GeneralAgent.Core/Models/ToolCallResult.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具调用结果
/// </summary>
public sealed record ToolCallResult
{
    public required string ToolCallId { get; init; }
    public required string ToolName { get; init; }
    public required string Content { get; init; }
    public bool IsError { get; init; }
}
```

- [ ] **步骤 2: 编写 ToolExecutor 测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/ToolExecutorTests.cs
namespace GeneralAgent.Application.Tests.Services;

public class ToolExecutorTests
{
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _executor;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutorTests()
    {
        _logger = Substitute.For<ILogger<ToolExecutor>>();
        _registry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
        _executor = new ToolExecutor(_registry, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteTool_WhenFound()
    {
        // Arrange
        var mockTool = CreateEchoTool("test_echo");
        _registry.Register(mockTool);

        var arguments = new Dictionary<string, object>
        {
            ["message"] = "Hello World"
        };

        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var result = await _executor.ExecuteAsync("test_echo", arguments, context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Hello World", result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenToolNotFound()
    {
        // Arrange
        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var result = await _executor.ExecuteAsync(
            "non_existent",
            new Dictionary<string, object>(),
            context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("工具不存在", result.Error);
    }

    [Fact]
    public async Task ExecuteManyAsync_ShouldExecuteInParallel()
    {
        // Arrange
        var mockTool = CreateEchoTool("test_echo");
        _registry.Register(mockTool);

        var toolCalls = new[]
        {
            new ToolCall
            {
                Id = "call_1",
                FunctionName = "test_echo",
                Arguments = JsonSerializer.Serialize(new { message = "First" })
            },
            new ToolCall
            {
                Id = "call_2",
                FunctionName = "test_echo",
                Arguments = JsonSerializer.Serialize(new { message = "Second" })
            }
        };

        var context = new ToolExecutionContext
        {
            SessionId = Guid.NewGuid()
        };

        // Act
        var results = await _executor.ExecuteManyAsync(toolCalls, context);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.IsError));
        Assert.Contains(results, r => r.Content.Contains("First"));
        Assert.Contains(results, r => r.Content.Contains("Second"));
    }

    private ITool CreateEchoTool(string name)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns("Echo tool");
        tool.ExecuteAsync(
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var args = callInfo.Arg<Dictionary<string, object>>();
                var message = args.GetValueOrDefault("message", "No message");
                return Task.FromResult(Result<string>.Success($"Echo: {message}"));
            });

        return tool;
    }
}
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolExecutorTests.cs
```

预期: FAIL - ToolExecutor 类型未定义

- [ ] **步骤 4: 实现 ToolExecutor**

```csharp
// v3/src/GeneralAgent.Application/Services/ToolExecutor.cs
using System.Diagnostics;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具执行器
/// </summary>
public sealed class ToolExecutor
{
    private readonly ToolRegistry _registry;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        ToolRegistry registry,
        ILogger<ToolExecutor> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<Result<string>> ExecuteAsync(
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("执行工具: {ToolName}, 参数: {Arguments}",
                toolName, JsonSerializer.Serialize(arguments));

            var tool = _registry.GetTool(toolName);
            if (tool == null)
            {
                _logger.LogWarning("工具不存在: {ToolName}", toolName);
                return Result<string>.Failure($"工具不存在: {toolName}");
            }

            var startTime = Stopwatch.GetTimestamp();
            var result = await tool.ExecuteAsync(arguments, context, ct);
            var elapsed = Stopwatch.GetElapsedTime(startTime);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "工具执行成功: {ToolName}, 耗时: {Elapsed}ms, 输出长度: {Length}",
                    toolName, elapsed.TotalMilliseconds, result.Value?.Length ?? 0);
            }
            else
            {
                _logger.LogWarning(
                    "工具执行失败: {ToolName}, 错误: {Error}",
                    toolName, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行工具异常: {ToolName}", toolName);
            return Result<string>.Failure($"执行工具异常: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tool = _registry.GetTool(toolName);
        if (tool == null)
        {
            _logger.LogWarning("工具不存在: {ToolName}", toolName);
            yield return $"❌ 工具不存在: {toolName}";
            yield break;
        }

        _logger.LogDebug("流式执行工具: {ToolName}", toolName);
        var startTime = Stopwatch.GetTimestamp();

        await foreach (var chunk in tool.ExecuteStreamAsync(arguments, context, ct))
        {
            yield return chunk;
        }

        var elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogInformation(
            "工具流式执行完成: {ToolName}, 耗时: {Elapsed}ms",
            toolName, elapsed.TotalMilliseconds);
    }

    public async Task<List<ToolCallResult>> ExecuteManyAsync(
        IEnumerable<ToolCall> toolCalls,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        var tasks = toolCalls.Select(async toolCall =>
        {
            var arguments = ParseArguments(toolCall.Arguments);
            var result = await ExecuteAsync(toolCall.FunctionName, arguments, context, ct);

            return new ToolCallResult
            {
                ToolCallId = toolCall.Id,
                ToolName = toolCall.FunctionName,
                Content = result.IsSuccess ? result.Value! : $"错误: {result.Error}",
                IsError = !result.IsSuccess
            };
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private Dictionary<string, object> ParseArguments(string argumentsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson)
                ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析工具参数失败: {Arguments}", argumentsJson);
            return new Dictionary<string, object>();
        }
    }
}
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolExecutorTests.cs
```

预期: PASS - 所有测试通过

- [ ] **步骤 6: 提交**

```bash
git add src/GeneralAgent.Core/Models/ToolCallResult.cs \
        src/GeneralAgent.Application/Services/ToolExecutor.cs \
        tests/GeneralAgent.Application.Tests/Services/ToolExecutorTests.cs
git commit -m "feat(v3): 实现 ToolExecutor 工具执行器

- 支持单个工具执行（同步/流式）
- 支持并行批量执行
- 性能监控和日志记录
- 错误处理和异常捕获
- 单元测试覆盖率 100%"
```

---

## Phase 3.2: SkillTool 实现（2天）

### Task 4: 扩展 Skill 模型

**Files:**
- Modify: `v3/src/GeneralAgent.Infrastructure.Skills/Models/Skill.cs`
- Create: `v3/src/GeneralAgent.Infrastructure.Skills/Models/ContextConfig.cs`
- Create: `v3/tests/GeneralAgent.Infrastructure.Skills.Tests/Models/SkillTests.cs`

- [ ] **步骤 1: 编写 Skill 模型扩展测试**

```csharp
// v3/tests/GeneralAgent.Infrastructure.Skills.Tests/Models/SkillTests.cs
namespace GeneralAgent.Infrastructure.Skills.Tests.Models;

public class SkillTests
{
    [Fact]
    public void Skill_ShouldSupportContextConfig()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test template",
            Parameters = new List<SkillParameter>(),
            RequiresContext = true,
            ContextConfig = new ContextConfig
            {
                MaxMessages = 10,
                Roles = new[] { "user", "assistant" },
                IncludeSystemMessages = false
            }
        };

        // Assert
        Assert.True(skill.RequiresContext);
        Assert.NotNull(skill.ContextConfig);
        Assert.Equal(10, skill.ContextConfig.MaxMessages);
        Assert.Equal(2, skill.ContextConfig.Roles!.Length);
    }

    [Fact]
    public void Skill_ShouldSupportReturnToLLM()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test",
            Parameters = new List<SkillParameter>(),
            ReturnToLLM = false
        };

        // Assert
        Assert.False(skill.ReturnToLLM);
    }

    [Fact]
    public void Skill_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test",
            Parameters = new List<SkillParameter>()
        };

        // Assert
        Assert.False(skill.RequiresContext); // 默认 false
        Assert.Null(skill.ContextConfig);
        Assert.True(skill.ReturnToLLM); // 默认 true
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/Models/SkillTests.cs
```

预期: FAIL - RequiresContext、ContextConfig、ReturnToLLM 属性未定义

- [ ] **步骤 3: 创建 ContextConfig 模型**

```csharp
// v3/src/GeneralAgent.Infrastructure.Skills/Models/ContextConfig.cs
namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 上下文配置
/// </summary>
public sealed record ContextConfig
{
    /// <summary>
    /// 最大消息数量
    /// </summary>
    public int MaxMessages { get; init; } = 10;

    /// <summary>
    /// 包含的角色（null 表示所有角色）
    /// </summary>
    public string[]? Roles { get; init; }

    /// <summary>
    /// 是否包含系统消息
    /// </summary>
    public bool IncludeSystemMessages { get; init; } = false;
}
```

- [ ] **步骤 4: 扩展 Skill 模型**

```csharp
// v3/src/GeneralAgent.Infrastructure.Skills/Models/Skill.cs
namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能定义（扩展版）
/// </summary>
public sealed record Skill
{
    // 现有字段
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Template { get; init; }
    public required IReadOnlyList<SkillParameter> Parameters { get; init; }
    public string? Namespace { get; init; }
    public Dictionary<string, string>? Tags { get; init; }

    // 新增字段：上下文配置
    public bool RequiresContext { get; init; } = false;
    public ContextConfig? ContextConfig { get; init; }

    // 新增字段：执行配置
    public bool ReturnToLLM { get; init; } = true;

    /// <summary>
    /// 完整技能名称（包含命名空间）
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}:{Name}";
}
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/Models/SkillTests.cs
```

预期: PASS

- [ ] **步骤 6: 提交**

```bash
git add src/GeneralAgent.Infrastructure.Skills/Models/Skill.cs \
        src/GeneralAgent.Infrastructure.Skills/Models/ContextConfig.cs \
        tests/GeneralAgent.Infrastructure.Skills.Tests/Models/SkillTests.cs
git commit -m "feat(v3): 扩展 Skill 模型支持上下文和执行配置

- 添加 RequiresContext 字段
- 添加 ContextConfig 配置（消息数量、角色过滤）
- 添加 ReturnToLLM 字段
- 向后兼容（新字段都有默认值）"
```

---

### Task 5: 升级 SkillExecutor 支持 LLM 调用

**Files:**
- Modify: `v3/src/GeneralAgent.Infrastructure.Skills/Executors/SkillExecutor.cs`
- Modify: `v3/src/GeneralAgent.Infrastructure.Skills/Executors/ISkillExecutor.cs`
- Create: `v3/tests/GeneralAgent.Infrastructure.Skills.Tests/Executors/SkillExecutorTests.cs`

- [ ] **步骤 1: 编写 SkillExecutor LLM 调用测试**

```csharp
// v3/tests/GeneralAgent.Infrastructure.Skills.Tests/Executors/SkillExecutorTests.cs (部分)
[Fact]
public async Task ExecuteAsync_ShouldRenderTemplateAndCallLLM()
{
    // Arrange
    var skill = CreateSkill(
        name: "greeting",
        template: "Hello {user_name}!",
        parameters: new[]
        {
            CreateParameter("user_name", "string", required: true)
        });

    var arguments = new Dictionary<string, object>
    {
        ["user_name"] = "Alice"
    };

    var mockClient = Substitute.For<ILLMClient>();
    mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
        .Returns(new CompletionResponse
        {
            Content = "Hello Alice! How can I help you today?"
        });

    _llmFactory.GetClient(Arg.Any<string>()).Returns(mockClient);

    // Act
    var result = await _executor.ExecuteAsync(skill, arguments, Guid.NewGuid());

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Contains("Alice", result.Value);
    Assert.Contains("help", result.Value);

    // 验证 LLM 被调用，且提示词正确
    await mockClient.Received(1).CompleteAsync(
        Arg.Is<CompletionRequest>(req =>
            req.Messages.Count == 1 &&
            req.Messages[0].Content.Contains("Hello Alice!")),
        Arg.Any<CancellationToken>());
}
```

完整测试代码请参考设计文档中的 SkillExecutor 测试部分。

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/Executors/SkillExecutorTests.cs
```

预期: FAIL - SkillExecutor 未调用 LLM

- [ ] **步骤 3: 更新 ISkillExecutor 接口**

将签名修改为支持 sessionId 和 providerName：

```csharp
// v3/src/GeneralAgent.Infrastructure.Skills/Executors/ISkillExecutor.cs
public interface ISkillExecutor
{
    Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> ExecuteStreamAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default);
}
```

- [ ] **步骤 4: 重构 SkillExecutor 集成 LLM**

```csharp
// v3/src/GeneralAgent.Infrastructure.Skills/Executors/SkillExecutor.cs
using Scriban;

namespace GeneralAgent.Infrastructure.Skills.Executors;

public sealed class SkillExecutor : ISkillExecutor
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<SkillExecutor> _logger;

    public SkillExecutor(
        ILLMClientFactory llmFactory,
        IMessageRepository messageRepo,
        ILogger<SkillExecutor> logger)
    {
        _llmFactory = llmFactory;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public async Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("执行 skill: {SkillName}, 参数: {Arguments}",
                skill.FullName, JsonSerializer.Serialize(arguments));

            // 1. 验证参数
            var validationResult = ValidateArguments(skill, arguments);
            if (!validationResult.IsSuccess)
            {
                return Result<string>.Failure(validationResult.Error);
            }

            // 2. 注入上下文（如果需要）
            if (skill.RequiresContext)
            {
                _logger.LogDebug("注入上下文，配置: MaxMessages={Max}, Roles={Roles}",
                    skill.ContextConfig?.MaxMessages ?? 10,
                    skill.ContextConfig?.Roles != null
                        ? string.Join(",", skill.ContextConfig.Roles)
                        : "all");

                var messages = await GetContextMessagesAsync(sessionId, skill.ContextConfig, ct);
                arguments["context"] = new
                {
                    messages = messages.Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    }).ToList()
                };
            }

            // 3. 渲染 Scriban 模板
            var template = Template.Parse(skill.Template);
            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                _logger.LogError("Scriban 模板解析失败: {Errors}", errors);
                return Result<string>.Failure($"模板解析失败: {errors}");
            }

            var prompt = await template.RenderAsync(arguments);
            _logger.LogDebug("渲染后的提示词: {Prompt}", prompt);

            // 4. 调用 LLM（关键步骤！）
            var client = _llmFactory.GetClient(providerName);
            var response = await client.CompleteAsync(new CompletionRequest
            {
                Messages = new[]
                {
                    new ChatMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                }
            }, ct);

            _logger.LogInformation("Skill 执行成功: {SkillName}, 响应长度: {Length}",
                skill.FullName, response.Content?.Length ?? 0);

            // 5. 返回 LLM 生成的响应
            return Result<string>.Success(response.Content ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行 skill 异常: {SkillName}", skill.FullName);
            return Result<string>.Failure($"执行异常: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1-3 步骤同上（验证、注入上下文、渲染模板）
        var validationResult = ValidateArguments(skill, arguments);
        if (!validationResult.IsSuccess)
        {
            yield return $"❌ {validationResult.Error}";
            yield break;
        }

        if (skill.RequiresContext)
        {
            var messages = await GetContextMessagesAsync(sessionId, skill.ContextConfig, ct);
            arguments["context"] = new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content })
            };
        }

        var template = Template.Parse(skill.Template);
        if (template.HasErrors)
        {
            yield return $"❌ 模板解析失败";
            yield break;
        }

        var prompt = await template.RenderAsync(arguments);

        // 4. 流式调用 LLM
        var client = _llmFactory.GetClient(providerName);
        await foreach (var chunk in client.CompleteStreamAsync(new CompletionRequest
        {
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = prompt }
            }
        }, ct))
        {
            yield return chunk;
        }
    }

    private Result<Dictionary<string, object>> ValidateArguments(
        Skill skill,
        Dictionary<string, object> arguments)
    {
        foreach (var param in skill.Parameters)
        {
            if (param.Required && !arguments.ContainsKey(param.Name))
            {
                return Result<Dictionary<string, object>>.Failure(
                    $"缺少必需参数: {param.Name}");
            }

            if (!arguments.ContainsKey(param.Name) && param.DefaultValue != null)
            {
                arguments[param.Name] = param.DefaultValue;
            }
        }

        return Result<Dictionary<string, object>>.Success(arguments);
    }

    private async Task<List<Message>> GetContextMessagesAsync(
        Guid sessionId,
        ContextConfig? config,
        CancellationToken ct)
    {
        var maxMessages = config?.MaxMessages ?? 10;
        var roles = config?.Roles;
        var includeSystem = config?.IncludeSystemMessages ?? false;

        var messages = await _messageRepo.GetRecentAsync(sessionId, maxMessages, ct);

        return messages
            .Where(m =>
            {
                if (!includeSystem && m.Role == "system")
                    return false;

                if (roles != null && !roles.Contains(m.Role))
                    return false;

                return true;
            })
            .OrderBy(m => m.Timestamp)
            .ToList();
    }
}
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/Executors/SkillExecutorTests.cs
```

预期: PASS - 所有测试通过，包括 LLM 调用验证

- [ ] **步骤 6: 提交**

```bash
git add src/GeneralAgent.Infrastructure.Skills/Executors/ISkillExecutor.cs \
        src/GeneralAgent.Infrastructure.Skills/Executors/SkillExecutor.cs \
        tests/GeneralAgent.Infrastructure.Skills.Tests/Executors/SkillExecutorTests.cs
git commit -m "feat(v3): 升级 SkillExecutor 支持 LLM 调用

关键改进：
- 渲染模板后调用 LLM，返回智能响应
- 支持上下文注入（RequiresContext）
- 支持流式和非流式执行
- 完整的单元测试覆盖"
```

---

### Task 6: 实现 SkillToToolConverter

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.Skills/SkillToToolConverter.cs`
- Create: `v3/tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToToolConverterTests.cs`

- [ ] **步骤 1: 编写 Converter 测试**

参考设计文档中的 SkillToToolConverter 测试。

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToToolConverterTests.cs
```

预期: FAIL

- [ ] **步骤 3: 实现 SkillToToolConverter**

参考设计文档中的实现代码。

- [ ] **步骤 4: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToToolConverterTests.cs
```

预期: PASS

- [ ] **步骤 5: 提交**

```bash
git add src/GeneralAgent.Infrastructure.Skills/SkillToToolConverter.cs \
        tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToToolConverterTests.cs
git commit -m "feat(v3): 实现 SkillToToolConverter

- 将 Skill 参数转换为 ToolDefinition
- 支持参数类型映射
- 支持必需参数和默认值"
```

---

### Task 7: 实现 SkillTool

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure.Skills/SkillTool.cs`
- Create: `v3/tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToolTests.cs`

- [ ] **步骤 1: 编写 SkillTool 测试**

```csharp
// v3/tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToolTests.cs
namespace GeneralAgent.Infrastructure.Skills.Tests;

public class SkillToolTests
{
    [Fact]
    public void SkillTool_ShouldImplementITool()
    {
        // Arrange
        var skill = CreateTestSkill();
        var executor = Substitute.For<ISkillExecutor>();
        var converter = new SkillToToolConverter();

        // Act
        ITool tool = new SkillTool(skill, executor, converter);

        // Assert
        Assert.Equal(skill.FullName, tool.Name);
        Assert.Equal(skill.Description, tool.Description);
    }

    [Fact]
    public async Task SkillTool_ExecuteAsync_ShouldDelegateToExecutor()
    {
        // Arrange
        var skill = CreateTestSkill();
        var executor = Substitute.For<ISkillExecutor>();
        var converter = new SkillToToolConverter();

        executor.ExecuteAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("output"));

        var tool = new SkillTool(skill, executor, converter);
        var context = new ToolExecutionContext { SessionId = Guid.NewGuid() };

        // Act
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object>(),
            context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("output", result.Value);

        await executor.Received(1).ExecuteAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            context.SessionId,
            context.ProviderName,
            Arg.Any<CancellationToken>());
    }

    private Skill CreateTestSkill()
    {
        return new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Test {param}",
            Parameters = new List<SkillParameter>()
        };
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToolTests.cs
```

预期: FAIL

- [ ] **步骤 3: 实现 SkillTool**

参考设计文档中的 SkillTool 实现。

- [ ] **步骤 4: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToolTests.cs
```

预期: PASS

- [ ] **步骤 5: 提交**

```bash
git add src/GeneralAgent.Infrastructure.Skills/SkillTool.cs \
        tests/GeneralAgent.Infrastructure.Skills.Tests/SkillToolTests.cs
git commit -m "feat(v3): 实现 SkillTool 适配器

- Skill 实现 ITool 接口
- 委托给 SkillExecutor 执行
- 支持同步和流式执行"
```

---

## Phase 3.3: Tool Calling 循环（2天）

### Task 8: 实现 IToolCallingListener 接口和实现类

**Files:**
- Create: `v3/src/GeneralAgent.Core/Abstractions/IToolCallingListener.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ExtendDecision.cs`
- Create: `v3/src/GeneralAgent.Application/Services/ConsoleToolCallingListener.cs`
- Create: `v3/src/GeneralAgent.Application/Services/AutomaticToolCallingListener.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/ToolCallingListenerTests.cs`

- [ ] **步骤 1: 定义 ExtendDecision 模型**

```csharp
// v3/src/GeneralAgent.Core/Models/ExtendDecision.cs
namespace GeneralAgent.Core.Models;

/// <summary>
/// 用户决策（是否继续 Tool Calling）
/// </summary>
public sealed record ExtendDecision
{
    public bool Stop { get; init; }
    public int ExtendBy { get; init; }
}
```

- [ ] **步骤 2: 定义 IToolCallingListener 接口**

```csharp
// v3/src/GeneralAgent.Core/Abstractions/IToolCallingListener.cs
namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// Tool Calling 用户交互接口
/// </summary>
public interface IToolCallingListener
{
    /// <summary>
    /// 当达到最大轮数时调用
    /// </summary>
    Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default);
}
```

- [ ] **步骤 3: 编写 ConsoleToolCallingListener 测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/ToolCallingListenerTests.cs
namespace GeneralAgent.Application.Tests.Services;

public class ConsoleToolCallingListenerTests
{
    [Fact]
    public async Task OnMaxRoundsReached_ShouldReturnExtendDecision()
    {
        // Arrange
        var listener = new ConsoleToolCallingListener(
            Substitute.For<ILogger<ConsoleToolCallingListener>>());

        // 无法测试 Console 交互，此处仅测试方法签名
        // 实际测试通过手动验证或集成测试

        // Assert
        Assert.NotNull(listener);
    }
}

public class AutomaticToolCallingListenerTests
{
    [Fact]
    public async Task OnMaxRoundsReached_ShouldAutoExtend()
    {
        // Arrange
        var config = Options.Create(new ToolCallingConfig { AutoExtendBy = 5 });
        var listener = new AutomaticToolCallingListener(
            config,
            Substitute.For<ILogger<AutomaticToolCallingListener>>());

        // Act
        var decision = await listener.OnMaxRoundsReachedAsync(
            3,
            Guid.NewGuid(),
            new List<ToolCall>(),
            CancellationToken.None);

        // Assert
        Assert.False(decision.Stop);
        Assert.Equal(5, decision.ExtendBy);
    }
}
```

- [ ] **步骤 4: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolCallingListenerTests.cs
```

预期: FAIL - 实现类未定义

- [ ] **步骤 5: 实现 ConsoleToolCallingListener**

```csharp
// v3/src/GeneralAgent.Application/Services/ConsoleToolCallingListener.cs
namespace GeneralAgent.Application.Services;

public sealed class ConsoleToolCallingListener : IToolCallingListener
{
    private readonly ILogger<ConsoleToolCallingListener> _logger;

    public ConsoleToolCallingListener(ILogger<ConsoleToolCallingListener> logger)
    {
        _logger = logger;
    }

    public Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine($"⚠️  Tool Calling 已执行 {currentRounds} 轮");
        Console.WriteLine($"   工具调用数量: {toolCalls.Count}");
        Console.WriteLine();
        Console.WriteLine("是否继续？");
        Console.WriteLine("  [y] 继续 3 轮");
        Console.WriteLine("  [5] 继续 5 轮");
        Console.WriteLine("  [10] 继续 10 轮");
        Console.WriteLine("  [n] 停止");
        Console.Write("> ");

        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        _logger.LogInformation("用户选择: {Input}", input);

        var decision = input switch
        {
            "y" or "yes" => new ExtendDecision { Stop = false, ExtendBy = 3 },
            "5" => new ExtendDecision { Stop = false, ExtendBy = 5 },
            "10" => new ExtendDecision { Stop = false, ExtendBy = 10 },
            "n" or "no" => new ExtendDecision { Stop = true, ExtendBy = 0 },
            _ => new ExtendDecision { Stop = false, ExtendBy = 3 } // 默认继续 3 轮
        };

        return Task.FromResult(decision);
    }
}
```

- [ ] **步骤 6: 实现 AutomaticToolCallingListener**

```csharp
// v3/src/GeneralAgent.Application/Services/AutomaticToolCallingListener.cs
namespace GeneralAgent.Application.Services;

public sealed class AutomaticToolCallingListener : IToolCallingListener
{
    private readonly ToolCallingConfig _config;
    private readonly ILogger<AutomaticToolCallingListener> _logger;

    public AutomaticToolCallingListener(
        IOptions<ToolCallingConfig> config,
        ILogger<AutomaticToolCallingListener> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Tool Calling 达到 {Rounds} 轮，自动继续 {ExtendBy} 轮",
            currentRounds, _config.AutoExtendBy);

        var decision = new ExtendDecision
        {
            Stop = false,
            ExtendBy = _config.AutoExtendBy
        };

        return Task.FromResult(decision);
    }
}
```

- [ ] **步骤 7: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolCallingListenerTests.cs
```

预期: PASS

- [ ] **步骤 8: 提交**

```bash
git add src/GeneralAgent.Core/Abstractions/IToolCallingListener.cs \
        src/GeneralAgent.Core/Models/ExtendDecision.cs \
        src/GeneralAgent.Application/Services/ConsoleToolCallingListener.cs \
        src/GeneralAgent.Application/Services/AutomaticToolCallingListener.cs \
        tests/GeneralAgent.Application.Tests/Services/ToolCallingListenerTests.cs
git commit -m "feat(v3): 实现 IToolCallingListener 和两种实现

- ConsoleToolCallingListener: 交互式用户确认
- AutomaticToolCallingListener: 自动继续
- 支持停止或延长轮数"
```

---

### Task 9: 实现 IToolSerializer 接口和实现类

**Files:**
- Create: `v3/src/GeneralAgent.Core/Abstractions/IToolSerializer.cs`
- Create: `v3/src/GeneralAgent.Infrastructure.LLM/Serializers/OpenAIToolSerializer.cs`
- Create: `v3/src/GeneralAgent.Infrastructure.LLM/Serializers/AnthropicToolSerializer.cs`
- Create: `v3/tests/GeneralAgent.Infrastructure.LLM.Tests/Serializers/ToolSerializerTests.cs`

- [ ] **步骤 1: 定义 IToolSerializer 接口**

```csharp
// v3/src/GeneralAgent.Core/Abstractions/IToolSerializer.cs
using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 工具序列化器（LLM 提供商格式适配）
/// </summary>
public interface IToolSerializer
{
    /// <summary>
    /// 序列化工具定义为 LLM 格式
    /// </summary>
    JsonObject SerializeToolDefinition(ToolDefinition toolDef);

    /// <summary>
    /// 序列化多个工具定义
    /// </summary>
    JsonArray SerializeTools(IEnumerable<ToolDefinition> tools);
}
```

- [ ] **步骤 2: 编写序列化器测试**

```csharp
// v3/tests/GeneralAgent.Infrastructure.LLM.Tests/Serializers/ToolSerializerTests.cs
namespace GeneralAgent.Infrastructure.LLM.Tests.Serializers;

public class OpenAIToolSerializerTests
{
    private readonly OpenAIToolSerializer _serializer = new();

    [Fact]
    public void SerializeToolDefinition_ShouldProduceOpenAIFormat()
    {
        // Arrange
        var toolDef = new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool",
            InputSchema = JsonNode.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "param1": { "type": "string" }
                    },
                    "required": ["param1"]
                }
                """)!.AsObject()
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        Assert.Equal("function", result["type"]?.GetValue<string>());
        Assert.Equal("test_tool", result["function"]?["name"]?.GetValue<string>());
        Assert.NotNull(result["function"]?["parameters"]);
    }
}

public class AnthropicToolSerializerTests
{
    private readonly AnthropicToolSerializer _serializer = new();

    [Fact]
    public void SerializeToolDefinition_ShouldProduceAnthropicFormat()
    {
        // Arrange
        var toolDef = new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool",
            InputSchema = JsonNode.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "param1": { "type": "string" }
                    },
                    "required": ["param1"]
                }
                """)!.AsObject()
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        Assert.Equal("test_tool", result["name"]?.GetValue<string>());
        Assert.Equal("Test tool", result["description"]?.GetValue<string>());
        Assert.NotNull(result["input_schema"]);
    }
}
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Infrastructure.LLM.Tests/Serializers/ToolSerializerTests.cs
```

预期: FAIL

- [ ] **步骤 4: 实现 OpenAIToolSerializer**

```csharp
// v3/src/GeneralAgent.Infrastructure.LLM/Serializers/OpenAIToolSerializer.cs
using System.Text.Json.Nodes;

namespace GeneralAgent.Infrastructure.LLM.Serializers;

/// <summary>
/// OpenAI Function Calling 格式序列化器
/// </summary>
public sealed class OpenAIToolSerializer : IToolSerializer
{
    public JsonObject SerializeToolDefinition(ToolDefinition toolDef)
    {
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = toolDef.Name,
                ["description"] = toolDef.Description,
                ["parameters"] = toolDef.InputSchema
            }
        };
    }

    public JsonArray SerializeTools(IEnumerable<ToolDefinition> tools)
    {
        var array = new JsonArray();
        foreach (var tool in tools)
        {
            array.Add(SerializeToolDefinition(tool));
        }
        return array;
    }
}
```

- [ ] **步骤 5: 实现 AnthropicToolSerializer**

```csharp
// v3/src/GeneralAgent.Infrastructure.LLM/Serializers/AnthropicToolSerializer.cs
using System.Text.Json.Nodes;

namespace GeneralAgent.Infrastructure.LLM.Serializers;

/// <summary>
/// Anthropic Tool Use 格式序列化器
/// </summary>
public sealed class AnthropicToolSerializer : IToolSerializer
{
    public JsonObject SerializeToolDefinition(ToolDefinition toolDef)
    {
        return new JsonObject
        {
            ["name"] = toolDef.Name,
            ["description"] = toolDef.Description,
            ["input_schema"] = toolDef.InputSchema
        };
    }

    public JsonArray SerializeTools(IEnumerable<ToolDefinition> tools)
    {
        var array = new JsonArray();
        foreach (var tool in tools)
        {
            array.Add(SerializeToolDefinition(tool));
        }
        return array;
    }
}
```

- [ ] **步骤 6: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Infrastructure.LLM.Tests/Serializers/ToolSerializerTests.cs
```

预期: PASS

- [ ] **步骤 7: 提交**

```bash
git add src/GeneralAgent.Core/Abstractions/IToolSerializer.cs \
        src/GeneralAgent.Infrastructure.LLM/Serializers/OpenAIToolSerializer.cs \
        src/GeneralAgent.Infrastructure.LLM/Serializers/AnthropicToolSerializer.cs \
        tests/GeneralAgent.Infrastructure.LLM.Tests/Serializers/ToolSerializerTests.cs
git commit -m "feat(v3): 实现 IToolSerializer 和两种格式序列化器

- OpenAIToolSerializer: OpenAI Function Calling 格式
- AnthropicToolSerializer: Anthropic Tool Use 格式
- 统一接口支持多 LLM 提供商"
```

---

### Task 10: 实现 ToolCallingOrchestrator

**Files:**
- Create: `v3/src/GeneralAgent.Application/Services/ToolCallingOrchestrator.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ConversationResult.cs`
- Create: `v3/src/GeneralAgent.Core/Models/ToolCallingConfig.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/ToolCallingOrchestratorTests.cs`

- [ ] **步骤 1: 定义配置和结果模型**

```csharp
// v3/src/GeneralAgent.Core/Models/ToolCallingConfig.cs
namespace GeneralAgent.Core.Models;

public sealed record ToolCallingConfig
{
    public bool Enabled { get; init; } = true;
    public int MaxRounds { get; init; } = 3;
    public bool InteractiveMode { get; init; } = true;
    public int AutoExtendBy { get; init; } = 5;
    public int AbsoluteMaxRounds { get; init; } = 20;
}

// v3/src/GeneralAgent.Core/Models/ConversationResult.cs
namespace GeneralAgent.Core.Models;

public sealed record ConversationResult
{
    public required string FinalResponse { get; init; }
    public int TotalRounds { get; init; }
    public int TotalToolCalls { get; init; }
    public List<ChatMessage> Messages { get; init; } = new();
    public bool Truncated { get; init; }
    public string? TruncationReason { get; init; }
}
```

- [ ] **步骤 2: 编写 Orchestrator 测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/ToolCallingOrchestratorTests.cs
namespace GeneralAgent.Application.Tests.Services;

public class ToolCallingOrchestratorTests
{
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _toolExecutor;
    private readonly ILLMClient _mockClient;
    private readonly IToolCallingListener _mockListener;
    private readonly IToolSerializer _serializer;
    private readonly ToolCallingOrchestrator _orchestrator;

    public ToolCallingOrchestratorTests()
    {
        var registryLogger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(registryLogger);

        var executorLogger = Substitute.For<ILogger<ToolExecutor>>();
        _toolExecutor = new ToolExecutor(_registry, executorLogger);

        _mockClient = Substitute.For<ILLMClient>();
        _mockListener = Substitute.For<IToolCallingListener>();
        _serializer = new OpenAIToolSerializer();

        var config = Options.Create(new ToolCallingConfig
        {
            MaxRounds = 3,
            AbsoluteMaxRounds = 20
        });

        var logger = Substitute.For<ILogger<ToolCallingOrchestrator>>();
        _orchestrator = new ToolCallingOrchestrator(
            _toolExecutor,
            _registry,
            _mockClient,
            _mockListener,
            _serializer,
            config,
            logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnDirectResponse_WhenNoToolCalls()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Hi there!",
                ToolCalls = null
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(
            Guid.NewGuid(),
            history,
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("Hi there!", result.FinalResponse);
        Assert.Equal(0, result.TotalRounds);
        Assert.Equal(0, result.TotalToolCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteToolCalls_AndReturnFinalResponse()
    {
        // Arrange
        var echoTool = CreateEchoTool("echo");
        _registry.Register(echoTool);

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Echo hello" }
        };

        // 第一次调用：返回 tool call
        _mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Tools != null),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = null,
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "call_1",
                        FunctionName = "echo",
                        Arguments = JsonSerializer.Serialize(new { message = "hello" })
                    }
                }
            });

        // 第二次调用：返回最终响应
        _mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Any(m => m.Role == "tool")),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "I echoed your message: Echo: hello",
                ToolCalls = null
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(
            Guid.NewGuid(),
            history,
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("I echoed your message: Echo: hello", result.FinalResponse);
        Assert.Equal(1, result.TotalRounds);
        Assert.Equal(1, result.TotalToolCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPromptUser_WhenMaxRoundsReached()
    {
        // Arrange
        var echoTool = CreateEchoTool("echo");
        _registry.Register(echoTool);

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Keep echoing" }
        };

        // 始终返回 tool call
        _mockClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = null,
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "call_1",
                        FunctionName = "echo",
                        Arguments = "{\"message\":\"test\"}"
                    }
                }
            });

        // 用户决定停止
        _mockListener.OnMaxRoundsReachedAsync(
            Arg.Any<int>(),
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<ToolCall>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ExtendDecision { Stop = true });

        // Act
        var result = await _orchestrator.ExecuteAsync(
            Guid.NewGuid(),
            history,
            null,
            CancellationToken.None);

        // Assert
        Assert.True(result.Truncated);
        Assert.Contains("用户终止", result.TruncationReason);
        await _mockListener.Received(1).OnMaxRoundsReachedAsync(
            3,
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<ToolCall>>(),
            Arg.Any<CancellationToken>());
    }

    private ITool CreateEchoTool(string name)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns("Echo tool");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = name,
            Description = "Echo tool",
            InputSchema = JsonNode.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "message": { "type": "string" }
                    },
                    "required": ["message"]
                }
                """)!.AsObject()
        });
        tool.ExecuteAsync(
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var args = callInfo.Arg<Dictionary<string, object>>();
                var message = args.GetValueOrDefault("message", "No message");
                return Task.FromResult(Result<string>.Success($"Echo: {message}"));
            });
        return tool;
    }
}
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolCallingOrchestratorTests.cs
```

预期: FAIL - ToolCallingOrchestrator 未定义

- [ ] **步骤 4: 实现 ToolCallingOrchestrator**

```csharp
// v3/src/GeneralAgent.Application/Services/ToolCallingOrchestrator.cs
namespace GeneralAgent.Application.Services;

public sealed class ToolCallingOrchestrator
{
    private readonly ToolExecutor _toolExecutor;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILLMClient _llmClient;
    private readonly IToolCallingListener _listener;
    private readonly IToolSerializer _serializer;
    private readonly ToolCallingConfig _config;
    private readonly ILogger<ToolCallingOrchestrator> _logger;

    public ToolCallingOrchestrator(
        ToolExecutor toolExecutor,
        ToolRegistry toolRegistry,
        ILLMClient llmClient,
        IToolCallingListener listener,
        IToolSerializer serializer,
        IOptions<ToolCallingConfig> config,
        ILogger<ToolCallingOrchestrator> logger)
    {
        _toolExecutor = toolExecutor;
        _toolRegistry = toolRegistry;
        _llmClient = llmClient;
        _listener = listener;
        _serializer = serializer;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ConversationResult> ExecuteAsync(
        Guid sessionId,
        List<ChatMessage> history,
        string? providerName,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            // Tool Calling 禁用，直接调用 LLM
            var directResponse = await _llmClient.CompleteAsync(
                new CompletionRequest { Messages = history },
                ct);
            return new ConversationResult
            {
                FinalResponse = directResponse.Content ?? string.Empty,
                TotalRounds = 0,
                TotalToolCalls = 0,
                Messages = history
            };
        }

        // 准备工具定义
        var tools = _toolRegistry.GetAllTools()
            .Select(t => t.GetDefinition())
            .ToList();

        if (tools.Count == 0)
        {
            _logger.LogWarning("没有注册任何工具，禁用 Tool Calling");
            var directResponse = await _llmClient.CompleteAsync(
                new CompletionRequest { Messages = history },
                ct);
            return new ConversationResult
            {
                FinalResponse = directResponse.Content ?? string.Empty,
                TotalRounds = 0,
                TotalToolCalls = 0,
                Messages = history
            };
        }

        var serializedTools = _serializer.SerializeTools(tools);
        var messages = new List<ChatMessage>(history);
        int currentRound = 0;
        int maxRounds = _config.MaxRounds;
        int totalToolCalls = 0;

        while (currentRound < _config.AbsoluteMaxRounds)
        {
            _logger.LogDebug("Tool Calling 第 {Round} 轮", currentRound + 1);

            // 调用 LLM（带 tools）
            var response = await _llmClient.CompleteAsync(new CompletionRequest
            {
                Messages = messages,
                Tools = serializedTools
            }, ct);

            // 检查是否有 tool calls
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                // 无 tool calls，返回最终响应
                _logger.LogInformation(
                    "Tool Calling 完成，总轮数: {Rounds}, 总工具调用: {Calls}",
                    currentRound, totalToolCalls);

                return new ConversationResult
                {
                    FinalResponse = response.Content ?? string.Empty,
                    TotalRounds = currentRound,
                    TotalToolCalls = totalToolCalls,
                    Messages = messages
                };
            }

            // 递增轮数
            currentRound++;
            totalToolCalls += response.ToolCalls.Count;

            _logger.LogInformation(
                "第 {Round} 轮工具调用: {Count} 个工具",
                currentRound, response.ToolCalls.Count);

            // 检查是否达到最大轮数
            if (currentRound >= maxRounds)
            {
                var decision = await _listener.OnMaxRoundsReachedAsync(
                    currentRound,
                    sessionId,
                    response.ToolCalls,
                    ct);

                if (decision.Stop)
                {
                    _logger.LogWarning("用户终止 Tool Calling，当前轮数: {Round}", currentRound);
                    return new ConversationResult
                    {
                        FinalResponse = "⚠️ Tool Calling 已终止（达到最大轮数限制）",
                        TotalRounds = currentRound,
                        TotalToolCalls = totalToolCalls,
                        Messages = messages,
                        Truncated = true,
                        TruncationReason = "用户终止"
                    };
                }

                maxRounds += decision.ExtendBy;
                _logger.LogInformation("延长 Tool Calling 轮数: +{ExtendBy}, 新限制: {MaxRounds}",
                    decision.ExtendBy, maxRounds);
            }

            // 执行 tool calls
            var context = new ToolExecutionContext
            {
                SessionId = sessionId,
                ProviderName = providerName
            };

            var results = await _toolExecutor.ExecuteManyAsync(response.ToolCalls, context, ct);

            // 将 tool calls 和结果添加到历史
            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response.Content,
                ToolCalls = response.ToolCalls
            });

            foreach (var result in results)
            {
                messages.Add(new ChatMessage
                {
                    Role = "tool",
                    Content = result.Content,
                    ToolCallId = result.ToolCallId
                });
            }
        }

        // 达到绝对最大轮数
        _logger.LogError("达到绝对最大轮数限制: {AbsoluteMax}", _config.AbsoluteMaxRounds);
        return new ConversationResult
        {
            FinalResponse = "❌ Tool Calling 超时（达到绝对最大轮数限制）",
            TotalRounds = currentRound,
            TotalToolCalls = totalToolCalls,
            Messages = messages,
            Truncated = true,
            TruncationReason = $"达到绝对最大轮数 {_config.AbsoluteMaxRounds}"
        };
    }
}
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ToolCallingOrchestratorTests.cs
```

预期: PASS

- [ ] **步骤 6: 提交**

```bash
git add src/GeneralAgent.Core/Models/ToolCallingConfig.cs \
        src/GeneralAgent.Core/Models/ConversationResult.cs \
        src/GeneralAgent.Application/Services/ToolCallingOrchestrator.cs \
        tests/GeneralAgent.Application.Tests/Services/ToolCallingOrchestratorTests.cs
git commit -m "feat(v3): 实现 ToolCallingOrchestrator

关键功能：
- Tool Calling 循环管理
- 用户确认机制（达到限制时）
- 并行工具执行
- 完整历史记录（包含 tool calls 和 results）
- 绝对最大轮数保护"
```

---

### Task 11: Phase 3.3 集成测试

**Files:**
- Create: `v3/tests/GeneralAgent.Integration.Tests/ToolCallingIntegrationTests.cs`

- [ ] **步骤 1: 编写集成测试**

```csharp
// v3/tests/GeneralAgent.Integration.Tests/ToolCallingIntegrationTests.cs
namespace GeneralAgent.Integration.Tests;

public class ToolCallingIntegrationTests
{
    [Fact]
    public async Task ToolCalling_ShouldExecuteSkillAndReturnResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册测试 skill
        var greetingSkill = CreateGreetingSkill();
        var skillTool = new SkillTool(
            greetingSkill,
            provider.GetRequiredService<ISkillExecutor>(),
            new SkillToToolConverter());
        registry.Register(skillTool);

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "向 Alice 打个招呼" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(
            Guid.NewGuid(),
            history,
            null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result.FinalResponse);
        Assert.True(result.TotalToolCalls > 0);
        Assert.Contains("Alice", result.FinalResponse, StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddLogging();
        services.Configure<ToolCallingConfig>(config =>
        {
            config.Enabled = true;
            config.MaxRounds = 3;
        });

        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
        services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
        services.AddSingleton<ToolCallingOrchestrator>();

        // Mock LLM Client
        var mockClient = Substitute.For<ILLMClient>();
        // ... 配置 mock 返回值
        services.AddSingleton(mockClient);
    }

    private Skill CreateGreetingSkill()
    {
        return new Skill
        {
            Name = "greeting",
            Description = "向用户打招呼",
            Template = "Hello {user_name}!",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true,
                    Description = "用户名称"
                }
            }
        };
    }
}
```

- [ ] **步骤 2: 运行集成测试**

```bash
dotnet test tests/GeneralAgent.Integration.Tests/ToolCallingIntegrationTests.cs
```

预期: PASS

- [ ] **步骤 3: 提交**

```bash
git add tests/GeneralAgent.Integration.Tests/ToolCallingIntegrationTests.cs
git commit -m "test(v3): Phase 3.3 集成测试

- 验证 Tool Calling 完整流程
- 验证 Skill 隐式调用
- 验证用户确认机制"
```

---

## Phase 3.4: 完整集成（1-2天）

### Task 12: 重构 ConversationService

**Files:**
- Modify: `v3/src/GeneralAgent.Application/Services/ConversationService.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/ConversationServiceTests.cs`

- [ ] **步骤 1: 编写 ConversationService 测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/ConversationServiceTests.cs
namespace GeneralAgent.Application.Tests.Services;

public class ConversationServiceTests
{
    [Fact]
    public async Task SendMessageAsync_ShouldHandleExplicitSkillCall()
    {
        // Arrange
        var service = CreateConversationService();
        var sessionId = Guid.NewGuid();

        // Act
        var response = await service.SendMessageAsync(
            sessionId,
            "@greeting user_name='Bob'",
            null,
            CancellationToken.None);

        // Assert
        Assert.Contains("Bob", response);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldDelegateToOrchestrator_WhenNoExplicitCall()
    {
        // Arrange
        var mockOrchestrator = Substitute.For<ToolCallingOrchestrator>(/*...*/);
        mockOrchestrator.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<List<ChatMessage>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConversationResult
            {
                FinalResponse = "Orchestrator response"
            });

        var service = new ConversationService(
            mockOrchestrator,
            /*...其他依赖...*/);

        var sessionId = Guid.NewGuid();

        // Act
        var response = await service.SendMessageAsync(
            sessionId,
            "普通消息",
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("Orchestrator response", response);
        await mockOrchestrator.Received(1).ExecuteAsync(
            sessionId,
            Arg.Any<List<ChatMessage>>(),
            null,
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ConversationServiceTests.cs
```

预期: FAIL - ConversationService 未更新

- [ ] **步骤 3: 重构 ConversationService**

```csharp
// v3/src/GeneralAgent.Application/Services/ConversationService.cs
namespace GeneralAgent.Application.Services;

public sealed class ConversationService
{
    private readonly ToolCallingOrchestrator _orchestrator;
    private readonly ToolExecutor _toolExecutor;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        ToolCallingOrchestrator orchestrator,
        ToolExecutor toolExecutor,
        IMessageRepository messageRepo,
        ILogger<ConversationService> logger)
    {
        _orchestrator = orchestrator;
        _toolExecutor = toolExecutor;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        string? providerName,
        CancellationToken ct = default)
    {
        // 1. 保存用户消息
        await _messageRepo.CreateAsync(
            Message.CreateUser(sessionId, userMessage),
            ct);

        // 2. 检查显式 skill 调用（@skill 或 /skill）
        if (SkillCallParser.TryParse(userMessage, out var skillCall))
        {
            _logger.LogInformation("显式 skill 调用: {SkillName}", skillCall.SkillName);

            var result = await _toolExecutor.ExecuteAsync(
                skillCall.SkillName,
                skillCall.Arguments,
                new ToolExecutionContext
                {
                    SessionId = sessionId,
                    ProviderName = providerName
                },
                ct);

            var response = result.IsSuccess
                ? result.Value!
                : $"❌ {result.Error}";

            await _messageRepo.CreateAsync(
                Message.CreateAssistant(sessionId, response),
                ct);

            return response;
        }

        // 3. 进入 Tool Calling 模式（隐式调用）
        _logger.LogDebug("进入 Tool Calling 模式");

        var history = await GetChatHistoryAsync(sessionId, ct);
        var conversationResult = await _orchestrator.ExecuteAsync(
            sessionId,
            history,
            providerName,
            ct);

        // 4. 保存对话历史
        await SaveConversationHistoryAsync(sessionId, conversationResult.Messages, ct);

        // 5. 保存最终响应
        await _messageRepo.CreateAsync(
            Message.CreateAssistant(sessionId, conversationResult.FinalResponse),
            ct);

        return conversationResult.FinalResponse;
    }

    private async Task<List<ChatMessage>> GetChatHistoryAsync(
        Guid sessionId,
        CancellationToken ct)
    {
        var messages = await _messageRepo.GetBySessionAsync(sessionId, ct);
        return messages
            .Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content
            })
            .ToList();
    }

    private async Task SaveConversationHistoryAsync(
        Guid sessionId,
        List<ChatMessage> messages,
        CancellationToken ct)
    {
        // 只保存新增的 tool 消息（assistant with tool_calls 和 tool results）
        var toolMessages = messages
            .Where(m => m.Role == "tool" || m.ToolCalls != null)
            .Skip(1); // 跳过第一条（用户消息）

        foreach (var msg in toolMessages)
        {
            await _messageRepo.CreateAsync(
                new Message
                {
                    SessionId = sessionId,
                    Role = msg.Role,
                    Content = msg.Content ?? JsonSerializer.Serialize(msg.ToolCalls),
                    Timestamp = DateTime.UtcNow
                },
                ct);
        }
    }
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/ConversationServiceTests.cs
```

预期: PASS

- [ ] **步骤 5: 提交**

```bash
git add src/GeneralAgent.Application/Services/ConversationService.cs \
        tests/GeneralAgent.Application.Tests/Services/ConversationServiceTests.cs
git commit -m "refactor(v3): 重构 ConversationService 集成 Tool Calling

- 显式调用 → ToolExecutor
- 隐式调用 → ToolCallingOrchestrator
- 统一消息历史管理"
```

---

### Task 13: 更新 SkillService 注册为 Tool

**Files:**
- Modify: `v3/src/GeneralAgent.Application/Services/SkillService.cs`
- Create: `v3/tests/GeneralAgent.Application.Tests/Services/SkillServiceTests.cs`

- [ ] **步骤 1: 编写测试**

```csharp
// v3/tests/GeneralAgent.Application.Tests/Services/SkillServiceTests.cs
[Fact]
public async Task LoadSkillsAsync_ShouldRegisterSkillsAsTools()
{
    // Arrange
    var mockLoader = Substitute.For<ISkillLoader>();
    var mockExecutor = Substitute.For<ISkillExecutor>();
    var registry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
    var converter = new SkillToToolConverter();

    var skills = new List<Skill>
    {
        CreateTestSkill("greeting"),
        CreateTestSkill("reminder")
    };

    mockLoader.LoadSkillsAsync(Arg.Any<CancellationToken>())
        .Returns(skills);

    var service = new SkillService(
        mockLoader,
        mockExecutor,
        registry,
        converter,
        Substitute.For<ILogger<SkillService>>());

    // Act
    await service.LoadSkillsAsync(CancellationToken.None);

    // Assert
    Assert.Equal(2, registry.Count);
    Assert.NotNull(registry.GetTool("greeting"));
    Assert.NotNull(registry.GetTool("reminder"));
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/SkillServiceTests.cs
```

预期: FAIL

- [ ] **步骤 3: 修改 SkillService**

```csharp
// v3/src/GeneralAgent.Application/Services/SkillService.cs
public sealed class SkillService
{
    private readonly ISkillLoader _loader;
    private readonly ISkillExecutor _executor;
    private readonly ToolRegistry _toolRegistry;
    private readonly SkillToToolConverter _converter;
    private readonly ILogger<SkillService> _logger;

    public SkillService(
        ISkillLoader loader,
        ISkillExecutor executor,
        ToolRegistry toolRegistry,
        SkillToToolConverter converter,
        ILogger<SkillService> logger)
    {
        _loader = loader;
        _executor = executor;
        _toolRegistry = toolRegistry;
        _converter = converter;
        _logger = logger;
    }

    public async Task LoadSkillsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("加载 skills...");

        var skills = await _loader.LoadSkillsAsync(ct);

        _logger.LogInformation("加载到 {Count} 个 skills", skills.Count);

        // 将每个 skill 注册为 tool
        foreach (var skill in skills)
        {
            var skillTool = new SkillTool(skill, _executor, _converter);
            _toolRegistry.Register(skillTool);

            _logger.LogDebug("注册 skill 为 tool: {SkillName}", skill.FullName);
        }

        _logger.LogInformation("所有 skills 已注册为 tools");
    }
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
dotnet test tests/GeneralAgent.Application.Tests/Services/SkillServiceTests.cs
```

预期: PASS

- [ ] **步骤 5: 提交**

```bash
git add src/GeneralAgent.Application/Services/SkillService.cs \
        tests/GeneralAgent.Application.Tests/Services/SkillServiceTests.cs
git commit -m "feat(v3): SkillService 自动将 skills 注册为 tools

- 加载时自动创建 SkillTool 适配器
- 注册到 ToolRegistry
- 支持 LLM Function Calling"
```

---

### Task 14: 配置 DI 容器

**Files:**
- Modify: `v3/src/GeneralAgent.API/Program.cs`
- Create: `v3/src/GeneralAgent.Application/DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **步骤 1: 创建扩展方法**

```csharp
// v3/src/GeneralAgent.Application/DependencyInjection/ServiceCollectionExtensions.cs
namespace GeneralAgent.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolCallingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置
        services.Configure<ToolCallingConfig>(
            configuration.GetSection("ToolCalling"));

        // 核心服务
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<ToolCallingOrchestrator>();
        services.AddSingleton<SkillToToolConverter>();

        // 根据配置选择 Listener
        var interactiveMode = configuration.GetValue<bool>("ToolCalling:InteractiveMode");
        if (interactiveMode)
        {
            services.AddSingleton<IToolCallingListener, ConsoleToolCallingListener>();
        }
        else
        {
            services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
        }

        // 根据 LLM 提供商选择序列化器
        var provider = configuration["LLM:DefaultProvider"];
        if (provider?.Contains("Claude", StringComparison.OrdinalIgnoreCase) == true)
        {
            services.AddSingleton<IToolSerializer, AnthropicToolSerializer>();
        }
        else
        {
            services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
        }

        return services;
    }
}
```

- [ ] **步骤 2: 更新 Program.cs**

```csharp
// v3/src/GeneralAgent.API/Program.cs
var builder = WebApplication.CreateBuilder(args);

// 现有服务
builder.Services.AddDbContext<AgentDbContext>();
builder.Services.AddLLMServices(builder.Configuration);
builder.Services.AddSkillServices(builder.Configuration);

// 新增：Tool Calling 服务
builder.Services.AddToolCallingServices(builder.Configuration);

builder.Services.AddScoped<ConversationService>();
builder.Services.AddControllers();

var app = builder.Build();

// 启动时加载 skills
using (var scope = app.Services.CreateScope())
{
    var skillService = scope.ServiceProvider.GetRequiredService<SkillService>();
    await skillService.LoadSkillsAsync();
}

app.MapControllers();
app.Run();
```

- [ ] **步骤 3: 验证配置**

```bash
cd v3
dotnet build
```

预期: 编译成功，无错误

- [ ] **步骤 4: 提交**

```bash
git add src/GeneralAgent.Application/DependencyInjection/ServiceCollectionExtensions.cs \
        src/GeneralAgent.API/Program.cs
git commit -m "feat(v3): 配置 Tool Calling DI 容器

- 添加扩展方法 AddToolCallingServices
- 根据配置选择 Listener 和 Serializer
- 启动时自动加载 skills"
```

---

### Task 15: 端到端测试

**Files:**
- Create: `v3/tests/GeneralAgent.E2E.Tests/SkillSystemE2ETests.cs`

- [ ] **步骤 1: 创建 E2E 测试**

```csharp
// v3/tests/GeneralAgent.E2E.Tests/SkillSystemE2ETests.cs
namespace GeneralAgent.E2E.Tests;

public class SkillSystemE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SkillSystemE2ETests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExplicitSkillCall_ShouldReturnLLMResponse()
    {
        // Arrange
        var request = new
        {
            sessionId = Guid.NewGuid(),
            message = "@greeting user_name='Charlie'"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/conversations/message", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("Charlie", content);
    }

    [Fact]
    public async Task ImplicitSkillCall_ShouldTriggerToolCalling()
    {
        // Arrange
        var request = new
        {
            sessionId = Guid.NewGuid(),
            message = "帮我向 David 打个招呼"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/conversations/message", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("David", content);
    }

    [Fact]
    public async Task ContextAwareSkill_ShouldIncludeHistory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // 发送第一条消息
        await _client.PostAsJsonAsync("/api/conversations/message", new
        {
            sessionId,
            message = "我叫 Eve"
        });

        // 发送第二条消息（需要上下文）
        var request = new
        {
            sessionId,
            message = "@summarize count=1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/conversations/message", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("Eve", content);
    }
}
```

- [ ] **步骤 2: 运行 E2E 测试**

```bash
dotnet test tests/GeneralAgent.E2E.Tests/SkillSystemE2ETests.cs
```

预期: PASS（需要真实的 LLM 或 Mock）

- [ ] **步骤 3: 提交**

```bash
git add tests/GeneralAgent.E2E.Tests/SkillSystemE2ETests.cs
git commit -m "test(v3): E2E 测试验证完整 skill 系统

- 显式 skill 调用
- 隐式 skill 调用（Tool Calling）
- 上下文感知 skill"
```

---

### Task 16: 性能测试

**Files:**
- Create: `v3/tests/GeneralAgent.Performance.Tests/ToolCallingPerformanceTests.cs`

- [ ] **步骤 1: 创建性能测试**

```csharp
// v3/tests/GeneralAgent.Performance.Tests/ToolCallingPerformanceTests.cs
namespace GeneralAgent.Performance.Tests;

public class ToolCallingPerformanceTests
{
    [Fact]
    public async Task ToolCallingOverhead_ShouldBeLessThan200ms()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        // Warmup
        await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"Tool Calling 开销 {stopwatch.ElapsedMilliseconds}ms 超过 200ms 限制");
    }

    [Fact]
    public async Task ParallelToolExecution_ShouldBeFast()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var toolExecutor = provider.GetRequiredService<ToolExecutor>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册 3 个耗时 100ms 的 tool
        for (int i = 0; i < 3; i++)
        {
            var tool = CreateDelayTool($"tool{i}", TimeSpan.FromMilliseconds(100));
            registry.Register(tool);
        }

        var toolCalls = Enumerable.Range(0, 3)
            .Select(i => new ToolCall
            {
                Id = $"call_{i}",
                FunctionName = $"tool{i}",
                Arguments = "{}"
            })
            .ToList();

        var context = new ToolExecutionContext { SessionId = Guid.NewGuid() };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await toolExecutor.ExecuteManyAsync(toolCalls, context, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        // 并行执行 3 个 100ms 的工具应该接近 100ms，而不是 300ms
        Assert.True(stopwatch.ElapsedMilliseconds < 150,
            $"并行执行耗时 {stopwatch.ElapsedMilliseconds}ms，未达到并行效果");
    }

    private ITool CreateDelayTool(string name, TimeSpan delay)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.ExecuteAsync(Arg.Any<Dictionary<string, object>>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(delay);
                return Result<string>.Success("Done");
            });
        return tool;
    }
}
```

- [ ] **步骤 2: 运行性能测试**

```bash
dotnet test tests/GeneralAgent.Performance.Tests/ToolCallingPerformanceTests.cs
```

预期: PASS

- [ ] **步骤 3: 提交**

```bash
git add tests/GeneralAgent.Performance.Tests/ToolCallingPerformanceTests.cs
git commit -m "test(v3): 性能测试验证 Tool Calling 开销

- Tool Calling 开销 < 200ms
- 并行工具执行接近理论时间"
```

---

### Task 17: 文档更新

**Files:**
- Create: `v3/docs/tool-calling.md`
- Modify: `v3/README.md`

- [ ] **步骤 1: 创建 Tool Calling 文档**

```markdown
# Tool Calling 使用指南

## 概述

Tool Calling 允许 LLM 主动选择和调用 skills，实现更智能的对话。

## 配置

```json
{
  "ToolCalling": {
    "Enabled": true,
    "MaxRounds": 3,
    "InteractiveMode": true,
    "AutoExtendBy": 5,
    "AbsoluteMaxRounds": 20
  }
}
```

## 使用方式

### 显式调用

```bash
@greeting user_name='Alice'
```

### 隐式调用

```bash
帮我向 Bob 打个招呼
```

LLM 会自动决定调用 `greeting` skill。

## 上下文感知 Skill

```yaml
---
name: summarize
requires_context: true
context_config:
  max_messages: 10
  roles: [user, assistant]
---

请总结以下对话：
{{ for msg in context.messages }}
[{{ msg.role }}]: {{ msg.content }}
{{ end }}
```

## 用户确认机制

当达到最大轮数限制时，系统会询问用户是否继续：

```
⚠️  Tool Calling 已执行 3 轮
是否继续？
  [y] 继续 3 轮
  [5] 继续 5 轮
  [10] 继续 10 轮
  [n] 停止
```

## 最佳实践

1. **合理设置轮数限制**：避免无限循环
2. **优化 skill 描述**：帮助 LLM 理解何时使用
3. **使用上下文注入**：需要历史信息时启用
4. **监控性能**：关注 Tool Calling 开销

```

- [ ] **步骤 2: 更新 README**

```bash
# 在 v3/README.md 中添加 Tool Calling 章节
```

- [ ] **步骤 3: 提交**

```bash
git add v3/docs/tool-calling.md v3/README.md
git commit -m "docs(v3): 添加 Tool Calling 使用文档

- 配置说明
- 使用示例
- 最佳实践"
```

---

## 验收清单

### Phase 3.1 验收 ✅
- [ ] ITool 接口定义完整
- [ ] ToolRegistry 功能正常（注册、查找、列举）
- [ ] ToolExecutor 执行单个和批量工具
- [ ] 单元测试覆盖率 ≥ 80%

### Phase 3.2 验收 ✅
- [ ] Skill 模型扩展完成
- [ ] SkillExecutor 调用 LLM
- [ ] SkillExecutor 支持上下文注入
- [ ] 显式 skill 调用（`@greeting`）正常工作
- [ ] 集成测试通过

### Phase 3.3 验收 ✅
- [ ] ToolCallingOrchestrator 循环逻辑正常
- [ ] 用户确认机制工作正常
- [ ] 隐式 skill 调用正常工作
- [ ] LLM 能够主动选择和调用 skill
- [ ] 集成测试通过

### Phase 3.4 验收 ✅
- [ ] ConversationService 集成所有组件
- [ ] 支持流式和非流式响应
- [ ] 配置系统完整
- [ ] E2E 测试通过
- [ ] 性能测试通过（< 200ms overhead）
- [ ] 文档完整

---

## 注意事项

1. **TDD 原则**：每个功能都先写测试，验证失败后再实现
2. **频繁提交**：每完成一个小任务就提交，保持 git 历史清晰
3. **DRY 原则**：避免重复代码，提取公共逻辑
4. **YAGNI 原则**：只实现必需的功能，不过度设计
5. **类型安全**：充分利用 C# 的类型系统，编译时捕获错误
6. **异步优先**：所有 I/O 操作使用 async/await
7. **日志记录**：关键操作都记录日志，便于调试
8. **错误处理**：使用 Result 模式，避免抛出异常
9. **线程安全**：ToolRegistry 等共享状态使用 lock
10. **性能监控**：使用 Stopwatch 记录关键操作耗时

---

**计划编写完成**。准备进行规范审查循环。
