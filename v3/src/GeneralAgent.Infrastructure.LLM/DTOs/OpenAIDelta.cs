using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 流式响应的增量消息 DTO
/// </summary>
public sealed record OpenAIDelta
{
    /// <summary>
    /// 消息角色（仅在流式响应的第一个 chunk 中出现）
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    /// 增量文本内容
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
