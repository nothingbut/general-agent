using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Repositories;

/// <summary>
/// 计划任务仓储接口
/// </summary>
public interface IScheduledTaskRepository
{
    /// <summary>
    /// 创建任务
    /// </summary>
    Task<ScheduledTask> CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新任务
    /// </summary>
    Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除任务
    /// </summary>
    Task DeleteAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取任务
    /// </summary>
    Task<ScheduledTask?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出用户的所有任务
    /// </summary>
    Task<List<ScheduledTask>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按状态列出任务
    /// </summary>
    Task<List<ScheduledTask>> ListByStatusAsync(TaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待执行的任务（状态为 Pending 且下次执行时间在指定时间之前）
    /// </summary>
    Task<List<ScheduledTask>> GetPendingTasksAsync(DateTime before, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出用户的指定状态的任务
    /// </summary>
    Task<List<ScheduledTask>> ListByOwnerAndStatusAsync(string ownerId, TaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有任务
    /// </summary>
    Task<List<ScheduledTask>> ListAllAsync(CancellationToken cancellationToken = default);
}
