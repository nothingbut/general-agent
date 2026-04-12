using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 任务管理器接口 - 提供用户友好的任务管理 API
/// </summary>
public interface ITaskManager
{
    /// <summary>
    /// 创建任务
    /// </summary>
    /// <param name="name">任务名称</param>
    /// <param name="description">任务描述</param>
    /// <param name="scheduleType">调度类型</param>
    /// <param name="schedule">调度表达式（cron 或自然语言）</param>
    /// <param name="taskType">任务类型</param>
    /// <param name="taskPayload">任务负载（JSON）</param>
    /// <param name="startAt">开始时间（可选）</param>
    /// <param name="endAt">结束时间（可选）</param>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的任务</returns>
    Task<ScheduledTask> CreateTaskAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出任务
    /// </summary>
    /// <param name="status">按状态过滤（可选）</param>
    /// <param name="taskType">按任务类型过滤（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务列表</returns>
    Task<List<ScheduledTask>> ListTasksAsync(
        TaskStatus? status = null,
        TaskType? taskType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务详情
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务详情</returns>
    Task<ScheduledTask?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="schedule">新的调度表达式（可选）</param>
    /// <param name="description">新的描述（可选）</param>
    /// <param name="maxRetries">新的最大重试次数（可选）</param>
    /// <param name="timeoutSeconds">新的超时时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的任务</returns>
    Task<ScheduledTask> UpdateTaskAsync(
        Guid taskId,
        string? schedule = null,
        string? description = null,
        int? maxRetries = null,
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动触发任务执行
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行记录</returns>
    Task<TaskExecution> TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务执行历史
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行历史列表</returns>
    Task<List<TaskExecution>> GetExecutionHistoryAsync(
        Guid taskId,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
