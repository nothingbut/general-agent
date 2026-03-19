namespace GeneralAgent.Core.Models;

/// <summary>
/// 用户决策（是否继续 Tool Calling）
/// 表示用户对是否继续执行 Tool Calling 的决策
/// </summary>
public sealed record ExtendDecision
{
    /// <summary>
    /// 是否停止执行
    /// </summary>
    public bool Stop { get; init; }

    /// <summary>
    /// 延长的轮数
    /// 当 Stop = false 时有效
    /// </summary>
    public int ExtendBy { get; init; }
}
