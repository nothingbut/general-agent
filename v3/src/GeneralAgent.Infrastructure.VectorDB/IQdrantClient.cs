using Qdrant.Client.Grpc;

namespace GeneralAgent.Infrastructure.VectorDB;

/// <summary>
/// Qdrant Client 接口 - 用于依赖注入和测试
/// </summary>
public interface IQdrantClient
{
    /// <summary>
    /// 插入或更新向量点
    /// </summary>
    Task<UpdateResult> UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索向量
    /// </summary>
    Task<ScoredPoint[]> SearchAsync(
        string collectionName,
        float[] vector,
        Filter? filter,
        ulong limit,
        bool payloadSelector,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除向量
    /// </summary>
    Task<UpdateResult> DeleteAsync(
        string collectionName,
        Filter filter,
        bool wait,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集合信息
    /// </summary>
    Task<CollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default);
}
