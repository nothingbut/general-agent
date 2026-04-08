using Cronos;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Parsers;

/// <summary>
/// Cron 表达式解析器接口
/// </summary>
public interface ICronParser
{
    /// <summary>
    /// 解析 cron 表达式
    /// </summary>
    /// <param name="cronExpression">cron 表达式（如："0 9 * * *"）</param>
    /// <returns>Cronos.CronExpression 对象</returns>
    CronExpression Parse(string cronExpression);

    /// <summary>
    /// 计算下次执行时间
    /// </summary>
    /// <param name="cronExpression">cron 表达式</param>
    /// <param name="from">起始时间</param>
    /// <param name="timeZone">时区（可选，默认 UTC）</param>
    /// <returns>下次执行时间（UTC）</returns>
    DateTime? GetNextOccurrence(string cronExpression, DateTime from, TimeZoneInfo? timeZone = null);

    /// <summary>
    /// 验证 cron 表达式是否有效
    /// </summary>
    /// <param name="cronExpression">cron 表达式</param>
    /// <returns>true 表示有效，false 表示无效</returns>
    bool IsValid(string cronExpression);

    /// <summary>
    /// 计算接下来 N 次执行时间
    /// </summary>
    /// <param name="cronExpression">cron 表达式</param>
    /// <param name="from">起始时间</param>
    /// <param name="count">计算次数</param>
    /// <param name="timeZone">时区（可选，默认 UTC）</param>
    /// <returns>执行时间列表（UTC）</returns>
    List<DateTime> GetNextOccurrences(string cronExpression, DateTime from, int count, TimeZoneInfo? timeZone = null);
}
