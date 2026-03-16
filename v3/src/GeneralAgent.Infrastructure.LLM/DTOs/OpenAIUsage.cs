using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI Token 使用统计 DTO
/// </summary>
public sealed record OpenAIUsage
{
    /// <summary>
    /// 提示词 token 数量
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }

    /// <summary>
    /// 补全 token 数量
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; init; }

    /// <summary>
    /// 总 token 数量
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }
}
