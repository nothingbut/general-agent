using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 统一的工具接口
/// 所有工具（Skill、MCP、RAG 等）都必须实现此接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称（唯一标识）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（用于 LLM 理解工具用途）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取工具定义（供 LLM Function Calling 使用）
    /// </summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// 执行工具（非流式）
    /// </summary>
    Task<Result<string>> ExecuteAsync(
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// 执行工具（流式）
    /// </summary>
    IAsyncEnumerable<string> ExecuteStreamAsync(
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);
}
