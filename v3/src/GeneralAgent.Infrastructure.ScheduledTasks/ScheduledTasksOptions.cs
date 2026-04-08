namespace GeneralAgent.Infrastructure.ScheduledTasks;

/// <summary>
/// 计划任务配置选项
/// </summary>
public class ScheduledTasksOptions
{
    /// <summary>
    /// 数据库路径
    /// </summary>
    public string DatabasePath { get; set; } = "scheduled_tasks.db";

    /// <summary>
    /// 任务扫描间隔（秒，默认 60 秒）
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 最大并发任务数（默认 10）
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 10;
}
