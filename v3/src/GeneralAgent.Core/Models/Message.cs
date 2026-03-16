using System.Text.Json;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 消息实体（不可变）
/// </summary>
public sealed record Message
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 消息角色
    /// </summary>
    public MessageRole Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 元数据（可选，使用 JsonElement 保证类型安全）
    /// </summary>
    public Dictionary<string, JsonElement>? Metadata { get; init; }

    /// <summary>
    /// 创建用户消息
    /// </summary>
    public static Message CreateUser(Guid sessionId, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// 创建助手消息
    /// </summary>
    public static Message CreateAssistant(
        Guid sessionId,
        string content,
        Dictionary<string, JsonElement>? metadata = null)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata
        };
}
