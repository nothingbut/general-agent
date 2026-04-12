using System.CommandLine;
using System.CommandLine.Invocation;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Text.Json;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// task schedule 命令 - 创建计划任务
/// </summary>
public static class TaskScheduleCommand
{
    /// <summary>
    /// 创建 task schedule 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("schedule", "创建计划任务");

        // 参数：任务名称
        var nameArgument = new Argument<string>(
            "name",
            "任务名称");
        command.AddArgument(nameArgument);

        // 选项：调度表达式
        var scheduleOption = new Option<string>(
            aliases: new[] { "--schedule", "-s" },
            description: "调度表达式 (cron 或自然语言，如 '每天9:00', '0 9 * * *')");
        scheduleOption.IsRequired = true;
        command.AddOption(scheduleOption);

        // 选项：任务类型
        var taskTypeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            getDefaultValue: () => "custom",
            description: "任务类型 (skill, reminder, custom)");
        command.AddOption(taskTypeOption);

        // 选项：任务负载
        var payloadOption = new Option<string>(
            aliases: new[] { "--payload", "-p" },
            description: "任务负载 JSON");
        payloadOption.IsRequired = true;
        command.AddOption(payloadOption);

        // 选项：描述
        var descriptionOption = new Option<string>(
            aliases: new[] { "--description", "-d" },
            getDefaultValue: () => "",
            description: "任务描述");
        command.AddOption(descriptionOption);

        // 选项：最大重试次数
        var retriesOption = new Option<int>(
            aliases: new[] { "--retries", "-r" },
            getDefaultValue: () => 3,
            description: "最大重试次数");
        command.AddOption(retriesOption);

        // 选项：超时时间（秒）
        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout" },
            getDefaultValue: () => 300,
            description: "超时时间（秒）");
        command.AddOption(timeoutOption);

        // 选项：开始时间
        var startAtOption = new Option<DateTime?>(
            aliases: new[] { "--start-at" },
            description: "开始时间 (格式: yyyy-MM-ddTHH:mm:ss)");
        command.AddOption(startAtOption);

        // 选项：结束时间
        var endAtOption = new Option<DateTime?>(
            aliases: new[] { "--end-at" },
            description: "结束时间 (格式: yyyy-MM-ddTHH:mm:ss)");
        command.AddOption(endAtOption);

        command.SetHandler(async (context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);
            var schedule = context.ParseResult.GetValueForOption(scheduleOption)!;
            var taskType = context.ParseResult.GetValueForOption(taskTypeOption)!;
            var payload = context.ParseResult.GetValueForOption(payloadOption)!;
            var description = context.ParseResult.GetValueForOption(descriptionOption)!;
            var retries = context.ParseResult.GetValueForOption(retriesOption);
            var timeout = context.ParseResult.GetValueForOption(timeoutOption);
            var startAt = context.ParseResult.GetValueForOption(startAtOption);
            var endAt = context.ParseResult.GetValueForOption(endAtOption);

            try
            {
                using var scope = serviceProvider.CreateScope();
                var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();

                // 解析任务类型
                TaskType parsedTaskType = taskType.ToLower() switch
                {
                    "skill" => TaskType.SkillInvocation,
                    "reminder" => TaskType.MemoryReminder,
                    "custom" => TaskType.CustomCommand,
                    _ => throw new ArgumentException($"不支持的任务类型: {taskType}。支持的类型: skill, reminder, custom")
                };

                // 判断调度类型（cron 或自然语言）
                ScheduleType scheduleType = schedule.Contains("*") || schedule.Contains("/")
                    ? ScheduleType.Cron
                    : ScheduleType.Natural;

                // 创建任务
                AnsiConsole.Status()
                    .Start("正在创建任务...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                    });

                var task = await taskManager.CreateTaskAsync(
                    name: name,
                    description: description,
                    scheduleType: scheduleType,
                    schedule: schedule,
                    taskType: parsedTaskType,
                    taskPayload: payload,
                    startAt: startAt,
                    endAt: endAt,
                    maxRetries: retries,
                    timeoutSeconds: timeout
                );

                // 显示成功信息
                AnsiConsole.MarkupLine($"[green]✓ 任务创建成功[/]");
                AnsiConsole.WriteLine();

                // 显示任务详情
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("属性")
                    .AddColumn("值");

                table.AddRow("任务 ID", $"[cyan]{task.Id}[/]");
                table.AddRow("任务名称", task.Name);
                table.AddRow("描述", task.Description ?? "-");
                table.AddRow("调度类型", task.ScheduleType.ToString());
                table.AddRow("调度表达式", task.Schedule);
                table.AddRow("任务类型", task.TaskType.ToString());
                table.AddRow("状态", $"[green]{task.Status}[/]");
                table.AddRow("下次执行", task.NextExecutionAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
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

                AnsiConsole.Write(table);

                // 显示使用提示
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]使用 'agent task show {task.Id}' 查看任务详情[/]");
                AnsiConsole.MarkupLine($"[dim]使用 'agent task pause {task.Id}' 暂停任务[/]");
                AnsiConsole.MarkupLine($"[dim]使用 'agent task run {task.Id}' 立即执行任务[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 创建任务失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        });

        return command;
    }
}
