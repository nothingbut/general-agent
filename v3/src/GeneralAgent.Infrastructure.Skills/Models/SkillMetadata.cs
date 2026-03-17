using System.Collections.Generic;

namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能元数据（从 YAML frontmatter 解析）
/// </summary>
public sealed record SkillMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public List<SkillParameterMetadata> Parameters { get; init; } = new();
    public string? Namespace { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// 技能参数元数据
/// </summary>
public sealed record SkillParameterMetadata
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool Required { get; init; }
    public string? Description { get; init; }
    public object? DefaultValue { get; init; }
}
