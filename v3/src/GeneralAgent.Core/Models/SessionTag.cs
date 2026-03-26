namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话标签实体（不可变）
/// </summary>
public sealed record SessionTag
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 标签名称（小写存储）
    /// </summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>
    /// 标签颜色（RGB 格式，如 "#FF5733"）
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 标签图标（Emoji）
    /// </summary>
    public string? Emoji { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 标签来源
    /// </summary>
    public TagSource Source { get; init; } = TagSource.User;

    /// <summary>
    /// 创建新标签
    /// </summary>
    public static SessionTag Create(
        Guid sessionId,
        string tag,
        TagSource source = TagSource.User,
        string? color = null,
        string? emoji = null)
        => new()
        {
            SessionId = sessionId,
            Tag = tag.Trim().ToLowerInvariant(),
            Color = color,
            Emoji = emoji,
            CreatedAt = DateTime.UtcNow,
            Source = source
        };
}

/// <summary>
/// 标签来源
/// </summary>
public enum TagSource
{
    /// <summary>
    /// 用户手动添加
    /// </summary>
    User,

    /// <summary>
    /// 系统自动生成
    /// </summary>
    Auto
}
