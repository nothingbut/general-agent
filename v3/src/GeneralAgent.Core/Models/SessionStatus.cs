namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话状态
///
/// 状态转换规则：
/// - Normal 会话: Active (默认，保持不变)
/// - Subagent 会话: Active → Running → Completed/Failed
/// - 父会话: Active → Running (有子会话时) → Active (子会话完成)
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// 活跃中
    /// </summary>
    Active,

    /// <summary>
    /// 运行中（有子代理在执行）
    /// </summary>
    Running,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed
}
