namespace GeneralAgent.Core.Models;

/// <summary>
/// 流式响应块
/// </summary>
public sealed record StreamChunk
{
    /// <summary>
    /// 本次流式返回的内容片段
    /// </summary>
    public required string Delta { get; init; }

    /// <summary>
    /// 是否为流的结束
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Token 使用统计（仅在 IsComplete = true 时有值）
    /// </summary>
    public TokenUsage? Usage { get; init; }
}
