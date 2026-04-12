using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task run 命令 - 手动执行任务
/// </summary>
public static class TaskRunCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("run", "手动执行任务");

        var taskIdArgument = new Argument<string>("task-id", "任务 ID");
        command.AddArgument(taskIdArgument);

        command.SetHandler(async (taskIdStr) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务 ID
                var tasks = await taskManager.ListTasksAsync();
                var task = tasks.FirstOrDefault(t => t.Id.ToString().StartsWith(taskIdStr))
                    ?? throw new InvalidOperationException($"任务不存在: {taskIdStr}");

                // 手动执行任务
                AnsiConsole.Status()
                    .Start($"正在执行任务 '{task.Name}'...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                    });

                var execution = await taskManager.TriggerTaskAsync(task.Id);

                // 显示执行结果
                var statusColor = execution.Status switch
                {
                    Infrastructure.ScheduledTasks.Models.ExecutionStatus.Completed => "green",
                    Infrastructure.ScheduledTasks.Models.ExecutionStatus.Failed => "red",
                    Infrastructure.ScheduledTasks.Models.ExecutionStatus.Timeout => "yellow",
                    Infrastructure.ScheduledTasks.Models.ExecutionStatus.Cancelled => "yellow",
                    _ => "white"
                };

                AnsiConsole.MarkupLine($"[{statusColor}]✓ 任务执行{execution.Status}[/]");
                AnsiConsole.WriteLine();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("属性")
                    .AddColumn("值");

                table.AddRow("执行 ID", $"[cyan]{execution.Id}[/]");
                table.AddRow("任务 ID", $"[cyan]{execution.TaskId}[/]");
                table.AddRow("状态", $"[{statusColor}]{execution.Status}[/]");
                table.AddRow("开始时间", execution.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                table.AddRow("完成时间", execution.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
                table.AddRow("耗时", $"{execution.DurationMs} 毫秒");
                table.AddRow("重试次数", execution.RetryCount.ToString());

                if (!string.IsNullOrEmpty(execution.Output))
                {
                    table.AddRow("输出", $"[dim]{execution.Output}[/]");
                }

                if (!string.IsNullOrEmpty(execution.Error))
                {
                    table.AddRow("错误", $"[red]{execution.Error}[/]");
                }

                AnsiConsole.Write(table);

                // 显示使用提示
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]使用 'agent task history {task.Id}' 查看完整执行历史[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 执行任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument);

        return command;
    }
}
