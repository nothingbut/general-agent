# 技能提取功能使用指南

## 概述

技能提取功能（Skill Extraction）使用 LLM 自动分析对话历史，识别重复性任务模式，并生成可复用的技能定义文件。

## 核心功能

1. **自动识别模式** - 分析对话历史，发现重复任务
2. **智能生成** - 创建 YAML frontmatter + Markdown 格式的技能文件
3. **用户交互** - 支持接受、编辑、拒绝建议
4. **历史管理** - 记录所有提取事件，支持统计分析
5. **性能优化** - LLM 调用缓存，避免重复分析

## 快速开始

### 1. 添加依赖注入

```csharp
using GeneralAgent.Infrastructure.SkillExtraction.Extensions;

// 在 Program.cs 或 Startup.cs 中
services.AddSkillExtraction(options =>
{
    options.SkillsDirectory = "skills";  // 技能文件保存目录
    options.MinimumConfidence = 0.6;     // 最小置信度阈值
    options.LookbackMessages = 50;       // 回溯消息数量
    options.AutoCreateNamespaceDirectory = true;
    options.OverwriteExisting = false;
});

// 添加内存缓存（可选，用于性能优化）
services.AddMemoryCache();
```

### 2. 使用编排器执行提取

```csharp
using GeneralAgent.Infrastructure.SkillExtraction.Services;

public class SkillExtractionHandler
{
    private readonly ISkillExtractionOrchestrator _orchestrator;

    public SkillExtractionHandler(ISkillExtractionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ExtractFromSessionAsync(string sessionId)
    {
        // 从会话提取并创建技能（完整流程）
        var createdSkillPaths = await _orchestrator.ExtractAndCreateFromSessionAsync(
            sessionId,
            lookbackMessages: 50
        );

        Console.WriteLine($"成功创建 {createdSkillPaths.Count} 个技能:");
        foreach (var path in createdSkillPaths)
        {
            Console.WriteLine($"  - {path}");
        }
    }
}
```

### 3. 实现自定义用户交互

```csharp
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using GeneralAgent.Infrastructure.SkillExtraction.Models;

public class ConsoleUserInteraction : IUserInteraction
{
    public async Task<EditResult> PromptForActionAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n💡 技能建议\n");
        Console.WriteLine($"名称: {suggestion.FullName}");
        Console.WriteLine($"描述: {suggestion.Description}");
        Console.WriteLine($"置信度: {suggestion.Confidence:P0}");
        Console.WriteLine($"出现次数: {suggestion.Occurrences}");
        
        Console.Write("\n是否创建此技能? [Y/n/e(编辑)]: ");
        var input = Console.ReadLine()?.Trim().ToLower();

        return input switch
        {
            "y" or "" => new EditResult { Action = EditAction.Accept },
            "e" => new EditResult { Action = EditAction.Edit },
            _ => new EditResult 
            { 
                Action = EditAction.Reject,
                RejectionReason = "用户拒绝"
            }
        };
    }

    // 实现其他方法...
}

// 注册自定义实现
services.AddSingleton<IUserInteraction, ConsoleUserInteraction>();
```

## 使用数据库持久化

### 1. 配置 EF Core

```csharp
using GeneralAgent.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

// 添加 DbContext
services.AddDbContext<AgentDbContext>(options =>
    options.UseSqlite("Data Source=agent.db"));

// 注册基于 EF Core 的 Repository
services.AddSingleton<IExtractionHistoryRepository, ExtractionHistoryRepository>();
```

### 2. 创建数据库迁移

```bash
# 创建迁移
dotnet ef migrations add AddExtractionHistory \
    --project src/GeneralAgent.Infrastructure \
    --startup-project src/YourApp

# 应用迁移
dotnet ef database update \
    --project src/GeneralAgent.Infrastructure \
    --startup-project src/YourApp
```

## 启用缓存优化

### 1. 使用缓存装饰器

```csharp
using GeneralAgent.Infrastructure.SkillExtraction.Services;

// 注册缓存装饰器
services.AddMemoryCache();

services.AddSingleton<ISkillExtractionService>(provider =>
{
    var innerService = new SkillExtractionService(
        provider.GetRequiredService<ILLMClientFactory>(),
        provider.GetRequiredService<IMessageRepository>(),
        provider.GetRequiredService<ILogger<SkillExtractionService>>()
    );

    return new CachedSkillExtractionService(
        innerService,
        provider.GetRequiredService<IMemoryCache>(),
        provider.GetRequiredService<ILogger<CachedSkillExtractionService>>()
    );
});
```

### 2. 配置缓存策略

缓存装饰器会自动：
- 基于会话 ID 和消息数量缓存结果
- 基于消息内容哈希缓存结果
- 缓存有效期：1 小时
- 仅缓存有建议的结果

## 查询历史和统计

### 1. 使用历史服务

```csharp
using GeneralAgent.Infrastructure.SkillExtraction.Services;

public class SkillAnalytics
{
    private readonly IExtractionHistoryService _historyService;

    public SkillAnalytics(IExtractionHistoryService historyService)
    {
        _historyService = historyService;
    }

    public async Task ShowStatisticsAsync()
    {
        // 获取总体统计
        var stats = await _historyService.GetStatisticsAsync();
        Console.WriteLine($"总提取次数: {stats.TotalExtractions}");
        Console.WriteLine($"接受率: {(stats.AcceptedCount + stats.EditedCount) * 100.0 / stats.TotalExtractions:F1}%");
        Console.WriteLine($"平均置信度: {stats.AverageConfidence:P0}");

        // 获取最受欢迎的技能
        var popular = await _historyService.GetMostPopularSkillsAsync(limit: 10);
        Console.WriteLine("\n最受欢迎的技能:");
        foreach (var skill in popular)
        {
            Console.WriteLine($"  {skill.FullSkillName} - 接受 {skill.AcceptedCount} 次");
        }

        // 获取拒绝模式
        var rejections = await _historyService.GetRejectionPatternsAsync(limit: 5);
        Console.WriteLine("\n常见拒绝原因:");
        foreach (var rejection in rejections)
        {
            Console.WriteLine($"  {rejection.FullSkillName}:");
            foreach (var reason in rejection.CommonReasons)
            {
                Console.WriteLine($"    - {reason}");
            }
        }
    }
}
```

### 2. 查询历史记录

```csharp
// 按会话查询
var sessionHistory = await _historyService.GetHistoryBySessionAsync(sessionId);

// 按技能查询
var skillHistory = await _historyService.GetHistoryBySkillAsync("dev", "api-helper");

// 按动作过滤
var acceptedHistory = await _historyService.GetHistoryByActionAsync(EditAction.Accept);

// 获取最近记录
var recentHistory = await _historyService.GetHistoryAsync(limit: 50);
```

## 生成的技能文件格式

提取服务生成的技能文件格式如下：

```markdown
---
name: api-helper
description: 查看 API 文档并生成示例代码
namespace: dev
parameters:
  - name: api
    type: string
    required: true
    description: API 名称
---

请帮我完成以下任务：

1. 查看 {{api}} 的文档
2. 生成示例代码
3. 解释使用方法
```

生成后的技能可以立即使用：

```bash
# 使用生成的技能
@dev:api-helper api='用户登录'
```

## 配置选项

### SkillExtractionOptions

```csharp
public sealed record SkillExtractionOptions
{
    /// <summary>
    /// 技能目录路径（默认: "skills"）
    /// </summary>
    public string SkillsDirectory { get; init; } = "skills";

    /// <summary>
    /// 最小置信度阈值（默认: 0.6）
    /// </summary>
    public double MinimumConfidence { get; init; } = 0.6;

    /// <summary>
    /// 回溯消息数量（默认: 50）
    /// </summary>
    public int LookbackMessages { get; init; } = 50;

    /// <summary>
    /// 是否自动创建命名空间目录（默认: true）
    /// </summary>
    public bool AutoCreateNamespaceDirectory { get; init; } = true;

    /// <summary>
    /// 文件名冲突时是否覆盖（默认: false）
    /// </summary>
    public bool OverwriteExisting { get; init; } = false;
}
```

## 识别标准

LLM 会根据以下标准识别重复任务模式：

1. **出现频率** - 任务至少出现 2-3 次
2. **明确性** - 任务有明确的步骤和输入输出
3. **参数化** - 任务可以参数化（有变化的部分）
4. **复杂度** - 任务足够复杂，值得创建技能
5. **置信度** - LLM 的置信度 ≥ 0.6

## 命名空间建议

生成的技能会根据任务类型自动分配命名空间：

- `dev` - 开发相关（代码、API、工具）
- `productivity` - 生产力工具（任务、笔记、提醒）
- `personal` - 个人助手（问候、日程、习惯）
- `analysis` - 数据分析（统计、报表、可视化）
- `writing` - 写作辅助（文档、邮件、博客）

## 最佳实践

1. **定期提取** - 在会话结束后或每 10-20 条消息后触发提取
2. **审查建议** - 不要自动接受所有建议，审查后再保存
3. **编辑优化** - 利用编辑功能改进生成的技能定义
4. **分析拒绝** - 定期查看拒绝模式，改进提取算法
5. **监控统计** - 跟踪接受率和置信度，优化阈值配置

## 故障排除

### 问题：提取时间过长

**解决方案**:
- 启用缓存装饰器
- 减少 `LookbackMessages` 数量
- 使用更快的 LLM 模型

### 问题：生成的技能质量不高

**解决方案**:
- 提高 `MinimumConfidence` 阈值
- 提供更多对话历史（增加 `LookbackMessages`）
- 审查并编辑生成的内容

### 问题：文件冲突

**解决方案**:
- 设置 `OverwriteExisting = true`（谨慎使用）
- 手动删除旧文件
- 使用不同的命名空间或技能名称

## 性能指标

- **提取速度**: < 5 秒（50 条消息，带缓存）
- **生成速度**: < 3 秒
- **缓存命中率**: ~60-70%（典型场景）
- **测试覆盖率**: 56/56 测试通过

## 相关文档

- [技能系统设计](./skill-extraction-design.md)
- [实现计划](./skill-extraction-plan.md)
- [技能系统指南](../../CLAUDE.md#技能系统)
