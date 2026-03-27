using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// 压缩配置仓储实现
/// </summary>
public sealed class CompressionConfigRepository : ICompressionConfigRepository
{
    private readonly AgentDbContext _context;

    public CompressionConfigRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CompressionConfig?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.CompressionConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get compression config by session ID: {ex.Message}", ex);
        }
    }

    public async Task<CompressionConfig> SaveOrUpdateAsync(CompressionConfig config, CancellationToken ct = default)
    {
        try
        {
            var existing = await _context.CompressionConfigs
                .FirstOrDefaultAsync(c => c.SessionId == config.SessionId, ct);

            if (existing != null)
            {
                // 更新现有配置
                existing.AutoCompressionEnabled = config.AutoCompressionEnabled;
                existing.AutoCompressionThreshold = config.AutoCompressionThreshold;
                existing.DefaultStrategy = config.DefaultStrategy;
                existing.StrategyOptionsJson = config.StrategyOptionsJson;
                existing.UpdatedAt = DateTime.UtcNow;

                _context.CompressionConfigs.Update(existing);
                await _context.SaveChangesAsync(ct);
                return existing;
            }
            else
            {
                // 创建新配置
                _context.CompressionConfigs.Add(config);
                await _context.SaveChangesAsync(ct);
                return config;
            }
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to save or update compression config: {ex.Message}", ex);
        }
    }

    public async Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            var config = await _context.CompressionConfigs
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (config != null)
            {
                _context.CompressionConfigs.Remove(config);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete compression config: {ex.Message}", ex);
        }
    }

    public async Task<List<Guid>> GetAutoCompressionEnabledSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.CompressionConfigs
                .AsNoTracking()
                .Where(c => c.AutoCompressionEnabled)
                .Select(c => c.SessionId)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get auto-compression enabled sessions: {ex.Message}", ex);
        }
    }
}
