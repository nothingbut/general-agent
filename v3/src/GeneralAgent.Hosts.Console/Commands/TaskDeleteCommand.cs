using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task delete 命令 - 删除任务
/// </summary>
public static class TaskDeleteCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("delete", "删除任务");

        var taskIdArgument = new Argument<string>("task-id", "任务 ID");
        command.AddArgument(taskIdArgument);

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "跳过确认提示");
        command.AddOption(forceOption);

        command.SetHandler(async (taskIdStr, force) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务 ID
                var tasks = await taskManager.ListTasksAsync();
                var task = tasks.FirstOrDefault(t => t.Id.ToString().StartsWith(taskIdStr))
                    ?? throw new InvalidOperationException($"任务不存在: {taskIdStr}");

                // 确认删除
                if (!force)
                {
                    var confirmed = AnsiConsole.Confirm(
                        $"确定要删除任务 '[yellow]{task.Name}[/]' 吗？此操作无法撤销。");

                    if (!confirmed)
                    {
                        AnsiConsole.MarkupLine("[yellow]已取消删除操作[/]");
                        return;
                    }
                }

                // 删除任务
                await taskManager.DeleteTaskAsync(task.Id);

                AnsiConsole.MarkupLine($"[green]✓ 任务已删除: {task.Name}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 删除任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument, forceOption);

        return command;
    }
}
