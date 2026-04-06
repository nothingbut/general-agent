namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 编辑动作类型
/// </summary>
public enum EditAction
{
    /// <summary>
    /// 接受 - 直接使用建议的技能定义
    /// </summary>
    Accept,

    /// <summary>
    /// 编辑 - 用户修改后接受
    /// </summary>
    Edit,

    /// <summary>
    /// 拒绝 - 不创建技能
    /// </summary>
    Reject
}

/// <summary>
/// 编辑结果
/// </summary>
public sealed record EditResult
{
    /// <summary>
    /// 用户的动作
    /// </summary>
    public required EditAction Action { get; init; }

    /// <summary>
    /// 编辑后的内容（仅当 Action = Edit 时）
    /// </summary>
    public string? EditedContent { get; init; }

    /// <summary>
    /// 拒绝原因（仅当 Action = Reject 时）
    /// </summary>
    public string? RejectionReason { get; init; }
}
