using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// switch 命令 - 切换当前会话
/// </summary>
public static class SwitchCommand
{
    /// <summary>
    /// 创建 switch 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("switch", "切换当前会话");

        // 参数：会话 ID
        var sessionIdArgument = new Argument<string>(
            name: "session-id",
            description: "会话 ID（支持短格式，如前8位）");
        command.AddArgument(sessionIdArgument);

        command.SetHandler(async (sessionIdStr) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();

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

                // 保存当前会话 ID 到配置文件
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".agent",
                    "current-session.txt");

                var configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir!);
                }

                await File.WriteAllTextAsync(configPath, sessionId.ToString());

                // 显示成功信息
                AnsiConsole.MarkupLine($"[green]✓[/] 已切换到会话");
                AnsiConsole.MarkupLine($"  ID: [cyan]{session.Id}[/]");
                AnsiConsole.MarkupLine($"  标题: {session.Title}");
                AnsiConsole.MarkupLine($"  类型: {session.Type}");
                AnsiConsole.MarkupLine($"  创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 切换会话失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, sessionIdArgument);

        return command;
    }
}
