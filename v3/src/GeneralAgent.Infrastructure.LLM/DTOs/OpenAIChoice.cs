using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 聊天补全选项 DTO
/// 表示非流式响应中的单个生成选项
/// </summary>
public sealed record OpenAIChoice
{
    /// <summary>
    /// 选项的索引
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// 生成的消息
    /// </summary>
    [JsonPropertyName("message")]
    public required OpenAIChatMessage Message { get; init; }

    /// <summary>
    /// 生成结束的原因："stop", "length", "content_filter", etc.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}
