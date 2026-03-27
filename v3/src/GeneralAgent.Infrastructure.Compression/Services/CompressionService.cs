using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 压缩服务（协调压缩和持久化）
/// </summary>
public class CompressionService
{
    private readonly ICompressionOrchestrator _orchestrator;
    private readonly ICompressionHistoryRepository _historyRepository;
    private readonly ICompressionConfigRepository _configRepository;
    private readonly ILogger<CompressionService> _logger;

    public CompressionService(
        ICompressionOrchestrator orchestrator,
        ICompressionHistoryRepository historyRepository,
        ICompressionConfigRepository configRepository,
        ILogger<CompressionService> logger)
    {
        _orchestrator = orchestrator;
        _historyRepository = historyRepository;
        _configRepository = configRepository;
        _logger = logger;
    }

    /// <summary>
    /// 压缩会话消息并保存历史记录
    /// </summary>
    public async Task<CompressionResult> CompressSessionAsync(
        Guid sessionId,
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取会话配置（如果存在）
            var config = await _configRepository.GetBySessionIdAsync(sessionId, cancellationToken);
            if (config != null && !string.IsNullOrEmpty(config.DefaultStrategy))
            {
                options ??= new CompressionOptions();
                options.Strategy = config.DefaultStrategy;

                // 如果有自定义选项，合并它们
                if (!string.IsNullOrEmpty(config.StrategyOptionsJson))
                {
                    try
                    {
                        var customOptions = JsonSerializer.Deserialize<CompressionOptions>(config.StrategyOptionsJson);
                        if (customOptions != null)
                        {
                            // 合并自定义选项（仅覆盖非默认值）
                            MergeOptions(options, customOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize strategy options for session {SessionId}", sessionId);
                    }
                }
            }

            // 执行压缩
            var result = await _orchestrator.CompressAsync(messages, options, cancellationToken);

            // 保存历史记录
            if (result.Success)
            {
                var metadataJson = result.Metadata.Count > 0
                    ? JsonSerializer.Serialize(result.Metadata)
                    : null;

                var history = CompressionHistory.FromStats(sessionId, result.Stats, metadataJson);
                await _historyRepository.SaveAsync(history, cancellationToken);

                _logger.LogInformation(
                    "Compression completed for session {SessionId}: {Original} messages → {Compressed} messages, {Ratio:P2} compression ratio",
                    sessionId,
                    result.Stats.OriginalMessageCount,
                    result.Stats.CompressedMessageCount,
                    result.Stats.CompressionRatio);
            }
            else
            {
                _logger.LogError("Compression failed for session {SessionId}: {Error}", sessionId, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compression for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// 检查会话是否需要自动压缩
    /// </summary>
    public async Task<bool> ShouldAutoCompressAsync(
        Guid sessionId,
        int currentTokenCount,
        CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetBySessionIdAsync(sessionId, cancellationToken);

        if (config == null)
        {
            // 没有配置，使用默认阈值
            return currentTokenCount >= 3000;
        }

        return config.AutoCompressionEnabled && currentTokenCount >= config.AutoCompressionThreshold;
    }

    /// <summary>
    /// 获取会话的压缩历史
    /// </summary>
    public async Task<List<CompressionHistory>> GetSessionHistoryAsync(
        Guid sessionId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetBySessionIdAsync(sessionId, limit, cancellationToken);
    }

    /// <summary>
    /// 获取压缩统计信息
    /// </summary>
    public async Task<CompressionStatsSummary> GetStatsAsync(
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetStatsAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// 获取会话的压缩配置
    /// </summary>
    public async Task<CompressionConfig> GetOrCreateConfigAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetBySessionIdAsync(sessionId, cancellationToken);

        if (config == null)
        {
            // 创建默认配置
            config = new CompressionConfig
            {
                SessionId = sessionId,
                AutoCompressionEnabled = true,
                AutoCompressionThreshold = 3000,
                DefaultStrategy = "sliding_window"
            };

            config = await _configRepository.SaveOrUpdateAsync(config, cancellationToken);
        }

        return config;
    }

    /// <summary>
    /// 更新会话的压缩配置
    /// </summary>
    public async Task<CompressionConfig> UpdateConfigAsync(
        CompressionConfig config,
        CancellationToken cancellationToken = default)
    {
        return await _configRepository.SaveOrUpdateAsync(config, cancellationToken);
    }

    /// <summary>
    /// 合并压缩选项
    /// </summary>
    private void MergeOptions(CompressionOptions target, CompressionOptions source)
    {
        if (source.TargetTokenLimit != 2000)
        {
            target.TargetTokenLimit = source.TargetTokenLimit;
        }

        if (source.WindowSize != 10)
        {
            target.WindowSize = source.WindowSize;
        }

        if (source.PreserveRecentCount != 5)
        {
            target.PreserveRecentCount = source.PreserveRecentCount;
        }

        if (source.HierarchicalRecentCount != 5)
        {
            target.HierarchicalRecentCount = source.HierarchicalRecentCount;
        }

        if (source.HierarchicalMiddleCount != 3)
        {
            target.HierarchicalMiddleCount = source.HierarchicalMiddleCount;
        }

        if (source.EnableLlmSummary)
        {
            target.EnableLlmSummary = source.EnableLlmSummary;
            target.LlmModel = source.LlmModel;
        }
    }
}
