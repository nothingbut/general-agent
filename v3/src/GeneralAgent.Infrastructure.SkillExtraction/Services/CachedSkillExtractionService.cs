using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 带缓存的技能提取服务装饰器
/// </summary>
public sealed class CachedSkillExtractionService : ISkillExtractionService
{
    private readonly ISkillExtractionService _innerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedSkillExtractionService> _logger;

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(1);

    public CachedSkillExtractionService(
        ISkillExtractionService innerService,
        IMemoryCache cache,
        ILogger<CachedSkillExtractionService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<SkillSuggestion>> ExtractFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"extraction_session_{sessionId}_{lookbackMessages}";

        if (_cache.TryGetValue<List<SkillSuggestion>>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("从缓存返回提取结果: {SessionId}", sessionId);
            return cachedResult!;
        }

        _logger.LogDebug("缓存未命中，执行提取: {SessionId}", sessionId);

        var result = await _innerService.ExtractFromSessionAsync(
            sessionId, lookbackMessages, cancellationToken);

        // 缓存结果（仅当有建议时）
        if (result.Count > 0)
        {
            _cache.Set(cacheKey, result, DefaultCacheDuration);
            _logger.LogDebug("已缓存提取结果: {SessionId}, 建议数: {Count}",
                sessionId, result.Count);
        }

        return result;
    }

    public async Task<List<SkillSuggestion>> ExtractFromMessagesAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        // 基于消息内容生成缓存键
        var cacheKey = GenerateMessagesCacheKey(messages);

        if (_cache.TryGetValue<List<SkillSuggestion>>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("从缓存返回提取结果（消息数: {Count}）", messages.Count);
            return cachedResult!;
        }

        _logger.LogDebug("缓存未命中，执行提取（消息数: {Count}）", messages.Count);

        var result = await _innerService.ExtractFromMessagesAsync(messages, cancellationToken);

        // 缓存结果（仅当有建议时）
        if (result.Count > 0)
        {
            _cache.Set(cacheKey, result, DefaultCacheDuration);
            _logger.LogDebug("已缓存提取结果，建议数: {Count}", result.Count);
        }

        return result;
    }

    /// <summary>
    /// 生成消息列表的缓存键（基于内容哈希）
    /// </summary>
    private string GenerateMessagesCacheKey(IReadOnlyList<Message> messages)
    {
        // 构建消息内容的简化表示
        var contentBuilder = new StringBuilder();
        foreach (var message in messages)
        {
            contentBuilder.Append(message.Role);
            contentBuilder.Append(':');
            contentBuilder.Append(message.Content.Length > 200
                ? message.Content[..200]
                : message.Content);
            contentBuilder.Append('|');
        }

        var content = contentBuilder.ToString();

        // 计算 SHA256 哈希
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var hash = Convert.ToBase64String(hashBytes);

        return $"extraction_messages_{messages.Count}_{hash}";
    }

    /// <summary>
    /// 清除特定会话的缓存
    /// </summary>
    public void ClearCacheForSession(string sessionId)
    {
        // 注意：IMemoryCache 不提供按前缀清除的功能
        // 这里只是记录日志，实际清除需要手动管理缓存键
        _logger.LogInformation("请求清除会话缓存: {SessionId}（需手动实现）", sessionId);
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearAllCache()
    {
        // IMemoryCache 不支持清除所有项，需要重新创建实例
        _logger.LogWarning("IMemoryCache 不支持清除所有缓存");
    }
}
