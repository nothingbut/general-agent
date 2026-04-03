using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Processors;

/// <summary>
/// TextFileProcessor 单元测试
/// </summary>
public class TextFileProcessorTests : IDisposable
{
    private readonly TextFileProcessor _processor;
    private readonly ILogger<TextFileProcessor> _logger;

    public TextFileProcessorTests()
    {
        _logger = Substitute.For<ILogger<TextFileProcessor>>();
        _processor = new TextFileProcessor(_logger);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".markdown")]
    [InlineData(".log")]
    public void CanProcess_应该支持文本文件类型(string extension)
    {
        // Act
        var canProcess = _processor.CanProcess(extension);

        // Assert
        canProcess.Should().BeTrue();
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".cs")]
    [InlineData(".pdf")]
    public void CanProcess_不应该支持非文本文件类型(string extension)
    {
        // Act
        var canProcess = _processor.CanProcess(extension);

        // Assert
        canProcess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_应该正确处理文本文件()
    {
        // Arrange
        var content = "第一行\n第二行\n第三行";
        var filePath = TestFileHelper.CreateTempTextFile(content);

        // Act
        var result = await _processor.ProcessAsync(filePath, maxLength: 10000);

        // Assert
        result.Content.Should().Be(content);
        result.IsTruncated.Should().BeFalse();
        result.Metadata.Should().ContainKey("line_count");
        result.Metadata!["line_count"].Should().Be(3);
    }

    [Fact]
    public async Task ProcessAsync_超长内容应该被截断()
    {
        // Arrange
        var content = new string('A', 15000);
        var filePath = TestFileHelper.CreateTempTextFile(content);

        // Act
        var result = await _processor.ProcessAsync(filePath, maxLength: 10000);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.OriginalLength.Should().Be(15000);
        result.ProcessedLength.Should().BeLessThan(result.OriginalLength);
        result.Content.Should().Contain("[内容已截断]");
    }

    [Fact]
    public async Task ProcessAsync_文件不存在应该抛出异常()
    {
        // Arrange
        var filePath = "/path/to/nonexistent/file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _processor.ProcessAsync(filePath, maxLength: 10000));
    }

    [Fact]
    public async Task ProcessAsync_应该统计行数()
    {
        // Arrange
        var content = "第一行\n第二行\n第三行\n第四行\n第五行";
        var filePath = TestFileHelper.CreateTempTextFile(content);

        // Act
        var result = await _processor.ProcessAsync(filePath, maxLength: 10000);

        // Assert
        result.Metadata.Should().ContainKey("line_count");
        result.Metadata!["line_count"].Should().Be(5);
    }

    [Fact]
    public void SupportedExtensions_应该包含所有文本文件类型()
    {
        // Act
        var extensions = _processor.SupportedExtensions;

        // Assert
        extensions.Should().Contain(".txt");
        extensions.Should().Contain(".md");
        extensions.Should().Contain(".markdown");
        extensions.Should().Contain(".log");
    }

    [Fact]
    public void Priority_应该返回正确的优先级()
    {
        // Act
        var priority = _processor.Priority;

        // Assert
        priority.Should().Be(10);
    }

    public void Dispose()
    {
        // 临时文件会在测试结束后自动清理
        // TestFileHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
