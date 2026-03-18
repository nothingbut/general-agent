namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具执行上下文
/// </summary>
public sealed record ToolExecutionContext
{
    public required Guid SessionId { get; init; }
    public string? ProviderName { get; init; }
    public IReadOnlyList<Message>? HistoryMessages { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
