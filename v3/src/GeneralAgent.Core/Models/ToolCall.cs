namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具调用请求
/// 表示 LLM 发起的一次工具调用请求
/// 不可变记录类型，支持安全的并发访问
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// 工具调用 ID（唯一标识）
    /// 用于追踪单次工具调用
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 工具名称
    /// 必须与 ITool.Name 匹配
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// 工具参数
    /// 键值对形式的参数字典
    /// </summary>
    public required IReadOnlyDictionary<string, object> Arguments { get; init; }

    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
