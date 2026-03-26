using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// SessionTag 仓储实现
/// </summary>
public sealed class SessionTagRepository : ISessionTagRepository
{
    private readonly AgentDbContext _context;

    public SessionTagRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(SessionTag tag, CancellationToken ct = default)
    {
        try
        {
            // 检查是否已存在相同的标签（大小写不敏感）
            var exists = await _context.Set<SessionTag>()
                .AnyAsync(t => t.SessionId == tag.SessionId && t.Tag == tag.Tag.ToLowerInvariant(), ct);

            if (exists)
            {
                throw new StorageException($"Tag '{tag.Tag}' already exists for session {tag.SessionId}");
            }

            _context.Set<SessionTag>().Add(tag);
            await _context.SaveChangesAsync(ct);
        }
        catch (StorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to add tag: {ex.Message}", ex);
        }
    }

    public async Task RemoveAsync(Guid sessionId, string tag, CancellationToken ct = default)
    {
        try
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            var tagToRemove = await _context.Set<SessionTag>()
                .FirstOrDefaultAsync(t => t.SessionId == sessionId && t.Tag == normalizedTag, ct);

            if (tagToRemove != null)
            {
                _context.Set<SessionTag>().Remove(tagToRemove);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to remove tag: {ex.Message}", ex);
        }
    }

    public async Task<List<SessionTag>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.Set<SessionTag>()
                .AsNoTracking()
                .Where(t => t.SessionId == sessionId)
                .OrderBy(t => t.Tag)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get tags by session: {ex.Message}", ex);
        }
    }

    public async Task<List<Guid>> GetByTagAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            return await _context.Set<SessionTag>()
                .AsNoTracking()
                .Where(t => t.Tag == normalizedTag)
                .Select(t => t.SessionId)
                .Distinct()
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get sessions by tag: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetAllTagsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.Set<SessionTag>()
                .AsNoTracking()
                .Select(t => t.Tag)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get all tags: {ex.Message}", ex);
        }
    }

    public async Task RemoveBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            await _context.Set<SessionTag>()
                .Where(t => t.SessionId == sessionId)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to remove tags by session: {ex.Message}", ex);
        }
    }
}
