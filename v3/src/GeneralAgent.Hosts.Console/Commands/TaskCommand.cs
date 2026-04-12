using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task 命令组 - 计划任务管理
/// </summary>
public static class TaskCommand
{
    /// <summary>
    /// 创建 task 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("task", "计划任务管理命令");

        // 任务操作命令
        command.AddCommand(TaskScheduleCommand.Create(serviceProvider));
        command.AddCommand(TaskListCommand.Create(serviceProvider));
        command.AddCommand(TaskShowCommand.Create(serviceProvider));
        command.AddCommand(TaskUpdateCommand.Create(serviceProvider));
        command.AddCommand(TaskPauseCommand.Create(serviceProvider));
        command.AddCommand(TaskResumeCommand.Create(serviceProvider));
        command.AddCommand(TaskDeleteCommand.Create(serviceProvider));
        command.AddCommand(TaskRunCommand.Create(serviceProvider));
        command.AddCommand(TaskHistoryCommand.Create(serviceProvider));

        return command;
    }
}
