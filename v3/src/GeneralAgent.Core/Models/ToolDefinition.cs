using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具定义（LLM Function Calling 格式）
/// 遵循 OpenAI Function Calling 规范
/// 不可变记录类型，用于安全的并发访问
/// </summary>
public sealed record ToolDefinition
{
    /// <summary>
    /// 工具名称（唯一标识）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 工具描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 输入参数的 JSON Schema（OpenAI 格式）
    /// 示例:
    /// {
    ///   "type": "object",
    ///   "properties": {
    ///     "param1": { "type": "string", "description": "..." },
    ///     "param2": { "type": "number", "description": "..." }
    ///   },
    ///   "required": ["param1"]
    /// }
    /// </summary>
    public required JsonObject InputSchema { get; init; }
}
