# 自动上下文压缩功能设计

**创建时间**: 2026-04-06
**状态**: 📋 设计阶段
**优先级**: ⭐⭐⭐⭐ 高

---

## 📋 功能概述

当对话的上下文窗口使用率超过 **90%** 时，自动触发上下文压缩，避免达到 token 限制导致对话中断。

---

## 🎯 核心需求

### 用户故事

**作为用户**，我希望：
- 在长对话中不会因为 token 限制而中断
- Agent 能自动检测并压缩上下文
- 压缩过程透明且可配置
- 保留对话的关键信息

**作为开发者**，我希望：
- 压缩逻辑解耦且可测试
- 支持多种 LLM 的 token 限制
- 可配置触发阈值和策略
- 有完整的日志和监控

---

## 🏗️ 架构设计

### 1. 核心组件

```
┌─────────────────────────────────────────────────────┐
│           ConversationService (对话服务)              │
├─────────────────────────────────────────────────────┤
│  - HandleUserMessageAsync()                         │
│  - CheckAndCompressIfNeeded() ← 新增                │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────────────────┐
│    AutoCompressionManager (自动压缩管理器) ← 新增    │
├─────────────────────────────────────────────────────┤
│  - CheckShouldCompress()                            │
│  - TriggerCompressionAsync()                        │
│  - NotifyUserAsync()                                │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────────────────┐
│    ContextUsageMonitor (上下文监控器) ← 新增         │
├─────────────────────────────────────────────────────┤
│  - CalculateUsageRatio()                            │
│  - GetTokenLimit()                                  │
│  - GetCurrentTokens()                               │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────────────────┐
│    CompressionOrchestrator (压缩编排器) - 已存在     │
├─────────────────────────────────────────────────────┤
│  - CompressAsync()                                  │
│  - RecommendStrategy()                              │
└─────────────────────────────────────────────────────┘
```

---

### 2. 数据模型

#### AutoCompressionOptions（配置）

```csharp
/// <summary>
/// 自动压缩配置
/// </summary>
public sealed record AutoCompressionOptions
{
    /// <summary>
    /// 是否启用自动压缩（默认 true）
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 触发阈值（上下文使用率百分比，默认 0.9 = 90%）
    /// </summary>
    public double TriggerThreshold { get; init; } = 0.9;

    /// <summary>
    /// 压缩后目标使用率（默认 0.6 = 60%）
    /// </summary>
    public double TargetUsageAfterCompression { get; init; } = 0.6;

    /// <summary>
    /// 最小消息数才考虑压缩（默认 10）
    /// </summary>
    public int MinMessagesForCompression { get; init; } = 10;

    /// <summary>
    /// 自动压缩策略（null = 自动推荐）
    /// </summary>
    public string? PreferredStrategy { get; init; } = null;

    /// <summary>
    /// 是否通知用户（默认 true）
    /// </summary>
    public bool NotifyUser { get; init; } = true;

    /// <summary>
    /// 通知延迟（毫秒，默认 500ms）
    /// </summary>
    public int NotificationDelayMs { get; init; } = 500;

    /// <summary>
    /// 是否记录压缩历史（默认 true）
    /// </summary>
    public bool LogCompressionHistory { get; init; } = true;

    /// <summary>
    /// 冷却时间（秒，两次压缩之间的最小间隔，默认 60 秒）
    /// </summary>
    public int CooldownSeconds { get; init; } = 60;
}
```

#### ContextUsageInfo（使用率信息）

```csharp
/// <summary>
/// 上下文使用率信息
/// </summary>
public sealed record ContextUsageInfo
{
    /// <summary>
    /// 当前 token 数
    /// </summary>
    public int CurrentTokens { get; init; }

    /// <summary>
    /// Token 限制
    /// </summary>
    public int TokenLimit { get; init; }

    /// <summary>
    /// 使用率（0.0 - 1.0）
    /// </summary>
    public double UsageRatio => (double)CurrentTokens / TokenLimit;

    /// <summary>
    /// 剩余 token 数
    /// </summary>
    public int RemainingTokens => TokenLimit - CurrentTokens;

    /// <summary>
    /// 是否超过阈值
    /// </summary>
    public bool ExceedsThreshold(double threshold) => UsageRatio >= threshold;

    /// <summary>
    /// 使用率百分比（格式化）
    /// </summary>
    public string UsagePercentage => $"{UsageRatio:P1}";
}
```

#### AutoCompressionEvent（压缩事件）

```csharp
/// <summary>
/// 自动压缩事件
/// </summary>
public sealed record AutoCompressionEvent
{
    public Guid SessionId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ContextUsageInfo BeforeCompression { get; init; } = null!;
    public ContextUsageInfo AfterCompression { get; init; } = null!;
    public string StrategyUsed { get; init; } = "";
    public int MessagesBefore { get; init; }
    public int MessagesAfter { get; init; }
    public long CompressionTimeMs { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## 🔧 核心实现

### 1. ContextUsageMonitor（上下文监控器）

```csharp
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 上下文使用率监控器
/// </summary>
public sealed class ContextUsageMonitor
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<ContextUsageMonitor> _logger;

    // 不同模型的 token 限制
    private static readonly Dictionary<string, int> ModelTokenLimits = new()
    {
        // Ollama 模型
        ["qwen2.5:0.5b"] = 8192,
        ["qwen2.5:3b"] = 32768,
        ["qwen2.5:7b"] = 131072,
        ["qwen2.5:14b"] = 131072,
        
        // Anthropic Claude
        ["claude-3-opus"] = 200000,
        ["claude-3-sonnet"] = 200000,
        ["claude-3-haiku"] = 200000,
        ["claude-3.5-sonnet"] = 200000,
        
        // OpenAI
        ["gpt-4-turbo"] = 128000,
        ["gpt-4"] = 8192,
        ["gpt-3.5-turbo"] = 16384,
    };

    public ContextUsageMonitor(
        ITokenCounter tokenCounter,
        ILogger<ContextUsageMonitor> logger)
    {
        _tokenCounter = tokenCounter;
        _logger = logger;
    }

    /// <summary>
    /// 计算当前上下文使用率
    /// </summary>
    public ContextUsageInfo CalculateUsage(
        List<Message> messages,
        string modelName)
    {
        var currentTokens = _tokenCounter.CountMessagesTokens(messages);
        var tokenLimit = GetTokenLimit(modelName);

        var info = new ContextUsageInfo
        {
            CurrentTokens = currentTokens,
            TokenLimit = tokenLimit
        };

        _logger.LogDebug(
            "上下文使用率: {Usage} ({Current}/{Limit} tokens)",
            info.UsagePercentage,
            currentTokens,
            tokenLimit);

        return info;
    }

    /// <summary>
    /// 获取模型的 token 限制
    /// </summary>
    public int GetTokenLimit(string modelName)
    {
        // 尝试精确匹配
        if (ModelTokenLimits.TryGetValue(modelName, out var limit))
        {
            return limit;
        }

        // 尝试前缀匹配（例如 "qwen2.5:7b-instruct" → "qwen2.5:7b"）
        foreach (var (key, value) in ModelTokenLimits)
        {
            if (modelName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "模型 '{Model}' 通过前缀匹配到 '{Key}'，token 限制: {Limit}",
                    modelName, key, value);
                return value;
            }
        }

        // 默认值（保守估计）
        _logger.LogWarning(
            "未知模型 '{Model}'，使用默认 token 限制: 8192",
            modelName);
        return 8192;
    }

    /// <summary>
    /// 检查是否应该压缩
    /// </summary>
    public bool ShouldCompress(
        ContextUsageInfo usageInfo,
        AutoCompressionOptions options)
    {
        return usageInfo.ExceedsThreshold(options.TriggerThreshold);
    }
}
```

---

### 2. AutoCompressionManager（自动压缩管理器）

```csharp
using System.Diagnostics;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 自动压缩管理器
/// </summary>
public sealed class AutoCompressionManager
{
    private readonly ContextUsageMonitor _usageMonitor;
    private readonly ICompressionOrchestrator _compressionOrchestrator;
    private readonly IMessageRepository _messageRepository;
    private readonly AutoCompressionOptions _options;
    private readonly ILogger<AutoCompressionManager> _logger;

    // 会话冷却时间跟踪（SessionId → 最后压缩时间）
    private readonly Dictionary<Guid, DateTime> _lastCompressionTime = new();

    public AutoCompressionManager(
        ContextUsageMonitor usageMonitor,
        ICompressionOrchestrator compressionOrchestrator,
        IMessageRepository messageRepository,
        IOptions<AutoCompressionOptions> options,
        ILogger<AutoCompressionManager> logger)
    {
        _usageMonitor = usageMonitor;
        _compressionOrchestrator = compressionOrchestrator;
        _messageRepository = messageRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 检查并在需要时触发自动压缩
    /// </summary>
    public async Task<AutoCompressionResult> CheckAndCompressIfNeededAsync(
        Guid sessionId,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        // 1. 检查是否启用
        if (!_options.Enabled)
        {
            return AutoCompressionResult.Skipped("自动压缩未启用");
        }

        // 2. 获取会话消息
        var messages = await _messageRepository.GetBySessionAsync(sessionId, cancellationToken);

        // 3. 检查最小消息数
        if (messages.Count < _options.MinMessagesForCompression)
        {
            return AutoCompressionResult.Skipped(
                $"消息数 ({messages.Count}) 少于最小值 ({_options.MinMessagesForCompression})");
        }

        // 4. 计算上下文使用率
        var usageInfo = _usageMonitor.CalculateUsage(messages, modelName);

        // 5. 检查是否超过阈值
        if (!usageInfo.ExceedsThreshold(_options.TriggerThreshold))
        {
            return AutoCompressionResult.Skipped(
                $"使用率 {usageInfo.UsagePercentage} 未达到阈值 {_options.TriggerThreshold:P0}");
        }

        // 6. 检查冷却时间
        if (IsInCooldown(sessionId))
        {
            var remaining = GetCooldownRemaining(sessionId);
            return AutoCompressionResult.Skipped(
                $"压缩冷却中，剩余 {remaining.TotalSeconds:F0} 秒");
        }

        // 7. 触发压缩
        _logger.LogWarning(
            "🔔 自动压缩触发: SessionId={SessionId}, 使用率={Usage}, 阈值={Threshold}",
            sessionId,
            usageInfo.UsagePercentage,
            _options.TriggerThreshold);

        return await TriggerCompressionAsync(
            sessionId,
            messages,
            usageInfo,
            modelName,
            cancellationToken);
    }

    /// <summary>
    /// 触发压缩
    /// </summary>
    private async Task<AutoCompressionResult> TriggerCompressionAsync(
        Guid sessionId,
        List<Message> messages,
        ContextUsageInfo usageInfoBefore,
        string modelName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. 选择压缩策略
            var strategy = _options.PreferredStrategy
                ?? _compressionOrchestrator.RecommendStrategy(messages, null);

            _logger.LogInformation(
                "📦 开始自动压缩: 策略={Strategy}, 消息数={Count}, 当前tokens={Tokens}",
                strategy,
                messages.Count,
                usageInfoBefore.CurrentTokens);

            // 2. 计算目标 token 数
            var targetTokens = (int)(usageInfoBefore.TokenLimit * _options.TargetUsageAfterCompression);

            // 3. 执行压缩
            var compressionOptions = new CompressionOptions
            {
                Strategy = strategy,
                EnableLlmSummary = true,
                PreserveSystemMessages = true,
                PreserveRecentCount = CalculatePreserveCount(messages, targetTokens)
            };

            var compressionResult = await _compressionOrchestrator.CompressAsync(
                messages,
                compressionOptions,
                cancellationToken);

            if (!compressionResult.Success)
            {
                _logger.LogError("压缩失败: {Error}", compressionResult.ErrorMessage);
                return AutoCompressionResult.Failed(compressionResult.ErrorMessage ?? "未知错误");
            }

            // 4. 替换会话消息
            await ReplaceSessionMessagesAsync(
                sessionId,
                compressionResult.CompressedMessages,
                cancellationToken);

            // 5. 计算压缩后的使用率
            var usageInfoAfter = _usageMonitor.CalculateUsage(
                compressionResult.CompressedMessages,
                modelName);

            stopwatch.Stop();

            // 6. 记录压缩事件
            var compressionEvent = new AutoCompressionEvent
            {
                SessionId = sessionId,
                BeforeCompression = usageInfoBefore,
                AfterCompression = usageInfoAfter,
                StrategyUsed = strategy,
                MessagesBefore = messages.Count,
                MessagesAfter = compressionResult.CompressedMessages.Count,
                CompressionTimeMs = stopwatch.ElapsedMilliseconds,
                Success = true
            };

            if (_options.LogCompressionHistory)
            {
                await LogCompressionEventAsync(compressionEvent, cancellationToken);
            }

            // 7. 更新冷却时间
            _lastCompressionTime[sessionId] = DateTime.UtcNow;

            _logger.LogInformation(
                "✅ 自动压缩完成: {Before}条 → {After}条, {UsageBefore} → {UsageAfter}, 耗时 {Duration}ms",
                compressionEvent.MessagesBefore,
                compressionEvent.MessagesAfter,
                usageInfoBefore.UsagePercentage,
                usageInfoAfter.UsagePercentage,
                stopwatch.ElapsedMilliseconds);

            return AutoCompressionResult.Success(compressionEvent);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "自动压缩异常");
            return AutoCompressionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// 计算应保留的最近消息数
    /// </summary>
    private int CalculatePreserveCount(List<Message> messages, int targetTokens)
    {
        // 从最后一条消息开始累计，直到达到目标 token 数
        int totalTokens = 0;
        int count = 0;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msg = messages[i];
            var msgTokens = _usageMonitor._tokenCounter.CountMessageTokens(msg);
            totalTokens += msgTokens;

            if (totalTokens >= targetTokens)
            {
                break;
            }

            count++;
        }

        // 至少保留 5 条
        return Math.Max(5, count);
    }

    /// <summary>
    /// 替换会话消息
    /// </summary>
    private async Task ReplaceSessionMessagesAsync(
        Guid sessionId,
        List<Message> compressedMessages,
        CancellationToken cancellationToken)
    {
        // 1. 删除旧消息
        await _messageRepository.DeleteBySessionAsync(sessionId, cancellationToken);

        // 2. 插入压缩后的消息
        foreach (var msg in compressedMessages)
        {
            msg.SessionId = sessionId; // 确保 SessionId 正确
            await _messageRepository.AddAsync(msg, cancellationToken);
        }

        _logger.LogDebug(
            "会话消息已替换: SessionId={SessionId}, 新消息数={Count}",
            sessionId,
            compressedMessages.Count);
    }

    /// <summary>
    /// 记录压缩事件
    /// </summary>
    private async Task LogCompressionEventAsync(
        AutoCompressionEvent compressionEvent,
        CancellationToken cancellationToken)
    {
        // 保存到 CompressionHistory 表
        // TODO: 实现保存逻辑
        await Task.CompletedTask;
    }

    /// <summary>
    /// 检查是否在冷却期
    /// </summary>
    private bool IsInCooldown(Guid sessionId)
    {
        if (!_lastCompressionTime.TryGetValue(sessionId, out var lastTime))
        {
            return false;
        }

        var elapsed = DateTime.UtcNow - lastTime;
        return elapsed.TotalSeconds < _options.CooldownSeconds;
    }

    /// <summary>
    /// 获取冷却剩余时间
    /// </summary>
    private TimeSpan GetCooldownRemaining(Guid sessionId)
    {
        if (!_lastCompressionTime.TryGetValue(sessionId, out var lastTime))
        {
            return TimeSpan.Zero;
        }

        var elapsed = DateTime.UtcNow - lastTime;
        var remaining = TimeSpan.FromSeconds(_options.CooldownSeconds) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}

/// <summary>
/// 自动压缩结果
/// </summary>
public sealed record AutoCompressionResult
{
    public bool Triggered { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
    public AutoCompressionEvent? Event { get; init; }

    public static AutoCompressionResult Skipped(string reason)
        => new() { Triggered = false, Success = false, Message = reason };

    public static AutoCompressionResult Success(AutoCompressionEvent compressionEvent)
        => new() { Triggered = true, Success = true, Event = compressionEvent };

    public static AutoCompressionResult Failed(string error)
        => new() { Triggered = true, Success = false, Message = error };
}
```

---

### 3. 集成到 ConversationService

```csharp
public class ConversationService
{
    private readonly AutoCompressionManager _autoCompressionManager;
    private readonly ILLMClientFactory _llmFactory;
    private readonly ILogger<ConversationService> _logger;

    public async Task<string> HandleUserMessageAsync(
        Guid sessionId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        // 1. 检查并触发自动压缩（在处理消息前）
        var modelName = "qwen2.5:7b"; // 从配置获取
        var compressionResult = await _autoCompressionManager.CheckAndCompressIfNeededAsync(
            sessionId,
            modelName,
            cancellationToken);

        // 2. 通知用户（如果压缩成功）
        if (compressionResult.Triggered && compressionResult.Success && compressionResult.Event != null)
        {
            await NotifyUserAboutCompressionAsync(compressionResult.Event);
        }

        // 3. 正常处理用户消息
        // ... 原有逻辑
    }

    private async Task NotifyUserAboutCompressionAsync(AutoCompressionEvent compressionEvent)
    {
        var notification = $"""
            ⚡ **自动压缩完成**
            
            - 消息数: {compressionEvent.MessagesBefore} → {compressionEvent.MessagesAfter}
            - 上下文使用率: {compressionEvent.BeforeCompression.UsagePercentage} → {compressionEvent.AfterCompression.UsagePercentage}
            - 策略: {compressionEvent.StrategyUsed}
            - 耗时: {compressionEvent.CompressionTimeMs}ms
            
            > 对话上下文已自动优化，你可以继续提问。
            """;

        // 通过 REPL 或 TUI 显示通知
        _logger.LogInformation(notification);

        // TODO: 集成到 TUI 通知系统
        await Task.Delay(500); // 延迟显示，避免打断用户输入
    }
}
```

---

## 📋 配置示例

### appsettings.json

```json
{
  "AutoCompression": {
    "Enabled": true,
    "TriggerThreshold": 0.9,
    "TargetUsageAfterCompression": 0.6,
    "MinMessagesForCompression": 10,
    "PreferredStrategy": null,
    "NotifyUser": true,
    "NotificationDelayMs": 500,
    "LogCompressionHistory": true,
    "CooldownSeconds": 60
  }
}
```

### 依赖注入注册

```csharp
// Program.cs 或 DependencyInjection.cs
public static IServiceCollection AddAutoCompression(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // 注册配置
    services.Configure<AutoCompressionOptions>(
        configuration.GetSection("AutoCompression"));

    // 注册服务
    services.AddSingleton<ContextUsageMonitor>();
    services.AddSingleton<AutoCompressionManager>();

    return services;
}
```

---

## 🧪 测试计划

### 单元测试

```csharp
public class ContextUsageMonitorTests
{
    [Theory]
    [InlineData("qwen2.5:7b", 131072)]
    [InlineData("claude-3-sonnet", 200000)]
    [InlineData("gpt-4", 8192)]
    [InlineData("unknown-model", 8192)] // 默认值
    public void GetTokenLimit_ShouldReturnCorrectLimit(string modelName, int expected)
    {
        var monitor = CreateMonitor();
        var limit = monitor.GetTokenLimit(modelName);
        limit.Should().Be(expected);
    }

    [Fact]
    public void CalculateUsage_ShouldReturnCorrectInfo()
    {
        var monitor = CreateMonitor();
        var messages = CreateTestMessages(count: 10, avgLength: 100);

        var usageInfo = monitor.CalculateUsage(messages, "qwen2.5:7b");

        usageInfo.CurrentTokens.Should().BeGreaterThan(0);
        usageInfo.TokenLimit.Should().Be(131072);
        usageInfo.UsageRatio.Should().BeInRange(0.0, 1.0);
    }

    [Theory]
    [InlineData(0.85, 0.9, false)] // 未超过
    [InlineData(0.91, 0.9, true)]  // 超过
    [InlineData(0.95, 0.9, true)]  // 超过
    public void ShouldCompress_ShouldReturnCorrectResult(
        double usageRatio,
        double threshold,
        bool expected)
    {
        var monitor = CreateMonitor();
        var usageInfo = new ContextUsageInfo
        {
            CurrentTokens = (int)(131072 * usageRatio),
            TokenLimit = 131072
        };
        var options = new AutoCompressionOptions { TriggerThreshold = threshold };

        var result = monitor.ShouldCompress(usageInfo, options);

        result.Should().Be(expected);
    }
}

public class AutoCompressionManagerTests
{
    [Fact]
    public async Task CheckAndCompressIfNeeded_WhenDisabled_ShouldSkip()
    {
        var manager = CreateManager(enabled: false);

        var result = await manager.CheckAndCompressIfNeededAsync(
            Guid.NewGuid(),
            "qwen2.5:7b");

        result.Triggered.Should().BeFalse();
        result.Message.Should().Contain("未启用");
    }

    [Fact]
    public async Task CheckAndCompressIfNeeded_WhenBelowThreshold_ShouldSkip()
    {
        var sessionId = Guid.NewGuid();
        var messages = CreateTestMessages(count: 20, avgLength: 50); // 低使用率
        var manager = CreateManager(messages: messages);

        var result = await manager.CheckAndCompressIfNeededAsync(sessionId, "qwen2.5:7b");

        result.Triggered.Should().BeFalse();
        result.Message.Should().Contain("未达到阈值");
    }

    [Fact]
    public async Task CheckAndCompressIfNeeded_WhenExceedsThreshold_ShouldCompress()
    {
        var sessionId = Guid.NewGuid();
        var messages = CreateTestMessages(count: 200, avgLength: 500); // 高使用率
        var manager = CreateManager(messages: messages);

        var result = await manager.CheckAndCompressIfNeededAsync(sessionId, "qwen2.5:7b");

        result.Triggered.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.MessagesAfter.Should().BeLessThan(messages.Count);
    }

    [Fact]
    public async Task CheckAndCompressIfNeeded_WhenInCooldown_ShouldSkip()
    {
        var sessionId = Guid.NewGuid();
        var messages = CreateTestMessages(count: 200, avgLength: 500);
        var manager = CreateManager(messages: messages, cooldownSeconds: 60);

        // 第一次压缩
        await manager.CheckAndCompressIfNeededAsync(sessionId, "qwen2.5:7b");

        // 立即第二次（应跳过）
        var result = await manager.CheckAndCompressIfNeededAsync(sessionId, "qwen2.5:7b");

        result.Triggered.Should().BeFalse();
        result.Message.Should().Contain("冷却中");
    }
}
```

---

### 集成测试

```csharp
public class AutoCompressionIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task AutoCompression_EndToEnd_ShouldWork()
    {
        // 1. 创建长对话（90% 使用率）
        var sessionId = await CreateLongConversationAsync(messageCount: 150);

        // 2. 添加新消息触发检查
        var conversationService = CreateConversationService();
        var response = await conversationService.HandleUserMessageAsync(
            sessionId,
            "继续讨论",
            CancellationToken.None);

        // 3. 验证压缩已触发
        var messages = await _messageRepository.GetBySessionAsync(sessionId);
        messages.Count.Should().BeLessThan(150); // 已压缩

        // 4. 验证上下文使用率降低
        var usageInfo = _usageMonitor.CalculateUsage(messages, "qwen2.5:7b");
        usageInfo.UsageRatio.Should().BeLessThan(0.7); // 目标 60%，允许误差

        // 5. 验证历史记录
        var history = await _compressionHistoryRepository.GetBySessionAsync(sessionId);
        history.Should().ContainSingle();
        history[0].StrategyUsed.Should().NotBeNullOrEmpty();
    }
}
```

---

## 📊 性能和监控

### 性能指标

| 场景 | 预期性能 |
|------|----------|
| 使用率计算 | < 10ms |
| 压缩触发检查 | < 50ms |
| 自动压缩（缓存未命中） | 2-5秒 |
| 自动压缩（缓存命中） | 50-200ms |
| 消息替换（100条） | < 200ms |

### 监控指标

```csharp
// 添加 Prometheus 指标
public class AutoCompressionMetrics
{
    private static readonly Counter TriggerCount = Metrics.CreateCounter(
        "auto_compression_trigger_total",
        "自动压缩触发次数");

    private static readonly Counter SuccessCount = Metrics.CreateCounter(
        "auto_compression_success_total",
        "自动压缩成功次数");

    private static readonly Histogram CompressionDuration = Metrics.CreateHistogram(
        "auto_compression_duration_seconds",
        "自动压缩耗时");

    private static readonly Gauge ContextUsageRatio = Metrics.CreateGauge(
        "context_usage_ratio",
        "上下文使用率",
        labelNames: new[] { "session_id", "model" });
}
```

---

## 🚀 实施计划

### Phase 1: 核心功能（3-4 天）

**目标**: 实现基本的自动压缩功能

1. ✅ 实现 `ContextUsageMonitor`（1 天）
   - Token 限制映射
   - 使用率计算
   - 阈值检查

2. ✅ 实现 `AutoCompressionManager`（2 天）
   - 压缩触发逻辑
   - 消息替换
   - 冷却时间管理

3. ✅ 集成到 `ConversationService`（0.5 天）
   - 消息处理前检查
   - 用户通知

4. ✅ 单元测试（0.5 天）
   - 覆盖率 > 80%

---

### Phase 2: 配置和优化（2-3 天）

**目标**: 添加配置支持和性能优化

1. ✅ 配置系统（0.5 天）
   - `AutoCompressionOptions`
   - appsettings.json 绑定
   - 依赖注入注册

2. ✅ 性能优化（1 天）
   - 缓存 token 计数结果
   - 批量消息操作
   - 并发安全

3. ✅ 集成测试（1 天）
   - 端到端场景
   - 数据库持久化

4. ✅ 日志和监控（0.5 天）
   - 结构化日志
   - 性能指标

---

### Phase 3: 用户体验（1-2 天）

**目标**: 改进用户通知和可视化

1. ✅ REPL 命令（0.5 天）
   - `/auto-compression status` - 查看状态
   - `/auto-compression enable/disable` - 启用/禁用
   - `/auto-compression config` - 查看配置

2. ✅ TUI 通知（1 天）
   - 压缩进度条
   - 压缩完成通知
   - 使用率可视化

3. ✅ 文档（0.5 天）
   - 用户指南
   - 配置参考

---

## 📝 待创建的文件

### 核心代码

1. `v3/src/GeneralAgent.Application/Services/ContextUsageMonitor.cs`
2. `v3/src/GeneralAgent.Application/Services/AutoCompressionManager.cs`
3. `v3/src/GeneralAgent.Application/Models/AutoCompressionOptions.cs`
4. `v3/src/GeneralAgent.Application/Models/ContextUsageInfo.cs`
5. `v3/src/GeneralAgent.Application/Models/AutoCompressionEvent.cs`

### 测试

6. `v3/tests/GeneralAgent.Application.Tests/Services/ContextUsageMonitorTests.cs`
7. `v3/tests/GeneralAgent.Application.Tests/Services/AutoCompressionManagerTests.cs`
8. `v3/tests/GeneralAgent.Application.Tests/Integration/AutoCompressionIntegrationTests.cs`

### 文档

9. `v3/docs/guides/AUTO_COMPRESSION_USER_GUIDE.md` - 用户指南
10. `v3/docs/features/auto-compression-config-reference.md` - 配置参考

---

## 🎯 成功指标

### 功能完整性

- ✅ 自动检测上下文使用率
- ✅ 超过阈值时自动压缩
- ✅ 支持多种 LLM 模型
- ✅ 可配置触发阈值和策略
- ✅ 用户通知和日志记录
- ✅ 冷却时间机制

### 性能

- ✅ 使用率计算 < 10ms
- ✅ 压缩触发检查 < 50ms
- ✅ 自动压缩（缓存命中）< 200ms

### 质量

- ✅ 单元测试覆盖率 > 80%
- ✅ 集成测试覆盖核心场景
- ✅ 无已知 bug
- ✅ 代码审查通过

### 用户体验

- ✅ 压缩过程透明（用户知道发生了什么）
- ✅ 不会打断用户输入
- ✅ 压缩效果可预测（目标 60% 使用率）
- ✅ 可通过配置调整行为

---

## 💡 未来增强（可选）

### 1. 智能预测压缩

**动机**: 在达到 90% 之前预测何时需要压缩

**方案**:
- 分析对话模式（消息长度、频率）
- 预测未来 5-10 条消息的 token 增长
- 提前触发压缩

**预计耗时**: 1-2 周

---

### 2. 分段压缩

**动机**: 避免一次性压缩大量消息导致阻塞

**方案**:
- 将消息分为多个时间段
- 逐段压缩，允许中断
- 后台异步压缩

**预计耗时**: 1-2 周

---

### 3. 用户偏好学习

**动机**: 根据用户行为调整压缩策略

**方案**:
- 记录用户对压缩的反应（继续对话/重新提问）
- 学习用户偏好的压缩率和策略
- 自适应调整参数

**预计耗时**: 2-3 周

---

### 4. 压缩质量评估

**动机**: 确保压缩不丢失关键信息

**方案**:
- 使用 LLM 评估压缩前后的信息保留度
- 如果质量不达标，尝试其他策略
- 提供质量报告

**预计耗时**: 1-2 周

---

## 📞 反馈和讨论

如果你对自动压缩功能有任何建议，欢迎反馈！

---

**最后更新**: 2026-04-06
**维护者**: General Agent Team
**版本**: v1.0
