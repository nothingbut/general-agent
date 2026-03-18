using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具定义（LLM Function Calling 格式）
/// </summary>
public sealed record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject InputSchema { get; init; }
}
