using System.Collections.Generic;

namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能定义
/// </summary>
public sealed record Skill
{
    /// <summary>
    /// 技能名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 技能描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 提示词模板
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// 参数列表
    /// </summary>
    public required IReadOnlyList<SkillParameter> Parameters { get; init; }

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// 是否需要上下文（对话历史）
    /// </summary>
    public bool RequiresContext { get; init; } = false;

    /// <summary>
    /// 上下文配置
    /// </summary>
    public ContextConfig? ContextConfig { get; init; }

    /// <summary>
    /// 执行结果是否返回给 LLM
    /// </summary>
    public bool ReturnToLLM { get; init; } = true;

    /// <summary>
    /// 完整技能名称（包含命名空间）
    /// 例如：personal:greeting
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}:{Name}";
}
