using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// SkillService 单元测试
/// 测试技能加载后自动注册为工具的功能
/// </summary>
public sealed class SkillServiceTests : IDisposable
{
    private readonly ISkillLoader _mockLoader;
    private readonly Infrastructure.Skills.Registry.ISkillRegistry _mockSkillRegistry;
    private readonly ISkillExecutor _mockExecutor;
    private readonly ToolRegistry _toolRegistry;
    private readonly SkillToToolConverter _converter;
    private readonly ILogger<SkillService> _mockLogger;
    private readonly string _tempDirectory;

    public SkillServiceTests()
    {
        _mockLoader = Substitute.For<ISkillLoader>();
        _mockSkillRegistry = Substitute.For<Infrastructure.Skills.Registry.ISkillRegistry>();
        _mockExecutor = Substitute.For<ISkillExecutor>();
        _toolRegistry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
        _converter = new SkillToToolConverter();
        _mockLogger = Substitute.For<ILogger<SkillService>>();

        // 创建临时目录用于测试
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"skills_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        // 清理临时目录
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_ShouldRegisterSkillsAsTools()
    {
        // Arrange
        var skills = new List<Skill>
        {
            CreateTestSkill("greeting", "personal"),
            CreateTestSkill("reminder", "personal")
        };

        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Success(skills));

        _mockSkillRegistry.RegisterMany(Arg.Any<List<Skill>>())
            .Returns(Result<int>.Success(2));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        var result = await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        // 验证技能已注册到 ToolRegistry
        _toolRegistry.Count.Should().Be(2);
        _toolRegistry.GetTool("personal:greeting").Should().NotBeNull();
        _toolRegistry.GetTool("personal:reminder").Should().NotBeNull();
    }

    [Fact]
    public async Task LoadSkillsAsync_ShouldCreateSkillToolForEachSkill()
    {
        // Arrange
        var greeting = CreateTestSkill("greeting", "personal");
        var reminder = CreateTestSkill("reminder", "personal");
        var skills = new List<Skill> { greeting, reminder };

        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Success(skills));

        _mockSkillRegistry.RegisterMany(Arg.Any<List<Skill>>())
            .Returns(Result<int>.Success(2));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert
        var greetingTool = _toolRegistry.GetTool("personal:greeting");
        greetingTool.Should().NotBeNull();
        greetingTool.Should().BeOfType<SkillTool>();
        greetingTool!.Name.Should().Be("personal:greeting");
        greetingTool.Description.Should().Be("向用户问候");

        var reminderTool = _toolRegistry.GetTool("personal:reminder");
        reminderTool.Should().NotBeNull();
        reminderTool.Should().BeOfType<SkillTool>();
        reminderTool!.Name.Should().Be("personal:reminder");
    }

    [Fact]
    public async Task LoadSkillsAsync_WithNoSkills_ShouldNotRegisterAnyTools()
    {
        // Arrange
        var emptySkills = new List<Skill>();

        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Success(emptySkills));

        _mockSkillRegistry.RegisterMany(Arg.Any<List<Skill>>())
            .Returns(Result<int>.Success(0));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        var result = await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _toolRegistry.Count.Should().Be(0);
    }

    [Fact]
    public async Task LoadSkillsAsync_WhenLoaderFails_ShouldReturnFailure()
    {
        // Arrange
        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Failure("加载失败：文件不存在"));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        var result = await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("加载失败");
        _toolRegistry.Count.Should().Be(0);
    }

    [Fact]
    public async Task LoadSkillsAsync_ShouldLogInformationMessages()
    {
        // Arrange
        var skills = new List<Skill>
        {
            CreateTestSkill("greeting", "personal")
        };

        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Success(skills));

        _mockSkillRegistry.RegisterMany(Arg.Any<List<Skill>>())
            .Returns(Result<int>.Success(1));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert - 验证日志记录
        _mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadSkillsAsync_WithDuplicateSkillNames_ShouldOverwriteTools()
    {
        // Arrange
        var skill1 = CreateTestSkill("greeting", "personal");
        var skill2 = new Skill
        {
            Name = "greeting",
            Namespace = "personal",
            Description = "新的问候技能",
            Template = "Hello!",
            Parameters = Array.Empty<SkillParameter>()
        };

        var skills = new List<Skill> { skill1, skill2 };

        _mockLoader.LoadFromDirectoryAsync(_tempDirectory)
            .Returns(Result<List<Skill>>.Success(skills));

        _mockSkillRegistry.RegisterMany(Arg.Any<List<Skill>>())
            .Returns(Result<int>.Success(2));

        var service = new SkillService(
            _mockLoader,
            _mockSkillRegistry,
            _mockExecutor,
            _toolRegistry,
            _converter,
            _mockLogger);

        // Act
        await service.LoadSkillsAsync(_tempDirectory, CancellationToken.None);

        // Assert
        _toolRegistry.Count.Should().Be(1); // 只有一个工具（被覆盖）
        var tool = _toolRegistry.GetTool("personal:greeting");
        tool.Should().NotBeNull();
        tool!.Description.Should().Be("新的问候技能"); // 使用最新的描述
    }

    #region Helper Methods

    /// <summary>
    /// 创建测试用的技能对象
    /// </summary>
    private Skill CreateTestSkill(string name, string? namespaceName = null)
    {
        return new Skill
        {
            Name = name,
            Namespace = namespaceName,
            Description = name == "greeting" ? "向用户问候" : "创建提醒",
            Template = $"Template for {name}",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "param1",
                    Type = "string",
                    Required = true,
                    Description = "参数1"
                }
            }
        };
    }

    #endregion
}
