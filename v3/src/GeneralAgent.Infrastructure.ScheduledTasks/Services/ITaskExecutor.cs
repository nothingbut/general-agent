using GeneralAgent.Infrastructure.ScheduledTasks.Models;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务执行器接口
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// 执行任务
    /// </summary>
    /// <param name="task">要执行的任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行记录</returns>
    Task<TaskExecution> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken = default);
}
