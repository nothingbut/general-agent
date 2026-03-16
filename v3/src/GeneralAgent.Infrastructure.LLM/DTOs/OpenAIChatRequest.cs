using System.Text.Json.Serialization;

namespace GeneralAgent.Infrastructure.LLM.DTOs;

/// <summary>
/// OpenAI 聊天补全请求 DTO
/// 表示 /v1/chat/completions API 的请求体
/// </summary>
public sealed record OpenAIChatRequest
{
    /// <summary>
    /// 模型名称（如 "gpt-3.5-turbo", "llama3.2"）
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// 对话历史消息
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<OpenAIChatMessage> Messages { get; init; }

    /// <summary>
    /// 温度参数（0.0-2.0），控制响应的随机性
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    /// <summary>
    /// 最大生成 token 数
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 是否使用流式响应
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = false;

    /// <summary>
    /// Top-p 采样参数（0.0-1.0）
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    /// <summary>
    /// 生成选项数量
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; init; }

    /// <summary>
    /// 停止序列
    /// </summary>
    [JsonPropertyName("stop")]
    public IReadOnlyList<string>? Stop { get; init; }
}
