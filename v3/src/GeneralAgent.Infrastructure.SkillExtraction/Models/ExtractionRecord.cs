namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 技能提取历史记录
/// </summary>
public sealed record ExtractionRecord
{
    /// <summary>
    /// 记录 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 关联的会话 ID（可选）
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// 技能名称
    /// </summary>
    public required string SkillName { get; init; }

    /// <summary>
    /// 技能命名空间
    /// </summary>
    public required string SkillNamespace { get; init; }

    /// <summary>
    /// 用户的动作
    /// </summary>
    public required EditAction Action { get; init; }

    /// <summary>
    /// 置信度
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// 出现次数
    /// </summary>
    public int Occurrences { get; init; }

    /// <summary>
    /// 拒绝原因（如果被拒绝）
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// 额外元数据（JSON）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 完整技能名称
    /// </summary>
    public string FullSkillName => $"{SkillNamespace}:{SkillName}";
}
