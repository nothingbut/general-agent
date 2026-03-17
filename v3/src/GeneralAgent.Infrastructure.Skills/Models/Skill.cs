using System.Collections.Generic;

namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能定义
/// </summary>
public sealed record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Template { get; init; }
    public required IReadOnlyList<SkillParameter> Parameters { get; init; }
    public string? Namespace { get; init; }
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// 完整技能名称（包含命名空间）
    /// 例如：personal:greeting
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}:{Name}";
}
