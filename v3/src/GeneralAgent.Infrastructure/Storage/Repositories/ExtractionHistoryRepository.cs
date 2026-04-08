using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// 基于 EF Core 的技能提取历史仓储实现
/// </summary>
public sealed class ExtractionHistoryRepository : IExtractionHistoryRepository
{
    private readonly AgentDbContext _context;
    private readonly ILogger<ExtractionHistoryRepository> _logger;

    public ExtractionHistoryRepository(
        AgentDbContext context,
        ILogger<ExtractionHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ExtractionRecord> CreateAsync(
        ExtractionRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.ExtractionRecords.Add(record);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("已创建提取记录: {SkillName}", record.FullSkillName);

            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建提取记录失败: {SkillName}", record.FullSkillName);
            throw;
        }
    }

    public async Task<ExtractionRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExtractionRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询提取记录失败: {Id}", id);
            throw;
        }
    }

    public async Task<List<ExtractionRecord>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExtractionRecords
                .AsNoTracking()
                .Where(r => r.SessionId == sessionId)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按会话查询提取记录失败: {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<List<ExtractionRecord>> GetBySkillAsync(
        string skillNamespace,
        string skillName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExtractionRecords
                .AsNoTracking()
                .Where(r => r.SkillNamespace == skillNamespace && r.SkillName == skillName)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按技能查询提取记录失败: {Namespace}:{Name}",
                skillNamespace, skillName);
            throw;
        }
    }

    public async Task<List<ExtractionRecord>> GetByActionAsync(
        EditAction action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExtractionRecords
                .AsNoTracking()
                .Where(r => r.Action == action)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按动作查询提取记录失败: {Action}", action);
            throw;
        }
    }

    public async Task<ExtractionStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var totalCount = await _context.ExtractionRecords.CountAsync(cancellationToken);

            if (totalCount == 0)
            {
                return new ExtractionStatistics
                {
                    TotalExtractions = 0,
                    AcceptedCount = 0,
                    EditedCount = 0,
                    RejectedCount = 0,
                    AverageConfidence = 0
                };
            }

            var acceptedCount = await _context.ExtractionRecords
                .CountAsync(r => r.Action == EditAction.Accept, cancellationToken);

            var editedCount = await _context.ExtractionRecords
                .CountAsync(r => r.Action == EditAction.Edit, cancellationToken);

            var rejectedCount = await _context.ExtractionRecords
                .CountAsync(r => r.Action == EditAction.Reject, cancellationToken);

            var averageConfidence = await _context.ExtractionRecords
                .AverageAsync(r => r.Confidence, cancellationToken);

            return new ExtractionStatistics
            {
                TotalExtractions = totalCount,
                AcceptedCount = acceptedCount,
                EditedCount = editedCount,
                RejectedCount = rejectedCount,
                AverageConfidence = averageConfidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取提取统计失败");
            throw;
        }
    }
}
