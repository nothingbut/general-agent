namespace GeneralAgent.Core.Models;

/// <summary>
/// 工具调用结果
/// 包含工具执行的完整信息（请求、响应、耗时等）
/// 不可变记录类型，支持安全的并发访问
/// </summary>
public sealed record ToolCallResult
{
    /// <summary>
    /// 关联的工具调用请求
    /// </summary>
    public required ToolCall Call { get; init; }

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 执行结果内容
    /// 成功时为工具返回值，失败时为错误消息
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public required long ElapsedMs { get; init; }

    /// <summary>
    /// 完成时间戳
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 错误消息（仅失败时有值）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ToolCallResult Success(ToolCall call, string content, long elapsedMs) =>
        new()
        {
            Call = call,
            IsSuccess = true,
            Content = content,
            ElapsedMs = elapsedMs
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ToolCallResult Failure(ToolCall call, string errorMessage, long elapsedMs) =>
        new()
        {
            Call = call,
            IsSuccess = false,
            Content = string.Empty,
            ElapsedMs = elapsedMs,
            ErrorMessage = errorMessage
        };
}
