using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// 压缩历史记录仓储实现
/// </summary>
public sealed class CompressionHistoryRepository : ICompressionHistoryRepository
{
    private readonly AgentDbContext _context;

    public CompressionHistoryRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CompressionHistory> SaveAsync(CompressionHistory history, CancellationToken ct = default)
    {
        try
        {
            _context.CompressionHistories.Add(history);
            await _context.SaveChangesAsync(ct);
            return history;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to save compression history: {ex.Message}", ex);
        }
    }

    public async Task<List<CompressionHistory>> GetBySessionIdAsync(
        Guid sessionId,
        int limit = 100,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.CompressionHistories
                .AsNoTracking()
                .Where(h => h.SessionId == sessionId)
                .OrderByDescending(h => h.CompressedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get compression history by session ID: {ex.Message}", ex);
        }
    }

    public async Task<List<CompressionHistory>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        try
        {
            return await _context.CompressionHistories
                .AsNoTracking()
                .OrderByDescending(h => h.CompressedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get recent compression history: {ex.Message}", ex);
        }
    }

    public async Task<CompressionStatsSummary> GetStatsAsync(
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.CompressionHistories.AsNoTracking();

            if (sessionId.HasValue)
            {
                query = query.Where(h => h.SessionId == sessionId.Value);
            }

            var histories = await query.ToListAsync(ct);

            if (histories.Count == 0)
            {
                return new CompressionStatsSummary
                {
                    TotalCompressions = 0,
                    AverageCompressionRatio = 0,
                    TotalTokensSaved = 0,
                    MostUsedStrategy = null,
                    AverageDurationMs = 0
                };
            }

            var totalCompressions = histories.Count;
            var averageCompressionRatio = histories.Average(h => h.CompressionRatio);
            var totalTokensSaved = histories.Sum(h => h.OriginalTokens - h.CompressedTokens);
            var mostUsedStrategy = histories
                .GroupBy(h => h.StrategyUsed)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
            var averageDurationMs = histories.Average(h => h.DurationMs);

            return new CompressionStatsSummary
            {
                TotalCompressions = totalCompressions,
                AverageCompressionRatio = averageCompressionRatio,
                TotalTokensSaved = totalTokensSaved,
                MostUsedStrategy = mostUsedStrategy,
                AverageDurationMs = averageDurationMs
            };
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get compression stats: {ex.Message}", ex);
        }
    }

    public async Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            var histories = await _context.CompressionHistories
                .Where(h => h.SessionId == sessionId)
                .ToListAsync(ct);

            _context.CompressionHistories.RemoveRange(histories);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete compression history: {ex.Message}", ex);
        }
    }
}
