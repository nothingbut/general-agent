namespace GeneralAgent.Core.Models;

/// <summary>
/// 对话结果
/// 包含完整的对话循环结果，包括最终响应、轮数统计、消息历史等
/// </summary>
public sealed record ConversationResult
{
    /// <summary>
    /// 最终响应内容
    /// </summary>
    public required string FinalResponse { get; init; }

    /// <summary>
    /// 总轮数
    /// </summary>
    public int TotalRounds { get; init; }

    /// <summary>
    /// 总工具调用数
    /// </summary>
    public int TotalToolCalls { get; init; }

    /// <summary>
    /// 完整的消息历史（包括工具调用和结果）
    /// </summary>
    public List<ChatMessage> Messages { get; init; } = new();

    /// <summary>
    /// 是否因达到限制而截断
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// 截断原因（仅当 Truncated = true 时有值）
    /// </summary>
    public string? TruncationReason { get; init; }
}
