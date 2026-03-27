# General Agent V3 - 上下文压缩设计文档

**功能**: 上下文压缩（Context Compression）
**Phase**: Phase 6 - Task 1
**创建日期**: 2026-03-26
**预计时间**: 1-2 周
**优先级**: P0

---

## 📋 目录

- [需求分析](#需求分析)
- [技术架构](#技术架构)
- [数据模型](#数据模型)
- [API 设计](#api-设计)
- [压缩策略](#压缩策略)
- [实现计划](#实现计划)
- [测试策略](#测试策略)

---

## 🎯 需求分析

### 1.1 问题定义

**核心问题**:
- 长对话导致 token 数量激增，增加 API 成本
- 超过模型上下文窗口限制时对话中断
- 携带完整历史影响响应速度

**用户场景**:
```
场景 1: 长时间技术讨论
- 用户与 Agent 进行 50+ 轮对话
- 当前 token: ~8000
- 问题: 接近 Claude 的上下文限制，成本高

场景 2: 代码审查会话
- 用户粘贴多个代码片段
- 早期代码片段不再相关
- 问题: 浪费 token，影响相关性

场景 3: 多主题讨论
- 对话涉及多个独立主题
- 需要保留每个主题的关键信息
- 问题: 如何智能选择保留内容
```

### 1.2 功能目标

#### 主要目标
1. **降低成本**: Token 使用减少 40-60%
2. **保持质量**: 对话连贯性和准确性不降低
3. **透明可控**: 用户可以查看和控制压缩行为
4. **性能优化**: 压缩操作 < 100ms

#### 次要目标
1. 支持多种压缩策略
2. 可配置的压缩阈值
3. 压缩历史记录
4. 压缩效果统计

### 1.3 非功能需求

| 需求 | 目标 | 说明 |
|------|------|------|
| 性能 | < 100ms | 压缩操作延迟 |
| 准确性 | > 90% | 关键信息保留率 |
| Token 节省 | 40-60% | 压缩率 |
| 可用性 | 99.9% | 服务可用性 |
| 可扩展性 | 支持新策略 | 插件化设计 |

---

## 🏗️ 技术架构

### 2.1 系统架构

```
┌─────────────────────────────────────────────────────────┐
│                    AgentRepl / CLI                       │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ /compress    │  │ /context     │  │ Automatic    │  │
│  │ command      │  │ status       │  │ Compression  │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │
└─────────┼──────────────────┼──────────────────┼─────────┘
          │                  │                  │
          └──────────────────┴──────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │   CompressionOrchestrator           │
          │  - Strategy Selection               │
          │  - Trigger Management               │
          │  - Compression Execution            │
          └──────────────────┬──────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │   ICompressionStrategy (Interface)  │
          └──────────────────┬──────────────────┘
                             │
      ┌──────────┬───────────┼───────────┬──────────┐
      │          │           │           │          │
┌─────▼────┐ ┌──▼────┐ ┌────▼────┐ ┌────▼────┐ ┌──▼────┐
│ Sliding  │ │Hierarch│ │ Semantic│ │Adaptive │ │Custom │
│ Window   │ │ical    │ │         │ │         │ │       │
└─────┬────┘ └──┬─────┘ └────┬────┘ └────┬────┘ └──┬────┘
      │         │            │           │          │
      └─────────┴────────────┴───────────┴──────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │      Supporting Services             │
          │                                      │
          │  ┌────────────┐  ┌────────────┐    │
          │  │ Token      │  │ LLM Client │    │
          │  │ Counter    │  │ (Summary)  │    │
          │  └────────────┘  └────────────┘    │
          │                                      │
          │  ┌────────────┐  ┌────────────┐    │
          │  │ Key Point  │  │ Compression│    │
          │  │ Extractor  │  │ Stats      │    │
          │  └────────────┘  └────────────┘    │
          └─────────────────────────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │       Storage Layer                  │
          │                                      │
          │  ┌────────────┐  ┌────────────┐    │
          │  │ Messages   │  │ Compression│    │
          │  │ Table      │  │ History    │    │
          │  └────────────┘  └────────────┘    │
          └─────────────────────────────────────┘
```

### 2.2 核心组件

#### 2.2.1 CompressionOrchestrator
**职责**: 协调压缩流程，选择和执行策略

```csharp
public class CompressionOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CompressionConfig _config;
    private readonly ILogger<CompressionOrchestrator> _logger;
    
    // 执行压缩
    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null)
    {
        // 1. 评估是否需要压缩
        if (!ShouldCompress(messages, options))
        {
            return CompressionResult.NotNeeded(messages);
        }
        
        // 2. 选择压缩策略
        var strategy = SelectStrategy(messages, options);
        
        // 3. 执行压缩
        var startTime = DateTime.UtcNow;
        var compressed = await strategy.CompressAsync(messages, options);
        var duration = DateTime.UtcNow - startTime;
        
        // 4. 记录压缩历史
        await SaveCompressionHistory(messages, compressed, duration);
        
        // 5. 更新统计
        await UpdateStatistics(messages, compressed);
        
        return compressed;
    }
    
    // 评估是否需要压缩
    private bool ShouldCompress(
        List<Message> messages,
        CompressionOptions? options)
    {
        // 检查触发条件
        var tokenCount = _tokenCounter.CountTokens(messages);
        var messageCount = messages.Count;
        
        return tokenCount > _config.TokenThreshold ||
               messageCount > _config.MessageThreshold;
    }
    
    // 选择压缩策略
    private ICompressionStrategy SelectStrategy(
        List<Message> messages,
        CompressionOptions? options)
    {
        // 优先使用用户指定的策略
        if (options?.Strategy != null)
        {
            return _serviceProvider.GetRequiredService(options.Strategy);
        }
        
        // 根据配置选择默认策略
        var strategyName = _config.DefaultStrategy;
        return strategyName switch
        {
            "sliding" => _serviceProvider.GetRequiredService<SlidingWindowStrategy>(),
            "hierarchical" => _serviceProvider.GetRequiredService<HierarchicalStrategy>(),
            "semantic" => _serviceProvider.GetRequiredService<SemanticStrategy>(),
            "adaptive" => _serviceProvider.GetRequiredService<AdaptiveStrategy>(),
            _ => _serviceProvider.GetRequiredService<SlidingWindowStrategy>()
        };
    }
}
```

#### 2.2.2 ICompressionStrategy 接口
**职责**: 定义压缩策略的统一接口

```csharp
public interface ICompressionStrategy
{
    string Name { get; }
    string Description { get; }
    
    // 执行压缩
    Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null);
    
    // 估算压缩后的 token 数
    int EstimateCompressedTokens(List<Message> messages);
    
    // 检查策略是否适用
    bool IsApplicable(List<Message> messages);
}
```

#### 2.2.3 TokenCounter
**职责**: 准确计算 token 数量

```csharp
public class TokenCounter
{
    private readonly Dictionary<string, TikToken> _encoders = new();
    
    public TokenCounter()
    {
        // 预加载常用模型的 encoder
        _encoders["claude-3"] = TikToken.EncodingForModel("gpt-4"); // Claude 使用类似的 tokenizer
        _encoders["gpt-4"] = TikToken.EncodingForModel("gpt-4");
        _encoders["gpt-3.5"] = TikToken.EncodingForModel("gpt-3.5-turbo");
    }
    
    public int CountTokens(string text, string model = "claude-3")
    {
        if (!_encoders.TryGetValue(model, out var encoder))
        {
            encoder = _encoders["claude-3"]; // 默认
        }
        
        return encoder.Encode(text).Count;
    }
    
    public int CountTokens(List<Message> messages, string model = "claude-3")
    {
        // 消息格式开销：每条消息约 4 个 token
        var formatOverhead = messages.Count * 4;
        
        // 内容 token
        var contentTokens = messages.Sum(m => CountTokens(m.Content, model));
        
        // 角色标记：每个角色约 1 个 token
        var roleTokens = messages.Count;
        
        return formatOverhead + contentTokens + roleTokens;
    }
    
    // 估算压缩比例
    public float EstimateCompressionRatio(int originalTokens, int compressedTokens)
    {
        if (originalTokens == 0) return 0;
        return 1 - ((float)compressedTokens / originalTokens);
    }
}
```

---

## 💾 数据模型

### 3.1 配置模型

```csharp
public class CompressionConfig
{
    // 触发阈值
    public int TokenThreshold { get; set; } = 4000;
    public int MessageThreshold { get; set; } = 50;
    
    // 默认策略
    public string DefaultStrategy { get; set; } = "hierarchical";
    
    // 自动压缩
    public bool AutoCompressionEnabled { get; set; } = true;
    
    // 策略配置
    public SlidingWindowConfig SlidingWindow { get; set; } = new();
    public HierarchicalConfig Hierarchical { get; set; } = new();
    public SemanticConfig Semantic { get; set; } = new();
}

public class SlidingWindowConfig
{
    public int WindowSize { get; set; } = 10;
}

public class HierarchicalConfig
{
    public int RecentMessagesCount { get; set; } = 10;
    public int SummaryMessagesCount { get; set; } = 30;
    public int ArchiveThreshold { get; set; } = 50;
}

public class SemanticConfig
{
    public bool Enabled { get; set; } = false; // 默认关闭（成本考虑）
    public int MaxSummaryTokens { get; set; } = 500;
    public string SummaryPrompt { get; set; } = 
        "请总结以下对话的关键点，保留重要信息：\n\n{messages}";
}
```

### 3.2 压缩结果模型

```csharp
public class CompressionResult
{
    public bool WasCompressed { get; set; }
    public string Strategy { get; set; }
    
    // 压缩后的消息
    public List<Message> CompressedMessages { get; set; }
    
    // 统计信息
    public CompressionStats Stats { get; set; }
    
    // 压缩元数据
    public CompressionMetadata Metadata { get; set; }
    
    public static CompressionResult NotNeeded(List<Message> messages)
    {
        return new CompressionResult
        {
            WasCompressed = false,
            CompressedMessages = messages,
            Stats = new CompressionStats
            {
                OriginalTokens = 0,
                CompressedTokens = 0,
                CompressionRatio = 0
            }
        };
    }
}

public class CompressionStats
{
    public int OriginalMessages { get; set; }
    public int CompressedMessages { get; set; }
    public int OriginalTokens { get; set; }
    public int CompressedTokens { get; set; }
    public float CompressionRatio { get; set; }
    public TimeSpan Duration { get; set; }
}

public class CompressionMetadata
{
    public DateTime CompressedAt { get; set; }
    public string Strategy { get; set; }
    public Dictionary<string, object> StrategyParams { get; set; }
    
    // 保留的关键信息摘要
    public string? Summary { get; set; }
    
    // 被压缩的消息 ID 列表
    public List<string> CompressedMessageIds { get; set; }
}
```

### 3.3 数据库模式

```sql
-- 压缩历史表
CREATE TABLE compression_history (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    strategy TEXT NOT NULL,
    
    -- 压缩前
    original_message_count INTEGER NOT NULL,
    original_token_count INTEGER NOT NULL,
    
    -- 压缩后
    compressed_message_count INTEGER NOT NULL,
    compressed_token_count INTEGER NOT NULL,
    
    -- 统计
    compression_ratio REAL NOT NULL,
    duration_ms INTEGER NOT NULL,
    
    -- 元数据
    metadata TEXT, -- JSON
    summary TEXT,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

-- 压缩配置表（用户自定义）
CREATE TABLE compression_configs (
    id TEXT PRIMARY KEY,
    user_id TEXT,
    name TEXT NOT NULL,
    config TEXT NOT NULL, -- JSON
    is_default BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

---

## 🔧 API 设计

### 4.1 服务层 API

```csharp
// CompressionService.cs
public class CompressionService
{
    // 压缩会话消息
    public async Task<CompressionResult> CompressSessionAsync(
        Guid sessionId,
        CompressionOptions? options = null)
    {
        var messages = await _messageRepo.GetBySessionAsync(sessionId);
        return await _orchestrator.CompressAsync(messages, options);
    }
    
    // 压缩指定消息
    public async Task<CompressionResult> CompressMessagesAsync(
        List<Message> messages,
        CompressionOptions? options = null)
    {
        return await _orchestrator.CompressAsync(messages, options);
    }
    
    // 获取压缩统计
    public async Task<CompressionStatistics> GetStatisticsAsync(
        Guid sessionId)
    {
        var history = await _historyRepo.GetBySessionAsync(sessionId);
        
        return new CompressionStatistics
        {
            TotalCompressions = history.Count,
            TotalTokensSaved = history.Sum(h => 
                h.OriginalTokenCount - h.CompressedTokenCount),
            AverageCompressionRatio = history.Average(h => 
                h.CompressionRatio),
            MostUsedStrategy = history.GroupBy(h => h.Strategy)
                .OrderByDescending(g => g.Count())
                .First().Key
        };
    }
    
    // 获取压缩历史
    public async Task<List<CompressionHistory>> GetHistoryAsync(
        Guid sessionId,
        int limit = 10)
    {
        return await _historyRepo.GetBySessionAsync(sessionId, limit);
    }
}
```

### 4.2 CLI 命令 API

```bash
# 查看上下文状态
/context status                   # 显示当前 token 使用情况
/context stats                    # 显示压缩统计

# 手动压缩
/context compress                 # 使用默认策略压缩
/context compress --strategy sliding  # 指定策略
/context compress --preview       # 预览压缩结果

# 配置压缩
/context config                   # 查看配置
/context config --threshold 5000  # 设置 token 阈值
/context config --strategy hierarchical  # 设置默认策略
/context config --auto on         # 启用自动压缩

# 压缩历史
/context history                  # 查看压缩历史
/context history --limit 20       # 限制数量
```

### 4.3 REPL 集成

```csharp
// AgentRepl.cs 中的集成
private async Task HandleContextCommandAsync(string[] args)
{
    if (args.Length == 0)
    {
        ShowContextHelp();
        return;
    }
    
    var subCommand = args[0].ToLower();
    
    switch (subCommand)
    {
        case "status":
            await ShowContextStatusAsync();
            break;
            
        case "stats":
            await ShowCompressionStatsAsync();
            break;
            
        case "compress":
            await CompressContextAsync(args.Skip(1).ToArray());
            break;
            
        case "config":
            await ConfigureCompressionAsync(args.Skip(1).ToArray());
            break;
            
        case "history":
            await ShowCompressionHistoryAsync(args.Skip(1).ToArray());
            break;
            
        default:
            AnsiConsole.MarkupLine($"[red]✗ 未知子命令: {subCommand}[/]");
            ShowContextHelp();
            break;
    }
}

private async Task ShowContextStatusAsync()
{
    var messages = await _messageRepo.GetBySessionAsync(_currentSessionId);
    var tokenCount = _tokenCounter.CountTokens(messages);
    var config = _compressionService.GetConfig();
    
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("指标")
        .AddColumn("值");
    
    table.AddRow("消息数量", messages.Count.ToString());
    table.AddRow("Token 数量", tokenCount.ToString());
    table.AddRow("Token 阈值", config.TokenThreshold.ToString());
    table.AddRow("压缩策略", config.DefaultStrategy);
    table.AddRow("自动压缩", config.AutoCompressionEnabled ? "启用" : "禁用");
    
    // 进度条
    var percentage = (float)tokenCount / config.TokenThreshold * 100;
    var color = percentage > 80 ? "red" : percentage > 50 ? "yellow" : "green";
    
    table.AddRow(
        "使用率",
        $"[{color}]{percentage:F1}%[/]"
    );
    
    AnsiConsole.Write(table);
    
    // 建议
    if (tokenCount > config.TokenThreshold)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]⚠ Token 数量已超过阈值，建议压缩[/]");
        AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /context compress 进行压缩[/]");
    }
}
```

---

## 🎨 压缩策略

### 5.1 滑动窗口策略（Sliding Window）

**适用场景**: 简单快速的压缩，适合短期对话

**实现**:
```csharp
public class SlidingWindowStrategy : ICompressionStrategy
{
    public string Name => "sliding";
    public string Description => "保留最近 N 条消息";
    
    private readonly TokenCounter _tokenCounter;
    private readonly CompressionConfig _config;
    
    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null)
    {
        var windowSize = options?.WindowSize 
            ?? _config.SlidingWindow.WindowSize;
        
        // 保留最近 N 条消息
        var recentMessages = messages
            .TakeLast(windowSize)
            .ToList();
        
        return new CompressionResult
        {
            WasCompressed = true,
            Strategy = Name,
            CompressedMessages = recentMessages,
            Stats = new CompressionStats
            {
                OriginalMessages = messages.Count,
                CompressedMessages = recentMessages.Count,
                OriginalTokens = _tokenCounter.CountTokens(messages),
                CompressedTokens = _tokenCounter.CountTokens(recentMessages),
                CompressionRatio = CalculateRatio(messages, recentMessages)
            }
        };
    }
    
    public int EstimateCompressedTokens(List<Message> messages)
    {
        var windowSize = _config.SlidingWindow.WindowSize;
        var recentMessages = messages.TakeLast(windowSize).ToList();
        return _tokenCounter.CountTokens(recentMessages);
    }
    
    public bool IsApplicable(List<Message> messages)
    {
        // 始终适用
        return true;
    }
    
    private float CalculateRatio(
        List<Message> original,
        List<Message> compressed)
    {
        var originalTokens = _tokenCounter.CountTokens(original);
        var compressedTokens = _tokenCounter.CountTokens(compressed);
        return _tokenCounter.EstimateCompressionRatio(
            originalTokens, compressedTokens);
    }
}
```

### 5.2 分层压缩策略（Hierarchical）

**适用场景**: 平衡质量和成本，推荐作为默认策略

**实现**:
```csharp
public class HierarchicalStrategy : ICompressionStrategy
{
    public string Name => "hierarchical";
    public string Description => "分层保留：近期详细，远期摘要";
    
    private readonly TokenCounter _tokenCounter;
    private readonly CompressionConfig _config;
    
    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null)
    {
        var config = _config.Hierarchical;
        var result = new List<Message>();
        
        // 第 1 层：最近的消息（完整保留）
        var recentCount = config.RecentMessagesCount;
        var recentMessages = messages.TakeLast(recentCount).ToList();
        
        // 第 2 层：中期消息（提取关键点）
        var middleStart = Math.Max(0, messages.Count - config.SummaryMessagesCount);
        var middleEnd = messages.Count - recentCount;
        var middleMessages = messages
            .Skip(middleStart)
            .Take(middleEnd - middleStart)
            .ToList();
        
        if (middleMessages.Any())
        {
            var keyPoints = ExtractKeyPoints(middleMessages);
            result.Add(Message.CreateSystem(
                _currentSessionId,
                $"[中期消息关键点]\n{string.Join("\n", keyPoints)}"
            ));
        }
        
        // 第 3 层：早期消息（全局摘要）
        if (messages.Count > config.SummaryMessagesCount)
        {
            var archiveMessages = messages
                .Take(middleStart)
                .ToList();
            
            if (archiveMessages.Any())
            {
                var summary = GenerateSummary(archiveMessages);
                result.Insert(0, Message.CreateSystem(
                    _currentSessionId,
                    $"[早期消息摘要]\n{summary}"
                ));
            }
        }
        
        // 添加最近消息
        result.AddRange(recentMessages);
        
        return new CompressionResult
        {
            WasCompressed = true,
            Strategy = Name,
            CompressedMessages = result,
            Stats = CalculateStats(messages, result),
            Metadata = new CompressionMetadata
            {
                CompressedAt = DateTime.UtcNow,
                Strategy = Name,
                StrategyParams = new Dictionary<string, object>
                {
                    ["recent_count"] = recentCount,
                    ["middle_range"] = $"{middleStart}-{middleEnd}",
                    ["archive_count"] = middleStart
                }
            }
        };
    }
    
    private List<string> ExtractKeyPoints(List<Message> messages)
    {
        // 简单实现：提取包含关键词的句子
        var keyPoints = new List<string>();
        var keywords = new[] { "重要", "关键", "问题", "解决", "总结", "结论" };
        
        foreach (var message in messages)
        {
            var sentences = message.Content.Split('。', '！', '？');
            foreach (var sentence in sentences)
            {
                if (keywords.Any(k => sentence.Contains(k)))
                {
                    keyPoints.Add($"- {sentence.Trim()}");
                }
            }
        }
        
        // 限制数量
        return keyPoints.Take(20).ToList();
    }
    
    private string GenerateSummary(List<Message> messages)
    {
        // 简单实现：提取用户问题和 Agent 关键回答
        var summary = new StringBuilder();
        
        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg.Role == MessageRole.User)
            {
                // 用户问题
                var question = msg.Content.Length > 100 
                    ? msg.Content.Substring(0, 100) + "..." 
                    : msg.Content;
                summary.AppendLine($"Q: {question}");
                
                // 查找对应的回答
                if (i + 1 < messages.Count && 
                    messages[i + 1].Role == MessageRole.Assistant)
                {
                    var answer = messages[i + 1].Content;
                    var answerSummary = answer.Length > 150 
                        ? answer.Substring(0, 150) + "..." 
                        : answer;
                    summary.AppendLine($"A: {answerSummary}");
                    summary.AppendLine();
                }
            }
        }
        
        return summary.ToString();
    }
}
```

### 5.3 语义压缩策略（Semantic）

**适用场景**: 最高质量，但成本较高（需要额外 LLM 调用）

**实现**:
```csharp
public class SemanticStrategy : ICompressionStrategy
{
    public string Name => "semantic";
    public string Description => "使用 LLM 生成智能摘要";
    
    private readonly ILLMClient _llmClient;
    private readonly TokenCounter _tokenCounter;
    private readonly CompressionConfig _config;
    
    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null)
    {
        var config = _config.Semantic;
        
        if (!config.Enabled)
        {
            throw new InvalidOperationException(
                "语义压缩未启用。请在配置中启用或使用其他策略。");
        }
        
        // 保留最近的消息
        var recentCount = 10;
        var recentMessages = messages.TakeLast(recentCount).ToList();
        
        // 需要摘要的消息
        var toSummarize = messages
            .Take(messages.Count - recentCount)
            .ToList();
        
        if (!toSummarize.Any())
        {
            // 消息太少，不需要压缩
            return CompressionResult.NotNeeded(messages);
        }
        
        // 生成摘要
        var summary = await GenerateLLMSummaryAsync(toSummarize, config);
        
        // 构建压缩结果
        var result = new List<Message>();
        
        // 添加摘要
        result.Add(Message.CreateSystem(
            _currentSessionId,
            $"[对话摘要 - {toSummarize.Count} 条消息]\n{summary}"
        ));
        
        // 添加最近消息
        result.AddRange(recentMessages);
        
        return new CompressionResult
        {
            WasCompressed = true,
            Strategy = Name,
            CompressedMessages = result,
            Stats = CalculateStats(messages, result),
            Metadata = new CompressionMetadata
            {
                CompressedAt = DateTime.UtcNow,
                Strategy = Name,
                Summary = summary,
                StrategyParams = new Dictionary<string, object>
                {
                    ["summarized_count"] = toSummarize.Count,
                    ["recent_count"] = recentCount,
                    ["summary_tokens"] = _tokenCounter.CountTokens(summary)
                }
            }
        };
    }
    
    private async Task<string> GenerateLLMSummaryAsync(
        List<Message> messages,
        SemanticConfig config)
    {
        // 构建对话文本
        var conversationText = string.Join("\n\n", messages.Select(m =>
            $"{m.Role}: {m.Content}"));
        
        // 构建提示词
        var prompt = config.SummaryPrompt.Replace("{messages}", conversationText);
        
        // 调用 LLM
        var response = await _llmClient.CompleteAsync(new[]
        {
            Message.CreateSystem(Guid.Empty, 
                "你是一个专业的对话摘要助手。请生成简洁准确的摘要，保留关键信息。"),
            Message.CreateUser(Guid.Empty, prompt)
        });
        
        return response.Content;
    }
    
    public bool IsApplicable(List<Message> messages)
    {
        // 需要配置启用
        return _config.Semantic.Enabled && messages.Count > 10;
    }
}
```

---

## 📅 实现计划

### Phase 1: 基础架构 (3 天)

**目标**: 搭建核心框架和接口

**任务**:
- [ ] Day 1: 创建项目结构
  - 创建 `GeneralAgent.Infrastructure.Compression` 项目
  - 定义接口和模型
  - 配置依赖注入
  
- [ ] Day 2: 实现 TokenCounter
  - 集成 TikToken 库
  - 实现 token 计数逻辑
  - 编写单元测试 (10+ 测试)
  
- [ ] Day 3: 实现 CompressionOrchestrator
  - 实现策略选择逻辑
  - 实现触发检测
  - 编写单元测试 (15+ 测试)

**验收标准**:
- ✅ 所有测试通过
- ✅ Token 计数准确率 > 95%
- ✅ 编译 0 警告

---

### Phase 2: 实现压缩策略 (4 天)

**目标**: 实现 3 种核心压缩策略

**任务**:
- [ ] Day 4: 滑动窗口策略
  - 实现 SlidingWindowStrategy
  - 编写单元测试 (8+ 测试)
  - 性能测试
  
- [ ] Day 5-6: 分层压缩策略
  - 实现 HierarchicalStrategy
  - 实现关键点提取
  - 实现简单摘要生成
  - 编写单元测试 (12+ 测试)
  
- [ ] Day 7: 语义压缩策略（可选）
  - 实现 SemanticStrategy
  - 集成 LLM 客户端
  - 编写单元测试 (10+ 测试)

**验收标准**:
- ✅ 所有策略正常工作
- ✅ 压缩率 > 40%
- ✅ 压缩延迟 < 100ms（非语义策略）

---

### Phase 3: CLI 集成 (2 天)

**目标**: 集成到 CLI 和 REPL

**任务**:
- [ ] Day 8: CLI 命令实现
  - 实现 /context 命令系列
  - 实现状态显示
  - 实现配置管理
  
- [ ] Day 9: REPL 集成
  - 自动压缩触发
  - 压缩通知
  - 统计面板

**验收标准**:
- ✅ 所有命令正常工作
- ✅ 自动压缩触发正确
- ✅ UI 显示友好

---

### Phase 4: 测试和优化 (2 天)

**目标**: 完整测试和性能优化

**任务**:
- [ ] Day 10: 集成测试
  - 端到端测试 (5+ 场景)
  - 压力测试
  - 边界情况测试
  
- [ ] Day 11: 文档和优化
  - 更新用户文档
  - 性能优化
  - Bug 修复

**验收标准**:
- ✅ 100+ 个测试通过
- ✅ 测试覆盖率 > 85%
- ✅ 文档完整

---

## 🧪 测试策略

### 7.1 单元测试

```csharp
// TokenCounter 测试
public class TokenCounterTests
{
    [Fact]
    public void CountTokens_EmptyString_ReturnsZero()
    {
        var counter = new TokenCounter();
        var count = counter.CountTokens("");
        Assert.Equal(0, count);
    }
    
    [Fact]
    public void CountTokens_SimpleText_ReturnsCorrectCount()
    {
        var counter = new TokenCounter();
        var text = "Hello, world!";
        var count = counter.CountTokens(text);
        Assert.InRange(count, 3, 5); // 大约 4 个 token
    }
    
    [Theory]
    [InlineData("你好世界", 4, 6)] // 中文
    [InlineData("Hello world", 2, 4)] // 英文
    [InlineData("こんにちは", 4, 8)] // 日文
    public void CountTokens_DifferentLanguages_ReturnsReasonableCount(
        string text, int minTokens, int maxTokens)
    {
        var counter = new TokenCounter();
        var count = counter.CountTokens(text);
        Assert.InRange(count, minTokens, maxTokens);
    }
}

// SlidingWindowStrategy 测试
public class SlidingWindowStrategyTests
{
    [Fact]
    public async Task Compress_WithDefaultWindowSize_ReturnsLast10Messages()
    {
        var strategy = CreateStrategy();
        var messages = CreateTestMessages(20);
        
        var result = await strategy.CompressAsync(messages);
        
        Assert.Equal(10, result.CompressedMessages.Count);
        Assert.Equal(messages[10].Id, result.CompressedMessages[0].Id);
    }
    
    [Fact]
    public async Task Compress_CalculatesCorrectCompressionRatio()
    {
        var strategy = CreateStrategy();
        var messages = CreateTestMessages(20);
        
        var result = await strategy.CompressAsync(messages);
        
        Assert.InRange(result.Stats.CompressionRatio, 0.4f, 0.6f);
    }
}
```

### 7.2 集成测试

```csharp
public class CompressionIntegrationTests
{
    [Fact]
    public async Task EndToEnd_AutoCompression_TriggersAtThreshold()
    {
        // Arrange
        var services = CreateTestServices();
        var orchestrator = services.GetRequiredService<CompressionOrchestrator>();
        
        // 创建 50 条消息（超过阈值）
        var messages = CreateTestMessages(50);
        
        // Act
        var result = await orchestrator.CompressAsync(messages);
        
        // Assert
        Assert.True(result.WasCompressed);
        Assert.True(result.Stats.CompressionRatio > 0.4f);
    }
    
    [Fact]
    public async Task EndToEnd_HierarchicalStrategy_PreservesImportantInfo()
    {
        // Arrange
        var messages = new List<Message>
        {
            // 早期消息
            CreateMessage("我喜欢 Python"),
            CreateMessage("好的，记住了"),
            // ... 30 条中间消息
            // 最近消息
            CreateMessage("用 Python 写个示例"),
        };
        
        // Act
        var result = await CompressWithStrategy("hierarchical", messages);
        
        // Assert - 应该保留 Python 的上下文
        var allContent = string.Join(" ", 
            result.CompressedMessages.Select(m => m.Content));
        Assert.Contains("Python", allContent);
    }
}
```

### 7.3 性能测试

```csharp
[Fact]
public async Task Performance_SlidingWindow_FastEnough()
{
    var strategy = CreateStrategy();
    var messages = CreateTestMessages(100);
    
    var sw = Stopwatch.StartNew();
    await strategy.CompressAsync(messages);
    sw.Stop();
    
    Assert.True(sw.ElapsedMilliseconds < 100, 
        $"压缩耗时 {sw.ElapsedMilliseconds}ms，超过 100ms 限制");
}

[Fact]
public async Task Performance_TokenCounting_IsEfficient()
{
    var counter = new TokenCounter();
    var messages = CreateTestMessages(1000);
    
    var sw = Stopwatch.StartNew();
    var count = counter.CountTokens(messages);
    sw.Stop();
    
    Assert.True(sw.ElapsedMilliseconds < 500,
        $"Token 计数耗时 {sw.ElapsedMilliseconds}ms");
}
```

---

## 📚 用户文档示例

### 使用指南

```markdown
# 上下文压缩功能

## 什么是上下文压缩？

当对话变长时，携带完整历史会：
- 增加 API 调用成本
- 降低响应速度
- 可能超过模型上下文限制

上下文压缩智能地减少消息数量，同时保留重要信息。

## 快速开始

### 查看当前状态
```bash
/context status
```

### 手动压缩
```bash
/context compress
```

### 配置自动压缩
```bash
/context config --auto on
/context config --threshold 5000
```

## 压缩策略

### 1. 滑动窗口 (sliding)
最简单快速，保留最近 N 条消息。

**适合**: 短期对话，追求速度

### 2. 分层压缩 (hierarchical) ⭐推荐
智能分层：近期详细，远期摘要。

**适合**: 大部分场景，平衡质量和成本

### 3. 语义压缩 (semantic)
使用 LLM 生成高质量摘要。

**适合**: 重要对话，追求最佳质量
**注意**: 需要额外 API 调用
```

---

## 🎯 验收标准

### 功能验收
- [ ] 支持 3 种压缩策略
- [ ] Token 节省 > 40%
- [ ] 对话连贯性保持
- [ ] 压缩延迟 < 100ms（非语义）
- [ ] 自动压缩正常触发

### 质量验收
- [ ] 测试覆盖率 > 85%
- [ ] 100+ 单元测试通过
- [ ] 5+ 集成测试通过
- [ ] 性能测试通过
- [ ] 0 编译警告

### 文档验收
- [ ] API 文档完整
- [ ] 用户指南完整
- [ ] 示例代码清晰
- [ ] 故障排除指南

---

**文档创建**: 2026-03-26
**创建者**: Claude Sonnet 4.5
**状态**: ✅ 设计完成，准备实施
**预计开始**: 即刻
