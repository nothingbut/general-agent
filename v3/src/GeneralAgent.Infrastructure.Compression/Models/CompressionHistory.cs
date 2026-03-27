namespace GeneralAgent.Infrastructure.Compression.Models;

/// <summary>
/// 压缩历史记录（数据库实体）
/// </summary>
public class CompressionHistory
{
    /// <summary>
    /// 历史记录 ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 使用的压缩策略
    /// </summary>
    public string StrategyUsed { get; set; } = string.Empty;

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
    public double CompressionRatio { get; set; }

    /// <summary>
    /// 压缩耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 压缩时间
    /// </summary>
    public DateTime CompressedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 压缩元数据（JSON 序列化）
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// 从 CompressionStats 创建历史记录
    /// </summary>
    public static CompressionHistory FromStats(Guid sessionId, CompressionStats stats, string? metadataJson = null)
    {
        return new CompressionHistory
        {
            SessionId = sessionId,
            StrategyUsed = stats.StrategyUsed,
            OriginalMessageCount = stats.OriginalMessageCount,
            CompressedMessageCount = stats.CompressedMessageCount,
            OriginalTokens = stats.OriginalTokens,
            CompressedTokens = stats.CompressedTokens,
            CompressionRatio = stats.CompressionRatio,
            DurationMs = stats.DurationMs,
            MetadataJson = metadataJson
        };
    }
}
