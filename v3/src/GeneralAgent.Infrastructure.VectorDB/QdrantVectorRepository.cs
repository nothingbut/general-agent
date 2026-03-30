using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace GeneralAgent.Infrastructure.VectorDB;

/// <summary>
/// Qdrant 向量数据库实现
/// </summary>
public sealed class QdrantVectorRepository : IVectorRepository
{
    private readonly IQdrantClient _client;
    private readonly VectorDBOptions _options;
    private readonly ILogger<QdrantVectorRepository> _logger;

    // 健康检查缓存
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private bool _lastHealthStatus;

    public QdrantVectorRepository(
        IQdrantClient client,
        IOptions<VectorDBOptions> options,
        ILogger<QdrantVectorRepository> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        Guid memoryId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pointId = new PointId { Uuid = memoryId.ToString() };
            var payload = ConvertMetadataToPayload(metadata);

            var point = new PointStruct
            {
                Id = pointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await _client.UpsertAsync(
                collectionName: _options.CollectionName,
                points: new[] { point },
                cancellationToken: cancellationToken);

            _logger.LogDebug("成功存储向量: {MemoryId}", memoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存储向量失败: {MemoryId}", memoryId);
            throw new VectorRepositoryException($"存储向量失败: {memoryId}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int topK = 5,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Filter? filter = filters is { Count: > 0 } ? CreateFilter(filters) : null;

            var response = await _client.SearchAsync(
                collectionName: _options.CollectionName,
                vector: queryVector,
                filter: filter,
                limit: (ulong)topK,
                payloadSelector: true,
                cancellationToken: cancellationToken);

            var results = response
                .Select(point => new VectorSearchResult
                {
                    MemoryId = Guid.Parse(point.Id.Uuid),
                    Score = (double)point.Score,
                    Metadata = ConvertPayloadToMetadata(point.Payload)
                })
                .ToList();

            _logger.LogDebug("向量搜索返回 {Count} 个结果", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向量搜索失败");
            throw new VectorRepositoryException("向量搜索失败", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hasIdCondition = new HasIdCondition();
            hasIdCondition.HasId.Add(new PointId { Uuid = memoryId.ToString() });

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        HasId = hasIdCondition
                    }
                }
            };

            await _client.DeleteAsync(
                collectionName: _options.CollectionName,
                filter: filter,
                wait: true,
                cancellationToken: cancellationToken);

            _logger.LogDebug("成功删除向量: {MemoryId}", memoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除向量失败: {MemoryId}", memoryId);
            throw new VectorRepositoryException($"删除向量失败: {memoryId}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        // 检查缓存
        var cacheAge = DateTime.UtcNow - _lastHealthCheck;
        var cacheExpiration = TimeSpan.FromSeconds(_options.HealthCheckCacheSeconds);

        if (cacheAge < cacheExpiration)
        {
            _logger.LogTrace("使用缓存的健康检查结果: {Status}", _lastHealthStatus);
            return _lastHealthStatus;
        }

        try
        {
            // 执行健康检查
            await _client.GetCollectionInfoAsync(
                _options.CollectionName,
                cancellationToken: cancellationToken);

            _lastHealthCheck = DateTime.UtcNow;
            _lastHealthStatus = true;

            _logger.LogDebug("VectorDB 健康检查通过");
            return true;
        }
        catch (Exception ex)
        {
            _lastHealthCheck = DateTime.UtcNow;
            _lastHealthStatus = false;

            _logger.LogWarning(ex, "VectorDB 健康检查失败");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<VectorCollectionStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionInfo = await _client.GetCollectionInfoAsync(
                _options.CollectionName,
                cancellationToken: cancellationToken);

            // 提取向量维度和索引类型
            int dimensions = 0;
            string indexType = "HNSW"; // 默认值

            if (collectionInfo.Config?.Params?.VectorsConfig != null)
            {
                var vectorsConfig = collectionInfo.Config.Params.VectorsConfig;

                // 尝试获取单一向量配置
                if (vectorsConfig.Params != null)
                {
                    dimensions = (int)vectorsConfig.Params.Size;
                    if (vectorsConfig.Params.HnswConfig != null)
                    {
                        indexType = "HNSW";
                    }
                }
            }

            var stats = new VectorCollectionStats
            {
                VectorCount = (long)collectionInfo.PointsCount,
                Dimensions = dimensions,
                IndexType = indexType
            };

            _logger.LogDebug("获取集合统计信息: {VectorCount} 个向量", stats.VectorCount);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取集合统计信息失败");
            throw new VectorRepositoryException("获取集合统计信息失败", ex);
        }
    }

    /// <summary>
    /// 将元数据字典转换为 Qdrant Payload
    /// </summary>
    private static Dictionary<string, Value> ConvertMetadataToPayload(Dictionary<string, object> metadata)
    {
        var payload = new Dictionary<string, Value>();

        foreach (var (key, value) in metadata)
        {
            payload[key] = value switch
            {
                string str => new Value { StringValue = str },
                int i => new Value { IntegerValue = i },
                long l => new Value { IntegerValue = l },
                double d => new Value { DoubleValue = d },
                float f => new Value { DoubleValue = f },
                bool b => new Value { BoolValue = b },
                _ => new Value { StringValue = value.ToString() ?? string.Empty }
            };
        }

        return payload;
    }

    /// <summary>
    /// 将 Qdrant Payload 转换为元数据字典
    /// </summary>
    private static Dictionary<string, object> ConvertPayloadToMetadata(
        IDictionary<string, Value> payload)
    {
        var metadata = new Dictionary<string, object>();

        foreach (var (key, value) in payload)
        {
            metadata[key] = value.KindCase switch
            {
                Value.KindOneofCase.StringValue => value.StringValue,
                Value.KindOneofCase.IntegerValue => value.IntegerValue,
                Value.KindOneofCase.DoubleValue => value.DoubleValue,
                Value.KindOneofCase.BoolValue => value.BoolValue,
                _ => value.ToString()
            };
        }

        return metadata;
    }

    /// <summary>
    /// 根据过滤条件创建 Qdrant Filter
    /// </summary>
    private static Filter CreateFilter(Dictionary<string, object> filters)
    {
        var conditions = new List<Condition>();

        foreach (var (key, value) in filters)
        {
            var matchValue = value switch
            {
                string str => new Match { Text = str },
                int i => new Match { Integer = i },
                long l => new Match { Integer = l },
                bool b => new Match { Boolean = b },
                _ => new Match { Text = value.ToString() ?? string.Empty }
            };

            var condition = new Condition
            {
                Field = new FieldCondition
                {
                    Key = key,
                    Match = matchValue
                }
            };

            conditions.Add(condition);
        }

        return new Filter
        {
            Must = { conditions }
        };
    }
}
