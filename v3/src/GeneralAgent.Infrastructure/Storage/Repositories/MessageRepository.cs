using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly AgentDbContext _context;

    public MessageRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Message> CreateAsync(Message message, CancellationToken ct = default)
    {
        try
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync(ct);
            return message;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to create message: {ex.Message}", ex);
        }
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get message by ID: {ex.Message}", ex);
        }
    }

    public async Task<List<Message>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages.AsNoTracking().Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get messages by session: {ex.Message}", ex);
        }
    }

    public async Task<List<Message>> GetRecentAsync(Guid sessionId, int limit, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages.AsNoTracking().Where(m => m.SessionId == sessionId).OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get recent messages: {ex.Message}", ex);
        }
    }

    public async Task<int> CountAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages.CountAsync(m => m.SessionId == sessionId, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to count messages: {ex.Message}", ex);
        }
    }

    public async Task DeleteBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            await _context.Messages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete messages by session: {ex.Message}", ex);
        }
    }
}
