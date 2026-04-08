using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Parsers;

/// <summary>
/// 自然语言时间解析器实现
/// </summary>
public class NaturalLanguageTimeParser : INaturalLanguageTimeParser
{
    private readonly ILogger<NaturalLanguageTimeParser> _logger;

    // 星期映射（中文 → 数字）
    private static readonly Dictionary<string, int> WeekdayMap = new()
    {
        { "周日", 0 }, { "星期日", 0 }, { "日", 0 },
        { "周一", 1 }, { "星期一", 1 }, { "一", 1 },
        { "周二", 2 }, { "星期二", 2 }, { "二", 2 },
        { "周三", 3 }, { "星期三", 3 }, { "三", 3 },
        { "周四", 4 }, { "星期四", 4 }, { "四", 4 },
        { "周五", 5 }, { "星期五", 5 }, { "五", 5 },
        { "周六", 6 }, { "星期六", 6 }, { "六", 6 }
    };

    // 时段映射（中文 → 小时）
    private static readonly Dictionary<string, int> TimePeriodMap = new()
    {
        { "凌晨", 0 }, { "早上", 9 }, { "早晨", 9 }, { "上午", 10 },
        { "中午", 12 }, { "下午", 14 }, { "傍晚", 18 }, { "晚上", 20 }
    };

    public NaturalLanguageTimeParser(ILogger<NaturalLanguageTimeParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析自然语言时间描述为 cron 表达式
    /// </summary>
    public string ParseToCron(string naturalLanguage)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            throw new ArgumentException("自然语言描述不能为空", nameof(naturalLanguage));
        }

        var input = naturalLanguage.Trim();

        // 模式 1: "每天 HH:mm" 或 "每天HH:mm" 或 "每天 H点" 或 "每天H点"
        var dailyPattern = @"每天\s*(\d{1,2}):?(\d{0,2})(?:点|时)?";
        var dailyMatch = Regex.Match(input, dailyPattern);
        if (dailyMatch.Success)
        {
            var hour = int.Parse(dailyMatch.Groups[1].Value);
            var minute = dailyMatch.Groups[2].Success && !string.IsNullOrEmpty(dailyMatch.Groups[2].Value)
                ? int.Parse(dailyMatch.Groups[2].Value)
                : 0;

            ValidateTime(hour, minute);
            return $"{minute} {hour} * * *";
        }

        // 模式 2: "每天早上9点"、"每天下午5点" 等（时段 + 点数）
        var dailyTimePeriodPattern = @"每天\s*(凌晨|早上|早晨|上午|中午|下午|傍晚|晚上)\s*(\d{1,2})(?:点|时)?";
        var dailyTimePeriodMatch = Regex.Match(input, dailyTimePeriodPattern);
        if (dailyTimePeriodMatch.Success)
        {
            var period = dailyTimePeriodMatch.Groups[1].Value;
            var hourOffset = int.Parse(dailyTimePeriodMatch.Groups[2].Value);
            var hour = ConvertTimePeriodToHour(period, hourOffset);

            ValidateTime(hour, 0);
            return $"0 {hour} * * *";
        }

        // 模式 3: "每周X HH:mm" 或 "每周X H点"
        var weeklyPattern = @"每(?:周|星期)(周?[日一二三四五六])\s*(\d{1,2}):?(\d{0,2})(?:点|时)?";
        var weeklyMatch = Regex.Match(input, weeklyPattern);
        if (weeklyMatch.Success)
        {
            var weekdayStr = weeklyMatch.Groups[1].Value;
            var hour = int.Parse(weeklyMatch.Groups[2].Value);
            var minute = weeklyMatch.Groups[3].Success && !string.IsNullOrEmpty(weeklyMatch.Groups[3].Value)
                ? int.Parse(weeklyMatch.Groups[3].Value)
                : 0;

            if (!WeekdayMap.TryGetValue(weekdayStr, out var weekday))
            {
                throw new ArgumentException($"无法识别的星期: {weekdayStr}");
            }

            ValidateTime(hour, minute);
            return $"{minute} {hour} * * {weekday}";
        }

        // 模式 4: "每周五下午5点" 等（星期 + 时段 + 点数）
        var weeklyTimePeriodPattern = @"每(?:周|星期)(周?[日一二三四五六])\s*(凌晨|早上|早晨|上午|中午|下午|傍晚|晚上)\s*(\d{1,2})(?:点|时)?";
        var weeklyTimePeriodMatch = Regex.Match(input, weeklyTimePeriodPattern);
        if (weeklyTimePeriodMatch.Success)
        {
            var weekdayStr = weeklyTimePeriodMatch.Groups[1].Value;
            var period = weeklyTimePeriodMatch.Groups[2].Value;
            var hourOffset = int.Parse(weeklyTimePeriodMatch.Groups[3].Value);

            if (!WeekdayMap.TryGetValue(weekdayStr, out var weekday))
            {
                throw new ArgumentException($"无法识别的星期: {weekdayStr}");
            }

            var hour = ConvertTimePeriodToHour(period, hourOffset);
            ValidateTime(hour, 0);
            return $"0 {hour} * * {weekday}";
        }

        // 模式 5: "每月DD号 HH:mm" 或 "每月DD号H点"
        var monthlyPattern = @"每月\s*(\d{1,2})号?\s*(\d{1,2}):?(\d{0,2})(?:点|时)?";
        var monthlyMatch = Regex.Match(input, monthlyPattern);
        if (monthlyMatch.Success)
        {
            var day = int.Parse(monthlyMatch.Groups[1].Value);
            var hour = int.Parse(monthlyMatch.Groups[2].Value);
            var minute = monthlyMatch.Groups[3].Success && !string.IsNullOrEmpty(monthlyMatch.Groups[3].Value)
                ? int.Parse(monthlyMatch.Groups[3].Value)
                : 0;

            ValidateDay(day);
            ValidateTime(hour, minute);
            return $"{minute} {hour} {day} * *";
        }

        // 模式 6: "每小时"
        if (Regex.IsMatch(input, @"每\s*小时"))
        {
            return "0 * * * *";
        }

        // 模式 7: "每N分钟" 或 "每N分"
        var minutePattern = @"每\s*(\d+)\s*分钟?";
        var minuteMatch = Regex.Match(input, minutePattern);
        if (minuteMatch.Success)
        {
            var interval = int.Parse(minuteMatch.Groups[1].Value);
            if (interval <= 0 || interval > 59)
            {
                throw new ArgumentException($"分钟间隔必须在 1-59 之间: {interval}");
            }

            return $"*/{interval} * * * *";
        }

        _logger.LogWarning("无法解析自然语言: {NaturalLanguage}", naturalLanguage);
        throw new ArgumentException($"无法解析自然语言: {naturalLanguage}。请使用支持的格式，如：'每天9:00'、'每周五17:00'、'每月1号20:00'");
    }

    /// <summary>
    /// 验证自然语言是否可解析
    /// </summary>
    public bool CanParse(string naturalLanguage)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return false;
        }

        try
        {
            ParseToCron(naturalLanguage);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取支持的自然语言模式示例
    /// </summary>
    public List<string> GetSupportedPatterns()
    {
        return new List<string>
        {
            "每天 9:00 → 每天早上9点",
            "每天早上9点 → 早上9:00",
            "每天下午5点 → 下午5:00",
            "每周五 17:00 → 每周五下午5点",
            "每周一早上9点 → 周一早上9:00",
            "每月1号 20:00 → 每月1号晚上8点",
            "每小时 → 每小时执行一次",
            "每30分钟 → 每30分钟执行一次"
        };
    }

    /// <summary>
    /// 将时段和小时偏移转换为24小时制
    /// </summary>
    private int ConvertTimePeriodToHour(string period, int hourOffset)
    {
        if (!TimePeriodMap.TryGetValue(period, out var baseHour))
        {
            throw new ArgumentException($"无法识别的时段: {period}");
        }

        // 特殊处理：如果是"下午X点"或"晚上X点"，且X <= 12，则需要加12
        if ((period == "下午" || period == "傍晚" || period == "晚上") && hourOffset <= 12)
        {
            return hourOffset + 12;
        }

        // 其他情况直接使用偏移值
        return hourOffset;
    }

    /// <summary>
    /// 验证时间的有效性
    /// </summary>
    private void ValidateTime(int hour, int minute)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentException($"小时必须在 0-23 之间: {hour}");
        }

        if (minute < 0 || minute > 59)
        {
            throw new ArgumentException($"分钟必须在 0-59 之间: {minute}");
        }
    }

    /// <summary>
    /// 验证日期的有效性
    /// </summary>
    private void ValidateDay(int day)
    {
        if (day < 1 || day > 31)
        {
            throw new ArgumentException($"日期必须在 1-31 之间: {day}");
        }
    }
}
