namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 技能参数定义
/// </summary>
public sealed record SkillParameterDefinition
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 参数类型（string、number、boolean）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public required bool Required { get; init; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 默认值（可选）
    /// </summary>
    public string? DefaultValue { get; init; }
}
