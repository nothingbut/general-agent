namespace GeneralAgent.Core.Models;

/// <summary>
/// 向量搜索结果
/// </summary>
public sealed record VectorSearchResult
{
    /// <summary>
    /// 记忆 ID
    /// </summary>
    public Guid MemoryId { get; init; }

    /// <summary>
    /// 相似度评分（0.0-1.0，越高越相似）
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// 元数据（从向量数据库返回）
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
