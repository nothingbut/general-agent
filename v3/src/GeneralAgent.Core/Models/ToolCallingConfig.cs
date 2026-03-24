namespace GeneralAgent.Core.Models;

/// <summary>
/// Tool Calling 配置
/// </summary>
public sealed record ToolCallingConfig
{
    /// <summary>
    /// 是否启用 Tool Calling
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 最大轮数（默认值）
    /// </summary>
    public int MaxRounds { get; init; } = 3;

    /// <summary>
    /// 是否启用交互模式（达到限制时询问用户）
    /// </summary>
    public bool InteractiveMode { get; init; } = true;

    /// <summary>
    /// 自动延长的轮数（用于自动模式）
    /// </summary>
    public int AutoExtendBy { get; init; } = 5;

    /// <summary>
    /// 绝对最大轮数（防止无限循环）
    /// </summary>
    public int AbsoluteMaxRounds { get; init; } = 20;
}
