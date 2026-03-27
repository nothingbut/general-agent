namespace GeneralAgent.Infrastructure.Compression.Models;

/// <summary>
/// 压缩统计信息
/// </summary>
public class CompressionStats
{
    /// <summary>
    /// 原始消息数量
    /// </summary>
    public int OriginalMessageCount { get; set; }

    /// <summary>
    /// 压缩后消息数量
    /// </summary>
    public int CompressedMessageCount { get; set; }

    /// <summary>
    /// 原始 Token 数
    /// </summary>
    public int OriginalTokens { get; set; }

    /// <summary>
    /// 压缩后 Token 数
    /// </summary>
    public int CompressedTokens { get; set; }

    /// <summary>
    /// 压缩比率（0.0 - 1.0）
    /// </summary>
    public double CompressionRatio => OriginalTokens > 0
        ? 1.0 - ((double)CompressedTokens / OriginalTokens)
        : 0.0;

    /// <summary>
    /// Token 节省数量
    /// </summary>
    public int TokensSaved => OriginalTokens - CompressedTokens;

    /// <summary>
    /// 压缩耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 使用的策略名称
    /// </summary>
    public string StrategyUsed { get; set; } = string.Empty;
}
