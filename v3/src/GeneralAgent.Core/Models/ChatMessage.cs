namespace GeneralAgent.Core.Models;

/// <summary>
/// 聊天消息（用于 LLM API，与持久化 Message 实体分离）
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// 消息角色（"user", "assistant", "system"）
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; init; }
}
