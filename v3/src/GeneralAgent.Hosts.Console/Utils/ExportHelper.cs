using System.Text;
using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Hosts.Console.Utils;

/// <summary>
/// 导出助手 - 支持多种格式的会话导出
/// </summary>
public static class ExportHelper
{
    /// <summary>
    /// 导出会话为 JSON 格式
    /// </summary>
    /// <param name="session">会话对象</param>
    /// <param name="messages">消息列表</param>
    /// <returns>JSON 字符串</returns>
    public static string ExportAsJson(Session session, IEnumerable<Message> messages)
    {
        var exportData = new
        {
            session.Id,
            session.Title,
            session.CreatedAt,
            session.UpdatedAt,
            session.Type,
            session.Status,
            session.ParentId,
            Messages = messages.Select(m => new
            {
                m.Id,
                Role = m.Role.ToString(),
                m.Content,
                CreatedAt = m.CreatedAt,
                m.Metadata
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(exportData, options);
    }

    /// <summary>
    /// 导出会话为 Markdown 格式
    /// </summary>
    /// <param name="session">会话对象</param>
    /// <param name="messages">消息列表</param>
    /// <returns>Markdown 字符串</returns>
    public static string ExportAsMarkdown(Session session, IEnumerable<Message> messages)
    {
        var sb = new StringBuilder();

        // 标题和元数据
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        sb.AppendLine($"**会话 ID**: `{session.Id}`");
        sb.AppendLine($"**创建时间**: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**更新时间**: {session.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**类型**: {session.Type}");
        sb.AppendLine($"**状态**: {session.Status}");

        if (session.ParentId.HasValue)
        {
            sb.AppendLine($"**父会话**: `{session.ParentId}`");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // 对话内容
        sb.AppendLine("## 对话历史");
        sb.AppendLine();

        foreach (var message in messages.OrderBy(m => m.CreatedAt))
        {
            var roleName = message.Role switch
            {
                MessageRole.User => "👤 **用户**",
                MessageRole.Assistant => "🤖 **助手**",
                MessageRole.System => "⚙️ **系统**",
                _ => $"**{message.Role}**"
            };

            sb.AppendLine($"### {roleName}");
            sb.AppendLine($"*{message.CreatedAt:yyyy-MM-dd HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine(message.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // 统计信息
        var messageCount = messages.Count();
        var userMessageCount = messages.Count(m => m.Role == MessageRole.User);
        var assistantMessageCount = messages.Count(m => m.Role == MessageRole.Assistant);

        sb.AppendLine("## 统计信息");
        sb.AppendLine();
        sb.AppendLine($"- 总消息数: {messageCount}");
        sb.AppendLine($"- 用户消息: {userMessageCount}");
        sb.AppendLine($"- 助手回复: {assistantMessageCount}");
        sb.AppendLine();

        // 时间跨度
        if (messageCount > 0)
        {
            var firstMessage = messages.OrderBy(m => m.CreatedAt).First();
            var lastMessage = messages.OrderByDescending(m => m.CreatedAt).First();
            var duration = lastMessage.CreatedAt - firstMessage.CreatedAt;

            sb.AppendLine($"- 会话时长: {FormatDuration(duration)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 导出会话为纯文本格式
    /// </summary>
    /// <param name="session">会话对象</param>
    /// <param name="messages">消息列表</param>
    /// <returns>纯文本字符串</returns>
    public static string ExportAsText(Session session, IEnumerable<Message> messages)
    {
        var sb = new StringBuilder();

        // 标题
        sb.AppendLine($"=== {session.Title} ===");
        sb.AppendLine($"ID: {session.Id}");
        sb.AppendLine($"创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("".PadRight(60, '='));
        sb.AppendLine();

        // 对话内容
        foreach (var message in messages.OrderBy(m => m.CreatedAt))
        {
            var rolePrefix = message.Role switch
            {
                MessageRole.User => "[用户]",
                MessageRole.Assistant => "[助手]",
                MessageRole.System => "[系统]",
                _ => $"[{message.Role}]"
            };

            sb.AppendLine($"{rolePrefix} {message.CreatedAt:HH:mm:ss}");
            sb.AppendLine(message.Content);
            sb.AppendLine();
            sb.AppendLine("".PadRight(60, '-'));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 根据格式名称选择导出方法
    /// </summary>
    /// <param name="format">格式名称（json, markdown, text）</param>
    /// <param name="session">会话对象</param>
    /// <param name="messages">消息列表</param>
    /// <returns>导出的字符串</returns>
    public static string Export(string format, Session session, IEnumerable<Message> messages)
    {
        return format.ToLower() switch
        {
            "json" => ExportAsJson(session, messages),
            "markdown" or "md" => ExportAsMarkdown(session, messages),
            "text" or "txt" => ExportAsText(session, messages),
            _ => throw new ArgumentException($"不支持的导出格式: {format}")
        };
    }

    /// <summary>
    /// 获取格式对应的文件扩展名
    /// </summary>
    /// <param name="format">格式名称</param>
    /// <returns>文件扩展名（包含点号）</returns>
    public static string GetFileExtension(string format)
    {
        return format.ToLower() switch
        {
            "json" => ".json",
            "markdown" or "md" => ".md",
            "text" or "txt" => ".txt",
            _ => ".txt"
        };
    }

    /// <summary>
    /// 格式化时间跨度
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{duration.Days} 天 {duration.Hours} 小时";
        }
        if (duration.TotalHours >= 1)
        {
            return $"{duration.Hours} 小时 {duration.Minutes} 分钟";
        }
        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes} 分钟 {duration.Seconds} 秒";
        }
        return $"{duration.Seconds} 秒";
    }
}
