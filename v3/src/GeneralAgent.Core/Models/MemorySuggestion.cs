namespace GeneralAgent.Core.Models;

/// <summary>
/// 从对话中提取的记忆建议
/// </summary>
public sealed record MemorySuggestion
{
    /// <summary>
    /// 记忆类型
    /// </summary>
    public required MemoryType Type { get; init; }

    /// <summary>
    /// 建议的记忆名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 建议的记忆描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 建议的记忆内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 置信度（0.0-1.0）
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// 建议的标签
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 提取原因说明
    /// </summary>
    public string? Rationale { get; init; }
}
