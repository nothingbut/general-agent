namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具执行上下文
/// 提供工具执行时所需的环境信息
/// 不可变记录类型，支持安全的并发访问
/// </summary>
public sealed record ToolExecutionContext
{
    /// <summary>
    /// 会话 ID（必需）
    /// 用于追踪工具执行属于哪个会话
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// 提供商名称（可选）
    /// 如: "Ollama", "Anthropic", "MCP_Server_1"
    /// 用于识别工具执行的来源提供商
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// 消息历史（可选）
    /// 用于需要对话历史的工具（如 LLM 调用）
    /// 按时间顺序排列的消息列表
    /// </summary>
    public IReadOnlyList<Message>? HistoryMessages { get; init; }

    /// <summary>
    /// 元数据字典（可选）
    /// 用于传递额外的上下文信息
    /// 如: { "temperature": 0.7, "max_tokens": 1000 }
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
