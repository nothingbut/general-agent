using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Text.Json;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task list 命令 - 列出所有任务
/// </summary>
public static class TaskListCommand
{
    /// <summary>
    /// 创建 task list 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "列出所有任务");

        // 选项：按状态过滤
        var statusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "按状态过滤 (pending, paused, completed, failed)");
        command.AddOption(statusOption);

        // 选项：按任务类型过滤
        var typeOption = new Option<string?>(
            aliases: new[] { "--type", "-t" },
            description: "按任务类型过滤 (skill, reminder, custom)");
        command.AddOption(typeOption);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (statusStr, typeStr, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析过滤条件
                TaskStatus? status = null;
                if (!string.IsNullOrEmpty(statusStr))
                {
                    status = statusStr.ToLower() switch
                    {
                        "pending" => TaskStatus.Pending,
                        "paused" => TaskStatus.Paused,
                        "completed" => TaskStatus.Completed,
                        "failed" => TaskStatus.Failed,
                        _ => throw new ArgumentException($"不支持的状态: {statusStr}")
                    };
                }

                TaskType? taskType = null;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    taskType = typeStr.ToLower() switch
                    {
                        "skill" => TaskType.SkillInvocation,
                        "reminder" => TaskType.MemoryReminder,
                        "custom" => TaskType.CustomCommand,
                        _ => throw new ArgumentException($"不支持的任务类型: {typeStr}")
                    };
                }

                // 获取任务列表
                var tasks = await taskManager.ListTasksAsync(status, taskType);

                if (tasks.Count == 0)
                {
                    var filterInfo = status.HasValue || taskType.HasValue
                        ? $"（过滤条件: {(status.HasValue ? $"状态={status}" : "")}{(taskType.HasValue ? $" 类型={taskType}" : "")}）"
                        : "";
                    AnsiConsole.MarkupLine($"[yellow]没有找到任务{filterInfo}[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var tasksData = tasks.Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.Description,
                        ScheduleType = t.ScheduleType.ToString(),
                        t.Schedule,
                        TaskType = t.TaskType.ToString(),
                        Status = t.Status.ToString(),
                        NextExecutionAt = t.NextExecutionAt?.ToString("O"),
                        LastExecutionAt = t.LastExecutionAt?.ToString("O"),
                        t.ExecutionCount,
                        CreatedAt = t.CreatedAt.ToString("O")
                    }).ToList();

                    var json = JsonSerializer.Serialize(new
                    {
                        Total = tasks.Count,
                        Tasks = tasksData
                    }, new JsonSerializerOptions { WriteIndented = true });

                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("ID")
                        .AddColumn("名称")
                        .AddColumn("类型")
                        .AddColumn("状态")
                        .AddColumn("调度")
                        .AddColumn("下次执行")
                        .AddColumn("执行次数");

                    foreach (var task in tasks.OrderByDescending(t => t.CreatedAt))
                    {
                        var statusColor = task.Status switch
                        {
                            TaskStatus.Pending => "green",
                            TaskStatus.Paused => "yellow",
                            TaskStatus.Completed => "blue",
                            TaskStatus.Failed => "red",
                            _ => "white"
                        };

                        var taskTypeDisplay = task.TaskType switch
                        {
                            TaskType.SkillInvocation => "技能",
                            TaskType.MemoryReminder => "提醒",
                            TaskType.CustomCommand => "命令",
                            _ => "未知"
                        };

                        table.AddRow(
                            $"[cyan]{task.Id.ToString()[..8]}...[/]",
                            task.Name,
                            taskTypeDisplay,
                            $"[{statusColor}]{task.Status}[/]",
                            $"[dim]{task.Schedule}[/]",
                            task.NextExecutionAt?.ToString("MM-dd HH:mm") ?? "-",
                            task.ExecutionCount.ToString()
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n共 [cyan]{tasks.Count}[/] 个任务");

                    // 显示使用提示
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用 'agent task show <ID>' 查看任务详情[/]");
                    AnsiConsole.MarkupLine("[dim]使用 'agent task pause <ID>' 暂停任务[/]");
                    AnsiConsole.MarkupLine("[dim]使用 'agent task run <ID>' 立即执行任务[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 列出任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, statusOption, typeOption, formatOption);

        return command;
    }
}
