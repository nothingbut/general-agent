using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具执行器
/// 负责执行单个工具或批量执行多个工具
/// 提供性能监控和错误处理能力
/// </summary>
public sealed class ToolExecutor
{
    private readonly ToolRegistry _registry;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        ToolRegistry registry,
        ILogger<ToolExecutor> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行单个工具
    /// </summary>
    public async Task<Result<string>> ExecuteAsync(
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("执行工具: {ToolName}, 参数: {Arguments}",
                toolName, JsonSerializer.Serialize(arguments));

            var tool = _registry.GetTool(toolName);
            if (tool == null)
            {
                _logger.LogWarning("工具不存在: {ToolName}", toolName);
                return Result<string>.Failure($"工具不存在: {toolName}");
            }

            var startTime = Stopwatch.GetTimestamp();
            var result = await tool.ExecuteAsync(arguments, context, ct);
            var elapsed = Stopwatch.GetElapsedTime(startTime);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "工具执行成功: {ToolName}, 耗时: {Elapsed}ms, 输出长度: {Length}",
                    toolName, elapsed.TotalMilliseconds, result.Value?.Length ?? 0);
            }
            else
            {
                _logger.LogWarning(
                    "工具执行失败: {ToolName}, 错误: {Error}",
                    toolName, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行工具异常: {ToolName}", toolName);
            return Result<string>.Failure($"执行工具异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 流式执行工具
    /// </summary>
    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        string toolName,
        Dictionary<string, object> arguments,
        ToolExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tool = _registry.GetTool(toolName);
        if (tool == null)
        {
            _logger.LogWarning("工具不存在: {ToolName}", toolName);
            yield return $"❌ 工具不存在: {toolName}";
            yield break;
        }

        _logger.LogDebug("流式执行工具: {ToolName}", toolName);
        var startTime = Stopwatch.GetTimestamp();

        await foreach (var chunk in tool.ExecuteStreamAsync(arguments, context, ct))
        {
            yield return chunk;
        }

        var elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogInformation(
            "工具流式执行完成: {ToolName}, 耗时: {Elapsed}ms",
            toolName, elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// 并行执行多个工具
    /// </summary>
    public async Task<List<ToolCallResult>> ExecuteManyAsync(
        IEnumerable<ToolCall> toolCalls,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        var tasks = toolCalls.Select(async toolCall =>
        {
            var arguments = ParseArguments(toolCall.Arguments);
            var result = await ExecuteAsync(toolCall.FunctionName, arguments, context, ct);

            return new ToolCallResult
            {
                ToolCallId = toolCall.Id,
                ToolName = toolCall.FunctionName,
                Content = result.IsSuccess ? result.Value! : $"错误: {result.Error}",
                IsError = !result.IsSuccess
            };
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>
    /// 解析工具参数JSON
    /// </summary>
    private Dictionary<string, object> ParseArguments(string argumentsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson)
                ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析工具参数失败: {Arguments}", argumentsJson);
            return new Dictionary<string, object>();
        }
    }
}
