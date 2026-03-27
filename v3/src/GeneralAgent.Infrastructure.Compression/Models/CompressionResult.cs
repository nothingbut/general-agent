using GeneralAgent.Core.Models;

namespace GeneralAgent.Infrastructure.Compression.Models;

/// <summary>
/// 压缩结果
/// </summary>
public class CompressionResult
{
    /// <summary>
    /// 压缩后的消息列表
    /// </summary>
    public List<Message> CompressedMessages { get; set; } = new();

    /// <summary>
    /// 压缩统计信息
    /// </summary>
    public CompressionStats Stats { get; set; } = new();

    /// <summary>
    /// 是否成功压缩
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 压缩元数据（策略特定信息）
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
