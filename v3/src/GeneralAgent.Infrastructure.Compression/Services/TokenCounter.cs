using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using SharpToken;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// Token 计数器实现（使用 SharpToken 库）
/// </summary>
public class TokenCounter : ITokenCounter
{
    private readonly ILogger<TokenCounter> _logger;
    private readonly GptEncoding _encoding;

    public TokenCounter(ILogger<TokenCounter> logger)
    {
        _logger = logger;
        try
        {
            // 使用 cl100k_base 编码（GPT-4/GPT-3.5-Turbo/Claude）
            _encoding = GptEncoding.GetEncoding("cl100k_base");
            _logger.LogInformation("TokenCounter 初始化成功，使用编码: cl100k_base");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenCounter 初始化失败");
            throw;
        }
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        try
        {
            var tokens = _encoding.Encode(text);
            return tokens.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算 Token 失败，文本长度: {Length}", text.Length);
            // 降级策略：按字符数估算（英文约 4 字符/token，中文约 1.5 字符/token）
            return EstimateTokensByCharCount(text);
        }
    }

    /// <inheritdoc />
    public int CountMessageTokens(Message message)
    {
        if (message == null)
        {
            return 0;
        }

        // 消息格式：role + content + 3 个特殊 token（<|start|>, <|end|>, <|role|>）
        var roleTokens = CountTokens(message.Role.ToString().ToLowerInvariant());
        var contentTokens = CountTokens(message.Content ?? string.Empty);
        const int formatTokens = 3; // 消息格式开销

        return roleTokens + contentTokens + formatTokens;
    }

    /// <inheritdoc />
    public int CountMessagesTokens(List<Message> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return 0;
        }

        var totalTokens = messages.Sum(m => CountMessageTokens(m));
        const int conversationOverhead = 3; // 对话开始/结束 token
        return totalTokens + conversationOverhead;
    }

    /// <inheritdoc />
    public string TruncateToTokenLimit(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return string.Empty;
        }

        try
        {
            var tokens = _encoding.Encode(text);
            if (tokens.Count <= maxTokens)
            {
                return text;
            }

            // 截断 tokens 并解码
            var truncatedTokens = tokens.Take(maxTokens).ToList();
            var truncated = _encoding.Decode(truncatedTokens);

            // 添加省略号提示
            return truncated + "...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "截断文本失败，目标 Token 数: {MaxTokens}", maxTokens);
            // 降级策略：按字符数截断
            return TruncateByCharCount(text, maxTokens);
        }
    }

    /// <summary>
    /// 降级策略：按字符数估算 Token（英文 ~4 字符/token，中文 ~1.5 字符/token）
    /// </summary>
    private int EstimateTokensByCharCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        // 简单启发式：统计中英文字符
        var chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherChars = text.Length - chineseChars;

        return (int)Math.Ceiling(chineseChars / 1.5) + (otherChars / 4);
    }

    /// <summary>
    /// 降级策略：按字符数截断
    /// </summary>
    private string TruncateByCharCount(string text, int maxTokens)
    {
        // 保守估计：每 token 对应 2 个字符
        var maxChars = maxTokens * 2;
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text.Substring(0, maxChars) + "...";
    }
}
