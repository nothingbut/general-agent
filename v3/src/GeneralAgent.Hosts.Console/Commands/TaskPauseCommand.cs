using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task pause 命令 - 暂停任务
/// </summary>
public static class TaskPauseCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("pause", "暂停任务");

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

                // 暂停任务
                await taskManager.PauseTaskAsync(task.Id);

                AnsiConsole.MarkupLine($"[green]✓ 任务已暂停: {task.Name}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 暂停任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument);

        return command;
    }
}
