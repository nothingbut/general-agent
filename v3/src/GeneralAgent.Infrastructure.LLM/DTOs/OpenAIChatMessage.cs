using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 聊天消息 DTO
/// 表示单条聊天消息，用于 /v1/chat/completions API
/// </summary>
public sealed record OpenAIChatMessage
{
    /// <summary>
    /// 消息角色："system", "user", "assistant"
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
