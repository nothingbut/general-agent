using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 记忆索引管理器接口（管理 MEMORY.md）
/// </summary>
public interface IMemoryIndexManager
{
    /// <summary>
    /// 重新构建完整的记忆索引
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加记忆到索引
    /// </summary>
    Task AddToIndexAsync(Memory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从索引中移除记忆
    /// </summary>
    Task RemoveFromIndexAsync(Guid memoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新索引中的记忆信息
    /// </summary>
    Task UpdateInIndexAsync(Memory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有索引条目
    /// </summary>
    Task<List<MemoryIndex>> GetAllIndexEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据类型获取索引条目
    /// </summary>
    Task<List<MemoryIndex>> GetIndexEntriesByTypeAsync(
        MemoryType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证索引完整性（检查索引与实际文件是否一致）
    /// </summary>
    Task<bool> ValidateIndexAsync(CancellationToken cancellationToken = default);
}
