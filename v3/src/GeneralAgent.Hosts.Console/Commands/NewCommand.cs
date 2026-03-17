using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// new 命令 - 创建新会话
/// </summary>
public static class NewCommand
{
    /// <summary>
    /// 创建 new 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("new", "创建新会话");

        // 选项：标题
        var titleOption = new Option<string?>(
            aliases: new[] { "--title", "-t" },
            description: "会话标题");
        command.AddOption(titleOption);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "text",
            description: "输出格式 (text, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (title, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();

                var defaultTitle = string.IsNullOrWhiteSpace(title) ? "新会话" : title;
                var session = await sessionService.CreateSessionAsync(defaultTitle);

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        session.Id,
                        session.Title,
                        session.CreatedAt,
                        session.Type,
                        session.Status
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.Console.WriteLine(json);
                }
                else
                {
                    // 文本格式输出
                    AnsiConsole.MarkupLine($"[green]✓[/] 会话创建成功");
                    AnsiConsole.MarkupLine($"  ID: [cyan]{session.Id}[/]");
                    AnsiConsole.MarkupLine($"  标题: {session.Title}");
                    AnsiConsole.MarkupLine($"  创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 创建会话失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, titleOption, formatOption);

        return command;
    }
}
