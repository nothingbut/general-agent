using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Repositories;

/// <summary>
/// 技能提取历史仓储接口
/// </summary>
public interface IExtractionHistoryRepository
{
    /// <summary>
    /// 记录提取历史
    /// </summary>
    Task<ExtractionRecord> CreateAsync(
        ExtractionRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 查询记录
    /// </summary>
    Task<ExtractionRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询会话的所有记录
    /// </summary>
    Task<List<ExtractionRecord>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询技能的所有记录
    /// </summary>
    Task<List<ExtractionRecord>> GetBySkillAsync(
        string skillNamespace,
        string skillName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询指定动作的记录
    /// </summary>
    Task<List<ExtractionRecord>> GetByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<ExtractionStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提取统计信息
/// </summary>
public sealed record ExtractionStatistics
{
    /// <summary>
    /// 总提取次数
    /// </summary>
    public int TotalExtractions { get; init; }

    /// <summary>
    /// 接受次数
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// 编辑次数
    /// </summary>
    public int EditedCount { get; init; }

    /// <summary>
    /// 拒绝次数
    /// </summary>
    public int RejectedCount { get; init; }

    /// <summary>
    /// 平均置信度
    /// </summary>
    public double AverageConfidence { get; init; }
}
