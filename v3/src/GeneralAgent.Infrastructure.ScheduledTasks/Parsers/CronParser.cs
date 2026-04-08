using Cronos;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Parsers;

/// <summary>
/// Cron 表达式解析器实现（基于 Cronos 库）
/// </summary>
public class CronParser : ICronParser
{
    private readonly ILogger<CronParser> _logger;

    public CronParser(ILogger<CronParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析 cron 表达式
    /// </summary>
    public CronExpression Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException("Cron 表达式不能为空", nameof(cronExpression));
        }

        try
        {
            // 使用标准 cron 格式（5 个字段）
            // 格式：分 时 日 月 周
            // 示例："0 9 * * *" 表示每天 9:00
            return CronExpression.Parse(cronExpression, CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            _logger.LogError(ex, "无效的 cron 表达式: {CronExpression}", cronExpression);
            throw new ArgumentException($"无效的 cron 表达式: {cronExpression}. {ex.Message}", nameof(cronExpression), ex);
        }
    }

    /// <summary>
    /// 计算下次执行时间
    /// </summary>
    public DateTime? GetNextOccurrence(string cronExpression, DateTime from, TimeZoneInfo? timeZone = null)
    {
        try
        {
            var cron = Parse(cronExpression);
            var tz = timeZone ?? TimeZoneInfo.Utc;

            // Cronos 需要 UTC 时间
            var fromUtc = from.Kind == DateTimeKind.Utc
                ? from
                : TimeZoneInfo.ConvertTimeToUtc(from, tz);

            var nextUtc = cron.GetNextOccurrence(fromUtc, tz);

            return nextUtc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算下次执行时间失败: {CronExpression}, From: {From}", cronExpression, from);
            return null;
        }
    }

    /// <summary>
    /// 验证 cron 表达式是否有效
    /// </summary>
    public bool IsValid(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        try
        {
            CronExpression.Parse(cronExpression, CronFormat.Standard);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 计算接下来 N 次执行时间
    /// </summary>
    public List<DateTime> GetNextOccurrences(string cronExpression, DateTime from, int count, TimeZoneInfo? timeZone = null)
    {
        if (count <= 0)
        {
            throw new ArgumentException("计算次数必须大于 0", nameof(count));
        }

        var occurrences = new List<DateTime>();

        try
        {
            var cron = Parse(cronExpression);
            var tz = timeZone ?? TimeZoneInfo.Utc;

            var fromUtc = from.Kind == DateTimeKind.Utc
                ? from
                : TimeZoneInfo.ConvertTimeToUtc(from, tz);

            var current = fromUtc;

            for (int i = 0; i < count; i++)
            {
                var next = cron.GetNextOccurrence(current, tz);
                if (next == null)
                {
                    break; // 没有更多的执行时间
                }

                occurrences.Add(next.Value);
                current = next.Value;
            }

            return occurrences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算多次执行时间失败: {CronExpression}, Count: {Count}", cronExpression, count);
            return occurrences;
        }
    }
}
