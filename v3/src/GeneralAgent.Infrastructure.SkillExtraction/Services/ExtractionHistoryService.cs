using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 提取历史服务实现
/// </summary>
public sealed class ExtractionHistoryService : IExtractionHistoryService
{
    private readonly IExtractionHistoryRepository _repository;
    private readonly ILogger<ExtractionHistoryService> _logger;

    public ExtractionHistoryService(
        IExtractionHistoryRepository repository,
        ILogger<ExtractionHistoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Guid> RecordExtractionAsync(
        SkillSuggestion suggestion,
        EditAction action,
        string? sessionId = null,
        string? rejectionReason = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("记录技能提取事件: {SkillName}, 动作: {Action}",
            suggestion.FullName, action);

        var record = new ExtractionRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            SessionId = sessionId,
            SkillName = suggestion.Name,
            SkillNamespace = suggestion.Namespace,
            Action = action,
            Confidence = suggestion.Confidence,
            Occurrences = suggestion.Occurrences,
            RejectionReason = rejectionReason,
            Metadata = new Dictionary<string, object>
            {
                ["Rationale"] = suggestion.Rationale,
                ["ExampleMessages"] = suggestion.ExampleMessages,
                ["ParameterCount"] = suggestion.Parameters.Count
            }
        };

        await _repository.CreateAsync(record, cancellationToken);

        return record.Id;
    }

    public async Task<List<ExtractionRecord>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("查询提取历史（限制 {Limit} 条）", limit);

        // 获取所有记录并按时间排序
        var allRecords = await GetAllRecordsAsync(cancellationToken);

        return allRecords
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToList();
    }

    public async Task<List<ExtractionRecord>> GetHistoryByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("按动作查询历史: {Action}", action);

        return await _repository.GetByActionAsync(action, cancellationToken);
    }

    public async Task<List<ExtractionRecord>> GetHistoryBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("按会话查询历史: {SessionId}", sessionId);

        return await _repository.GetBySessionAsync(sessionId, cancellationToken);
    }

    public async Task<List<ExtractionRecord>> GetHistoryBySkillAsync(
        string skillNamespace,
        string skillName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("按技能查询历史: {Namespace}:{Name}", skillNamespace, skillName);

        return await _repository.GetBySkillAsync(skillNamespace, skillName, cancellationToken);
    }

    public async Task<ExtractionStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("获取提取统计信息");

        return await _repository.GetStatisticsAsync(cancellationToken);
    }

    public async Task<List<SkillPopularity>> GetMostPopularSkillsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("获取最受欢迎的技能（限制 {Limit} 个）", limit);

        var allRecords = await GetAllRecordsAsync(cancellationToken);

        // 按技能分组并统计
        var popularity = allRecords
            .GroupBy(r => r.FullSkillName)
            .Select(g => new SkillPopularity
            {
                FullSkillName = g.Key,
                AcceptedCount = g.Count(r => r.Action == EditAction.Accept),
                EditedCount = g.Count(r => r.Action == EditAction.Edit),
                TotalSuggestions = g.Count()
            })
            .OrderByDescending(p => p.AcceptedCount + p.EditedCount)
            .Take(limit)
            .ToList();

        return popularity;
    }

    public async Task<List<RejectionSummary>> GetRejectionPatternsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("获取拒绝模式（限制 {Limit} 个）", limit);

        var rejectedRecords = await _repository.GetByActionAsync(
            EditAction.Reject, cancellationToken);

        // 按技能分组并统计拒绝原因
        var rejectionSummaries = rejectedRecords
            .GroupBy(r => r.FullSkillName)
            .Select(g => new RejectionSummary
            {
                FullSkillName = g.Key,
                RejectionCount = g.Count(),
                CommonReasons = g
                    .Where(r => !string.IsNullOrEmpty(r.RejectionReason))
                    .Select(r => r.RejectionReason!)
                    .GroupBy(reason => reason)
                    .OrderByDescending(reasonGroup => reasonGroup.Count())
                    .Take(3)
                    .Select(reasonGroup => reasonGroup.Key)
                    .ToList(),
                AverageConfidence = g.Average(r => r.Confidence)
            })
            .OrderByDescending(s => s.RejectionCount)
            .Take(limit)
            .ToList();

        return rejectionSummaries;
    }

    private async Task<List<ExtractionRecord>> GetAllRecordsAsync(
        CancellationToken cancellationToken)
    {
        // 从仓储获取所有记录
        // 注意: 这里假设 GetByActionAsync 返回所有该动作的记录
        // 一个更优的实现应该在 IExtractionHistoryRepository 中添加 GetAllAsync 方法

        var acceptedRecords = await _repository.GetByActionAsync(
            EditAction.Accept, cancellationToken);
        var editedRecords = await _repository.GetByActionAsync(
            EditAction.Edit, cancellationToken);
        var rejectedRecords = await _repository.GetByActionAsync(
            EditAction.Reject, cancellationToken);

        return acceptedRecords
            .Concat(editedRecords)
            .Concat(rejectedRecords)
            .ToList();
    }
}
