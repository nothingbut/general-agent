using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 记忆仓储接口（基于文件系统）
/// </summary>
public interface IMemoryRepository
{
    /// <summary>
    /// 保存记忆到文件系统
    /// </summary>
    Task<Memory> SaveAsync(Memory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取记忆
    /// </summary>
    Task<Memory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量根据 ID 获取记忆（优化 N+1 查询）
    /// </summary>
    Task<List<Memory>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称和类型获取记忆
    /// </summary>
    Task<Memory?> GetByNameAsync(
        string name,
        MemoryType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有记忆
    /// </summary>
    Task<List<Memory>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据类型获取记忆列表
    /// </summary>
    Task<List<Memory>> GetByTypeAsync(
        MemoryType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索记忆（根据关键词）
    /// </summary>
    Task<List<Memory>> SearchAsync(
        string keyword,
        MemoryType? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标签搜索记忆
    /// </summary>
    Task<List<Memory>> SearchByTagsAsync(
        List<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新记忆
    /// </summary>
    Task<Memory> UpdateAsync(Memory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除记忆
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查记忆是否存在
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查名称是否已被使用
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        MemoryType type,
        CancellationToken cancellationToken = default);
}
