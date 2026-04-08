using FluentAssertions;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Services;

/// <summary>
/// SkillWriter 单元测试
/// </summary>
public class SkillWriterTests : IDisposable
{
    private readonly ILogger<SkillWriter> _logger;
    private readonly SkillWriter _writer;
    private readonly string _testSkillsDirectory;

    public SkillWriterTests()
    {
        _logger = Substitute.For<ILogger<SkillWriter>>();

        // 创建临时测试目录
        _testSkillsDirectory = Path.Combine(Path.GetTempPath(), $"test-skills-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSkillsDirectory);

        var options = Options.Create(new SkillExtractionOptions
        {
            SkillsDirectory = _testSkillsDirectory,
            AutoCreateNamespaceDirectory = true,
            OverwriteExisting = false
        });

        _writer = new SkillWriter(options, _logger);
    }

    public void Dispose()
    {
        // 清理测试目录
        if (Directory.Exists(_testSkillsDirectory))
        {
            Directory.Delete(_testSkillsDirectory, true);
        }
    }

    [Fact]
    public async Task SaveSkillAsync_新技能_应该成功保存()
    {
        // Arrange
        var content = """
        ---
        name: test-skill
        description: 测试技能
        ---

        测试内容
        """;

        // Act
        var filePath = await _writer.SaveSkillAsync("test", "skill-1", content);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var savedContent = await File.ReadAllTextAsync(filePath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task SaveSkillAsync_自动创建命名空间目录()
    {
        // Arrange
        var content = "测试内容";

        // Act
        await _writer.SaveSkillAsync("new-namespace", "skill-1", content);

        // Assert
        var namespaceDir = Path.Combine(_testSkillsDirectory, "new-namespace");
        Directory.Exists(namespaceDir).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSkillAsync_文件已存在_应该抛出异常()
    {
        // Arrange
        var content = "测试内容";
        await _writer.SaveSkillAsync("test", "existing-skill", content);

        // Act
        var act = async () => await _writer.SaveSkillAsync("test", "existing-skill", content);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已存在*");
    }

    [Fact]
    public async Task SaveSkillAsync_非法命名空间_应该抛出异常()
    {
        // Arrange
        var content = "测试内容";

        // Act
        var act = async () => await _writer.SaveSkillAsync("test/illegal", "skill", content);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*非法字符*");
    }

    [Fact]
    public async Task SaveSkillAsync_非法技能名称_应该抛出异常()
    {
        // Arrange
        var content = "测试内容";

        // Act - 使用在所有平台都非法的字符（NULL字符）
        var act = async () => await _writer.SaveSkillAsync("test", "skill\0illegal", content);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*非法字符*");
    }

    [Fact]
    public async Task UpdateSkillAsync_存在的文件_应该成功更新()
    {
        // Arrange
        var originalContent = "原始内容";
        var filePath = await _writer.SaveSkillAsync("test", "update-skill", originalContent);

        var updatedContent = "更新后的内容";

        // Act
        await _writer.UpdateSkillAsync(filePath, updatedContent);

        // Assert
        var savedContent = await File.ReadAllTextAsync(filePath);
        savedContent.Should().Be(updatedContent);
    }

    [Fact]
    public async Task UpdateSkillAsync_文件不存在_应该抛出异常()
    {
        // Arrange
        var filePath = Path.Combine(_testSkillsDirectory, "nonexistent.md");
        var content = "测试内容";

        // Act
        var act = async () => await _writer.UpdateSkillAsync(filePath, content);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task DeleteSkillAsync_存在的文件_应该成功删除()
    {
        // Arrange
        var content = "测试内容";
        var filePath = await _writer.SaveSkillAsync("test", "delete-skill", content);

        // Act
        var result = await _writer.DeleteSkillAsync(filePath);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSkillAsync_文件不存在_应该返回false()
    {
        // Arrange
        var filePath = Path.Combine(_testSkillsDirectory, "nonexistent.md");

        // Act
        var result = await _writer.DeleteSkillAsync(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_存在的技能_应该返回true()
    {
        // Arrange
        var content = "测试内容";
        await _writer.SaveSkillAsync("test", "existing", content);

        // Act
        var exists = await _writer.ExistsAsync("test", "existing");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_不存在的技能_应该返回false()
    {
        // Act
        var exists = await _writer.ExistsAsync("test", "nonexistent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetSkillPath_应该返回正确路径()
    {
        // Act
        var path = _writer.GetSkillPath("dev", "api-helper");

        // Assert
        path.Should().EndWith(Path.Combine("dev", "api-helper.md"));
        path.Should().Contain(_testSkillsDirectory);
    }
}
