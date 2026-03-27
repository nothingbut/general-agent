using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GeneralAgent.Infrastructure.Compression.Strategies;

/// <summary>
/// 层级压缩策略：近期详细 + 中期关键点 + 旧消息摘要
/// </summary>
public class HierarchicalStrategy : ICompressionStrategy
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<HierarchicalStrategy> _logger;

    public string Name => "hierarchical";
    public string Description => "近期消息保留详细内容，中期提取关键点，旧消息生成摘要";

    public HierarchicalStrategy(
        ITokenCounter tokenCounter,
        ILogger<HierarchicalStrategy> logger)
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

            // 分离系统消息
            var systemMessages = options.PreserveSystemMessages
                ? messages.Where(m => m.Role == MessageRole.System).ToList()
                : new List<Message>();

            var nonSystemMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

            if (nonSystemMessages.Count == 0)
            {
                return new CompressionResult
                {
                    CompressedMessages = systemMessages,
                    Success = true,
                    Stats = new CompressionStats
                    {
                        OriginalMessageCount = messages.Count,
                        CompressedMessageCount = systemMessages.Count,
                        OriginalTokens = originalTokens,
                        CompressedTokens = _tokenCounter.CountMessagesTokens(systemMessages),
                        DurationMs = sw.ElapsedMilliseconds,
                        StrategyUsed = Name
                    }
                };
            }

            var compressedMessages = new List<Message>();
            compressedMessages.AddRange(systemMessages);

            var recentCount = options.HierarchicalRecentCount;
            var middleCount = options.HierarchicalMiddleCount;

            // 1. 近期消息：完整保留
            var recentMessages = nonSystemMessages
                .Skip(Math.Max(0, nonSystemMessages.Count - recentCount))
                .ToList();

            // 2. 中期消息：提取关键点（简化处理：保留较短的消息）
            var remainingCount = nonSystemMessages.Count - recentCount;
            var middleMessages = remainingCount > 0
                ? nonSystemMessages
                    .Skip(Math.Max(0, nonSystemMessages.Count - recentCount - middleCount))
                    .Take(middleCount)
                    .Select(m => ExtractKeyPoints(m))
                    .ToList()
                : new List<Message>();

            // 3. 旧消息：生成摘要
            var oldMessagesCount = Math.Max(0, nonSystemMessages.Count - recentCount - middleCount);
            if (oldMessagesCount > 0)
            {
                var oldMessages = nonSystemMessages.Take(oldMessagesCount).ToList();
                var summary = GenerateSummary(oldMessages);
                compressedMessages.Add(summary);
            }

            // 添加中期和近期消息
            compressedMessages.AddRange(middleMessages);
            compressedMessages.AddRange(recentMessages);

            var compressedTokens = _tokenCounter.CountMessagesTokens(compressedMessages);

            sw.Stop();

            _logger.LogInformation(
                "层级压缩完成: {Original} 条消息 ({OriginalTokens} tokens) → {Compressed} 条消息 ({CompressedTokens} tokens), 耗时 {Duration}ms",
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
                    ["recent_messages_count"] = recentMessages.Count,
                    ["middle_messages_count"] = middleMessages.Count,
                    ["old_messages_count"] = oldMessagesCount,
                    ["summary_generated"] = oldMessagesCount > 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "层级压缩失败");
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

        var recentCount = Math.Min(options.HierarchicalRecentCount, nonSystemMessages.Count);
        var middleCount = Math.Min(options.HierarchicalMiddleCount, Math.Max(0, nonSystemMessages.Count - recentCount));
        var oldCount = Math.Max(0, nonSystemMessages.Count - recentCount - middleCount);

        // 估算：系统消息 + 近期消息全部 + 中期消息 50% + 旧消息摘要（约 20%）
        var systemTokens = _tokenCounter.CountMessagesTokens(systemMessages);
        var recentMessages = nonSystemMessages.TakeLast(recentCount).ToList();
        var recentTokens = _tokenCounter.CountMessagesTokens(recentMessages);

        var middleMessages = nonSystemMessages
            .Skip(Math.Max(0, nonSystemMessages.Count - recentCount - middleCount))
            .Take(middleCount)
            .ToList();
        var middleTokens = _tokenCounter.CountMessagesTokens(middleMessages) / 2;

        var oldMessages = nonSystemMessages.Take(oldCount).ToList();
        var oldTokens = _tokenCounter.CountMessagesTokens(oldMessages) / 5;

        return systemTokens + recentTokens + middleTokens + oldTokens;
    }

    public bool IsApplicable(List<Message> messages, CompressionOptions? options = null)
    {
        options ??= new CompressionOptions();

        // 层级压缩适用于消息数量较多的场景（至少要有足够的消息分层）
        var minMessages = options.HierarchicalRecentCount + options.HierarchicalMiddleCount + 3;
        return messages.Count >= minMessages;
    }

    /// <summary>
    /// 提取消息的关键点（简化版：截断长消息）
    /// </summary>
    private Message ExtractKeyPoints(Message message)
    {
        if (string.IsNullOrEmpty(message.Content))
        {
            return message;
        }

        var content = message.Content;
        var tokens = _tokenCounter.CountTokens(content);

        // 如果消息较短（<100 tokens），直接返回
        if (tokens <= 100)
        {
            return message;
        }

        // 截断到 50 tokens 并添加省略号
        var truncated = _tokenCounter.TruncateToTokenLimit(content, 50);

        return message with
        {
            Content = $"[关键点] {truncated}"
        };
    }

    /// <summary>
    /// 生成旧消息的摘要（简化版：统计性摘要）
    /// </summary>
    private Message GenerateSummary(List<Message> oldMessages)
    {
        var userMessages = oldMessages.Count(m => m.Role == MessageRole.User);
        var assistantMessages = oldMessages.Count(m => m.Role == MessageRole.Assistant);
        var totalTokens = _tokenCounter.CountMessagesTokens(oldMessages);

        var summary = $"[历史摘要] 前 {oldMessages.Count} 条消息（用户: {userMessages}, 助手: {assistantMessages}, 约 {totalTokens} tokens）已被压缩。";

        return new Message
        {
            Id = Guid.NewGuid(),
            SessionId = oldMessages.First().SessionId,
            Role = MessageRole.System,
            Content = summary,
            CreatedAt = oldMessages.Last().CreatedAt
        };
    }
}
