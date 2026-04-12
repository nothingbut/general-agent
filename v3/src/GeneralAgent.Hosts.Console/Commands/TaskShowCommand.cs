using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Text.Json;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task show 命令 - 显示任务详情
/// </summary>
public static class TaskShowCommand
{
    /// <summary>
    /// 创建 task show 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("show", "显示任务详情");

        // 参数：任务 ID
        var taskIdArgument = new Argument<string>(
            "task-id",
            "任务 ID（完整 GUID 或前8位）");
        command.AddArgument(taskIdArgument);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (taskIdStr, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务 ID
                Guid taskId;
                if (Guid.TryParse(taskIdStr, out var fullGuid))
                {
                    taskId = fullGuid;
                }
                else
                {
                    // 尝试通过前缀查找
                    var tasks = await taskManager.ListTasksAsync();
                    var matchedTask = tasks.FirstOrDefault(t => t.Id.ToString().StartsWith(taskIdStr));
                    if (matchedTask == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 未找到任务: {taskIdStr}[/]");
                        Environment.Exit(1);
                        return;
                    }
                    taskId = matchedTask.Id;
                }

                // 获取任务详情
                var task = await taskManager.GetTaskAsync(taskId);
                if (task == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 任务不存在: {taskId}[/]");
                    Environment.Exit(1);
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var taskData = new
                    {
                        task.Id,
                        task.Name,
                        task.Description,
                        ScheduleType = task.ScheduleType.ToString(),
                        task.Schedule,
                        TaskType = task.TaskType.ToString(),
                        task.TaskPayload,
                        Status = task.Status.ToString(),
                        NextExecutionAt = task.NextExecutionAt?.ToString("O"),
                        LastExecutionAt = task.LastExecutionAt?.ToString("O"),
                        task.ExecutionCount,
                        task.MaxRetries,
                        task.TimeoutSeconds,
                        StartAt = task.StartAt?.ToString("O"),
                        EndAt = task.EndAt?.ToString("O"),
                        CreatedAt = task.CreatedAt.ToString("O"),
                        UpdatedAt = task.UpdatedAt?.ToString("O")
                    };

                    var json = JsonSerializer.Serialize(taskData, new JsonSerializerOptions { WriteIndented = true });
                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    AnsiConsole.MarkupLine($"[bold cyan]任务详情[/]");
                    AnsiConsole.WriteLine();

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("属性")
                        .AddColumn("值");

                    var statusColor = task.Status switch
                    {
                        Infrastructure.ScheduledTasks.Models.TaskStatus.Pending => "green",
                        Infrastructure.ScheduledTasks.Models.TaskStatus.Paused => "yellow",
                        Infrastructure.ScheduledTasks.Models.TaskStatus.Completed => "blue",
                        Infrastructure.ScheduledTasks.Models.TaskStatus.Failed => "red",
                        _ => "white"
                    };

                    table.AddRow("任务 ID", $"[cyan]{task.Id}[/]");
                    table.AddRow("任务名称", task.Name);
                    table.AddRow("描述", task.Description ?? "-");
                    table.AddRow("调度类型", task.ScheduleType.ToString());
                    table.AddRow("调度表达式", task.Schedule);
                    table.AddRow("任务类型", task.TaskType.ToString());
                    table.AddRow("任务负载", $"[dim]{task.TaskPayload}[/]");
                    table.AddRow("状态", $"[{statusColor}]{task.Status}[/]");
                    table.AddRow("下次执行", task.NextExecutionAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
                    table.AddRow("最后执行", task.LastExecutionAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
                    table.AddRow("执行次数", task.ExecutionCount.ToString());
                    table.AddRow("最大重试", task.MaxRetries.ToString());
                    table.AddRow("超时时间", $"{task.TimeoutSeconds} 秒");

                    if (task.StartAt.HasValue)
                    {
                        table.AddRow("开始时间", task.StartAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    }

                    if (task.EndAt.HasValue)
                    {
                        table.AddRow("结束时间", task.EndAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    }

                    table.AddRow("创建时间", task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    table.AddRow("更新时间", task.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");

                    AnsiConsole.Write(table);

                    // 显示使用提示
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]使用 'agent task history {taskId}' 查看执行历史[/]");
                    AnsiConsole.MarkupLine($"[dim]使用 'agent task update {taskId} --schedule \"...\"' 更新任务[/]");
                    AnsiConsole.MarkupLine($"[dim]使用 'agent task pause {taskId}' 暂停任务[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 获取任务详情失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument, formatOption);

        return command;
    }
}
