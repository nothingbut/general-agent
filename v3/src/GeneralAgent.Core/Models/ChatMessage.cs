namespace GeneralAgent.Core.Models;

/// <summary>
/// 聊天消息（用于 LLM API，与持久化 Message 实体分离）
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// 消息角色（"user", "assistant", "system", "tool"）
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 工具调用列表（仅当 Role = "assistant" 且 LLM 调用了工具时有值）
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// 工具调用 ID（仅当 Role = "tool" 时有值，用于关联工具结果到对应的调用）
    /// </summary>
    public string? ToolCallId { get; init; }
}
