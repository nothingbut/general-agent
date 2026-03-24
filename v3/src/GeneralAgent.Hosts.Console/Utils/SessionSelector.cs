using GeneralAgent.Application.Services;
using GeneralAgent.Core.Models;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Utils;

/// <summary>
/// 会话选择器 - 提供交互式会话选择功能
/// </summary>
public static class SessionSelector
{
    /// <summary>
    /// 解析会话 ID（支持完整 GUID 或短格式）
    /// </summary>
    /// <param name="sessionIdStr">会话 ID 字符串</param>
    /// <param name="sessionService">会话服务</param>
    /// <returns>解析后的会话 ID，如果失败则返回 null</returns>
    public static async Task<Guid?> ResolveSessionIdAsync(
        string sessionIdStr,
        SessionService sessionService)
    {
        // 尝试解析完整 GUID
        if (Guid.TryParse(sessionIdStr, out var fullId))
        {
            return fullId;
        }

        // 短格式：查找匹配的会话
        var pagedResult = await sessionService.ListSessionsAsync(100, 0);
        var matchingSessions = pagedResult.Items
            .Where(s => s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingSessions.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
            return null;
        }

        if (matchingSessions.Count > 1)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ 找到多个匹配的会话，请使用更长的 ID[/]");
            foreach (var s in matchingSessions)
            {
                AnsiConsole.MarkupLine($"  - [cyan]{s.Id.ToString()[..8]}...[/] {s.Title}");
            }
            return null;
        }

        return matchingSessions[0].Id;
    }

    /// <summary>
    /// 交互式选择会话
    /// </summary>
    /// <param name="sessionService">会话服务</param>
    /// <param name="prompt">提示文本</param>
    /// <returns>选择的会话，如果取消则返回 null</returns>
    public static async Task<Session?> SelectSessionInteractivelyAsync(
        SessionService sessionService,
        string prompt = "选择会话")
    {
        var pagedResult = await sessionService.ListSessionsAsync(50, 0);

        if (pagedResult.Total == 0)
        {
            AnsiConsole.MarkupLine("[yellow]没有可用的会话[/]");
            return null;
        }

        var choices = pagedResult.Items
            .Select(s => new
            {
                Session = s,
                Display = $"{s.Title} ({s.Id.ToString()[..8]}... - {s.CreatedAt:yyyy-MM-dd HH:mm})"
            })
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[green]{prompt}[/]")
                .PageSize(10)
                .AddChoices(choices.Select(c => c.Display)));

        var selectedChoice = choices.FirstOrDefault(c => c.Display == selection);
        return selectedChoice?.Session;
    }

    /// <summary>
    /// 获取当前会话 ID（从配置文件）
    /// </summary>
    /// <returns>当前会话 ID，如果未设置则返回 null</returns>
    public static async Task<Guid?> GetCurrentSessionIdAsync()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agent",
            "current-session.txt");

        if (!File.Exists(configPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(configPath);
        if (Guid.TryParse(content, out var sessionId))
        {
            return sessionId;
        }

        return null;
    }

    /// <summary>
    /// 设置当前会话 ID（保存到配置文件）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    public static async Task SetCurrentSessionIdAsync(Guid sessionId)
    {
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
    }

    /// <summary>
    /// 清除当前会话配置
    /// </summary>
    public static void ClearCurrentSession()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agent",
            "current-session.txt");

        if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }
    }
}
