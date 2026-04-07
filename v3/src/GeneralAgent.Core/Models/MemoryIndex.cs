namespace GeneralAgent.Core.Models;

/// <summary>
/// 记忆索引条目（用于 MEMORY.md）
/// </summary>
public sealed record MemoryIndex
{
    /// <summary>
    /// 记忆 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 记忆类型
    /// </summary>
    public MemoryType Type { get; init; }

    /// <summary>
    /// 记忆名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简短描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径（相对路径）
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 标签列表
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 从 Memory 创建索引条目
    /// </summary>
    public static MemoryIndex FromMemory(Memory memory)
    {
        return new MemoryIndex
        {
            Id = memory.Id,
            Type = memory.Type,
            Name = memory.Name,
            Description = memory.Description,
            FilePath = memory.FilePath,
            Tags = memory.Tags,
            UpdatedAt = memory.UpdatedAt
        };
    }

    /// <summary>
    /// 转换为 Markdown 格式的索引行
    /// 格式: - [Title](filepath.md) — description [tags] <!-- id: guid -->
    /// </summary>
    public string ToMarkdownLine()
    {
        var tagsStr = Tags.Count > 0 ? $" `{string.Join("` `", Tags)}`" : "";
        return $"- [{Name}]({FilePath}) — {Description}{tagsStr} <!-- id:{Id} -->";
    }
}
