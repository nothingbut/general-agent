using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Models;

/// <summary>
/// LLM 补全请求
/// </summary>
public sealed record CompletionRequest
{
    /// <summary>
    /// 模型名称（如 "llama3.2", "mistral"）
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 对话历史消息（包含 Role 和 Content）
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// 系统提示词（可选）
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 温度参数（0.0-2.0，默认 0.7）
    /// 控制响应的随机性，越高越随机
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// 最大生成 token 数（可选）
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 可用的工具列表（序列化后的 JSON 数组）
    /// 用于 Tool Calling 场景
    /// </summary>
    public JsonArray? Tools { get; init; }
}
