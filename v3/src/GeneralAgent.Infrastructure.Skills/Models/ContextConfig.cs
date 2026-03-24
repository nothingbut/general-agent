namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 上下文配置
/// </summary>
public sealed record ContextConfig
{
    /// <summary>
    /// 最大消息数量
    /// </summary>
    public int MaxMessages { get; init; } = 10;

    /// <summary>
    /// 包含的角色（null 表示所有角色）
    /// </summary>
    public string[]? Roles { get; init; }

    /// <summary>
    /// 是否包含系统消息
    /// </summary>
    public bool IncludeSystemMessages { get; init; } = false;
}
