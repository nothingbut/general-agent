using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Parsers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Skills.Tests.Loaders;

public class FileSystemSkillLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemSkillLoader _loader;
    private readonly ILogger<FileSystemSkillLoader> _loggerMock;

    public FileSystemSkillLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _loggerMock = Substitute.For<ILogger<FileSystemSkillLoader>>();
        var parser = new MarkdownSkillParser();
        _loader = new FileSystemSkillLoader(parser, _loggerMock);
    }

    [Fact]
    public async Task LoadFromDirectory_ValidSkills_ReturnsSkills()
    {
        // Arrange
        var skillContent = """
            ---
            name: test_skill
            description: 测试技能
            parameters: []
            ---
            测试内容
            """;

        var skillPath = Path.Combine(_tempDir, "test_skill.md");
        await File.WriteAllTextAsync(skillPath, skillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("test_skill");
        result.Value[0].Description.Should().Be("测试技能");
    }

    [Fact]
    public async Task LoadFromDirectory_WithNamespace_SetsNamespace()
    {
        // Arrange
        var personalDir = Path.Combine(_tempDir, "personal");
        Directory.CreateDirectory(personalDir);

        var skillContent = """
            ---
            name: greeting
            description: 问候技能
            parameters: []
            ---
            你好！
            """;

        var skillPath = Path.Combine(personalDir, "greeting.md");
        await File.WriteAllTextAsync(skillPath, skillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Namespace.Should().Be("personal");
        result.Value[0].FullName.Should().Be("personal:greeting");
    }

    [Fact]
    public async Task LoadFromDirectory_NestedNamespace_UsesFullPath()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempDir, "work", "meetings");
        Directory.CreateDirectory(nestedDir);

        var skillContent = """
            ---
            name: schedule
            description: 安排会议
            parameters: []
            ---
            会议安排
            """;

        var skillPath = Path.Combine(nestedDir, "schedule.md");
        await File.WriteAllTextAsync(skillPath, skillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value![0].Namespace.Should().Be("work.meetings");
        result.Value[0].FullName.Should().Be("work.meetings:schedule");
    }

    [Fact]
    public async Task LoadFromDirectory_EmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadFromDirectory_InvalidFiles_SkipsWithLog()
    {
        // Arrange
        var validSkillContent = """
            ---
            name: valid
            description: 有效技能
            parameters: []
            ---
            内容
            """;

        var invalidSkillContent = "无效的技能文件，没有 frontmatter";

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "valid.md"), validSkillContent);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "invalid.md"), invalidSkillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("valid");

        // 验证记录了警告日志
        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("invalid.md")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadFromDirectory_WithIgnoreFile_SkipsIgnoredFiles()
    {
        // Arrange
        var ignoreContent = """
            # 忽略草稿
            draft_*.md
            _*.md
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".ignore"), ignoreContent);

        var skills = new[]
        {
            ("skill1.md", "skill1", "技能1"),
            ("draft_skill2.md", "skill2", "技能2"),
            ("_private.md", "private", "私有技能")
        };

        foreach (var (filename, name, desc) in skills)
        {
            var content = $"""
                ---
                name: {name}
                description: {desc}
                parameters: []
                ---
                内容
                """;
            await File.WriteAllTextAsync(Path.Combine(_tempDir, filename), content);
        }

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("skill1");
    }

    [Fact]
    public async Task LoadFromDirectory_NonExistentDirectory_ReturnsFailure()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_tempDir, "non_existent");

        // Act
        var result = await _loader.LoadFromDirectoryAsync(nonExistentDir);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task LoadFromDirectory_OnlyLoadsMarkdownFiles_SkipsOthers()
    {
        // Arrange
        var skillContent = """
            ---
            name: skill
            description: 技能
            parameters: []
            ---
            内容
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "skill.md"), skillContent);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "readme.txt"), "不是技能文件");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"), "{}");

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
