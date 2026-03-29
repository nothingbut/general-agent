namespace GeneralAgent.Core.Models;

/// <summary>
/// 向量集合统计信息
/// </summary>
public sealed record VectorCollectionStats
{
    /// <summary>
    /// 向量总数
    /// </summary>
    public long VectorCount { get; init; }

    /// <summary>
    /// 向量维度
    /// </summary>
    public int Dimensions { get; init; }

    /// <summary>
    /// 索引类型（如 "HNSW"）
    /// </summary>
    public string IndexType { get; init; } = string.Empty;
}
