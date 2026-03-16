namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话实体（不可变）
/// </summary>
public sealed record Session
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 会话标题
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 会话类型
    /// </summary>
    public SessionType Type { get; init; } = SessionType.Normal;

    /// <summary>
    /// 父会话 ID（Subagent 场景）
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// 会话状态
    /// </summary>
    public SessionStatus Status { get; init; } = SessionStatus.Active;

    /// <summary>
    /// 创建新会话
    /// </summary>
    public static Session Create(string? title = null, Guid? parentId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ParentId = parentId,
            Type = parentId.HasValue ? SessionType.Subagent : SessionType.Normal
        };

    /// <summary>
    /// 更新标题（返回新实例）
    /// </summary>
    public Session WithTitle(string? title)
        => this with { Title = title, UpdatedAt = DateTime.UtcNow };

    /// <summary>
    /// 更新状态（返回新实例）
    /// </summary>
    public Session WithStatus(SessionStatus status)
        => this with { Status = status, UpdatedAt = DateTime.UtcNow };
}
