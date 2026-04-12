using System.CommandLine;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task update 命令 - 更新任务
/// </summary>
public static class TaskUpdateCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("update", "更新任务");

        var taskIdArgument = new Argument<string>("task-id", "任务 ID");
        command.AddArgument(taskIdArgument);

        var scheduleOption = new Option<string?>("--schedule", "新的调度表达式");
        var descriptionOption = new Option<string?>("--description", "新的描述");
        var retriesOption = new Option<int?>("--retries", "新的最大重试次数");
        var timeoutOption = new Option<int?>("--timeout", "新的超时时间（秒）");

        command.AddOption(scheduleOption);
        command.AddOption(descriptionOption);
        command.AddOption(retriesOption);
        command.AddOption(timeoutOption);

        command.SetHandler(async (taskIdStr, schedule, description, retries, timeout) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务 ID
                var tasks = await taskManager.ListTasksAsync();
                var task = tasks.FirstOrDefault(t => t.Id.ToString().StartsWith(taskIdStr))
                    ?? throw new InvalidOperationException($"任务不存在: {taskIdStr}");

                // 更新任务
                var updatedTask = await taskManager.UpdateTaskAsync(
                    task.Id, schedule, description, retries, timeout);

                AnsiConsole.MarkupLine($"[green]✓ 任务已更新: {updatedTask.Name}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 更新任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, taskIdArgument, scheduleOption, descriptionOption, retriesOption, timeoutOption);

        return command;
    }
}
