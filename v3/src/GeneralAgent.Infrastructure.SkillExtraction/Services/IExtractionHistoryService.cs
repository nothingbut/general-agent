using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 提取历史服务接口 - 提供历史记录的业务逻辑
/// </summary>
public interface IExtractionHistoryService
{
    /// <summary>
    /// 记录提取事件
    /// </summary>
    /// <param name="suggestion">技能建议</param>
    /// <param name="action">用户动作</param>
    /// <param name="sessionId">会话 ID（可选）</param>
    /// <param name="rejectionReason">拒绝原因（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>记录 ID</returns>
    Task<Guid> RecordExtractionAsync(
        SkillSuggestion suggestion,
        EditAction action,
        string? sessionId = null,
        string? rejectionReason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取提取历史
    /// </summary>
    /// <param name="limit">限制数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>历史记录列表</returns>
    Task<List<ExtractionRecord>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动作过滤历史
    /// </summary>
    /// <param name="action">动作类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>过滤后的历史记录</returns>
    Task<List<ExtractionRecord>> GetHistoryByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按会话过滤历史
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话的历史记录</returns>
    Task<List<ExtractionRecord>> GetHistoryBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按技能过滤历史
    /// </summary>
    /// <param name="skillNamespace">技能命名空间</param>
    /// <param name="skillName">技能名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能的历史记录</returns>
    Task<List<ExtractionRecord>> GetHistoryBySkillAsync(
        string skillNamespace,
        string skillName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计信息</returns>
    Task<ExtractionStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最受欢迎的技能（按接受次数排序）
    /// </summary>
    /// <param name="limit">限制数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能统计列表</returns>
    Task<List<SkillPopularity>> GetMostPopularSkillsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最常被拒绝的建议（用于改进提取算法）
    /// </summary>
    /// <param name="limit">限制数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拒绝原因统计</returns>
    Task<List<RejectionSummary>> GetRejectionPatternsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 技能流行度统计
/// </summary>
public sealed record SkillPopularity
{
    /// <summary>
    /// 技能完整名称
    /// </summary>
    public required string FullSkillName { get; init; }

    /// <summary>
    /// 被接受的次数
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// 被编辑的次数
    /// </summary>
    public int EditedCount { get; init; }

    /// <summary>
    /// 总建议次数
    /// </summary>
    public int TotalSuggestions { get; init; }

    /// <summary>
    /// 接受率
    /// </summary>
    public double AcceptanceRate => TotalSuggestions > 0
        ? (double)(AcceptedCount + EditedCount) / TotalSuggestions
        : 0;
}

/// <summary>
/// 拒绝原因摘要
/// </summary>
public sealed record RejectionSummary
{
    /// <summary>
    /// 技能完整名称
    /// </summary>
    public required string FullSkillName { get; init; }

    /// <summary>
    /// 拒绝次数
    /// </summary>
    public int RejectionCount { get; init; }

    /// <summary>
    /// 常见拒绝原因
    /// </summary>
    public List<string> CommonReasons { get; init; } = new();

    /// <summary>
    /// 平均置信度
    /// </summary>
    public double AverageConfidence { get; init; }
}
