using System.Collections.Concurrent;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.SkillExtraction.Repositories;

/// <summary>
/// 内存技能提取历史仓储 - 用于开发和测试
/// </summary>
public sealed class InMemoryExtractionHistoryRepository : IExtractionHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, ExtractionRecord> _records = new();
    private readonly ILogger<InMemoryExtractionHistoryRepository> _logger;

    public InMemoryExtractionHistoryRepository(
        ILogger<InMemoryExtractionHistoryRepository> logger)
    {
        _logger = logger;
    }

    public Task<ExtractionRecord> CreateAsync(
        ExtractionRecord record,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("创建提取记录: {SkillName}", record.FullSkillName);

        if (!_records.TryAdd(record.Id, record))
        {
            throw new InvalidOperationException($"记录 ID 已存在: {record.Id}");
        }

        return Task.FromResult(record);
    }

    public Task<ExtractionRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<List<ExtractionRecord>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var records = _records.Values
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        return Task.FromResult(records);
    }

    public Task<List<ExtractionRecord>> GetBySkillAsync(
        string skillNamespace,
        string skillName,
        CancellationToken cancellationToken = default)
    {
        var records = _records.Values
            .Where(r => r.SkillNamespace == skillNamespace && r.SkillName == skillName)
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        return Task.FromResult(records);
    }

    public Task<List<ExtractionRecord>> GetByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default)
    {
        var records = _records.Values
            .Where(r => r.Action == action)
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        return Task.FromResult(records);
    }

    public Task<ExtractionStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var allRecords = _records.Values.ToList();

        var statistics = new ExtractionStatistics
        {
            TotalExtractions = allRecords.Count,
            AcceptedCount = allRecords.Count(r => r.Action == EditAction.Accept),
            EditedCount = allRecords.Count(r => r.Action == EditAction.Edit),
            RejectedCount = allRecords.Count(r => r.Action == EditAction.Reject),
            AverageConfidence = allRecords.Count > 0
                ? allRecords.Average(r => r.Confidence)
                : 0
        };

        return Task.FromResult(statistics);
    }

    /// <summary>
    /// 清空所有记录（仅用于测试）
    /// </summary>
    public void Clear()
    {
        _records.Clear();
        _logger.LogDebug("已清空所有提取记录");
    }

    /// <summary>
    /// 获取记录总数（仅用于测试）
    /// </summary>
    public int Count => _records.Count;
}
