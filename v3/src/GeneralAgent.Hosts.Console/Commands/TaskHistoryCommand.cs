using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Text.Json;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task history 命令 - 查看任务执行历史
/// </summary>
public static class TaskHistoryCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("history", "查看任务执行历史");

        var taskIdArgument = new Argument<string>("task-id", "任务 ID");
        command.AddArgument(taskIdArgument);

        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-n" },
            getDefaultValue: () => 20,
            description: "返回记录数量");
        command.AddOption(limitOption);

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (taskIdStr, limit, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务 ID
                var tasks = await taskManager.ListTasksAsync();
                var task = tasks.FirstOrDefault(t => t.Id.ToString().StartsWith(taskIdStr))
                    ?? throw new InvalidOperationException($"任务不存在: {taskIdStr}");

                // 获取执行历史
                var history = await taskManager.GetExecutionHistoryAsync(task.Id, limit);

                if (history.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]该任务还没有执行历史[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var historyData = history.Select(h => new
                    {
                        h.Id,
                        h.TaskId,
                        Status = h.Status.ToString(),
                        StartedAt = h.StartedAt.ToString("O"),
                        CompletedAt = h.CompletedAt?.ToString("O"),
                        h.DurationMs,
                        h.RetryCount,
                        h.Output,
                        h.Error
                    }).ToList();

                    var json = JsonSerializer.Serialize(new
                    {
                        TaskId = task.Id,
                        TaskName = task.Name,
                        Total = history.Count,
                        History = historyData
                    }, new JsonSerializerOptions { WriteIndented = true });

                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    AnsiConsole.MarkupLine($"[bold cyan]任务执行历史[/]: {task.Name}");
                    AnsiConsole.WriteLine();

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("执行 ID")
                        .AddColumn("状态")
                        .AddColumn("开始时间")
                        .AddColumn("耗时 (ms)")
                        .AddColumn("重试")
                        .AddColumn("结果");

                    foreach (var exec in history.OrderByDescending(h => h.StartedAt))
                    {
                        var statusColor = exec.Status switch
                        {
                            Infrastructure.ScheduledTasks.Models.ExecutionStatus.Completed => "green",
                            Infrastructure.ScheduledTasks.Models.ExecutionStatus.Failed => "red",
                            Infrastructure.ScheduledTasks.Models.ExecutionStatus.Timeout => "yellow",
                            Infrastructure.ScheduledTasks.Models.ExecutionStatus.Cancelled => "yellow",
                            Infrastructure.ScheduledTasks.Models.ExecutionStatus.Running => "blue",
                            _ => "white"
                        };

                        var result = !string.IsNullOrEmpty(exec.Error)
                            ? $"[red]{exec.Error[..Math.Min(30, exec.Error.Length)]}...[/]"
                            : !string.IsNullOrEmpty(exec.Output)
                                ? $"[dim]{exec.Output[..Math.Min(30, exec.Output.Length)]}...[/]"
                                : "-";

                        table.AddRow(
                            $"[cyan]{exec.Id.ToString()[..8]}...[/]",
                            $"[{statusColor}]{exec.Status}[/]",
                            exec.StartedAt.ToString("MM-dd HH:mm:ss"),
                            exec.DurationMs?.ToString() ?? "-",
                            exec.RetryCount.ToString(),
                            result
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n共 [cyan]{history.Count}[/] 条执行记录");

                    // 统计信息
                    var completed = history.Count(h => h.Status == Infrastructure.ScheduledTasks.Models.ExecutionStatus.Completed);
                    var failed = history.Count(h => h.Status == Infrastructure.ScheduledTasks.Models.ExecutionStatus.Failed);
                    var successRate = history.Count > 0 ? (double)completed / history.Count * 100 : 0;

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"成功: [green]{completed}[/] | 失败: [red]{failed}[/] | 成功率: [cyan]{successRate:F1}%[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 获取执行历史失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument, limitOption, formatOption);

        return command;
    }
}
