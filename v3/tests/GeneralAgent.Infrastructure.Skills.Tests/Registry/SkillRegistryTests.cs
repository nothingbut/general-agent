using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeneralAgent.Infrastructure.Skills.Tests.Registry;

public class SkillRegistryTests
{
    private readonly SkillRegistry _registry;
    private readonly Mock<ILogger<SkillRegistry>> _loggerMock;

    public SkillRegistryTests()
    {
        _loggerMock = new Mock<ILogger<SkillRegistry>>();
        _registry = new SkillRegistry(_loggerMock.Object);
    }

    [Fact]
    public void Register_ValidSkill_ReturnsSuccess()
    {
        // Arrange
        var skill = CreateSkill("greeting", "问候技能");

        // Act
        var result = _registry.Register(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Register_DuplicateSkill_ReturnsFailure()
    {
        // Arrange
        var skill1 = CreateSkill("greeting", "问候技能");
        var skill2 = CreateSkill("greeting", "另一个问候技能");

        _registry.Register(skill1);

        // Act
        var result = _registry.Register(skill2);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("已存在");
    }

    [Fact]
    public void GetByFullName_ExistingSkill_ReturnsSkill()
    {
        // Arrange
        var skill = CreateSkill("greeting", "问候技能", "personal");
        _registry.Register(skill);

        // Act
        var result = _registry.GetByFullName("personal:greeting");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("greeting");
        result.Namespace.Should().Be("personal");
    }

    [Fact]
    public void GetByFullName_NonExistentSkill_ReturnsNull()
    {
        // Act
        var result = _registry.GetByFullName("nonexistent:skill");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetByName_WithoutNamespace_ReturnsSkill()
    {
        // Arrange
        var skill = CreateSkill("greeting", "问候技能");
        _registry.Register(skill);

        // Act
        var result = _registry.GetByName("greeting");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("greeting");
    }

    [Fact]
    public void GetByName_WithNamespace_ReturnsSkill()
    {
        // Arrange
        var skill = CreateSkill("greeting", "问候技能", "personal");
        _registry.Register(skill);

        // Act
        var result = _registry.GetByName("greeting", "personal");

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("personal:greeting");
    }

    [Fact]
    public void GetByName_MultipleSkillsSameName_ReturnsFirstWithoutNamespace()
    {
        // Arrange
        var skill1 = CreateSkill("greeting", "默认问候", null);
        var skill2 = CreateSkill("greeting", "个人问候", "personal");
        var skill3 = CreateSkill("greeting", "工作问候", "work");

        _registry.Register(skill1);
        _registry.Register(skill2);
        _registry.Register(skill3);

        // Act
        var result = _registry.GetByName("greeting");

        // Assert
        result.Should().NotBeNull();
        result!.Namespace.Should().BeNull();
    }

    [Fact]
    public void GetAllSkills_ReturnsAllRegisteredSkills()
    {
        // Arrange
        var skills = new[]
        {
            CreateSkill("greeting", "问候", "personal"),
            CreateSkill("task", "任务管理", "work"),
            CreateSkill("note", "笔记", "personal")
        };

        foreach (var skill in skills)
        {
            _registry.Register(skill);
        }

        // Act
        var result = _registry.GetAllSkills();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.Name == "greeting");
        result.Should().Contain(s => s.Name == "task");
        result.Should().Contain(s => s.Name == "note");
    }

    [Fact]
    public void GetAllSkills_EmptyRegistry_ReturnsEmptyList()
    {
        // Act
        var result = _registry.GetAllSkills();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void RegisterMany_ValidSkills_ReturnsSuccessCount()
    {
        // Arrange
        var skills = new[]
        {
            CreateSkill("skill1", "技能1"),
            CreateSkill("skill2", "技能2"),
            CreateSkill("skill3", "技能3")
        };

        // Act
        var result = _registry.RegisterMany(skills);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
        _registry.GetAllSkills().Should().HaveCount(3);
    }

    [Fact]
    public void RegisterMany_WithDuplicates_SkipsDuplicates()
    {
        // Arrange
        var skill1 = CreateSkill("greeting", "问候1");
        _registry.Register(skill1);

        var skills = new[]
        {
            CreateSkill("greeting", "问候2"), // 重复
            CreateSkill("task", "任务"),
            CreateSkill("note", "笔记")
        };

        // Act
        var result = _registry.RegisterMany(skills);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2); // 只注册了 task 和 note
        _registry.GetAllSkills().Should().HaveCount(3); // 总共3个（包括之前的 greeting）
    }

    [Fact]
    public void GetSkillsByNamespace_ReturnsMatchingSkills()
    {
        // Arrange
        var skills = new[]
        {
            CreateSkill("greeting", "问候", "personal"),
            CreateSkill("reminder", "提醒", "personal"),
            CreateSkill("task", "任务", "work"),
            CreateSkill("meeting", "会议", "work")
        };

        foreach (var skill in skills)
        {
            _registry.Register(skill);
        }

        // Act
        var personalSkills = _registry.GetSkillsByNamespace("personal");

        // Assert
        personalSkills.Should().HaveCount(2);
        personalSkills.Should().OnlyContain(s => s.Namespace == "personal");
    }

    [Fact]
    public void GetSkillsByNamespace_NoMatchingSkills_ReturnsEmpty()
    {
        // Arrange
        var skill = CreateSkill("greeting", "问候", "personal");
        _registry.Register(skill);

        // Act
        var result = _registry.GetSkillsByNamespace("work");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Clear_RemovesAllSkills()
    {
        // Arrange
        var skills = new[]
        {
            CreateSkill("skill1", "技能1"),
            CreateSkill("skill2", "技能2")
        };

        foreach (var skill in skills)
        {
            _registry.Register(skill);
        }

        // Act
        _registry.Clear();

        // Assert
        _registry.GetAllSkills().Should().BeEmpty();
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentRegistration_AllSkillsRegistered()
    {
        // Arrange
        var skills = Enumerable.Range(0, 100)
            .Select(i => CreateSkill($"skill_{i}", $"技能{i}"))
            .ToList();

        // Act - 并发注册
        var tasks = skills.Select(skill => Task.Run(() => _registry.Register(skill)));
        await Task.WhenAll(tasks);

        // Assert
        _registry.GetAllSkills().Should().HaveCount(100);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentReadWrite_NoExceptions()
    {
        // Arrange
        var skill = CreateSkill("test", "测试技能");
        _registry.Register(skill);

        // Act - 并发读写
        var writeTasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => _registry.Register(CreateSkill($"skill_{i}", $"技能{i}"))))
            .ToArray();

        var readTasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => _registry.GetByName("test")))
            .ToArray();

        await Task.WhenAll(writeTasks);
        await Task.WhenAll(readTasks);

        // Assert - 没有异常抛出，并且可以读取技能
        var result = _registry.GetByName("test");
        result.Should().NotBeNull();
    }

    private static Skill CreateSkill(string name, string description, string? ns = null)
    {
        return new Skill
        {
            Name = name,
            Description = description,
            Template = "测试模板",
            Parameters = new List<SkillParameter>(),
            Namespace = ns
        };
    }
}
