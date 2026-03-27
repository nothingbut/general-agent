using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;

namespace GeneralAgent.Infrastructure.Compression;

/// <summary>
/// 压缩策略接口
/// </summary>
public interface ICompressionStrategy
{
    /// <summary>
    /// 策略名称（例如 "sliding_window", "hierarchical", "semantic"）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 策略描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 执行压缩
    /// </summary>
    /// <param name="messages">原始消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>压缩结果</returns>
    Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 估算压缩后的 Token 数量（快速估算，不执行实际压缩）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <returns>估算的 Token 数</returns>
    int EstimateCompressedTokens(List<Message> messages, CompressionOptions? options = null);

    /// <summary>
    /// 判断此策略是否适用于给定的消息列表
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <returns>是否适用</returns>
    bool IsApplicable(List<Message> messages, CompressionOptions? options = null);
}
