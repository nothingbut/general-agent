using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 统一的工具接口
/// 所有工具（Skill、MCP、RAG 等）都必须实现此接口
/// 提供统一的工具定义、执行和流式执行能力
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称（唯一标识）
    /// 格式: namespace:tool_name 或 tool_name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（用于 LLM 理解工具用途）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取工具定义（供 LLM Function Calling 使用）
    /// 返回 OpenAI Function Calling 格式的工具定义
    /// </summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// 执行工具（非流式）
    /// </summary>
    /// <param name="arguments">工具参数字典</param>
    /// <param name="context">工具执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行结果（成功或失败）</returns>
    Task<Result<string>> ExecuteAsync(
        IReadOnlyDictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// 执行工具（流式）
    /// 用于需要实时返回结果的工具（如 LLM 调用）
    /// </summary>
    /// <param name="arguments">工具参数字典</param>
    /// <param name="context">工具执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>异步枚举，逐个返回结果块</returns>
    IAsyncEnumerable<string> ExecuteStreamAsync(
        IReadOnlyDictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default);
}
