using GeneralAgent.Infrastructure.ScheduledTasks.Models;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务调度器接口
/// </summary>
public interface ITaskScheduler
{
    /// <summary>
    /// 启动调度器
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止调度器
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加任务到调度队列
    /// </summary>
    Task ScheduleTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除任务
    /// </summary>
    Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停任务
    /// </summary>
    Task PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复任务
    /// </summary>
    Task ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动触发任务执行（不等待下次调度时间）
    /// </summary>
    Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    bool IsRunning { get; }
}
