using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 自动 Tool Calling 监听器
/// 在达到最大轮数时自动继续，无需用户交互
/// </summary>
public sealed class AutomaticToolCallingListener : IToolCallingListener
{
    private readonly ToolCallingConfig _config;
    private readonly ILogger<AutomaticToolCallingListener> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">Tool Calling 配置</param>
    /// <param name="logger">日志记录器</param>
    /// <exception cref="ArgumentNullException">参数为 null</exception>
    public AutomaticToolCallingListener(
        IOptions<ToolCallingConfig> config,
        ILogger<AutomaticToolCallingListener> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Tool Calling 达到 {Rounds} 轮，自动继续 {ExtendBy} 轮",
            currentRounds,
            _config.AutoExtendBy);

        var decision = new ExtendDecision
        {
            Stop = false,
            ExtendBy = _config.AutoExtendBy
        };

        return Task.FromResult(decision);
    }
}
