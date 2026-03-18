namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具调用结果
/// </summary>
public sealed record ToolCallResult
{
    public required string ToolCallId { get; init; }
    public required string ToolName { get; init; }
    public required string Content { get; init; }
    public bool IsError { get; init; }
}
