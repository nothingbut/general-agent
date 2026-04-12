using System.Text.Json;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务执行器实现
/// </summary>
public class TaskExecutor : ITaskExecutor
{
    private readonly ITaskExecutionRepository _executionRepository;
    private readonly ILogger<TaskExecutor> _logger;

    // TODO: 在后续实现中注入这些依赖
    // private readonly ISkillRegistry _skillRegistry;
    // private readonly IMemoryService _memoryService;

    public TaskExecutor(
        ITaskExecutionRepository executionRepository,
        ILogger<TaskExecutor> logger)
    {
        _executionRepository = executionRepository;
        _logger = logger;
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    public async Task<TaskExecution> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running,
            RetryCount = 0
        };

        // 保存执行记录
        await _executionRepository.CreateAsync(execution, cancellationToken);

        var retryCount = 0;
        var maxRetries = task.MaxRetries;

        while (retryCount <= maxRetries)
        {
            execution.RetryCount = retryCount;

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;

            try
            {
                // 创建超时取消令牌
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(task.TimeoutSeconds));
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // 根据任务类型执行
                string output = task.TaskType switch
                {
                    TaskType.SkillInvocation => await ExecuteSkillAsync(task, linkedCts.Token),
                    TaskType.MemoryReminder => await ExecuteReminderAsync(task, linkedCts.Token),
                    TaskType.CustomCommand => await ExecuteCommandAsync(task, linkedCts.Token),
                    _ => throw new NotSupportedException($"不支持的任务类型: {task.TaskType}")
                };

                // 执行成功
                execution.Output = output;
                execution.Status = ExecutionStatus.Completed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;

                await _executionRepository.UpdateAsync(execution, cancellationToken);

                _logger.LogInformation("任务执行成功: {TaskId}, 耗时: {Duration}ms", task.Id, execution.DurationMs);
                return execution;
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                // 超时
                execution.Status = ExecutionStatus.Timeout;
                execution.Error = $"任务执行超时（{task.TimeoutSeconds} 秒）";
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;

                await _executionRepository.UpdateAsync(execution, cancellationToken);

                _logger.LogWarning("任务执行超时: {TaskId}, 超时时间: {Timeout}秒", task.Id, task.TimeoutSeconds);
                return execution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "任务执行失败: {TaskId}, 重试次数: {RetryCount}/{MaxRetries}",
                    task.Id, retryCount, maxRetries);

                if (retryCount < maxRetries)
                {
                    // 还有重试次数，等待后重试（指数退避）
                    retryCount++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // 2^n 秒
                    _logger.LogInformation("等待 {Delay} 秒后重试...", delay.TotalSeconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 取消了重试
                        execution.Status = ExecutionStatus.Cancelled;
                        execution.Error = "任务执行被取消";
                        execution.CompletedAt = DateTime.UtcNow;
                        execution.DurationMs = (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;

                        await _executionRepository.UpdateAsync(execution, cancellationToken);
                        return execution;
                    }

                    continue; // 重试
                }

                // 达到最大重试次数，标记为失败
                execution.Status = ExecutionStatus.Failed;
                execution.Error = $"任务执行失败（重试 {maxRetries} 次后仍然失败）: {ex.Message}";
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;

                await _executionRepository.UpdateAsync(execution, cancellationToken);
                return execution;
            }
            finally
            {
                // 确保每次迭代都释放资源
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }
        }

        // 不应该到达这里
        return execution;
    }

    /// <summary>
    /// 执行技能调用任务
    /// </summary>
    private async Task<string> ExecuteSkillAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("执行技能调用任务: {TaskId}", task.Id);

        try
        {
            // 解析 TaskPayload
            var payload = JsonSerializer.Deserialize<SkillInvocationPayload>(task.TaskPayload);
            if (payload == null || string.IsNullOrEmpty(payload.Skill))
            {
                throw new InvalidOperationException("无效的技能调用配置");
            }

            // TODO: 调用 ISkillRegistry.ExecuteAsync
            // 目前返回模拟结果
            _logger.LogWarning("技能调用功能尚未集成，返回模拟结果");

            return $"[模拟] 技能 '{payload.Skill}' 执行成功";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"解析技能调用配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行记忆提醒任务
    /// </summary>
    private async Task<string> ExecuteReminderAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("执行记忆提醒任务: {TaskId}", task.Id);

        try
        {
            // 解析 TaskPayload
            var payload = JsonSerializer.Deserialize<MemoryReminderPayload>(task.TaskPayload);
            if (payload == null || string.IsNullOrEmpty(payload.Message))
            {
                throw new InvalidOperationException("无效的记忆提醒配置");
            }

            // TODO: 创建提醒或发送通知
            // 目前返回模拟结果
            _logger.LogWarning("记忆提醒功能尚未集成，返回模拟结果");

            return $"[模拟] 提醒 '{payload.Message}' 已发送";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"解析记忆提醒配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行自定义命令任务
    /// </summary>
    private async Task<string> ExecuteCommandAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("执行自定义命令任务: {TaskId}", task.Id);

        try
        {
            // 解析 TaskPayload
            var payload = JsonSerializer.Deserialize<CustomCommandPayload>(task.TaskPayload);
            if (payload == null || string.IsNullOrEmpty(payload.Command))
            {
                throw new InvalidOperationException("无效的自定义命令配置");
            }

            // TODO: 执行命令（需要白名单验证）
            // 目前返回模拟结果
            _logger.LogWarning("自定义命令功能尚未集成，返回模拟结果");

            return $"[模拟] 命令 '{payload.Command}' 执行成功";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"解析自定义命令配置失败: {ex.Message}", ex);
        }
    }

    // ==================== Payload 类型定义 ====================

    private class SkillInvocationPayload
    {
        public string Skill { get; set; } = string.Empty;
        public Dictionary<string, object>? Args { get; set; }
    }

    private class MemoryReminderPayload
    {
        public string Message { get; set; } = string.Empty;
    }

    private class CustomCommandPayload
    {
        public string Command { get; set; } = string.Empty;
    }
}
