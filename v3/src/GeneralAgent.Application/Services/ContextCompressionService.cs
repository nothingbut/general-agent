using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 上下文压缩应用服务（协调压缩和会话消息）
/// </summary>
public class ContextCompressionService
{
    private readonly CompressionService _compressionService;
    private readonly IMessageRepository _messageRepository;
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<ContextCompressionService> _logger;

    public ContextCompressionService(
        CompressionService compressionService,
        IMessageRepository messageRepository,
        ITokenCounter tokenCounter,
        ILogger<ContextCompressionService> logger)
    {
        _compressionService = compressionService;
        _messageRepository = messageRepository;
        _tokenCounter = tokenCounter;
        _logger = logger;
    }

    /// <summary>
    /// 获取会话的上下文状态
    /// </summary>
    public async Task<ContextStatus> GetContextStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _messageRepository.GetBySessionAsync(sessionId, cancellationToken);
        var currentTokens = _tokenCounter.CountMessagesTokens(messages);

        var config = await _compressionService.GetOrCreateConfigAsync(sessionId, cancellationToken);
        var shouldCompress = await _compressionService.ShouldAutoCompressAsync(sessionId, currentTokens, cancellationToken);

        var recentHistory = await _compressionService.GetSessionHistoryAsync(sessionId, limit: 1, cancellationToken);
        var lastCompression = recentHistory.FirstOrDefault();

        return new ContextStatus
        {
            SessionId = sessionId,
            MessageCount = messages.Count,
            CurrentTokens = currentTokens,
            AutoCompressionEnabled = config.AutoCompressionEnabled,
            CompressionThreshold = config.AutoCompressionThreshold,
            ShouldCompress = shouldCompress,
            DefaultStrategy = config.DefaultStrategy,
            LastCompressionAt = lastCompression?.CompressedAt
        };
    }

    /// <summary>
    /// 压缩会话消息
    /// </summary>
    public async Task<CompressionResult> CompressSessionMessagesAsync(
        Guid sessionId,
        string? strategyName = null,
        CancellationToken cancellationToken = default)
    {
        // 获取所有消息
        var messages = await _messageRepository.GetBySessionAsync(sessionId, cancellationToken);

        if (messages.Count == 0)
        {
            return new CompressionResult
            {
                Success = false,
                ErrorMessage = "会话没有消息，无需压缩"
            };
        }

        // 构建压缩选项
        var options = new CompressionOptions();
        if (!string.IsNullOrEmpty(strategyName))
        {
            options.Strategy = strategyName;
        }

        // 执行压缩
        var result = await _compressionService.CompressSessionAsync(sessionId, messages, options, cancellationToken);

        if (result.Success && result.CompressedMessages.Count < messages.Count)
        {
            _logger.LogInformation(
                "会话 {SessionId} 压缩完成: {Original} → {Compressed} 条消息",
                sessionId, messages.Count, result.CompressedMessages.Count);
        }

        return result;
    }

    /// <summary>
    /// 获取压缩历史记录
    /// </summary>
    public async Task<List<CompressionHistory>> GetCompressionHistoryAsync(
        Guid sessionId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _compressionService.GetSessionHistoryAsync(sessionId, limit, cancellationToken);
    }

    /// <summary>
    /// 获取压缩统计信息
    /// </summary>
    public async Task<CompressionStatsSummary> GetCompressionStatsAsync(
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        return await _compressionService.GetStatsAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// 更新压缩配置
    /// </summary>
    public async Task<CompressionConfig> UpdateCompressionConfigAsync(
        Guid sessionId,
        bool? autoCompressionEnabled = null,
        int? autoCompressionThreshold = null,
        string? defaultStrategy = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _compressionService.GetOrCreateConfigAsync(sessionId, cancellationToken);

        if (autoCompressionEnabled.HasValue)
        {
            config.AutoCompressionEnabled = autoCompressionEnabled.Value;
        }

        if (autoCompressionThreshold.HasValue)
        {
            config.AutoCompressionThreshold = autoCompressionThreshold.Value;
        }

        if (!string.IsNullOrEmpty(defaultStrategy))
        {
            config.DefaultStrategy = defaultStrategy;
        }

        config.UpdatedAt = DateTime.UtcNow;

        return await _compressionService.UpdateConfigAsync(config, cancellationToken);
    }

    /// <summary>
    /// 检查是否需要自动压缩并执行
    /// </summary>
    public async Task<CompressionResult?> AutoCompressIfNeededAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var status = await GetContextStatusAsync(sessionId, cancellationToken);

        if (!status.ShouldCompress)
        {
            return null;
        }

        _logger.LogInformation(
            "会话 {SessionId} 达到自动压缩阈值 ({CurrentTokens}/{Threshold} tokens)，开始压缩",
            sessionId,
            status.CurrentTokens,
            status.CompressionThreshold);

        return await CompressSessionMessagesAsync(sessionId, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// 上下文状态
/// </summary>
public class ContextStatus
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 当前消息数量
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// 当前 Token 数量
    /// </summary>
    public int CurrentTokens { get; set; }

    /// <summary>
    /// 是否启用自动压缩
    /// </summary>
    public bool AutoCompressionEnabled { get; set; }

    /// <summary>
    /// 压缩阈值
    /// </summary>
    public int CompressionThreshold { get; set; }

    /// <summary>
    /// 是否应该压缩
    /// </summary>
    public bool ShouldCompress { get; set; }

    /// <summary>
    /// 默认策略
    /// </summary>
    public string DefaultStrategy { get; set; } = string.Empty;

    /// <summary>
    /// 最后压缩时间
    /// </summary>
    public DateTime? LastCompressionAt { get; set; }

    /// <summary>
    /// Token 使用率（0.0 - 1.0）
    /// </summary>
    public double TokenUsageRatio => CompressionThreshold > 0
        ? (double)CurrentTokens / CompressionThreshold
        : 0;
}
