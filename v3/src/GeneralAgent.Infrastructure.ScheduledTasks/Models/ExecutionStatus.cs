namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 执行状态（单次执行记录的状态）
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// 待执行：执行记录已创建，等待执行
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 执行中：正在执行
    /// </summary>
    Running = 1,

    /// <summary>
    /// 成功完成：执行成功
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败：执行失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 超时：执行超时
    /// </summary>
    Timeout = 4,

    /// <summary>
    /// 取消：执行被取消
    /// </summary>
    Cancelled = 5
}
