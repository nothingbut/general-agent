using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 会话业务服务
///
/// 职责：
/// - 协调 ISessionRepository 的调用
/// - 提供应用层的会话管理功能
/// - 使用 Session 的不可变更新方法
/// </summary>
public sealed class SessionService
{
    private readonly ISessionRepository _repository;

    /// <summary>
    /// 初始化 SessionService
    /// </summary>
    /// <param name="repository">会话仓储</param>
    public SessionService(ISessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="title">会话标题（可选）</param>
    /// <param name="parentId">父会话 ID（可选，用于 Subagent 场景）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>创建的会话</returns>
    public async Task<Session> CreateSessionAsync(
        string? title = null,
        Guid? parentId = null,
        CancellationToken ct = default)
    {
        var session = Session.Create(title, parentId);
        return await _repository.CreateAsync(session, ct);
    }

    /// <summary>
    /// 获取会话
    /// </summary>
    /// <param name="id">会话 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>会话，如果不存在则返回 null</returns>
    public async Task<Session?> GetSessionAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    /// <summary>
    /// 列出会话（分页）
    /// </summary>
    /// <param name="limit">每页限制（默认 20）</param>
    /// <param name="offset">偏移量（默认 0）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分页会话列表</returns>
    public async Task<PagedResult<Session>> ListSessionsAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await _repository.ListAsync(limit, offset, ct);
    }

    /// <summary>
    /// 更新会话标题
    /// </summary>
    /// <param name="id">会话 ID</param>
    /// <param name="title">新标题（可为 null 清空标题）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新后的会话</returns>
    /// <exception cref="InvalidOperationException">会话不存在</exception>
    public async Task<Session> UpdateSessionTitleAsync(
        Guid id,
        string? title,
        CancellationToken ct = default)
    {
        var session = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"会话不存在: {id}");

        var updatedSession = session.WithTitle(title);
        await _repository.UpdateAsync(updatedSession, ct);
        return updatedSession;
    }

    /// <summary>
    /// 更新会话状态
    /// </summary>
    /// <param name="id">会话 ID</param>
    /// <param name="status">新状态</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新后的会话</returns>
    /// <exception cref="InvalidOperationException">会话不存在</exception>
    public async Task<Session> UpdateSessionStatusAsync(
        Guid id,
        SessionStatus status,
        CancellationToken ct = default)
    {
        var session = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"会话不存在: {id}");

        var updatedSession = session.WithStatus(status);
        await _repository.UpdateAsync(updatedSession, ct);
        return updatedSession;
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="id">会话 ID</param>
    /// <param name="ct">取消令牌</param>
    public async Task DeleteSessionAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }
}
