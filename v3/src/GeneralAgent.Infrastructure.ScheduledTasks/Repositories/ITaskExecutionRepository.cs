using GeneralAgent.Infrastructure.ScheduledTasks.Models;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Repositories;

/// <summary>
/// 任务执行记录仓储接口
/// </summary>
public interface ITaskExecutionRepository
{
    /// <summary>
    /// 创建执行记录
    /// </summary>
    Task<TaskExecution> CreateAsync(TaskExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新执行记录
    /// </summary>
    Task UpdateAsync(TaskExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务的执行历史（按开始时间降序）
    /// </summary>
    Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务的最新执行记录
    /// </summary>
    Task<TaskExecution?> GetLatestByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取执行记录
    /// </summary>
    Task<TaskExecution?> GetByIdAsync(Guid executionId, CancellationToken cancellationToken = default);
}
