using GeneralAgent.Infrastructure.Compression.Models;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 压缩历史记录仓储接口
/// </summary>
public interface ICompressionHistoryRepository
{
    /// <summary>
    /// 保存压缩历史记录
    /// </summary>
    Task<CompressionHistory> SaveAsync(CompressionHistory history, CancellationToken ct = default);

    /// <summary>
    /// 获取会话的压缩历史记录
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="limit">返回记录数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>历史记录列表（按时间降序）</returns>
    Task<List<CompressionHistory>> GetBySessionIdAsync(
        Guid sessionId,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// 获取最近的压缩历史记录
    /// </summary>
    /// <param name="limit">返回记录数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>历史记录列表（按时间降序）</returns>
    Task<List<CompressionHistory>> GetRecentAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// 获取压缩统计信息
    /// </summary>
    /// <param name="sessionId">会话 ID（可选，null 表示全局统计）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>统计信息</returns>
    Task<CompressionStatsSummary> GetStatsAsync(Guid? sessionId = null, CancellationToken ct = default);

    /// <summary>
    /// 删除会话的压缩历史记录
    /// </summary>
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// 压缩统计摘要
/// </summary>
public class CompressionStatsSummary
{
    /// <summary>
    /// 总压缩次数
    /// </summary>
    public int TotalCompressions { get; set; }

    /// <summary>
    /// 平均压缩比率
    /// </summary>
    public double AverageCompressionRatio { get; set; }

    /// <summary>
    /// 总节省 Token 数
    /// </summary>
    public int TotalTokensSaved { get; set; }

    /// <summary>
    /// 最常用的策略
    /// </summary>
    public string? MostUsedStrategy { get; set; }

    /// <summary>
    /// 平均压缩耗时（毫秒）
    /// </summary>
    public double AverageDurationMs { get; set; }
}
