namespace GeneralAgent.Core.Models;

/// <summary>
/// LLM 补全响应
/// </summary>
public sealed record CompletionResponse
{
    /// <summary>
    /// 生成的内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Token 使用统计
    /// </summary>
    public required TokenUsage Usage { get; init; }

    /// <summary>
    /// 实际使用的模型名称
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 响应时间戳（必须显式设置以确保准确性）
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// LLM 返回的工具调用列表（仅在支持 Tool Calling 时有值）
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}
