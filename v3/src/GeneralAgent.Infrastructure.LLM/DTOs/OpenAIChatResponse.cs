using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 聊天补全响应 DTO
/// 表示非流式 /v1/chat/completions API 的响应
/// </summary>
public sealed record OpenAIChatResponse
{
    /// <summary>
    /// 响应的唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 对象类型，固定为 "chat.completion"
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// 创建响应的 Unix 时间戳
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// 生成的选项列表
    /// </summary>
    [JsonPropertyName("choices")]
    public required List<OpenAIChoice> Choices { get; init; }

    /// <summary>
    /// Token 使用统计
    /// </summary>
    [JsonPropertyName("usage")]
    public required OpenAIUsage Usage { get; init; }
}
