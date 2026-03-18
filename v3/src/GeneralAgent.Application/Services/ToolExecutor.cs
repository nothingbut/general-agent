using System.Diagnostics;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具执行器
/// 负责执行工具调用、超时控制、错误处理和结果封装
/// 线程安全，支持并发执行
/// </summary>
public sealed class ToolExecutor
{
    private readonly ToolRegistry _registry;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

    public ToolExecutor(
        ToolRegistry registry,
        ILogger<ToolExecutor> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// 执行单个工具调用
    /// </summary>
    /// <param name="call">工具调用请求</param>
    /// <param name="context">执行上下文</param>
    /// <param name="timeout">超时时间（可选，默认 30 秒）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>工具调用结果</returns>
    public async Task<ToolCallResult> ExecuteAsync(
        ToolCall call,
        ToolExecutionContext context,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        try
        {
            _logger.LogDebug(
                "开始执行工具调用: {ToolName} (ID: {CallId})",
                call.ToolName,
                call.Id);

            // 查找工具
            var tool = _registry.GetTool(call.ToolName);
            if (tool == null)
            {
                _logger.LogWarning(
                    "工具未找到: {ToolName} (ID: {CallId})",
                    call.ToolName,
                    call.Id);

                stopwatch.Stop();
                return ToolCallResult.Failure(
                    call,
                    $"工具未找到: {call.ToolName}",
                    stopwatch.ElapsedMilliseconds);
            }

            // 执行工具（带超时控制）
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var result = await tool.ExecuteAsync(
                call.Arguments,
                context,
                linkedCts.Token);

            stopwatch.Stop();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "工具执行成功: {ToolName} (ID: {CallId}) - 耗时 {ElapsedMs}ms",
                    call.ToolName,
                    call.Id,
                    stopwatch.ElapsedMilliseconds);

                return ToolCallResult.Success(
                    call,
                    result.Value!,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "工具执行失败: {ToolName} (ID: {CallId}) - {Error}",
                    call.ToolName,
                    call.Id,
                    result.Error);

                return ToolCallResult.Failure(
                    call,
                    result.Error!,
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户取消
            stopwatch.Stop();
            _logger.LogInformation(
                "工具执行已取消: {ToolName} (ID: {CallId})",
                call.ToolName,
                call.Id);

            return ToolCallResult.Failure(
                call,
                "工具执行已取消",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // 超时
            stopwatch.Stop();
            _logger.LogWarning(
                "工具执行超时: {ToolName} (ID: {CallId}) - 超时时间 {TimeoutSeconds}s",
                call.ToolName,
                call.Id,
                effectiveTimeout.TotalSeconds);

            return ToolCallResult.Failure(
                call,
                $"工具执行超时（{effectiveTimeout.TotalSeconds}秒）",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // 未预期的异常
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "工具执行失败: {ToolName} (ID: {CallId}) - 异常: {Message}",
                call.ToolName,
                call.Id,
                ex.Message);

            return ToolCallResult.Failure(
                call,
                $"工具执行失败: {ex.Message}",
                stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 批量执行工具调用（并行）
    /// </summary>
    /// <param name="calls">工具调用请求列表</param>
    /// <param name="context">执行上下文</param>
    /// <param name="timeout">超时时间（可选，默认 30 秒）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>工具调用结果列表（顺序与输入一致）</returns>
    public async Task<IReadOnlyList<ToolCallResult>> ExecuteManyAsync(
        IEnumerable<ToolCall> calls,
        ToolExecutionContext context,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(context);

        var callsList = calls.ToList();

        _logger.LogInformation(
            "开始批量执行工具调用: {Count} 个工具",
            callsList.Count);

        // 并行执行所有工具调用
        var tasks = callsList.Select(call =>
            ExecuteAsync(call, context, timeout, ct));

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);

        _logger.LogInformation(
            "批量执行完成: 成功 {SuccessCount}，失败 {FailureCount}",
            successCount,
            failureCount);

        return results;
    }

    /// <summary>
    /// 流式执行工具调用
    /// 用于需要实时返回结果的工具（如 LLM 调用）
    /// </summary>
    /// <param name="call">工具调用请求</param>
    /// <param name="context">执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>异步枚举，逐个返回结果块</returns>
    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        ToolCall call,
        ToolExecutionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug(
            "开始流式执行工具: {ToolName} (ID: {CallId})",
            call.ToolName,
            call.Id);

        // 查找工具
        var tool = _registry.GetTool(call.ToolName);
        if (tool == null)
        {
            _logger.LogWarning(
                "工具未找到: {ToolName} (ID: {CallId})",
                call.ToolName,
                call.Id);

            yield return $"错误: 工具未找到 - {call.ToolName}";
            yield break;
        }

        // 流式执行工具
        await foreach (var chunk in tool.ExecuteStreamAsync(call.Arguments, context, ct))
        {
            yield return chunk;
        }

        _logger.LogDebug(
            "流式执行完成: {ToolName} (ID: {CallId})",
            call.ToolName,
            call.Id);
    }
}
