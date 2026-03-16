using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// Session 仓储实现
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly AgentDbContext _context;

    public SessionRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Session> CreateAsync(Session session, CancellationToken ct = default)
    {
        try
        {
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync(ct);
            return session;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to create session: {ex.Message}", ex);
        }
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get session by ID: {ex.Message}", ex);
        }
    }

    public async Task<PagedResult<Session>> ListAsync(int limit, int offset, CancellationToken ct = default)
    {
        try
        {
            var total = await _context.Sessions.CountAsync(ct);

            var items = await _context.Sessions
                .AsNoTracking()
                .OrderByDescending(s => s.UpdatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(ct);

            return new PagedResult<Session>(items, total, limit, offset);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to list sessions: {ex.Message}", ex);
        }
    }

    public async Task<List<Session>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        try
        {
            return await _context.Sessions
                .AsNoTracking()
                .Where(s => s.Title != null && s.Title.Contains(query))
                .OrderByDescending(s => s.UpdatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to search sessions: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        try
        {
            // 先分离可能已跟踪的实体
            var tracked = _context.ChangeTracker.Entries<Session>()
                .FirstOrDefault(e => e.Entity.Id == session.Id);
            if (tracked != null)
            {
                _context.Entry(tracked.Entity).State = EntityState.Detached;
            }

            _context.Sessions.Update(session);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to update session: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(new object[] { id }, ct);
            if (session != null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete session: {ex.Message}", ex);
        }
    }
}
