# 技能提取 CLI 命令集成示例

本文档演示如何在 CLI 应用中集成技能提取功能。

## 命令结构

```bash
# 从当前会话提取技能
/skill extract

# 从指定会话提取
/skill extract --session <session-id>

# 指定回溯消息数量
/skill extract --messages 100

# 查看提取历史
/skill extraction-history

# 查看统计信息
/skill extraction-stats

# 查看最受欢迎的技能
/skill popular-skills --limit 10
```

## 实现示例

### 1. 命令处理器基类

```csharp
using System.CommandLine;
using GeneralAgent.Infrastructure.SkillExtraction.Services;

public abstract class SkillExtractionCommandBase
{
    protected readonly ISkillExtractionOrchestrator Orchestrator;
    protected readonly IExtractionHistoryService HistoryService;
    protected readonly IUserInteraction UserInteraction;

    protected SkillExtractionCommandBase(
        ISkillExtractionOrchestrator orchestrator,
        IExtractionHistoryService historyService,
        IUserInteraction userInteraction)
    {
        Orchestrator = orchestrator;
        HistoryService = historyService;
        UserInteraction = userInteraction;
    }
}
```

### 2. Extract 命令

```csharp
using System.CommandLine;
using GeneralAgent.Infrastructure.SkillExtraction.Services;

public class ExtractSkillCommand : SkillExtractionCommandBase
{
    public ExtractSkillCommand(
        ISkillExtractionOrchestrator orchestrator,
        IExtractionHistoryService historyService,
        IUserInteraction userInteraction)
        : base(orchestrator, historyService, userInteraction)
    {
    }

    public Command CreateCommand()
    {
        var command = new Command("extract", "从对话历史中提取技能");

        var sessionOption = new Option<string?>(
            "--session",
            "会话 ID（不指定则使用当前会话）");

        var messagesOption = new Option<int>(
            "--messages",
            () => 50,
            "回溯分析的消息数量");

        command.AddOption(sessionOption);
        command.AddOption(messagesOption);

        command.SetHandler(async (sessionId, messages) =>
        {
            await ExecuteAsync(sessionId, messages);
        }, sessionOption, messagesOption);

        return command;
    }

    private async Task ExecuteAsync(string? sessionId, int messages)
    {
        try
        {
            // 获取当前会话 ID（如果未指定）
            sessionId ??= GetCurrentSessionId();

            if (string.IsNullOrEmpty(sessionId))
            {
                await UserInteraction.ShowErrorAsync("无法确定会话 ID");
                return;
            }

            // 执行提取
            await UserInteraction.ShowMessageAsync("正在分析对话历史...");

            var createdSkills = await Orchestrator.ExtractAndCreateFromSessionAsync(
                sessionId,
                lookbackMessages: messages
            );

            if (createdSkills.Count > 0)
            {
                await UserInteraction.ShowSuccessAsync(
                    $"✅ 成功创建 {createdSkills.Count} 个技能");

                foreach (var skillPath in createdSkills)
                {
                    await UserInteraction.ShowMessageAsync($"  📝 {skillPath}");
                }
            }
            else
            {
                await UserInteraction.ShowMessageAsync(
                    "ℹ️ 未发现明显的重复任务模式");
            }
        }
        catch (Exception ex)
        {
            await UserInteraction.ShowErrorAsync($"提取失败: {ex.Message}");
        }
    }

    private string GetCurrentSessionId()
    {
        // TODO: 从应用上下文获取当前会话 ID
        return Environment.GetEnvironmentVariable("CURRENT_SESSION_ID") ?? "";
    }
}
```

### 3. History 命令

```csharp
using System.CommandLine;

public class ExtractionHistoryCommand : SkillExtractionCommandBase
{
    public ExtractionHistoryCommand(
        ISkillExtractionOrchestrator orchestrator,
        IExtractionHistoryService historyService,
        IUserInteraction userInteraction)
        : base(orchestrator, historyService, userInteraction)
    {
    }

    public Command CreateCommand()
    {
        var command = new Command("extraction-history", "查看技能提取历史");

        var limitOption = new Option<int>(
            "--limit",
            () => 50,
            "显示数量");

        var actionOption = new Option<string?>(
            "--action",
            "按动作过滤 (Accept/Edit/Reject)");

        command.AddOption(limitOption);
        command.AddOption(actionOption);

        command.SetHandler(async (limit, action) =>
        {
            await ExecuteAsync(limit, action);
        }, limitOption, actionOption);

        return command;
    }

    private async Task ExecuteAsync(int limit, string? action)
    {
        try
        {
            List<ExtractionRecord> records;

            if (!string.IsNullOrEmpty(action) && Enum.TryParse<EditAction>(action, out var editAction))
            {
                records = await HistoryService.GetHistoryByActionAsync(editAction);
            }
            else
            {
                records = await HistoryService.GetHistoryAsync(limit);
            }

            if (records.Count == 0)
            {
                await UserInteraction.ShowMessageAsync("暂无历史记录");
                return;
            }

            await UserInteraction.ShowMessageAsync($"\n技能提取历史（共 {records.Count} 条）:\n");

            // 表格头
            Console.WriteLine($"{"时间",-20} | {"技能名称",-30} | {"动作",-10} | {"置信度",-8}");
            Console.WriteLine(new string('-', 75));

            // 数据行
            foreach (var record in records)
            {
                var timestamp = record.Timestamp.ToString("yyyy-MM-dd HH:mm");
                var skillName = record.FullSkillName.Length > 30
                    ? record.FullSkillName[..27] + "..."
                    : record.FullSkillName;
                var action = record.Action.ToString();
                var confidence = $"{record.Confidence:P0}";

                Console.WriteLine($"{timestamp,-20} | {skillName,-30} | {action,-10} | {confidence,-8}");

                if (!string.IsNullOrEmpty(record.RejectionReason))
                {
                    Console.WriteLine($"  └─ 拒绝原因: {record.RejectionReason}");
                }
            }
        }
        catch (Exception ex)
        {
            await UserInteraction.ShowErrorAsync($"查询失败: {ex.Message}");
        }
    }
}
```

### 4. Stats 命令

```csharp
public class ExtractionStatsCommand : SkillExtractionCommandBase
{
    public ExtractionStatsCommand(
        ISkillExtractionOrchestrator orchestrator,
        IExtractionHistoryService historyService,
        IUserInteraction userInteraction)
        : base(orchestrator, historyService, userInteraction)
    {
    }

    public Command CreateCommand()
    {
        var command = new Command("extraction-stats", "查看提取统计信息");

        command.SetHandler(async () =>
        {
            await ExecuteAsync();
        });

        return command;
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var stats = await HistoryService.GetStatisticsAsync();

            await UserInteraction.ShowMessageAsync("\n📊 技能提取统计\n");

            Console.WriteLine($"总提取次数: {stats.TotalExtractions}");
            Console.WriteLine($"接受次数: {stats.AcceptedCount} ({GetPercentage(stats.AcceptedCount, stats.TotalExtractions)})");
            Console.WriteLine($"编辑次数: {stats.EditedCount} ({GetPercentage(stats.EditedCount, stats.TotalExtractions)})");
            Console.WriteLine($"拒绝次数: {stats.RejectedCount} ({GetPercentage(stats.RejectedCount, stats.TotalExtractions)})");
            Console.WriteLine($"平均置信度: {stats.AverageConfidence:P0}");

            var acceptanceRate = stats.TotalExtractions > 0
                ? (double)(stats.AcceptedCount + stats.EditedCount) / stats.TotalExtractions
                : 0;

            Console.WriteLine($"\n✅ 接受率: {acceptanceRate:P1}");

            // 显示最受欢迎的技能
            var popularSkills = await HistoryService.GetMostPopularSkillsAsync(limit: 5);

            if (popularSkills.Count > 0)
            {
                await UserInteraction.ShowMessageAsync("\n🌟 最受欢迎的技能:\n");

                foreach (var skill in popularSkills.Take(5))
                {
                    Console.WriteLine($"  {skill.FullSkillName}");
                    Console.WriteLine($"    接受: {skill.AcceptedCount} 次, 编辑: {skill.EditedCount} 次");
                    Console.WriteLine($"    接受率: {skill.AcceptanceRate:P0}\n");
                }
            }
        }
        catch (Exception ex)
        {
            await UserInteraction.ShowErrorAsync($"查询失败: {ex.Message}");
        }
    }

    private string GetPercentage(int count, int total)
    {
        if (total == 0) return "0%";
        return $"{count * 100.0 / total:F1}%";
    }
}
```

### 5. 命令注册

```csharp
using System.CommandLine;

public class SkillExtractionCommands
{
    private readonly IServiceProvider _serviceProvider;

    public SkillExtractionCommands(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Command CreateRootCommand()
    {
        var skillCommand = new Command("skill", "技能管理命令");

        // 添加子命令
        skillCommand.AddCommand(CreateExtractCommand());
        skillCommand.AddCommand(CreateHistoryCommand());
        skillCommand.AddCommand(CreateStatsCommand());
        skillCommand.AddCommand(CreatePopularCommand());

        return skillCommand;
    }

    private Command CreateExtractCommand()
    {
        var orchestrator = _serviceProvider.GetRequiredService<ISkillExtractionOrchestrator>();
        var historyService = _serviceProvider.GetRequiredService<IExtractionHistoryService>();
        var userInteraction = _serviceProvider.GetRequiredService<IUserInteraction>();

        var command = new ExtractSkillCommand(orchestrator, historyService, userInteraction);
        return command.CreateCommand();
    }

    private Command CreateHistoryCommand()
    {
        var orchestrator = _serviceProvider.GetRequiredService<ISkillExtractionOrchestrator>();
        var historyService = _serviceProvider.GetRequiredService<IExtractionHistoryService>();
        var userInteraction = _serviceProvider.GetRequiredService<IUserInteraction>();

        var command = new ExtractionHistoryCommand(orchestrator, historyService, userInteraction);
        return command.CreateCommand();
    }

    private Command CreateStatsCommand()
    {
        var orchestrator = _serviceProvider.GetRequiredService<ISkillExtractionOrchestrator>();
        var historyService = _serviceProvider.GetRequiredService<IExtractionHistoryService>();
        var userInteraction = _serviceProvider.GetRequiredService<IUserInteraction>();

        var command = new ExtractionStatsCommand(orchestrator, historyService, userInteraction);
        return command.CreateCommand();
    }

    private Command CreatePopularCommand()
    {
        var historyService = _serviceProvider.GetRequiredService<IExtractionHistoryService>();

        var command = new Command("popular-skills", "查看最受欢迎的技能");

        var limitOption = new Option<int>(
            "--limit",
            () => 10,
            "显示数量");

        command.AddOption(limitOption);

        command.SetHandler(async (limit) =>
        {
            var popularSkills = await historyService.GetMostPopularSkillsAsync(limit);

            Console.WriteLine($"\n🌟 最受欢迎的技能 (Top {limit}):\n");

            int rank = 1;
            foreach (var skill in popularSkills)
            {
                Console.WriteLine($"{rank}. {skill.FullSkillName}");
                Console.WriteLine($"   接受: {skill.AcceptedCount}, 编辑: {skill.EditedCount}, 总建议: {skill.TotalSuggestions}");
                Console.WriteLine($"   接受率: {skill.AcceptanceRate:P0}\n");
                rank++;
            }
        }, limitOption);

        return command;
    }
}
```

### 6. 在 Program.cs 中集成

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // 添加技能提取服务
        services.AddSkillExtraction(options =>
        {
            options.SkillsDirectory = "skills";
            options.MinimumConfidence = 0.6;
        });

        // 添加其他必要的服务
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite("Data Source=agent.db"));

        services.AddMemoryCache();

        // 注册自定义用户交互
        services.AddSingleton<IUserInteraction, ConsoleUserInteraction>();
    })
    .Build();

// 创建命令
var skillCommands = new SkillExtractionCommands(host.Services);
var rootCommand = new RootCommand("General Agent CLI");
rootCommand.AddCommand(skillCommands.CreateRootCommand());

// 运行命令
return await rootCommand.InvokeAsync(args);
```

## 使用示例

```bash
# 从当前会话提取技能
./agent skill extract

# 从特定会话提取（分析最近 100 条消息）
./agent skill extract --session abc123 --messages 100

# 查看最近 20 条提取历史
./agent skill extraction-history --limit 20

# 只查看被接受的技能
./agent skill extraction-history --action Accept

# 查看统计信息
./agent skill extraction-stats

# 查看最受欢迎的 5 个技能
./agent skill popular-skills --limit 5
```

## 输出示例

### Extract 命令输出

```
正在分析对话历史...

找到 2 个潜在的技能模式

💡 技能建议

名称: dev:api-helper
描述: 查看 API 文档并生成示例代码
置信度: 85%
出现次数: 3

是否创建此技能? [Y/n/e(编辑)]: y

✅ 成功创建 1 个技能
  📝 /Users/user/skills/dev/api-helper.md
```

### Stats 命令输出

```
📊 技能提取统计

总提取次数: 25
接受次数: 15 (60.0%)
编辑次数: 5 (20.0%)
拒绝次数: 5 (20.0%)
平均置信度: 78%

✅ 接受率: 80.0%

🌟 最受欢迎的技能:

  dev:api-helper
    接受: 3 次, 编辑: 1 次
    接受率: 100%

  productivity:task-manager
    接受: 2 次, 编辑: 1 次
    接受率: 100%
```

## 相关文档

- [使用指南](./skill-extraction-usage.md)
- [技能系统设计](./skill-extraction-design.md)
