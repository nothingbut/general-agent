using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GeneralAgent.Infrastructure.Tests.ScheduledTasks;

/// <summary>
/// CronParser 单元测试
/// </summary>
public class CronParserTests
{
    private readonly CronParser _parser;

    public CronParserTests()
    {
        var mockLogger = new Mock<ILogger<CronParser>>();
        _parser = new CronParser(mockLogger.Object);
    }

    [Theory]
    [InlineData("0 9 * * *", true)]           // 每天 9:00
    [InlineData("0 17 * * 5", true)]          // 每周五 17:00
    [InlineData("0 0 1 * *", true)]           // 每月 1 号 0:00
    [InlineData("*/30 * * * *", true)]        // 每 30 分钟
    [InlineData("0 9-17 * * 1-5", true)]      // 工作日 9:00-17:00
    [InlineData("invalid", false)]            // 无效表达式
    [InlineData("", false)]                   // 空表达式
    public void IsValid_ShouldValidateCronExpression(string cronExpression, bool expected)
    {
        // Act
        var result = _parser.IsValid(cronExpression);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_ValidExpression_ShouldReturnCronExpression()
    {
        // Arrange
        var cronExpression = "0 9 * * *";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_InvalidExpression_ShouldThrowException()
    {
        // Arrange
        var cronExpression = "invalid";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(cronExpression));
    }

    [Fact]
    public void GetNextOccurrence_ShouldReturnNextExecutionTime()
    {
        // Arrange
        var cronExpression = "0 9 * * *"; // 每天 9:00
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _parser.GetNextOccurrence(cronExpression, from);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9, result.Value.Hour);
        Assert.Equal(0, result.Value.Minute);
    }

    [Fact]
    public void GetNextOccurrence_WithTimezone_ShouldRespectTimezone()
    {
        // Arrange
        var cronExpression = "0 9 * * *"; // 每天 9:00
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

        // Act
        var result = _parser.GetNextOccurrence(cronExpression, from, timezone);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetNextOccurrences_ShouldReturnMultipleOccurrences()
    {
        // Arrange
        var cronExpression = "0 9 * * *"; // 每天 9:00
        var from = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var count = 5;

        // Act
        var results = _parser.GetNextOccurrences(cronExpression, from, count);

        // Assert
        Assert.Equal(count, results.Count);
        Assert.All(results, dt =>
        {
            Assert.Equal(9, dt.Hour);
            Assert.Equal(0, dt.Minute);
        });
    }

    [Theory]
    [InlineData("0 9 * * *", 9)]              // 每天 9:00
    [InlineData("0 17 * * 5", 17)]            // 每周五 17:00
    [InlineData("0 0 1 * *", 0)]              // 每月 1 号 0:00
    public void GetNextOccurrence_DifferentPatterns_ShouldReturnCorrectTime(string cronExpression, int expectedHour)
    {
        // Arrange
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _parser.GetNextOccurrence(cronExpression, from);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedHour, result.Value.Hour);
    }

    [Fact]
    public void GetNextOccurrence_AfterScheduledTime_ShouldReturnNextDay()
    {
        // Arrange
        var cronExpression = "0 9 * * *"; // 每天 9:00
        var from = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00，已过 9:00

        // Act
        var result = _parser.GetNextOccurrence(cronExpression, from);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Day); // 下一天
        Assert.Equal(9, result.Value.Hour);
    }

    [Fact]
    public void GetNextOccurrence_EveryMinute_ShouldReturnNextMinute()
    {
        // Arrange
        var cronExpression = "* * * * *"; // 每分钟
        var from = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _parser.GetNextOccurrence(cronExpression, from);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9, result.Value.Hour);
        Assert.Equal(31, result.Value.Minute);
    }

    [Fact]
    public void GetNextOccurrences_WithInterval_ShouldReturnCorrectSequence()
    {
        // Arrange
        var cronExpression = "*/30 * * * *"; // 每 30 分钟
        var from = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var count = 3;

        // Act
        var results = _parser.GetNextOccurrences(cronExpression, from, count);

        // Assert
        Assert.Equal(count, results.Count);
        Assert.Equal(30, results[0].Minute);
        Assert.Equal(0, results[1].Minute);
        Assert.Equal(30, results[2].Minute);
    }
}
