using System.Collections.Concurrent;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using GeneralAgent.Infrastructure.ScheduledTasks.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务调度器实现（基于 System.Threading.Timer）
/// </summary>
public class TaskScheduler : ITaskScheduler, IDisposable
{
    private readonly IScheduledTaskRepository _taskRepository;
    private readonly ITaskExecutor _taskExecutor;
    private readonly ICronParser _cronParser;
    private readonly INaturalLanguageTimeParser _naturalLanguageParser;
    private readonly ILogger<TaskScheduler> _logger;
    private readonly ScheduledTasksOptions _options;

    // 定时器（用于定期扫描任务）
    private Timer? _scanTimer;

    // 任务队列（按下次执行时间排序）
    private readonly PriorityQueue<Guid, DateTime> _taskQueue = new();

    // 运行中的任务（taskId -> CancellationTokenSource）
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningTasks = new();

    // 线程同步（保护任务队列）
    private readonly SemaphoreSlim _queueLock = new(1, 1);

    // 调度器状态
    private bool _isRunning;
    private bool _disposed;

    public TaskScheduler(
        IScheduledTaskRepository taskRepository,
        ITaskExecutor taskExecutor,
        ICronParser cronParser,
        INaturalLanguageTimeParser naturalLanguageParser,
        IOptions<ScheduledTasksOptions> options,
        ILogger<TaskScheduler> logger)
    {
        _taskRepository = taskRepository;
        _taskExecutor = taskExecutor;
        _cronParser = cronParser;
        _naturalLanguageParser = naturalLanguageParser;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 调度器是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 启动调度器
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("调度器已经在运行中");
            return;
        }

        _logger.LogInformation("启动任务调度器...");

        // 加载待执行的任务
        await LoadPendingTasksAsync(cancellationToken);

        // 启动定时扫描器（每隔配置的时间扫描一次）
        var scanInterval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);
        _scanTimer = new Timer(
            callback: _ => _ = ScanAndExecuteTasksAsync(),
            state: null,
            dueTime: scanInterval,
            period: scanInterval
        );

        _isRunning = true;
        _logger.LogInformation("任务调度器已启动，扫描间隔: {Interval}秒", _options.ScanIntervalSeconds);
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("调度器未运行");
            return;
        }

        _logger.LogInformation("停止任务调度器...");

        // 停止定时器
        if (_scanTimer != null)
        {
            await _scanTimer.DisposeAsync();
            _scanTimer = null;
        }

        // 取消所有运行中的任务
        foreach (var kvp in _runningTasks)
        {
            try
            {
                kvp.Value.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务失败: {TaskId}", kvp.Key);
            }
        }

        // 等待所有任务完成（最多等待 5 秒）
        var waitTask = Task.Run(async () =>
        {
            while (_runningTasks.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
        }, cancellationToken);

        try
        {
            await waitTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("等待任务完成超时，强制停止");
        }

        _runningTasks.Clear();
        _isRunning = false;

        _logger.LogInformation("任务调度器已停止");
    }

    /// <summary>
    /// 添加任务到调度队列
    /// </summary>
    public async Task ScheduleTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        if (task.Status != TaskStatus.Pending)
        {
            _logger.LogWarning("只能调度状态为 Pending 的任务: {TaskId}, Status: {Status}", task.Id, task.Status);
            return;
        }

        // 计算下次执行时间
        var nextExecution = CalculateNextExecution(task);
        if (nextExecution == null)
        {
            _logger.LogWarning("无法计算任务的下次执行时间: {TaskId}", task.Id);
            return;
        }

        // 更新任务的下次执行时间
        task.NextExecutionAt = nextExecution;
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 加入任务队列
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            _taskQueue.Enqueue(task.Id, nextExecution.Value);
            _logger.LogInformation("任务已加入调度队列: {TaskName} (ID: {TaskId}), 下次执行: {NextExecution}",
                task.Name, task.Id, nextExecution);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    /// <summary>
    /// 移除任务
    /// </summary>
    public async Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        // 如果任务正在运行，先取消它
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("已取消运行中的任务: {TaskId}", taskId);
        }

        // 注意：PriorityQueue 不支持移除特定元素
        // 在扫描时会过滤掉已删除的任务
        _logger.LogInformation("任务已从调度器移除: {TaskId}", taskId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public async Task PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        task.Status = TaskStatus.Paused;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 如果任务正在运行，取消它
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("任务已暂停: {TaskName} (ID: {TaskId})", task.Name, taskId);
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public async Task ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        if (task.Status != TaskStatus.Paused)
        {
            throw new InvalidOperationException($"只能恢复暂停的任务: {taskId}, 当前状态: {task.Status}");
        }

        task.Status = TaskStatus.Pending;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 重新调度任务
        await ScheduleTaskAsync(task, cancellationToken);

        _logger.LogInformation("任务已恢复: {TaskName} (ID: {TaskId})", task.Name, taskId);
    }

    /// <summary>
    /// 手动触发任务执行
    /// </summary>
    public async Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        _logger.LogInformation("手动触发任务执行: {TaskName} (ID: {TaskId})", task.Name, taskId);

        // 立即执行任务
        await ExecuteTaskAsync(task);
    }

    /// <summary>
    /// 加载待执行的任务
    /// </summary>
    private async Task LoadPendingTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pendingTasks = await _taskRepository.ListByStatusAsync(TaskStatus.Pending, cancellationToken);

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var task in pendingTasks)
                {
                    // 计算下次执行时间
                    var nextExecution = CalculateNextExecution(task);
                    if (nextExecution != null)
                    {
                        // 更新数据库中的下次执行时间
                        task.NextExecutionAt = nextExecution;
                        await _taskRepository.UpdateAsync(task, cancellationToken);

                        // 加入队列
                        _taskQueue.Enqueue(task.Id, nextExecution.Value);
                    }
                }

                _logger.LogInformation("已加载 {Count} 个待执行任务", pendingTasks.Count);
            }
            finally
            {
                _queueLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载待执行任务失败");
        }
    }

    /// <summary>
    /// 扫描并执行到期的任务
    /// </summary>
    private async Task ScanAndExecuteTasksAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var tasksToExecute = new List<Guid>();

            // 从队列中取出所有到期的任务
            await _queueLock.WaitAsync();
            try
            {
                while (_taskQueue.TryPeek(out _, out var nextExecutionTime) && nextExecutionTime <= now)
                {
                    var taskId = _taskQueue.Dequeue();
                    tasksToExecute.Add(taskId);
                }
            }
            finally
            {
                _queueLock.Release();
            }

            // 执行任务
            foreach (var taskId in tasksToExecute)
            {
                // 检查并发限制
                if (_runningTasks.Count >= _options.MaxConcurrentTasks)
                {
                    _logger.LogWarning("达到最大并发任务数限制: {MaxConcurrent}，任务 {TaskId} 将延迟执行",
                        _options.MaxConcurrentTasks, taskId);

                    // 将任务重新加入队列（延迟 1 分钟）
                    await _queueLock.WaitAsync();
                    try
                    {
                        _taskQueue.Enqueue(taskId, now.AddMinutes(1));
                    }
                    finally
                    {
                        _queueLock.Release();
                    }

                    continue;
                }

                // 从数据库加载任务
                var task = await _taskRepository.GetByIdAsync(taskId);
                if (task == null || task.Status != TaskStatus.Pending)
                {
                    // 任务已被删除或状态已改变，跳过
                    continue;
                }

                // 异步执行任务（不等待）
                _ = ExecuteTaskAsync(task);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描和执行任务失败");
        }
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        var cts = new CancellationTokenSource();

        if (!_runningTasks.TryAdd(task.Id, cts))
        {
            _logger.LogWarning("任务已在运行中: {TaskId}", task.Id);
            return;
        }

        try
        {
            _logger.LogInformation("开始执行任务: {TaskName} (ID: {TaskId})", task.Name, task.Id);

            // 执行任务
            await _taskExecutor.ExecuteAsync(task, cts.Token);

            // 更新任务状态
            task.LastExecutionAt = DateTime.UtcNow;
            task.ExecutionCount++;

            // 计算下次执行时间
            var nextExecution = CalculateNextExecution(task);
            if (nextExecution != null && (task.EndAt == null || nextExecution < task.EndAt))
            {
                // 还有下次执行，重新加入队列
                task.NextExecutionAt = nextExecution;
                task.Status = TaskStatus.Pending;
                await _taskRepository.UpdateAsync(task);

                await _queueLock.WaitAsync();
                try
                {
                    _taskQueue.Enqueue(task.Id, nextExecution.Value);
                }
                finally
                {
                    _queueLock.Release();
                }

                _logger.LogInformation("任务执行完成，下次执行: {NextExecution}", nextExecution);
            }
            else
            {
                // 没有下次执行，标记为已完成
                task.Status = TaskStatus.Completed;
                task.NextExecutionAt = null;
                await _taskRepository.UpdateAsync(task);

                _logger.LogInformation("任务执行完成，无下次执行");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务执行失败: {TaskName} (ID: {TaskId})", task.Name, task.Id);

            // 标记任务为失败
            task.Status = TaskStatus.Failed;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepository.UpdateAsync(task);
        }
        finally
        {
            _runningTasks.TryRemove(task.Id, out _);
            cts.Dispose();
        }
    }

    /// <summary>
    /// 计算任务的下次执行时间
    /// </summary>
    private DateTime? CalculateNextExecution(ScheduledTask task)
    {
        try
        {
            var now = DateTime.UtcNow;

            // 如果有开始时间且还未到，使用开始时间作为基准
            var baseTime = task.StartAt.HasValue && task.StartAt > now
                ? task.StartAt.Value
                : now;

            // 根据调度类型计算
            string cronExpression;

            if (task.ScheduleType == ScheduleType.Cron)
            {
                cronExpression = task.Schedule;
            }
            else // Natural
            {
                // 将自然语言转换为 cron 表达式
                cronExpression = _naturalLanguageParser.ParseToCron(task.Schedule);
            }

            // 使用 CronParser 计算下次执行时间
            var nextExecution = _cronParser.GetNextOccurrence(cronExpression, baseTime);

            // 如果有结束时间，确保下次执行时间在结束时间之前
            if (nextExecution.HasValue && task.EndAt.HasValue && nextExecution > task.EndAt)
            {
                return null;
            }

            return nextExecution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算下次执行时间失败: {TaskId}, Schedule: {Schedule}",
                task.Id, task.Schedule);
            return null;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _scanTimer?.Dispose();
        _queueLock.Dispose();

        foreach (var cts in _runningTasks.Values)
        {
            cts.Dispose();
        }

        _runningTasks.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
