namespace GeneralAgent.Infrastructure.ScheduledTasks.Models;

/// <summary>
/// 调度类型
/// </summary>
public enum ScheduleType
{
    /// <summary>
    /// 标准 cron 表达式（如："0 9 * * *"）
    /// </summary>
    Cron = 0,

    /// <summary>
    /// 自然语言描述（如："每天早上9点"、"每周五下午5点"）
    /// </summary>
    Natural = 1
}
