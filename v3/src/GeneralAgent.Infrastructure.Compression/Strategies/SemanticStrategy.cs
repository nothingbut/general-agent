using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.Compression.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace GeneralAgent.Infrastructure.Compression.Strategies;

/// <summary>
/// 语义压缩策略：使用 LLM 生成会话摘要（可选）
/// </summary>
public class SemanticStrategy : ICompressionStrategy
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ILLMClient? _llmClient;
    private readonly ILogger<SemanticStrategy> _logger;

    public string Name => "semantic";
    public string Description => "使用语义理解生成会话摘要，保留关键信息";

    public SemanticStrategy(
        ITokenCounter tokenCounter,
        ILogger<SemanticStrategy> logger,
        ILLMClient? llmClient = null)
    {
        _tokenCounter = tokenCounter;
        _llmClient = llmClient;
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

            // 保留最近的消息
            var recentCount = options.PreserveRecentCount;
            var recentMessages = nonSystemMessages
                .Skip(Math.Max(0, nonSystemMessages.Count - recentCount))
                .ToList();

            // 需要压缩的旧消息
            var oldMessages = nonSystemMessages
                .Take(Math.Max(0, nonSystemMessages.Count - recentCount))
                .ToList();

            var compressedMessages = new List<Message>();
            compressedMessages.AddRange(systemMessages);

            // 生成旧消息的摘要
            if (oldMessages.Count > 0)
            {
                Message summary;
                if (options.EnableLlmSummary && _llmClient != null)
                {
                    // 使用 LLM 生成语义摘要
                    summary = await GenerateLlmSummaryAsync(oldMessages, options, cancellationToken);
                }
                else
                {
                    // 降级：使用规则生成摘要
                    summary = GenerateRuleBasedSummary(oldMessages);
                }

                compressedMessages.Add(summary);
            }

            // 添加近期消息
            compressedMessages.AddRange(recentMessages);

            var compressedTokens = _tokenCounter.CountMessagesTokens(compressedMessages);

            sw.Stop();

            _logger.LogInformation(
                "语义压缩完成: {Original} 条消息 ({OriginalTokens} tokens) → {Compressed} 条消息 ({CompressedTokens} tokens), 耗时 {Duration}ms",
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
                    ["llm_summary_used"] = options.EnableLlmSummary && _llmClient != null,
                    ["old_messages_summarized"] = oldMessages.Count,
                    ["recent_messages_kept"] = recentMessages.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语义压缩失败");
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

        var recentCount = Math.Min(options.PreserveRecentCount, nonSystemMessages.Count);
        var oldCount = Math.Max(0, nonSystemMessages.Count - recentCount);

        // 估算：系统消息 + 近期消息全部 + 旧消息摘要（约 200 tokens）
        var systemTokens = _tokenCounter.CountMessagesTokens(systemMessages);
        var recentMessages = nonSystemMessages.TakeLast(recentCount).ToList();
        var recentTokens = _tokenCounter.CountMessagesTokens(recentMessages);
        var summaryTokens = oldCount > 0 ? 200 : 0; // 摘要固定约 200 tokens

        return systemTokens + recentTokens + summaryTokens;
    }

    public bool IsApplicable(List<Message> messages, CompressionOptions? options = null)
    {
        options ??= new CompressionOptions();

        // 语义压缩适用于较长的对话（需要足够的上下文来生成有意义的摘要）
        return messages.Count >= options.MinMessagesForCompression + 5;
    }

    /// <summary>
    /// 使用 LLM 生成语义摘要
    /// </summary>
    private async Task<Message> GenerateLlmSummaryAsync(
        List<Message> messages,
        CompressionOptions options,
        CancellationToken cancellationToken)
    {
        if (_llmClient == null)
        {
            throw new InvalidOperationException("LLM 客户端未配置");
        }

        // 构建摘要提示词
        var conversationText = BuildConversationText(messages);
        var promptMessages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "system",
                Content = "你是一个专业的会话摘要助手。请将以下对话内容总结成一段简洁的摘要（不超过 200 tokens），保留关键信息和讨论要点。"
            },
            new ChatMessage
            {
                Role = "user",
                Content = $"请总结以下对话：\n\n{conversationText}"
            }
        };

        var request = new CompletionRequest
        {
            Model = options.LlmModel,
            Messages = promptMessages,
            MaxTokens = 250,
            Temperature = 0.3 // 较低的温度以获得稳定的摘要
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);

        return new Message
        {
            Id = Guid.NewGuid(),
            SessionId = messages.First().SessionId,
            Role = MessageRole.System,
            Content = $"[语义摘要] {response.Content}",
            CreatedAt = messages.Last().CreatedAt
        };
    }

    /// <summary>
    /// 使用规则生成摘要（降级策略）
    /// </summary>
    private Message GenerateRuleBasedSummary(List<Message> messages)
    {
        var userMessages = messages.Where(m => m.Role == MessageRole.User).ToList();
        var assistantMessages = messages.Where(m => m.Role == MessageRole.Assistant).ToList();

        // 提取每条消息的前 50 个字符作为概要
        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"[会话摘要] 共 {messages.Count} 条消息：");

        foreach (var msg in messages.Take(5)) // 仅包含前 5 条消息的摘要
        {
            var preview = msg.Content?.Length > 50
                ? msg.Content.Substring(0, 50) + "..."
                : msg.Content;
            summaryBuilder.AppendLine($"- {msg.Role}: {preview}");
        }

        if (messages.Count > 5)
        {
            summaryBuilder.AppendLine($"...还有 {messages.Count - 5} 条消息");
        }

        return new Message
        {
            Id = Guid.NewGuid(),
            SessionId = messages.First().SessionId,
            Role = MessageRole.System,
            Content = summaryBuilder.ToString(),
            CreatedAt = messages.Last().CreatedAt
        };
    }

    /// <summary>
    /// 构建对话文本（用于 LLM 摘要）
    /// </summary>
    private string BuildConversationText(List<Message> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            sb.AppendLine($"{msg.Role}: {msg.Content}");
        }
        return sb.ToString();
    }
}
