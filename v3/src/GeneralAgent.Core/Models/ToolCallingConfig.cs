namespace GeneralAgent.Core.Models;

/// <summary>
/// Tool Calling 配置
/// </summary>
public sealed record ToolCallingConfig
{
    /// <summary>
    /// 最大轮数（默认值）
    /// </summary>
    public int MaxRounds { get; init; } = 3;

    /// <summary>
    /// 绝对最大轮数（防止无限循环）
    /// </summary>
    public int AbsoluteMaxRounds { get; init; } = 50;

    /// <summary>
    /// 自动延长的轮数（用于自动模式）
    /// </summary>
    public int AutoExtendBy { get; init; } = 5;
}
