using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace GeneralAgent.Infrastructure.VectorDB;

/// <summary>
/// Qdrant Client 包装器 - 适配真实的 QdrantClient 到 IQdrantClient 接口
/// </summary>
public sealed class QdrantClientWrapper : IQdrantClient
{
    private readonly QdrantClient _client;

    public QdrantClientWrapper(QdrantClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<UpdateResult> UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken = default)
    {
        return await _client.UpsertAsync(
            collectionName: collectionName,
            points: points,
            cancellationToken: cancellationToken);
    }

    public async Task<ScoredPoint[]> SearchAsync(
        string collectionName,
        float[] vector,
        Filter? filter,
        ulong limit,
        bool payloadSelector,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.SearchAsync(
            collectionName: collectionName,
            vector: vector,
            filter: filter,
            limit: limit,
            payloadSelector: payloadSelector,
            cancellationToken: cancellationToken);

        return result.ToArray();
    }

    public async Task<UpdateResult> DeleteAsync(
        string collectionName,
        Filter filter,
        bool wait,
        CancellationToken cancellationToken = default)
    {
        return await _client.DeleteAsync(
            collectionName: collectionName,
            filter: filter,
            wait: wait,
            cancellationToken: cancellationToken);
    }

    public async Task<CollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        return await _client.GetCollectionInfoAsync(
            collectionName: collectionName,
            cancellationToken: cancellationToken);
    }
}
