namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 任务状态
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// 待执行：任务已创建，等待调度
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 执行中：任务正在运行
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已完成：一次性任务已成功完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败：任务执行失败（超过最大重试次数）
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 已暂停：用户手动暂停任务
    /// </summary>
    Paused = 4,

    /// <summary>
    /// 已取消：用户手动取消任务
    /// </summary>
    Cancelled = 5
}
