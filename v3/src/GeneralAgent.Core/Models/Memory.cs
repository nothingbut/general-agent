using System.Text.Json;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 长期记忆实体（不可变）
/// </summary>
public sealed record Memory
{
    /// <summary>
    /// 记忆 ID（唯一标识）
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 记忆类型
    /// </summary>
    public MemoryType Type { get; init; }

    /// <summary>
    /// 记忆名称（用于文件名，如：user_role.md）
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简短描述（用于索引和搜索，~150字符）
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 记忆内容（Markdown 格式）
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 标签列表（用于分类和检索）
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 元数据（可选，用于存储额外信息）
    /// </summary>
    public Dictionary<string, JsonElement>? Metadata { get; init; }

    /// <summary>
    /// 文件路径（相对于记忆根目录）
    /// </summary>
    public string FilePath => $"{Type.ToString().ToLower()}/{Name}.md";

    /// <summary>
    /// 创建新记忆
    /// </summary>
    public static Memory Create(
        MemoryType type,
        string name,
        string description,
        string content,
        List<string>? tags = null)
    {
        return new Memory
        {
            Id = Guid.NewGuid(),
            Type = type,
            Name = name,
            Description = description,
            Content = content,
            Tags = tags ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 更新记忆内容
    /// </summary>
    public Memory WithContent(string newContent)
    {
        return this with
        {
            Content = newContent,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 更新记忆描述
    /// </summary>
    public Memory WithDescription(string newDescription)
    {
        return this with
        {
            Description = newDescription,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 添加标签
    /// </summary>
    public Memory WithTags(params string[] newTags)
    {
        var updatedTags = new List<string>(Tags);
        updatedTags.AddRange(newTags.Where(t => !updatedTags.Contains(t)));

        return this with
        {
            Tags = updatedTags,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
