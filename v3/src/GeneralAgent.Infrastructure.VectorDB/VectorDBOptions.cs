namespace GeneralAgent.Infrastructure.VectorDB;

/// <summary>
/// VectorDB 配置选项
/// </summary>
public sealed class VectorDBOptions
{
    /// <summary>
    /// 配置段名称
    /// </summary>
    public const string SectionName = "VectorDB";

    /// <summary>
    /// 提供商名称（当前仅支持 "Qdrant"）
    /// </summary>
    public string Provider { get; init; } = "Qdrant";

    /// <summary>
    /// VectorDB 服务 URL
    /// </summary>
    public string Url { get; init; } = "http://localhost:6333";

    /// <summary>
    /// 集合名称
    /// </summary>
    public string CollectionName { get; init; } = "general_agent";

    /// <summary>
    /// 启用降级处理（当服务不可用时）
    /// </summary>
    public bool EnableFallback { get; init; } = true;

    /// <summary>
    /// 健康检查缓存时间（秒）
    /// </summary>
    public int HealthCheckCacheSeconds { get; init; } = 60;
}
