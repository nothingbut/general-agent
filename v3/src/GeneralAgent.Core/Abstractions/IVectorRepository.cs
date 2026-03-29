using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 向量存储和检索接口
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// 存储或更新向量及其元数据
    /// </summary>
    /// <param name="memoryId">记忆唯一标识</param>
    /// <param name="embedding">向量数据</param>
    /// <param name="metadata">元数据（type, name, created_at 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(
        Guid memoryId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量相似度搜索
    /// </summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topK">返回的结果数量</param>
    /// <param name="filters">过滤条件（可选，如 type=User）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果列表（按相似度降序）</returns>
    Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除向量
    /// </summary>
    /// <param name="memoryId">记忆唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示服务可用</returns>
    Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集合统计信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量数量、维度等统计</returns>
    Task<VectorCollectionStats> GetStatsAsync(
        CancellationToken cancellationToken = default);
}
