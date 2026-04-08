namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 计划任务定义
/// </summary>
public class ScheduledTask
{
    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 任务所有者用户 ID
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    // ==================== 调度配置 ====================

    /// <summary>
    /// 调度表达式（cron 表达式或自然语言）
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// 调度类型
    /// </summary>
    public ScheduleType ScheduleType { get; set; }

    /// <summary>
    /// 开始时间（可选，任务在此时间之后才开始执行）
    /// </summary>
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// 结束时间（可选，任务在此时间之后停止执行）
    /// </summary>
    public DateTime? EndAt { get; set; }

    // ==================== 任务配置 ====================

    /// <summary>
    /// 任务类型
    /// </summary>
    public TaskType TaskType { get; set; }

    /// <summary>
    /// 任务参数（JSON 格式）
    /// 根据 TaskType 不同，payload 结构也不同：
    /// - SkillInvocation: {"skill": "skill-name", "args": {...}}
    /// - MemoryReminder: {"message": "reminder text"}
    /// - CustomCommand: {"command": "command string"}
    /// </summary>
    public string TaskPayload { get; set; } = string.Empty;

    /// <summary>
    /// 最大重试次数（默认 3 次）
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 超时时间（秒，默认 300 秒 = 5 分钟）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    // ==================== 状态管理 ====================

    /// <summary>
    /// 任务状态
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 上次执行时间
    /// </summary>
    public DateTime? LastExecutionAt { get; set; }

    /// <summary>
    /// 下次执行时间（由调度器计算）
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// 已执行次数
    /// </summary>
    public int ExecutionCount { get; set; }

    // ==================== 元数据 ====================

    /// <summary>
    /// 标签（逗号分隔，用于分类和搜索）
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// 其他元数据（JSON 格式）
    /// </summary>
    public string? Metadata { get; set; }
}
