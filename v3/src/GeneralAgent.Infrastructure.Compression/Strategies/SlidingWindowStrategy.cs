using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GeneralAgent.Infrastructure.Compression.Strategies;

/// <summary>
/// 滑动窗口策略：保留最近的 N 条消息
/// </summary>
public class SlidingWindowStrategy : ICompressionStrategy
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<SlidingWindowStrategy> _logger;

    public string Name => "sliding_window";
    public string Description => "保留最近的 N 条消息，丢弃旧消息";

    public SlidingWindowStrategy(
        ITokenCounter tokenCounter,
        ILogger<SlidingWindowStrategy> logger)
    {
        _tokenCounter = tokenCounter;
        _logger = logger;
    }

    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        options ??= new CompressionOptions();

        try
        {
            if (messages.Count == 0)
            {
                return new CompressionResult
                {
                    CompressedMessages = new List<Message>(),
                    Success = true,
                    Stats = new CompressionStats { StrategyUsed = Name }
                };
            }

            var originalTokens = _tokenCounter.CountMessagesTokens(messages);

            // 分离系统消息和用户消息
            var systemMessages = options.PreserveSystemMessages
                ? messages.Where(m => m.Role == MessageRole.System).ToList()
                : new List<Message>();

            var nonSystemMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

            // 应用滑动窗口
            var windowSize = options.WindowSize;
            var recentMessages = nonSystemMessages
                .Skip(Math.Max(0, nonSystemMessages.Count - windowSize))
                .ToList();

            // 合并系统消息和滑动窗口消息
            var compressedMessages = new List<Message>();
            compressedMessages.AddRange(systemMessages);
            compressedMessages.AddRange(recentMessages);

            var compressedTokens = _tokenCounter.CountMessagesTokens(compressedMessages);

            sw.Stop();

            _logger.LogInformation(
                "滑动窗口压缩完成: {Original} 条消息 ({OriginalTokens} tokens) → {Compressed} 条消息 ({CompressedTokens} tokens), 耗时 {Duration}ms",
                messages.Count, originalTokens, compressedMessages.Count, compressedTokens, sw.ElapsedMilliseconds);

            return new CompressionResult
            {
                CompressedMessages = compressedMessages,
                Success = true,
                Stats = new CompressionStats
                {
                    OriginalMessageCount = messages.Count,
                    CompressedMessageCount = compressedMessages.Count,
                    OriginalTokens = originalTokens,
                    CompressedTokens = compressedTokens,
                    DurationMs = sw.ElapsedMilliseconds,
                    StrategyUsed = Name
                },
                Metadata = new Dictionary<string, object>
                {
                    ["window_size"] = windowSize,
                    ["system_messages_preserved"] = systemMessages.Count,
                    ["recent_messages_kept"] = recentMessages.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "滑动窗口压缩失败");
            return new CompressionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Stats = new CompressionStats { StrategyUsed = Name, DurationMs = sw.ElapsedMilliseconds }
            };
        }
    }

    public int EstimateCompressedTokens(List<Message> messages, CompressionOptions? options = null)
    {
        options ??= new CompressionOptions();

        if (messages.Count == 0)
        {
            return 0;
        }

        var systemMessages = options.PreserveSystemMessages
            ? messages.Where(m => m.Role == MessageRole.System).ToList()
            : new List<Message>();

        var nonSystemMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

        var windowSize = options.WindowSize;
        var recentMessages = nonSystemMessages
            .Skip(Math.Max(0, nonSystemMessages.Count - windowSize))
            .ToList();

        var estimatedMessages = new List<Message>();
        estimatedMessages.AddRange(systemMessages);
        estimatedMessages.AddRange(recentMessages);

        return _tokenCounter.CountMessagesTokens(estimatedMessages);
    }

    public bool IsApplicable(List<Message> messages, CompressionOptions? options = null)
    {
        options ??= new CompressionOptions();

        // 滑动窗口始终适用，只要消息数量超过最小阈值
        return messages.Count >= options.MinMessagesForCompression;
    }
}
