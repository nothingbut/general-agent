namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 技能建议 - 从对话中提取的技能模式
/// </summary>
public sealed record SkillSuggestion
{
    /// <summary>
    /// 技能名称（小写字母和连字符）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 技能描述（一句话）
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 命名空间（如 dev、productivity、personal）
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// 提示词模板（使用 {{param}} 占位符）
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// 参数定义列表
    /// </summary>
    public required List<SkillParameterDefinition> Parameters { get; init; }

    /// <summary>
    /// 置信度（0.0-1.0）
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// 提取原因
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// 出现次数
    /// </summary>
    public int Occurrences { get; init; }

    /// <summary>
    /// 示例消息
    /// </summary>
    public List<string> ExampleMessages { get; init; } = new();

    /// <summary>
    /// 完整技能名称（包含命名空间）
    /// </summary>
    public string FullName => $"{Namespace}:{Name}";
}
