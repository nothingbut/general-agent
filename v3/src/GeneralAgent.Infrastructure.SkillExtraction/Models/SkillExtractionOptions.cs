namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 技能提取配置选项
/// </summary>
public sealed record SkillExtractionOptions
{
    /// <summary>
    /// 技能目录路径
    /// </summary>
    public string SkillsDirectory { get; init; } = "skills";

    /// <summary>
    /// 最小置信度阈值
    /// </summary>
    public double MinimumConfidence { get; init; } = 0.6;

    /// <summary>
    /// 回溯消息数量
    /// </summary>
    public int LookbackMessages { get; init; } = 50;

    /// <summary>
    /// 是否自动创建命名空间目录
    /// </summary>
    public bool AutoCreateNamespaceDirectory { get; init; } = true;

    /// <summary>
    /// 文件名冲突时是否覆盖
    /// </summary>
    public bool OverwriteExisting { get; init; } = false;
}
