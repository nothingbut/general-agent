using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using GeneralAgent.Infrastructure.ScheduledTasks.Repositories;
using Microsoft.Extensions.Logging;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务管理器实现
/// </summary>
public class TaskManager : ITaskManager
{
    private readonly IScheduledTaskRepository _taskRepository;
    private readonly ITaskScheduler _scheduler;
    private readonly ITaskExecutor _executor;
    private readonly ITaskExecutionRepository _executionRepository;
    private readonly ICronParser _cronParser;
    private readonly INaturalLanguageTimeParser _naturalLanguageParser;
    private readonly ILogger<TaskManager> _logger;

    public TaskManager(
        IScheduledTaskRepository taskRepository,
        ITaskScheduler scheduler,
        ITaskExecutor executor,
        ITaskExecutionRepository executionRepository,
        ICronParser cronParser,
        INaturalLanguageTimeParser naturalLanguageParser,
        ILogger<TaskManager> logger)
    {
        _taskRepository = taskRepository;
        _scheduler = scheduler;
        _executor = executor;
        _executionRepository = executionRepository;
        _cronParser = cronParser;
        _naturalLanguageParser = naturalLanguageParser;
        _logger = logger;
    }

    /// <summary>
    /// 创建任务
    /// </summary>
    public async Task<ScheduledTask> CreateTaskAsync(
        string name,
        string description,
        ScheduleType scheduleType,
        string schedule,
        TaskType taskType,
        string taskPayload,
        DateTime? startAt = null,
        DateTime? endAt = null,
        int maxRetries = 3,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        // 验证输入
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("任务名称不能为空", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(schedule))
        {
            throw new ArgumentException("调度表达式不能为空", nameof(schedule));
        }

        if (maxRetries < 0)
        {
            throw new ArgumentException("最大重试次数不能为负数", nameof(maxRetries));
        }

        if (timeoutSeconds <= 0)
        {
            throw new ArgumentException("超时时间必须大于 0", nameof(timeoutSeconds));
        }

        // 验证调度表达式
        try
        {
            if (scheduleType == ScheduleType.Cron)
            {
                if (!_cronParser.IsValid(schedule))
                {
                    throw new ArgumentException($"无效的 cron 表达式: {schedule}", nameof(schedule));
                }
            }
            else // Natural
            {
                if (!_naturalLanguageParser.CanParse(schedule))
                {
                    throw new ArgumentException($"无法解析的自然语言表达式: {schedule}", nameof(schedule));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证调度表达式失败: {Schedule}", schedule);
            throw;
        }

        // 验证时间范围
        if (startAt.HasValue && endAt.HasValue && startAt >= endAt)
        {
            throw new ArgumentException("结束时间必须晚于开始时间");
        }

        // 创建任务
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ScheduleType = scheduleType,
            Schedule = schedule,
            TaskType = taskType,
            TaskPayload = taskPayload,
            Status = TaskStatus.Pending,
            MaxRetries = maxRetries,
            TimeoutSeconds = timeoutSeconds,
            StartAt = startAt,
            EndAt = endAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExecutionCount = 0
        };

        // 保存到数据库
        await _taskRepository.CreateAsync(task, cancellationToken);

        // 如果调度器正在运行，立即加入调度队列
        if (_scheduler.IsRunning)
        {
            try
            {
                await _scheduler.ScheduleTaskAsync(task, cancellationToken);
                _logger.LogInformation("任务已创建并加入调度队列: {TaskName} (ID: {TaskId})", name, task.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "任务已创建但加入调度队列失败: {TaskName} (ID: {TaskId})", name, task.Id);
            }
        }
        else
        {
            _logger.LogInformation("任务已创建（调度器未运行）: {TaskName} (ID: {TaskId})", name, task.Id);
        }

        return task;
    }

    /// <summary>
    /// 列出任务
    /// </summary>
    public async Task<List<ScheduledTask>> ListTasksAsync(
        TaskStatus? status = null,
        TaskType? taskType = null,
        CancellationToken cancellationToken = default)
    {
        if (status.HasValue && taskType.HasValue)
        {
            // 按状态和类型过滤
            var allTasks = await _taskRepository.ListByStatusAsync(status.Value, cancellationToken);
            return allTasks.Where(t => t.TaskType == taskType.Value).ToList();
        }
        else if (status.HasValue)
        {
            // 仅按状态过滤
            return await _taskRepository.ListByStatusAsync(status.Value, cancellationToken);
        }
        else if (taskType.HasValue)
        {
            // 仅按类型过滤
            var allTasks = await _taskRepository.ListAllAsync(cancellationToken);
            return allTasks.Where(t => t.TaskType == taskType.Value).ToList();
        }
        else
        {
            // 返回所有任务
            return await _taskRepository.ListAllAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 获取任务详情
    /// </summary>
    public async Task<ScheduledTask?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await _taskRepository.GetByIdAsync(taskId, cancellationToken);
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    public async Task<ScheduledTask> UpdateTaskAsync(
        Guid taskId,
        string? schedule = null,
        string? description = null,
        int? maxRetries = null,
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        var hasChanges = false;

        // 更新调度表达式
        if (!string.IsNullOrWhiteSpace(schedule) && schedule != task.Schedule)
        {
            // 验证新的调度表达式
            try
            {
                if (task.ScheduleType == ScheduleType.Cron)
                {
                    if (!_cronParser.IsValid(schedule))
                    {
                        throw new ArgumentException($"无效的 cron 表达式: {schedule}", nameof(schedule));
                    }
                }
                else // Natural
                {
                    if (!_naturalLanguageParser.CanParse(schedule))
                    {
                        throw new ArgumentException($"无法解析的自然语言表达式: {schedule}", nameof(schedule));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证新的调度表达式失败: {Schedule}", schedule);
                throw;
            }

            task.Schedule = schedule;
            hasChanges = true;
        }

        // 更新描述
        if (!string.IsNullOrWhiteSpace(description) && description != task.Description)
        {
            task.Description = description;
            hasChanges = true;
        }

        // 更新最大重试次数
        if (maxRetries.HasValue && maxRetries.Value != task.MaxRetries)
        {
            if (maxRetries.Value < 0)
            {
                throw new ArgumentException("最大重试次数不能为负数", nameof(maxRetries));
            }
            task.MaxRetries = maxRetries.Value;
            hasChanges = true;
        }

        // 更新超时时间
        if (timeoutSeconds.HasValue && timeoutSeconds.Value != task.TimeoutSeconds)
        {
            if (timeoutSeconds.Value <= 0)
            {
                throw new ArgumentException("超时时间必须大于 0", nameof(timeoutSeconds));
            }
            task.TimeoutSeconds = timeoutSeconds.Value;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            _logger.LogInformation("任务无变更: {TaskId}", taskId);
            return task;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 如果任务正在运行且调度表达式发生变化，重新调度
        if (hasChanges && task.Status == TaskStatus.Pending && _scheduler.IsRunning)
        {
            try
            {
                // 先取消当前调度
                await _scheduler.UnscheduleTaskAsync(taskId, cancellationToken);
                // 重新调度
                await _scheduler.ScheduleTaskAsync(task, cancellationToken);
                _logger.LogInformation("任务已更新并重新调度: {TaskName} (ID: {TaskId})", task.Name, taskId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "任务已更新但重新调度失败: {TaskName} (ID: {TaskId})", task.Name, taskId);
            }
        }
        else
        {
            _logger.LogInformation("任务已更新: {TaskName} (ID: {TaskId})", task.Name, taskId);
        }

        return task;
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    public async Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        // 如果任务正在运行，先从调度器中移除
        if (_scheduler.IsRunning)
        {
            try
            {
                await _scheduler.UnscheduleTaskAsync(taskId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "从调度器移除任务失败: {TaskId}", taskId);
            }
        }

        // 从数据库删除
        await _taskRepository.DeleteAsync(taskId, cancellationToken);
        _logger.LogInformation("任务已删除: {TaskName} (ID: {TaskId})", task.Name, taskId);
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public async Task PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await _scheduler.PauseTaskAsync(taskId, cancellationToken);
        _logger.LogInformation("任务已暂停: {TaskId}", taskId);
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public async Task ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await _scheduler.ResumeTaskAsync(taskId, cancellationToken);
        _logger.LogInformation("任务已恢复: {TaskId}", taskId);
    }

    /// <summary>
    /// 手动触发任务执行
    /// </summary>
    public async Task<TaskExecution> TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务不存在: {taskId}");
        }

        _logger.LogInformation("手动触发任务执行: {TaskName} (ID: {TaskId})", task.Name, taskId);

        // 直接执行任务（不通过调度器）
        var execution = await _executor.ExecuteAsync(task, cancellationToken);

        // 更新任务的最后执行时间和执行次数
        task.LastExecutionAt = DateTime.UtcNow;
        task.ExecutionCount++;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task, cancellationToken);

        return execution;
    }

    /// <summary>
    /// 获取任务执行历史
    /// </summary>
    public async Task<List<TaskExecution>> GetExecutionHistoryAsync(
        Guid taskId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentException("限制数量必须大于 0", nameof(limit));
        }

        return await _executionRepository.GetByTaskIdAsync(taskId, limit, cancellationToken);
    }
}
