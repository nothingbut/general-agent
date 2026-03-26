using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 会话标签仓储接口
/// </summary>
public interface ISessionTagRepository
{
    /// <summary>
    /// 添加标签到会话
    /// </summary>
    /// <exception cref="Core.Exceptions.StorageException">当添加重复标签或数据库操作失败时抛出</exception>
    Task AddAsync(SessionTag tag, CancellationToken ct = default);

    /// <summary>
    /// 从会话移除标签（大小写不敏感）
    /// </summary>
    Task RemoveAsync(Guid sessionId, string tag, CancellationToken ct = default);

    /// <summary>
    /// 获取会话的所有标签
    /// </summary>
    Task<List<SessionTag>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 根据标签查找所有会话 ID（大小写不敏感）
    /// </summary>
    Task<List<Guid>> GetByTagAsync(string tag, CancellationToken ct = default);

    /// <summary>
    /// 获取所有不重复的标签名称
    /// </summary>
    Task<List<string>> GetAllTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取所有标签及其使用次数（按会话数统计）
    /// </summary>
    Task<Dictionary<string, int>> GetTagStatisticsAsync(CancellationToken ct = default);

    /// <summary>
    /// 删除会话的所有标签
    /// </summary>
    Task RemoveBySessionAsync(Guid sessionId, CancellationToken ct = default);
}
