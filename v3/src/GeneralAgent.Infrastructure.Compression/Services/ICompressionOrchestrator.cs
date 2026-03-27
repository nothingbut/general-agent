using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 压缩编排器接口
/// </summary>
public interface ICompressionOrchestrator
{
    /// <summary>
    /// 执行压缩（自动选择最佳策略）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>压缩结果</returns>
    Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定策略执行压缩
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="messages">消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>压缩结果</returns>
    Task<CompressionResult> CompressWithStrategyAsync(
        string strategyName,
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有可用的策略列表
    /// </summary>
    /// <returns>策略名称列表</returns>
    List<string> GetAvailableStrategies();

    /// <summary>
    /// 根据消息列表推荐最佳策略
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">压缩选项</param>
    /// <returns>推荐的策略名称</returns>
    string RecommendStrategy(List<Message> messages, CompressionOptions? options = null);
}
