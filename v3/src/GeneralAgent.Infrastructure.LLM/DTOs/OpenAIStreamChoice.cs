using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 流式响应的选项 DTO
/// </summary>
public sealed record OpenAIStreamChoice
{
    /// <summary>
    /// 选项的索引
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// 增量消息内容
    /// </summary>
    [JsonPropertyName("delta")]
    public required OpenAIDelta Delta { get; init; }

    /// <summary>
    /// 生成结束的原因："stop", "length", "content_filter", etc.
    /// 仅在最后一个 chunk 中出现
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}
