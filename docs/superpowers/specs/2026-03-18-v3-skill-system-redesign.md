# V3 Skill 系统重新设计规范

**日期**: 2026-03-18
**版本**: 1.0
**状态**: Draft
**作者**: Claude (Sonnet 4.5)

---

## 目录

- [概述](#概述)
- [问题陈述](#问题陈述)
- [设计目标](#设计目标)
- [架构设计](#架构设计)
- [详细设计](#详细设计)
- [数据模型](#数据模型)
- [配置系统](#配置系统)
- [测试策略](#测试策略)
- [实施计划](#实施计划)
- [验收标准](#验收标准)
- [风险和缓解](#风险和缓解)

---

## 概述

### 背景

V3 (C#) 的 skill 系统目前存在架构缺陷：skill 渲染后的文本直接作为最终响应返回，没有调用 LLM。这导致用户只能获得静态的、模板化的响应，失去了 AI 助手的智能特性。

参考 V1 (Python) 的正确设计，skill 应该作为**提示词模板**，渲染后发送给 LLM 生成智能响应。

### 新需求

除了修复现有问题，用户还希望支持**隐式 skill 调用**：让 LLM 主动评估场景并选择调用合适的 skill，而不仅仅通过 `@skill` 显式语法。

这要求 skill 作为 **LLM 的工具（Tools/Function Calling）** 使用。

### 未来扩展

确定要集成 MCP (Model Context Protocol) 和 RAG，因此架构设计需要前瞻性，支持统一的工具抽象。

---

## 问题陈述

### 当前问题

**V3 现有实现**：
```csharp
SkillExecutor.Execute(skill, arguments)
  ├─ ValidateArguments()
  ├─ RenderTemplate()  // 使用 Scriban
  └─ return Result.Success(renderedText)  // ❌ 直接返回，没有调用 LLM
```

**V1 正确实现**：
```python
SkillExecutor.execute(skill, parameters)
  ├─ validate_parameters()
  ├─ build_prompt()      # 渲染模板
  ├─ call_llm(prompt)    # ✅ 调用 LLM
  └─ return SkillExecutionResult(output=llm_response)
```

### 影响

- ❌ 用户体验差：只能获得死板的模板文本
- ❌ 失去智能：无法根据上下文动态生成内容
- ❌ 功能受限：无法支持隐式调用（LLM 主动选择 skill）

---

## 设计目标

### 功能目标

1. **修复 LLM 集成**：skill 渲染后调用 LLM，返回智能响应
2. **支持隐式调用**：LLM 可以主动决定调用 skill（Function Calling）
3. **上下文感知**：skill 可以访问会话历史
4. **灵活配置**：
   - skill 可配置是否需要上下文
   - skill 结果可配置是否返回 LLM（`return_to_llm`）
   - Tool Calling 循环次数可配置
5. **用户控制**：达到循环限制时询问用户是否继续

### 架构目标

1. **统一抽象**：定义 `ITool` 接口，为 MCP 和 RAG 集成铺路
2. **职责清晰**：各组件职责单一，易于测试和维护
3. **开放-封闭**：添加新工具类型无需修改核心逻辑
4. **渐进交付**：分 4 个子阶段，每个阶段独立可验收

### 质量目标

1. **测试覆盖率** ≥ 80%
2. **性能开销** < 200ms（相比直接 LLM 调用）
3. **向后兼容**：现有 skill 文件格式保持兼容

---

## 架构设计

### 整体架构图

```
┌────────────────────────────────────────────────────────┐
│              ConversationService                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 1. 检查显式 skill (@skill)                        │  │
│  │    └→ ToolExecutor.ExecuteAsync()                │  │
│  │                                                    │  │
│  │ 2. 否则进入 Tool Calling 模式                     │  │
│  │    └→ ToolCallingOrchestrator.ExecuteAsync()     │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
                       │
       ┌───────────────┴───────────────┐
       │                               │
       ▼                               ▼
┌─────────────────┐      ┌──────────────────────────┐
│  ToolExecutor   │      │ ToolCallingOrchestrator  │
│                 │      │ - 管理 Tool Calling 循环  │
│ - ExecuteAsync()│      │ - 用户确认逻辑           │
└────────┬────────┘      │ - 调用 ToolExecutor      │
         │               └──────────────────────────┘
         │                            │
         ▼                            │
┌─────────────────┐                  │
│   ToolRegistry  │◄─────────────────┘
│                 │
│ - Register()    │
│ - GetTool()     │
└────────┬────────┘
         │
         │ 注册
         ▼
┌───────────────────────────────────────────────┐
│                  ITool                         │
│ - Name: string                                │
│ - Description: string                         │
│ - GetDefinition(): ToolDefinition             │
│ - ExecuteAsync(): Result<string>              │
└─────────────┬─────────────────────────────────┘
              │
     ┌────────┴─────┬───────────┬──────────┐
     │              │           │          │
     ▼              ▼           ▼          ▼
┌─────────┐  ┌──────────┐ ┌──────────┐ ┌─────────┐
│SkillTool│  │ MCPTool  │ │ RAGTool  │ │ Custom  │
│         │  │(Phase 4) │ │(Phase 5) │ │(Future) │
└─────────┘  └──────────┘ └──────────┘ └─────────┘
```

### 核心组件

| 组件 | 职责 | 依赖 |
|------|------|------|
| **ITool** | 统一工具接口 | 无 |
| **ToolRegistry** | 管理工具注册和查找 | ITool |
| **ToolExecutor** | 执行单个工具 | ToolRegistry |
| **SkillTool** | Skill 作为 Tool 的实现 | Skill, ISkillExecutor |
| **SkillExecutor** | 执行 Skill（渲染 + LLM） | ILLMClient, IMessageRepository |
| **ToolCallingOrchestrator** | 管理 Tool Calling 循环 | ToolExecutor, ILLMClient |
| **ConversationService** | 协调整体对话流程 | ToolExecutor, ToolCallingOrchestrator |

### 数据流

**显式调用流程**：
```
用户: @greeting user_name='Alice'
  ↓
ConversationService 识别显式调用
  ↓
ToolExecutor.ExecuteAsync("greeting", {user_name: "Alice"})
  ↓
ToolRegistry.GetTool("greeting") → SkillTool
  ↓
SkillTool.ExecuteAsync()
  ├─ SkillExecutor.ExecuteAsync()
  │  ├─ 验证参数
  │  ├─ 注入上下文（如需要）
  │  ├─ 渲染 Scriban 模板 → "你好 Alice！..."
  │  ├─ 调用 LLM（提示词 = 渲染结果）
  │  └─ 返回 LLM 响应
  └─ 返回 Result<string>
```

**隐式调用流程**：
```
用户: "帮我向 Alice 打个招呼"
  ↓
ConversationService → ToolCallingOrchestrator
  ↓
循环 (round = 1):
  ├─ 准备 tools 定义（所有 skill）
  ├─ LLM.CompleteAsync(messages, tools)
  ├─ LLM 响应: tool_calls=[{name: "greeting", args: {user_name: "Alice"}}]
  ├─ ToolExecutor.ExecuteAsync("greeting", ...)
  │  └─ 返回: "你好 Alice！很高兴见到你..."
  ├─ 将结果添加到 messages
  └─ 检查是否继续循环
  ↓
循环 (round = 2):
  ├─ LLM.CompleteAsync(messages_with_tool_result, tools)
  ├─ LLM 响应: "我已经向 Alice 打招呼了：[引用结果]"
  ├─ 无 tool_calls，结束循环
  └─ 返回最终响应
```

---

## 详细设计

### 1. ITool 接口

```csharp
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

**设计决策**：
- 统一接口，支持所有工具类型
- 同时支持流式和非流式
- `ToolExecutionContext` 封装执行所需的上下文信息

### 2. ToolRegistry

```csharp
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly object _lock = new();

    public void Register(ITool tool) { /* 线程安全注册 */ }
    public ITool? GetTool(string name) { /* 查找工具 */ }
    public IReadOnlyList<ITool> GetAllTools() { /* 列举所有工具 */ }
    public IReadOnlyList<ITool> GetToolsByNamespace(string ns) { /* 按命名空间过滤 */ }
}
```

**设计决策**：
- 单例模式，全局共享
- 线程安全（使用 lock）
- 支持命名空间过滤（如 "personal:*"）

### 3. ToolExecutor

```csharp
public sealed class ToolExecutor
{
    public async Task<Result<string>> ExecuteAsync(
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        var tool = _registry.GetTool(toolName);
        if (tool == null)
            return Result<string>.Failure($"工具不存在: {toolName}");

        return await tool.ExecuteAsync(arguments, context, ct);
    }

    public async Task<List<ToolCallResult>> ExecuteManyAsync(
        IEnumerable<ToolCall> toolCalls,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        // 并行执行多个工具
        var tasks = toolCalls.Select(tc => ExecuteAsync(...));
        return await Task.WhenAll(tasks);
    }
}
```

**设计决策**：
- 职责单一：执行工具，不关心循环逻辑
- 支持并行执行（`ExecuteManyAsync`）
- 错误处理统一

### 4. SkillTool 实现

```csharp
public sealed class SkillTool : ITool
{
    private readonly Skill _skill;
    private readonly ISkillExecutor _executor;

    public string Name => _skill.FullName;
    public string Description => _skill.Description;

    public ToolDefinition GetDefinition()
    {
        return _converter.Convert(_skill);
    }

    public async Task<Result<string>> ExecuteAsync(
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        return await _executor.ExecuteAsync(
            _skill,
            arguments,
            context.SessionId,
            context.ProviderName,
            ct);
    }
}
```

**设计决策**：
- 适配器模式：将 Skill 适配为 ITool
- 委托给 `ISkillExecutor` 执行实际逻辑
- 简单、职责清晰

### 5. SkillExecutor 升级

```csharp
public sealed class SkillExecutor : ISkillExecutor
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMessageRepository _messageRepo;

    public async Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName,
        CancellationToken ct)
    {
        // 1. 验证参数
        var validated = ValidateArguments(skill, arguments);

        // 2. 注入上下文（如果需要）
        if (skill.RequiresContext)
        {
            var messages = await _messageRepo.GetRecentAsync(...);
            arguments["context"] = new { messages };
        }

        // 3. 渲染 Scriban 模板
        var prompt = RenderTemplate(skill, arguments);

        // 4. 调用 LLM（关键步骤！）
        var client = _llmFactory.GetClient(providerName);
        var response = await client.CompleteAsync(new CompletionRequest
        {
            Model = "qwen3.5:0.8b",
            Messages = new[] { new ChatMessage { Role = "user", Content = prompt } }
        }, ct);

        // 5. 返回 LLM 生成的响应
        return Result<string>.Success(response.Content);
    }
}
```

**关键改进**：
- ✅ 步骤 4：渲染后调用 LLM，返回智能响应
- ✅ 步骤 2：支持上下文注入
- ✅ 错误处理：使用 Result 模式

### 6. ToolCallingOrchestrator

```csharp
public sealed class ToolCallingOrchestrator
{
    public async Task<ConversationResult> ExecuteAsync(
        Guid sessionId,
        List<ChatMessage> history,
        string? providerName,
        CancellationToken ct)
    {
        var tools = PrepareToolDefinitions(); // 转换所有工具为 LLM 格式
        int currentRound = 0;
        int maxRounds = _config.MaxRounds;

        while (currentRound < _config.AbsoluteMaxRounds)
        {
            // 1. 调用 LLM（带 tools）
            var response = await _llmClient.CompleteAsync(new CompletionRequest
            {
                Messages = history,
                Tools = tools
            }, ct);

            // 2. 检查 tool calls
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                // 无 tool calls，返回最终响应
                return new ConversationResult { FinalResponse = response.Content, ... };
            }

            // 3. 递增轮数并检查限制
            currentRound++;
            if (currentRound >= maxRounds)
            {
                var decision = await _listener.OnMaxRoundsReachedAsync(...);
                if (decision.Stop) break;
                maxRounds += decision.ExtendBy;
            }

            // 4. 执行 tool calls
            var results = await _toolExecutor.ExecuteManyAsync(response.ToolCalls, ...);

            // 5. 将结果添加到历史
            history.Add(new ChatMessage { Role = "assistant", ToolCalls = response.ToolCalls });
            foreach (var result in results)
            {
                history.Add(new ChatMessage { Role = "tool", Content = result.Content });
            }
        }
    }
}
```

**设计决策**：
- 职责：管理 Tool Calling 循环
- 支持用户确认（通过 `IToolCallingListener`）
- 记录完整历史（包含 tool calls 和 results）
- 绝对最大轮数限制（防止无限循环）

### 7. IToolCallingListener

```csharp
public interface IToolCallingListener
{
    Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls);
}

// Console 实现
public class ConsoleToolCallingListener : IToolCallingListener
{
    public async Task<ExtendDecision> OnMaxRoundsReachedAsync(...)
    {
        Console.WriteLine($"⚠️  已执行 {currentRounds} 轮，是否继续？");
        Console.WriteLine("[y] 继续 3 轮  [5] 继续 5 轮  [10] 继续 10 轮  [n] 停止");

        var input = Console.ReadLine();
        return input switch
        {
            "y" => new ExtendDecision { ExtendBy = 3 },
            "5" => new ExtendDecision { ExtendBy = 5 },
            "10" => new ExtendDecision { ExtendBy = 10 },
            "n" => new ExtendDecision { Stop = true },
            _ => new ExtendDecision { ExtendBy = 3 }
        };
    }
}

// 自动模式实现（后台服务）
public class AutomaticToolCallingListener : IToolCallingListener
{
    public Task<ExtendDecision> OnMaxRoundsReachedAsync(...)
    {
        return Task.FromResult(new ExtendDecision { ExtendBy = 5 });
    }
}
```

**设计决策**：
- 接口抽象，支持不同宿主（Console、Service）
- 提供两种实现：交互式、自动式
- 通过 DI 配置选择使用哪种

### 8. ConversationService 重构

```csharp
public sealed class ConversationService
{
    public async Task<string> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        string? providerName,
        CancellationToken ct)
    {
        // 1. 保存用户消息
        await _messageRepo.CreateAsync(Message.CreateUser(sessionId, userMessage), ct);

        // 2. 检查显式 skill 调用
        if (SkillCallParser.TryParse(userMessage, out var skillCall))
        {
            var result = await _toolExecutor.ExecuteAsync(
                skillCall.SkillName,
                skillCall.Arguments,
                new ToolExecutionContext { SessionId = sessionId, ProviderName = providerName },
                ct);

            var response = result.IsSuccess ? result.Value! : $"❌ {result.Error}";
            await SaveAssistantMessage(sessionId, response, ct);
            return response;
        }

        // 3. 进入 Tool Calling 模式
        var history = await GetChatHistory(sessionId, ct);
        var conversationResult = await _orchestrator.ExecuteAsync(sessionId, history, providerName, ct);

        // 4. 保存历史
        await SaveConversationHistory(sessionId, conversationResult.Messages, ct);
        await SaveAssistantMessage(sessionId, conversationResult.FinalResponse, ct);

        return conversationResult.FinalResponse;
    }
}
```

**设计决策**：
- 职责：协调器，不包含复杂逻辑
- 显式调用 → ToolExecutor
- 隐式调用 → ToolCallingOrchestrator
- 统一保存消息历史

---

## 数据模型

### 核心模型

```csharp
// 工具定义（LLM Function Calling 格式）
public record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject InputSchema { get; init; }
}

// 工具执行上下文
public record ToolExecutionContext
{
    public required Guid SessionId { get; init; }
    public string? ProviderName { get; init; }
    public IReadOnlyList<Message>? HistoryMessages { get; init; }
}

// 对话结果
public record ConversationResult
{
    public required string FinalResponse { get; init; }
    public int TotalRounds { get; init; }
    public int TotalToolCalls { get; init; }
    public List<ChatMessage> Messages { get; init; } = new();
    public bool Truncated { get; init; }
    public string? TruncationReason { get; init; }
}

// 用户决策
public record ExtendDecision
{
    public bool Stop { get; init; }
    public int ExtendBy { get; init; }
}
```

### Skill 模型扩展

```csharp
public record Skill
{
    // 现有字段
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Template { get; init; }
    public required IReadOnlyList<SkillParameter> Parameters { get; init; }

    // 新增字段
    public bool RequiresContext { get; init; } = false;
    public ContextConfig? ContextConfig { get; init; }
    public bool ReturnToLLM { get; init; } = true;
}

public record ContextConfig
{
    public int MaxMessages { get; init; } = 10;
    public string[]? Roles { get; init; }
    public bool IncludeSystemMessages { get; init; } = false;
}
```

### Skill 文件格式扩展

```markdown
---
name: summarize
description: 总结最近的对话内容
namespace: productivity

# 上下文配置
requires_context: true
context_config:
  max_messages: 10
  roles: [user, assistant]

# 执行配置
return_to_llm: true

# 参数
parameters:
  - name: count
    type: int
    required: false
    default_value: 5
---

请总结以下对话的最近 {{ count }} 条消息：

{{ if context }}
{{ for msg in context.messages }}
[{{ msg.role }}]: {{ msg.content }}
{{ end }}
{{ end }}

请用简洁的语言总结对话的核心要点。
```

---

## 配置系统

### appsettings.json

```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "Providers": {
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "DefaultModel": "qwen3.5:0.8b"
      }
    }
  },
  "ToolCalling": {
    "Enabled": true,
    "MaxRounds": 3,
    "InteractiveMode": true,
    "AutoExtendBy": 5,
    "AbsoluteMaxRounds": 20
  },
  "Skills": {
    "Directory": "skills"
  }
}
```

### 配置类

```csharp
public record ToolCallingConfig
{
    public bool Enabled { get; init; } = true;
    public int MaxRounds { get; init; } = 3;
    public bool InteractiveMode { get; init; } = true;
    public int AutoExtendBy { get; init; } = 5;
    public int AbsoluteMaxRounds { get; init; } = 20;
}
```

### 依赖注入

```csharp
services.Configure<ToolCallingConfig>(configuration.GetSection("ToolCalling"));

services.AddSingleton<ToolRegistry>();
services.AddSingleton<ToolExecutor>();
services.AddSingleton<ToolCallingOrchestrator>();

// 根据配置选择 Listener
if (configuration.GetValue<bool>("ToolCalling:InteractiveMode"))
{
    services.AddSingleton<IToolCallingListener, ConsoleToolCallingListener>();
}
else
{
    services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
}

// 根据 LLM 提供商选择序列化器
var provider = configuration["LLM:DefaultProvider"];
if (provider?.Contains("Claude") == true)
{
    services.AddSingleton<IToolSerializer, AnthropicToolSerializer>();
}
else
{
    services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
}
```

---

## 测试策略

### 测试金字塔

```
E2E Tests (5%)
├─ 完整对话流程
└─ Tool Calling 循环

Integration Tests (25%)
├─ ToolExecutor + SkillTool
├─ Orchestrator + LLM Mock
└─ ConversationService 集成

Unit Tests (70%)
├─ ToolRegistry
├─ SkillToToolConverter
├─ SkillExecutor
└─ 模型验证
```

### 测试覆盖率目标

| 组件 | 目标覆盖率 |
|------|-----------|
| ToolRegistry | 90%+ |
| ToolExecutor | 85%+ |
| SkillExecutor | 85%+ |
| ToolCallingOrchestrator | 80%+ |
| ConversationService | 75%+ |
| **整体** | **80%+** |

### 关键测试场景

**单元测试**：
- ToolRegistry 注册和查找
- SkillToToolConverter 参数映射
- SkillExecutor 参数验证
- SkillExecutor 上下文注入
- SkillExecutor LLM 调用

**集成测试**：
- ToolExecutor + SkillTool 完整流程
- Orchestrator 循环逻辑
- 用户确认交互
- 并行工具执行

**E2E 测试**：
- 显式 skill 调用
- 隐式 skill 调用
- 上下文感知 skill
- 多轮 Tool Calling

---

## 实施计划

### Phase 3.1: 核心抽象（2 天）

**目标**：实现统一的工具抽象

**任务**：
1. 定义 `ITool` 接口
2. 实现 `ToolRegistry`
3. 实现 `ToolExecutor`
4. 编写单元测试

**验收**：
- [ ] 接口定义完整
- [ ] ToolRegistry 功能正常
- [ ] 单元测试覆盖率 ≥ 90%

---

### Phase 3.2: SkillTool 实现（2 天）

**目标**：将 Skill 适配为 Tool

**任务**：
1. 实现 `SkillTool`
2. 实现 `SkillToToolConverter`
3. 升级 `SkillExecutor`（添加 LLM 调用）
4. 扩展 Skill 模型（上下文配置）
5. 编写集成测试

**验收**：
- [ ] SkillTool 实现 ITool
- [ ] SkillExecutor 调用 LLM
- [ ] 显式 skill 调用正常
- [ ] 上下文注入工作正常
- [ ] 集成测试通过

---

### Phase 3.3: Tool Calling 循环（2 天）

**目标**：实现隐式 skill 调用

**任务**：
1. 实现 `IToolCallingListener` 接口
2. 实现 `ConsoleToolCallingListener`
3. 实现 `AutomaticToolCallingListener`
4. 实现 `IToolSerializer` 接口
5. 实现 `OpenAIToolSerializer`
6. 实现 `ToolCallingOrchestrator`
7. 编写集成测试

**验收**：
- [ ] Orchestrator 循环逻辑正常
- [ ] 用户确认工作正常
- [ ] 隐式 skill 调用成功
- [ ] 集成测试通过

---

### Phase 3.4: 完整集成（1-2 天）

**目标**：集成所有组件并验收

**任务**：
1. 重构 `ConversationService`
2. 更新 `SkillService`（注册为 Tool）
3. 配置 DI 容器
4. 编写 E2E 测试
5. 性能测试
6. 文档更新

**验收**：
- [ ] ConversationService 集成完成
- [ ] 支持流式和非流式
- [ ] E2E 测试通过
- [ ] 性能测试通过（< 200ms overhead）
- [ ] 文档完整

---

## 验收标准

### 功能验收

#### 1. 显式 Skill 调用 ✅

```bash
# 输入
@greeting user_name='Alice'

# 预期输出（LLM 生成）
你好 Alice！很高兴见到你！我是你的 AI 助手，今天有什么可以帮助你的吗？
```

#### 2. 隐式 Skill 调用 ✅

```bash
# 输入
帮我向 Bob 打个招呼

# 预期过程
1. LLM 分析 → 决定调用 greeting(user_name="Bob")
2. 执行 skill → 渲染 → LLM → 返回 "你好 Bob！..."
3. LLM 接收结果 → 生成最终响应

# 预期输出
我已经向 Bob 打招呼了：你好 Bob！很高兴认识你！
```

#### 3. 上下文感知 Skill ✅

```bash
# 对话历史
用户: 我叫 Charlie
助手: 你好 Charlie！
用户: 我喜欢编程
助手: 编程很有趣！

# 输入
@summarize count=3

# 预期输出（包含历史信息）
根据最近的对话，我了解到：
1. 你的名字是 Charlie
2. 你对编程感兴趣
...
```

#### 4. Tool Calling 循环控制 ✅

```bash
# 场景：连续调用多个 skill

# 输入
帮我做三件事：1. 向 Alice 打招呼 2. 创建一个提醒 3. 总结对话

# 预期过程
Round 1: 调用 greeting(user_name="Alice")
Round 2: 调用 reminder(task="...", time="...")
Round 3: 调用 summarize(count=5)
Round 3 达到限制 → 询问用户
用户选择继续 3 轮
Round 4: LLM 生成最终响应

# 预期：用户确认提示出现，循环正常结束
```

### 性能验收

| 指标 | 目标 | 测试方法 |
|------|------|---------|
| Tool Calling 开销 | < 200ms | 对比直接 LLM 调用 |
| 并行工具执行 | 接近并行时间 | 3个工具 < 1.5倍单个时间 |
| 内存占用 | < 100MB 额外 | 对比 Phase 2 基线 |

### 质量验收

- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 集成测试覆盖率 ≥ 70%
- [ ] 无 Critical/High 级别 Bug
- [ ] 代码审查通过
- [ ] 文档完整（README、注释、设计文档）

---

## 风险和缓解

### 风险 1: LLM 不主动调用 skill

**描述**：LLM 可能不理解 tool 定义，无法正确选择 skill

**概率**：中
**影响**：高

**缓解措施**：
1. 优化 skill 描述，提供清晰的使用场景
2. 在 system prompt 中明确说明可以使用工具
3. 测试不同模型（Ollama、Claude）的 Function Calling 能力
4. 提供降级方案：显式调用始终可用

---

### 风险 2: 循环次数过多导致响应慢

**描述**：LLM 频繁调用 skill，导致用户等待时间过长

**概率**：中
**影响**：中

**缓解措施**：
1. 设置合理的默认最大轮数（3 轮）
2. 提供用户确认机制
3. 显示进度提示（"正在调用工具..."）
4. 记录和监控平均循环次数

---

### 风险 3: 上下文注入性能问题

**描述**：每次 skill 执行都查询数据库获取历史消息

**概率**：低
**影响**：低

**缓解措施**：
1. 只在 `RequiresContext = true` 时查询
2. 限制最大消息数量（默认 10）
3. 在 Orchestrator 层预加载历史（只查询一次）
4. 添加缓存层（如需要）

---

### 风险 4: 向后兼容性问题

**描述**：现有 skill 文件可能无法正常工作

**概率**：低
**影响**：中

**缓解措施**：
1. 新增字段都有默认值
2. 保持 YAML frontmatter 格式兼容
3. 提供迁移指南
4. 编写兼容性测试

---

## 附录

### A. 参考实现对比

| 特性 | V1 (Python) | V3 (重设计) |
|------|------------|------------|
| Skill 渲染 | ✅ 字符串替换 | ✅ Scriban 模板 |
| LLM 调用 | ✅ 是 | ✅ 是 |
| 隐式调用 | ❌ 否 | ✅ 是 |
| 上下文注入 | ❌ 否 | ✅ 是 |
| 统一工具抽象 | ❌ 否 | ✅ 是 |
| 流式响应 | ✅ 是 | ✅ 是 |

### B. 术语表

| 术语 | 定义 |
|------|------|
| **Skill** | 参数化的提示词模板，定义在 Markdown 文件中 |
| **Tool** | LLM 可调用的功能单元（Function Calling） |
| **显式调用** | 用户通过 `@skill` 语法直接调用 |
| **隐式调用** | LLM 分析场景后主动决定调用 |
| **Tool Calling** | LLM 的 Function Calling 机制 |
| **上下文注入** | 将会话历史注入到 skill 模板中 |
| **循环轮数** | Tool Calling 的执行次数 |

### C. 相关文档

- [V1 Skill 系统实现](../../../v1/src/skills/)
- [V3 Phase 2 LLM 集成](./2026-03-16-v3-phase2-llm-integration-design.md)
- [Skill 系统对比分析](../analysis/skill-system-comparison.md)
- [OpenAI Function Calling](https://platform.openai.com/docs/guides/function-calling)
- [Anthropic Tool Use](https://docs.anthropic.com/en/docs/tool-use)

---

**文档结束**
