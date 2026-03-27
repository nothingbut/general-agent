using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 压缩编排器：管理多个压缩策略，自动选择最佳策略
/// </summary>
public class CompressionOrchestrator : ICompressionOrchestrator
{
    private readonly Dictionary<string, ICompressionStrategy> _strategies;
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<CompressionOrchestrator> _logger;

    public CompressionOrchestrator(
        IEnumerable<ICompressionStrategy> strategies,
        ITokenCounter tokenCounter,
        ILogger<CompressionOrchestrator> logger)
    {
        _strategies = strategies.ToDictionary(s => s.Name, s => s);
        _tokenCounter = tokenCounter;
        _logger = logger;

        _logger.LogInformation(
            "CompressionOrchestrator 初始化完成，加载了 {Count} 个策略: {Strategies}",
            _strategies.Count,
            string.Join(", ", _strategies.Keys));
    }

    public async Task<CompressionResult> CompressAsync(
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CompressionOptions();

        // 如果消息数量少于阈值，不压缩
        if (messages.Count < options.MinMessagesForCompression)
        {
            _logger.LogDebug(
                "消息数量 ({Count}) 少于压缩阈值 ({Threshold})，跳过压缩",
                messages.Count,
                options.MinMessagesForCompression);

            var tokens = _tokenCounter.CountMessagesTokens(messages);
            return new CompressionResult
            {
                CompressedMessages = messages,
                Success = true,
                Stats = new CompressionStats
                {
                    OriginalMessageCount = messages.Count,
                    CompressedMessageCount = messages.Count,
                    OriginalTokens = tokens,
                    CompressedTokens = tokens,
                    DurationMs = 0,
                    StrategyUsed = "none"
                }
            };
        }

        // 自动推荐策略
        var strategyName = string.IsNullOrEmpty(options.Strategy)
            ? RecommendStrategy(messages, options)
            : options.Strategy;

        return await CompressWithStrategyAsync(strategyName, messages, options, cancellationToken);
    }

    public async Task<CompressionResult> CompressWithStrategyAsync(
        string strategyName,
        List<Message> messages,
        CompressionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CompressionOptions();

        if (!_strategies.TryGetValue(strategyName, out var strategy))
        {
            var availableStrategies = string.Join(", ", _strategies.Keys);
            var errorMessage = $"未找到压缩策略: {strategyName}。可用策略: {availableStrategies}";
            _logger.LogError(errorMessage);

            return new CompressionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Stats = new CompressionStats { StrategyUsed = strategyName }
            };
        }

        _logger.LogInformation(
            "使用策略 '{Strategy}' 压缩 {Count} 条消息",
            strategyName,
            messages.Count);

        try
        {
            var result = await strategy.CompressAsync(messages, options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "压缩成功: {Original} 条消息 ({OriginalTokens} tokens) → {Compressed} 条消息 ({CompressedTokens} tokens), 压缩率: {Ratio:P2}",
                    result.Stats.OriginalMessageCount,
                    result.Stats.OriginalTokens,
                    result.Stats.CompressedMessageCount,
                    result.Stats.CompressedTokens,
                    result.Stats.CompressionRatio);
            }
            else
            {
                _logger.LogError("压缩失败: {Error}", result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "压缩过程中发生异常");
            return new CompressionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Stats = new CompressionStats { StrategyUsed = strategyName }
            };
        }
    }

    public List<string> GetAvailableStrategies()
    {
        return _strategies.Keys.ToList();
    }

    public string RecommendStrategy(List<Message> messages, CompressionOptions? options = null)
    {
        options ??= new CompressionOptions();

        if (messages.Count == 0)
        {
            return "sliding_window";
        }

        var currentTokens = _tokenCounter.CountMessagesTokens(messages);

        // 策略选择逻辑：
        // 1. 如果消息数量较少（< 20），使用滑动窗口
        if (messages.Count < 20)
        {
            _logger.LogDebug("推荐策略: sliding_window (消息数量 < 20)");
            return "sliding_window";
        }

        // 2. 如果消息数量中等（20-50），使用层级压缩
        if (messages.Count < 50)
        {
            _logger.LogDebug("推荐策略: hierarchical (消息数量 20-50)");
            return "hierarchical";
        }

        // 3. 如果消息数量较多（>= 50），且启用了 LLM 摘要，使用语义压缩
        if (options.EnableLlmSummary && _strategies.ContainsKey("semantic"))
        {
            _logger.LogDebug("推荐策略: semantic (消息数量 >= 50，启用 LLM)");
            return "semantic";
        }

        // 4. 否则使用层级压缩（适用于大部分场景）
        _logger.LogDebug("推荐策略: hierarchical (默认)");
        return "hierarchical";
    }
}
