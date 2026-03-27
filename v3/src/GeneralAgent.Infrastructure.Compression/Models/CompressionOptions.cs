namespace GeneralAgent.Infrastructure.Compression.Models;

/// <summary>
/// 压缩选项配置
/// </summary>
public class CompressionOptions
{
    /// <summary>
    /// 压缩策略名称 ("sliding_window", "hierarchical", "semantic")
    /// </summary>
    public string Strategy { get; set; } = "sliding_window";

    /// <summary>
    /// 目标 Token 上限（默认 2000）
    /// </summary>
    public int TargetTokenLimit { get; set; } = 2000;

    /// <summary>
    /// 滑动窗口保留的消息数量
    /// </summary>
    public int WindowSize { get; set; } = 10;

    /// <summary>
    /// 是否保留系统消息（不参与压缩）
    /// </summary>
    public bool PreserveSystemMessages { get; set; } = true;

    /// <summary>
    /// 是否保留最新的 N 条消息（不参与压缩）
    /// </summary>
    public int PreserveRecentCount { get; set; } = 5;

    /// <summary>
    /// 层级压缩：近期详细消息数量
    /// </summary>
    public int HierarchicalRecentCount { get; set; } = 5;

    /// <summary>
    /// 层级压缩：中期关键点消息数量
    /// </summary>
    public int HierarchicalMiddleCount { get; set; } = 3;

    /// <summary>
    /// 语义压缩：是否启用 LLM 辅助摘要
    /// </summary>
    public bool EnableLlmSummary { get; set; } = false;

    /// <summary>
    /// 语义压缩：使用的 LLM 模型
    /// </summary>
    public string LlmModel { get; set; } = "claude-3-5-sonnet-20241022";

    /// <summary>
    /// 最小压缩阈值（消息数少于此值时不压缩）
    /// </summary>
    public int MinMessagesForCompression { get; set; } = 10;
}
