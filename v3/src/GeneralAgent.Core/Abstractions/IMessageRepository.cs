using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 消息仓储接口
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// 创建消息
    /// </summary>
    Task<Message> CreateAsync(Message message, CancellationToken ct = default);

    /// <summary>
    /// 根据 ID 查询消息
    /// </summary>
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 查询会话的所有消息
    /// </summary>
    Task<List<Message>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 查询会话的最近 N 条消息
    /// </summary>
    Task<List<Message>> GetRecentAsync(Guid sessionId, int limit, CancellationToken ct = default);

    /// <summary>
    /// 统计会话的消息数量
    /// </summary>
    Task<int> CountAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 删除会话的所有消息
    /// </summary>
    Task DeleteBySessionAsync(Guid sessionId, CancellationToken ct = default);
}
