namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 任务执行记录（单次执行的历史记录）
/// </summary>
public class TaskExecution
{
    /// <summary>
    /// 执行记录唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的任务 ID
    /// </summary>
    public Guid TaskId { get; set; }

    // ==================== 执行信息 ====================

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    // ==================== 结果和日志 ====================

    /// <summary>
    /// 执行输出（成功时的输出结果）
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 错误信息（失败时的错误详情）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 重试次数（第几次尝试，0 表示首次执行）
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? DurationMs { get; set; }

    // ==================== 元数据 ====================

    /// <summary>
    /// 其他元数据（JSON 格式）
    /// </summary>
    public string? Metadata { get; set; }
}
