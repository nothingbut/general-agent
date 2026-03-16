using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 流式聊天补全响应 DTO
/// 表示流式 /v1/chat/completions API 的单个响应块
/// </summary>
public sealed record OpenAIStreamChunk
{
    /// <summary>
    /// 响应块的唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 对象类型，固定为 "chat.completion.chunk"
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// 创建响应块的 Unix 时间戳
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// 流式响应的选项列表
    /// </summary>
    [JsonPropertyName("choices")]
    public required List<OpenAIStreamChoice> Choices { get; init; }
}
