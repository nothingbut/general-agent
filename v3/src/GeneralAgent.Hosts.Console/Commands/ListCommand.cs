using System.CommandLine;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// list 命令 - 列出会话
/// </summary>
public static class ListCommand
{
    /// <summary>
    /// 创建 list 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "列出所有会话");

        // 选项：限制数量
        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-l" },
            getDefaultValue: () => 20,
            description: "显示的会话数量");
        command.AddOption(limitOption);

        // 选项：偏移量
        var offsetOption = new Option<int>(
            aliases: new[] { "--offset", "-o" },
            getDefaultValue: () => 0,
            description: "跳过的会话数量");
        command.AddOption(offsetOption);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (limit, offset, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                var pagedResult = await sessionService.ListSessionsAsync(limit, offset);

                if (pagedResult.Total == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]没有会话[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var sessions = new List<object>();
                    foreach (var session in pagedResult.Items)
                    {
                        var messageCount = await messageRepository.CountAsync(session.Id);
                        sessions.Add(new
                        {
                            session.Id,
                            session.Title,
                            MessageCount = messageCount,
                            session.CreatedAt,
                            session.UpdatedAt,
                            session.Type,
                            session.Status
                        });
                    }

                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Total = pagedResult.Total,
                        Sessions = sessions
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("ID")
                        .AddColumn("标题")
                        .AddColumn("消息数")
                        .AddColumn("类型")
                        .AddColumn("创建时间")
                        .AddColumn("更新时间");

                    foreach (var session in pagedResult.Items)
                    {
                        var messageCount = await messageRepository.CountAsync(session.Id);
                        table.AddRow(
                            session.Id.ToString()[..8] + "...",
                            session.Title ?? "[dim]无标题[/]",
                            messageCount.ToString(),
                            session.Type.ToString(),
                            session.CreatedAt.ToString("MM-dd HH:mm"),
                            session.UpdatedAt.ToString("MM-dd HH:mm")
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n显示 [cyan]{pagedResult.Items.Count}[/] / [cyan]{pagedResult.Total}[/] 个会话");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 列出会话失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, limitOption, offsetOption, formatOption);

        return command;
    }
}
