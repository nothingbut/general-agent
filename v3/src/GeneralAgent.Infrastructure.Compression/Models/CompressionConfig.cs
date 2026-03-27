namespace GeneralAgent.Infrastructure.Compression.Models;

/// <summary>
/// 会话级别的压缩配置（持久化到数据库）
/// </summary>
public class CompressionConfig
{
    /// <summary>
    /// 配置 ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 是否启用自动压缩
    /// </summary>
    public bool AutoCompressionEnabled { get; set; } = true;

    /// <summary>
    /// 自动压缩触发的 Token 阈值
    /// </summary>
    public int AutoCompressionThreshold { get; set; } = 3000;

    /// <summary>
    /// 默认压缩策略
    /// </summary>
    public string DefaultStrategy { get; set; } = "sliding_window";

    /// <summary>
    /// 策略特定的选项（JSON 序列化）
    /// </summary>
    public string? StrategyOptionsJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
