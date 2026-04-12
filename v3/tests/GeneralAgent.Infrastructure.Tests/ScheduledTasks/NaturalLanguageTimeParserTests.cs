using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GeneralAgent.Infrastructure.Tests.ScheduledTasks;

/// <summary>
/// NaturalLanguageTimeParser 单元测试
/// </summary>
public class NaturalLanguageTimeParserTests
{
    private readonly NaturalLanguageTimeParser _parser;

    public NaturalLanguageTimeParserTests()
    {
        var mockLogger = new Mock<ILogger<NaturalLanguageTimeParser>>();
        _parser = new NaturalLanguageTimeParser(mockLogger.Object);
    }

    #region 每天系列测试

    [Theory]
    [InlineData("每天9:00", "0 9 * * *")]
    [InlineData("每天 9:00", "0 9 * * *")]
    [InlineData("每天9点", "0 9 * * *")]
    [InlineData("每天 9点", "0 9 * * *")]
    [InlineData("每天17:30", "30 17 * * *")]
    [InlineData("每天 23:59", "59 23 * * *")]
    public void ParseToCron_DailyTime_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("每天早上9点", "0 9 * * *")]
    [InlineData("每天早晨9点", "0 9 * * *")]
    [InlineData("每天上午10点", "0 10 * * *")]
    [InlineData("每天中午12点", "0 12 * * *")]
    [InlineData("每天下午5点", "0 17 * * *")]
    [InlineData("每天傍晚6点", "0 18 * * *")]
    [InlineData("每天晚上8点", "0 20 * * *")]
    [InlineData("每天凌晨0点", "0 0 * * *")]
    public void ParseToCron_DailyTimePeriod_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 每周系列测试

    [Theory]
    [InlineData("每周一9:00", "0 9 * * 1")]
    [InlineData("每周二17:00", "0 17 * * 2")]
    [InlineData("每周三 10:30", "30 10 * * 3")]
    [InlineData("每周四9点", "0 9 * * 4")]
    [InlineData("每周五 17点", "0 17 * * 5")]
    [InlineData("每周六8:00", "0 8 * * 6")]
    [InlineData("每周日20:00", "0 20 * * 0")]
    [InlineData("每星期一9:00", "0 9 * * 1")]
    [InlineData("每星期五17:00", "0 17 * * 5")]
    public void ParseToCron_WeeklyTime_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("每周一早上9点", "0 9 * * 1")]
    [InlineData("每周五下午5点", "0 17 * * 5")]
    [InlineData("每周六晚上8点", "0 20 * * 6")]
    [InlineData("每周日上午10点", "0 10 * * 0")]
    public void ParseToCron_WeeklyTimePeriod_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 每月系列测试

    [Theory]
    [InlineData("每月1号9:00", "0 9 1 * *")]
    [InlineData("每月15号 17:00", "0 17 15 * *")]
    [InlineData("每月31号23:59", "59 23 31 * *")]
    [InlineData("每月1号9点", "0 9 1 * *")]
    [InlineData("每月10号 20点", "0 20 10 * *")]
    public void ParseToCron_MonthlyTime_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 间隔系列测试

    [Fact]
    public void ParseToCron_Hourly_ShouldReturnCorrectCron()
    {
        // Arrange
        var input = "每小时";

        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal("0 * * * *", result);
    }

    [Theory]
    [InlineData("每5分钟", "*/5 * * * *")]
    [InlineData("每10分", "*/10 * * * *")]
    [InlineData("每30分钟", "*/30 * * * *")]
    [InlineData("每 15 分钟", "*/15 * * * *")]
    public void ParseToCron_MinuteInterval_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 验证和错误处理测试

    [Theory]
    [InlineData("每天9:00")]
    [InlineData("每周一17:00")]
    [InlineData("每月1号20:00")]
    [InlineData("每小时")]
    [InlineData("每30分钟")]
    public void CanParse_ValidInput_ShouldReturnTrue(string input)
    {
        // Act
        var result = _parser.CanParse(input);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("每天")]
    [InlineData("明天9点")]
    [InlineData("下周一")]
    public void CanParse_InvalidInput_ShouldReturnFalse(string input)
    {
        // Act
        var result = _parser.CanParse(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ParseToCron_EmptyString_ShouldThrowException()
    {
        // Arrange
        var input = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseToCron(input));
    }

    [Fact]
    public void ParseToCron_InvalidFormat_ShouldThrowException()
    {
        // Arrange
        var input = "无法识别的格式";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseToCron(input));
    }

    [Theory]
    [InlineData("每天25:00")]  // 无效小时
    [InlineData("每天9:60")]   // 无效分钟
    public void ParseToCron_InvalidTime_ShouldThrowException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseToCron(input));
    }

    [Theory]
    [InlineData("每月0号9:00")]   // 无效日期
    [InlineData("每月32号9:00")]  // 无效日期
    public void ParseToCron_InvalidDay_ShouldThrowException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseToCron(input));
    }

    [Theory]
    [InlineData("每0分钟")]   // 无效间隔
    [InlineData("每60分钟")]  // 无效间隔
    public void ParseToCron_InvalidInterval_ShouldThrowException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseToCron(input));
    }

    #endregion

    #region 支持的模式测试

    [Fact]
    public void GetSupportedPatterns_ShouldReturnPatternList()
    {
        // Act
        var patterns = _parser.GetSupportedPatterns();

        // Assert
        Assert.NotEmpty(patterns);
        Assert.Contains(patterns, p => p.Contains("每天"));
        Assert.Contains(patterns, p => p.Contains("每周"));
        Assert.Contains(patterns, p => p.Contains("每月"));
        Assert.Contains(patterns, p => p.Contains("每小时"));
        Assert.Contains(patterns, p => p.Contains("分钟"));
    }

    #endregion

    #region 边界条件测试

    [Theory]
    [InlineData("每天0:00", "0 0 * * *")]
    [InlineData("每天23:59", "59 23 * * *")]
    public void ParseToCron_BoundaryTime_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("每月1号9:00", "0 9 1 * *")]
    [InlineData("每月31号9:00", "0 9 31 * *")]
    public void ParseToCron_BoundaryDay_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("每1分钟", "*/1 * * * *")]
    [InlineData("每59分钟", "*/59 * * * *")]
    public void ParseToCron_BoundaryInterval_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 复杂场景测试

    [Theory]
    [InlineData("每天下午3点", "0 15 * * *")]  // 下午3点 = 15:00
    [InlineData("每天晚上11点", "0 23 * * *")] // 晚上11点 = 23:00
    public void ParseToCron_ComplexTimePeriod_ShouldReturnCorrectCron(string input, string expected)
    {
        // Act
        var result = _parser.ParseToCron(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
