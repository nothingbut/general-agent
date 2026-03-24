using System.CommandLine;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// delete 命令 - 删除会话
/// </summary>
public static class DeleteCommand
{
    /// <summary>
    /// 创建 delete 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("delete", "删除会话");

        // 参数：会话 ID
        var sessionIdArgument = new Argument<string>(
            name: "session-id",
            description: "会话 ID（支持短格式，如前8位）");
        command.AddArgument(sessionIdArgument);

        // 选项：确认删除
        var confirmOption = new Option<bool>(
            aliases: new[] { "--confirm", "-y" },
            getDefaultValue: () => false,
            description: "跳过确认提示，直接删除");
        command.AddOption(confirmOption);

        command.SetHandler(async (sessionIdStr, confirm) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                // 解析会话 ID（支持短格式）
                Guid sessionId;
                if (Guid.TryParse(sessionIdStr, out var fullId))
                {
                    sessionId = fullId;
                }
                else
                {
                    // 短格式：查找匹配的会话
                    var pagedResult = await sessionService.ListSessionsAsync(100, 0);
                    var matchingSessions = pagedResult.Items
                        .Where(s => s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (matchingSessions.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
                        Environment.Exit(1);
                        return;
                    }

                    if (matchingSessions.Count > 1)
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠ 找到多个匹配的会话，请使用更长的 ID[/]");
                        foreach (var s in matchingSessions)
                        {
                            AnsiConsole.MarkupLine($"  - [cyan]{s.Id}[/] {s.Title}");
                        }
                        Environment.Exit(1);
                        return;
                    }

                    sessionId = matchingSessions[0].Id;
                }

                // 验证会话存在
                var session = await sessionService.GetSessionAsync(sessionId);
                if (session == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取消息数量
                var messageCount = await messageRepository.CountAsync(sessionId);

                // 显示会话信息
                AnsiConsole.MarkupLine($"即将删除会话:");
                AnsiConsole.MarkupLine($"  ID: [cyan]{session.Id}[/]");
                AnsiConsole.MarkupLine($"  标题: {session.Title}");
                AnsiConsole.MarkupLine($"  消息数: [yellow]{messageCount}[/]");
                AnsiConsole.MarkupLine($"  创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                AnsiConsole.WriteLine();

                // 确认删除
                if (!confirm)
                {
                    if (!AnsiConsole.Confirm("[yellow]确认删除此会话？[/]", defaultValue: false))
                    {
                        AnsiConsole.MarkupLine("[yellow]已取消删除[/]");
                        return;
                    }
                }

                // 删除会话
                await sessionService.DeleteSessionAsync(sessionId);

                // 如果删除的是当前会话，清除配置
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".agent",
                    "current-session.txt");

                if (File.Exists(configPath))
                {
                    var currentSessionId = await File.ReadAllTextAsync(configPath);
                    if (Guid.TryParse(currentSessionId, out var currentId) && currentId == sessionId)
                    {
                        File.Delete(configPath);
                        AnsiConsole.MarkupLine("[yellow]⚠ 已清除当前会话配置[/]");
                    }
                }

                AnsiConsole.MarkupLine($"[green]✓[/] 会话已删除");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 删除会话失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, sessionIdArgument, confirmOption);

        return command;
    }
}
