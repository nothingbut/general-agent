using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 缓存装饰器 - 避免重复压缩相同的消息序列
/// </summary>
public sealed class CachedCompressionOrchestrator : ICompressionOrchestrator
{
    private readonly ICompressionOrchestrator _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedCompressionOrchestrator> _logger;
    private readonly TimeSpan _cacheDuration;

    public CachedCompressionOrchestrator(
        ICompressionOrchestrator inner,
        IMemoryCache cache,
        ILogger<CachedCompressionOrchestrator> logger,
        TimeSpan? cacheDuration = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDuration = cacheDuration ?? TimeSpan.FromHours(1);
    }

    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 生成缓存键（基于消息内容哈希）
        var cacheKey = GenerateCacheKey(messages, options);

        // 2. 尝试从缓存获取
        if (_cache.TryGetValue<CompressionResult>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogInformation("✅ 压缩缓存命中: {Key}", cacheKey);
            return cached;
        }

        _logger.LogDebug("🔍 压缩缓存未命中: {Key}", cacheKey);

        // 3. 调用内部服务执行压缩
        var result = await _inner.CompressAsync(messages, options, cancellationToken);

        // 4. 缓存结果（仅在成功时）
        if (result.Success)
        {
            _cache.Set(cacheKey, result, _cacheDuration);
            _logger.LogDebug("💾 压缩结果已缓存: {Key}, 过期时间: {Duration}", cacheKey, _cacheDuration);
        }

        return result;
    }

    public async Task<CompressionResult> CompressWithStrategyAsync(
        string strategyName,
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 确保选项中包含策略名称
        options ??= new CompressionOptions();
        options.Strategy = strategyName;

        // 1. 生成缓存键
        var cacheKey = GenerateCacheKey(messages, options);

        // 2. 尝试从缓存获取
        if (_cache.TryGetValue<CompressionResult>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogInformation("✅ 压缩缓存命中 (指定策略: {Strategy}): {Key}", strategyName, cacheKey);
            return cached;
        }

        _logger.LogDebug("🔍 压缩缓存未命中 (指定策略: {Strategy}): {Key}", strategyName, cacheKey);

        // 3. 调用内部服务
        var result = await _inner.CompressWithStrategyAsync(strategyName, messages, options, cancellationToken);

        // 4. 缓存结果
        if (result.Success)
        {
            _cache.Set(cacheKey, result, _cacheDuration);
            _logger.LogDebug("💾 压缩结果已缓存 (策略: {Strategy}): {Key}", strategyName, cacheKey);
        }

        return result;
    }

    public List<string> GetAvailableStrategies()
    {
        // 直接转发，无需缓存
        return _inner.GetAvailableStrategies();
    }

    public string RecommendStrategy(List<Message> messages, CompressionOptions? options = null)
    {
        // 策略推荐逻辑很轻量，无需缓存
        return _inner.RecommendStrategy(messages, options);
    }

    /// <summary>
    /// 生成缓存键（基于消息内容和选项的哈希值）
    /// </summary>
    private string GenerateCacheKey(List<Message> messages, CompressionOptions? options)
    {
        var content = new StringBuilder();

        // 1. 消息内容摘要（角色 + 内容长度）
        foreach (var msg in messages)
        {
            content.Append($"{msg.Role}:{msg.Content?.Length ?? 0}|");
        }

        // 2. 压缩选项
        if (options != null)
        {
            content.Append($"strategy:{options.Strategy}|");
            content.Append($"target:{options.TargetTokenLimit}|");
            content.Append($"window:{options.WindowSize}|");
            content.Append($"preserve_sys:{options.PreserveSystemMessages}|");
            content.Append($"preserve_recent:{options.PreserveRecentCount}|");
            content.Append($"llm_summary:{options.EnableLlmSummary}|");
            content.Append($"llm_model:{options.LlmModel}");
        }

        // 3. 计算 SHA256 哈希
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content.ToString()));
        var hashString = Convert.ToBase64String(hash);

        return $"compression_{hashString}";
    }
}
