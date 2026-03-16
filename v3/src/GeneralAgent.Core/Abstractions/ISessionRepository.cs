using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 会话仓储接口
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// 创建会话
    /// </summary>
    Task<Session> CreateAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// 根据 ID 查询会话
    /// </summary>
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 列出会话（分页）
    /// </summary>
    Task<PagedResult<Session>> ListAsync(int limit, int offset, CancellationToken ct = default);

    /// <summary>
    /// 搜索会话（按标题模糊匹配）
    /// </summary>
    Task<List<Session>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// 更新会话
    /// </summary>
    Task UpdateAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// 删除会话
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
