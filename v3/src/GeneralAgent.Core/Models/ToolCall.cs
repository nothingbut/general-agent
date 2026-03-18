namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具调用请求
/// 用于表示 LLM 进行 Function Calling 的工具调用请求
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// 工具调用ID（由LLM生成，用于关联响应）
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 工具函数名称
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// 工具参数（JSON字符串）
    /// </summary>
    public required string Arguments { get; init; }
}
