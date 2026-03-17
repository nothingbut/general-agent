using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Parsers;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GeneralAgent.Application.Tests.Integration;

/// <summary>
/// 技能系统集成测试
/// 测试从加载到执行的完整工作流程
/// </summary>
public sealed class SkillSystemIntegrationTests : IDisposable
{
    private readonly string _skillsDirectory;
    private readonly ISkillParser _parser;
    private readonly ISkillLoader _loader;
    private readonly ISkillRegistry _registry;
    private readonly ISkillExecutor _executor;
    private readonly SkillService _skillService;
    private bool _skillsLoaded;

    public SkillSystemIntegrationTests()
    {
        // 获取 skills 目录的绝对路径
        // 测试运行时当前目录在 tests/GeneralAgent.Application.Tests/bin/Debug/net10.0/
        // skills 目录在项目根目录
        var testProjectDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.Combine(testProjectDir, "..", "..", "..", "..", "..");
        _skillsDirectory = Path.GetFullPath(Path.Combine(projectRoot, "skills"));

        // 创建真实的组件（不使用 Mock）
        _parser = new MarkdownSkillParser();
        _loader = new FileSystemSkillLoader(_parser, NullLogger<FileSystemSkillLoader>.Instance);
        _registry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        _executor = new SkillExecutor(NullLogger<SkillExecutor>.Instance);
        _skillService = new SkillService(_loader, _registry, _executor, NullLogger<SkillService>.Instance);
    }

    /// <summary>
    /// 确保技能已加载
    /// </summary>
    private async Task EnsureSkillsLoadedAsync()
    {
        if (_skillsLoaded)
            return;

        var result = await _skillService.LoadSkillsAsync(_skillsDirectory);
        result.IsSuccess.Should().BeTrue($"技能加载失败: {result.Error}");
        result.Value.Should().BeGreaterThan(0, "至少应该加载一个技能");

        _skillsLoaded = true;
    }

    #region 1. 技能加载集成测试

    [Fact]
    public async Task LoadSkills_FromFileSystem_Success()
    {
        // Arrange & Act
        var result = await _skillService.LoadSkillsAsync(_skillsDirectory);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterOrEqualTo(6, "应该加载至少 6 个示例技能");

        var allSkills = _skillService.GetAllSkills();
        allSkills.Should().NotBeEmpty();
        allSkills.Should().Contain(s => s.Name == "greeting");
        allSkills.Should().Contain(s => s.Name == "reminder");
        allSkills.Should().Contain(s => s.Name == "task");
    }

    [Fact]
    public async Task LoadSkills_VerifyNamespaces_Success()
    {
        // Arrange & Act
        await EnsureSkillsLoadedAsync();

        // Assert
        var personalSkills = _skillService.GetSkillsByNamespace("personal");
        personalSkills.Should().NotBeEmpty();
        personalSkills.Should().Contain(s => s.Name == "greeting");
        personalSkills.Should().Contain(s => s.Name == "reminder");

        var productivitySkills = _skillService.GetSkillsByNamespace("productivity");
        productivitySkills.Should().NotBeEmpty();
        productivitySkills.Should().Contain(s => s.Name == "task");
        productivitySkills.Should().Contain(s => s.Name == "meeting");

        var utilitiesSkills = _skillService.GetSkillsByNamespace("utilities");
        utilitiesSkills.Should().NotBeEmpty();
        utilitiesSkills.Should().Contain(s => s.Name == "calculate");
        utilitiesSkills.Should().Contain(s => s.Name == "format");
    }

    #endregion

    #region 2. 技能调用解析测试

    [Theory]
    [InlineData("@greeting user_name='张三'", "greeting", "张三")]
    [InlineData("@greeting user_name=\"李四\"", "greeting", "李四")]
    [InlineData("/greeting user_name='王五'", "greeting", "王五")]
    public void ParseSkillCall_SimpleParameter_Success(string input, string expectedSkill, string expectedUserName)
    {
        // Act
        var success = SkillCallParser.TryParse(input, out var skillCall);

        // Assert
        success.Should().BeTrue();
        skillCall.Should().NotBeNull();
        skillCall!.SkillName.Should().Be(expectedSkill);
        skillCall.Arguments.Should().ContainKey("user_name");
        skillCall.Arguments["user_name"].Should().Be(expectedUserName);
    }

    [Theory]
    [InlineData("@personal:greeting user_name='张三'", "personal:greeting")]
    [InlineData("@productivity:task title='测试'", "productivity:task")]
    [InlineData("/utilities:calculate expression='1+1'", "utilities:calculate")]
    public void ParseSkillCall_WithNamespace_Success(string input, string expectedSkillName)
    {
        // Act
        var success = SkillCallParser.TryParse(input, out var skillCall);

        // Assert
        success.Should().BeTrue();
        skillCall.Should().NotBeNull();
        skillCall!.SkillName.Should().Be(expectedSkillName);
    }

    [Theory]
    [InlineData("@reminder task='买牛奶' time='5pm' is_urgent=true", true)]
    [InlineData("@reminder task='买牛奶' time='5pm' is_urgent=false", false)]
    public void ParseSkillCall_BoolParameter_Success(string input, bool expectedIsUrgent)
    {
        // Act
        var success = SkillCallParser.TryParse(input, out var skillCall);

        // Assert
        success.Should().BeTrue();
        skillCall.Should().NotBeNull();
        skillCall!.Arguments.Should().ContainKey("is_urgent");
        skillCall.Arguments["is_urgent"].Should().Be(expectedIsUrgent);
    }

    [Theory]
    [InlineData("@meeting duration=60", 60)]
    [InlineData("@task estimated_hours=8", 8)]
    public void ParseSkillCall_IntParameter_Success(string input, int expectedValue)
    {
        // Act
        var success = SkillCallParser.TryParse(input, out var skillCall);

        // Assert
        success.Should().BeTrue();
        skillCall.Should().NotBeNull();
        var key = input.Contains("duration") ? "duration" : "estimated_hours";
        skillCall!.Arguments.Should().ContainKey(key);
        skillCall.Arguments[key].Should().Be(expectedValue);
    }

    [Fact]
    public void ParseSkillCall_NotSkillCall_ReturnsFalse()
    {
        // Arrange
        var inputs = new[]
        {
            "Hello world",
            "This is a normal message",
            "# Not a skill call"
        };

        foreach (var input in inputs)
        {
            // Act
            var success = SkillCallParser.TryParse(input, out var skillCall);

            // Assert
            success.Should().BeFalse($"'{input}' 不应该被识别为技能调用");
            skillCall.Should().BeNull();
        }
    }

    #endregion

    #region 3. 技能执行集成测试

    [Fact]
    public async Task ExecuteSkill_Greeting_WithTimeOfDay_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "张三",
            ["time_of_day"] = "morning"
        };

        // Act
        var result = _skillService.ExecuteSkill("greeting", arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        result.Value.Should().Contain("张三");
        result.Value.Should().Contain("早上好");
    }

    [Fact]
    public async Task ExecuteSkill_Reminder_WithUrgentFlag_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>
        {
            ["task"] = "买牛奶",
            ["time"] = "5pm",
            ["is_urgent"] = true
        };

        // Act
        var result = _skillService.ExecuteSkill("reminder", arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        result.Value.Should().Contain("买牛奶");
        result.Value.Should().Contain("5pm");
        result.Value.Should().Contain("紧急");
    }

    [Fact]
    public async Task ExecuteSkill_Task_WithPriorityAndTags_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>
        {
            ["title"] = "Review PR",
            ["priority"] = "high",
            ["tags"] = new[] { "bug", "urgent" }
        };

        // Act
        var result = _skillService.ExecuteSkill("task", arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        result.Value.Should().Contain("Review PR");
        result.Value.Should().ContainAny("high", "High"); // Scriban capitalize 会首字母大写
        result.Value.Should().Contain("#bug");
        result.Value.Should().Contain("#urgent");
    }

    [Fact]
    public async Task ExecuteSkill_WithNamespace_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "李四"
        };

        // Act - 使用完整命名空间
        var result = _skillService.ExecuteSkill("personal:greeting", arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        result.Value.Should().Contain("李四");
    }

    [Fact]
    public async Task ExecuteSkill_Format_WithStringFilters_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>
        {
            ["text"] = "hello world",
            ["format_type"] = "uppercase",
            ["trim_whitespace"] = true
        };

        // Act
        var result = _skillService.ExecuteSkill("format", arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        result.Value.Should().Contain("hello world");
        result.Value.Should().Contain("HELLO WORLD");
    }

    #endregion

    #region 4. 错误处理测试

    [Fact]
    public async Task ExecuteSkill_NonExistentSkill_ReturnsFailure()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>();

        // Act
        var result = _skillService.ExecuteSkill("nonexistent_skill", arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task ExecuteSkill_MissingRequiredParameter_ReturnsFailure()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var arguments = new Dictionary<string, object>(); // 缺少 user_name

        // Act
        var result = _skillService.ExecuteSkill("greeting", arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().ContainAny("必需参数", "必填"); // 错误消息可能是"必需"或"必填"
    }

    [Fact]
    public void ExecuteSkill_BeforeLoadingSkills_ReturnsFailure()
    {
        // Arrange - 创建新的服务实例，不加载技能
        var freshRegistry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        var freshService = new SkillService(_loader, freshRegistry, _executor, NullLogger<SkillService>.Instance);
        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "张三"
        };

        // Act
        var result = freshService.ExecuteSkill("greeting", arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("未初始化");
    }

    [Fact]
    public async Task LoadSkills_NonExistentDirectory_ReturnsFailure()
    {
        // Arrange - 创建新的服务实例
        var freshRegistry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        var freshService = new SkillService(_loader, freshRegistry, _executor, NullLogger<SkillService>.Instance);
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = await freshService.LoadSkillsAsync(nonExistentDir);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    #endregion

    #region 5. 端到端测试

    [Fact]
    public async Task EndToEnd_ParseAndExecuteSkillCall_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var input = "@greeting user_name='王五' time_of_day='evening'";

        // Act - 解析技能调用
        var parseSuccess = SkillCallParser.TryParse(input, out var skillCall);
        parseSuccess.Should().BeTrue();

        // Act - 执行技能
        var executeResult = _skillService.ExecuteSkill(skillCall!.SkillName, skillCall.Arguments);

        // Assert
        executeResult.IsSuccess.Should().BeTrue();
        executeResult.Value.Should().Contain("王五");
        executeResult.Value.Should().Contain("晚上好");
    }

    [Fact]
    public async Task EndToEnd_ComplexSkillCall_WithAllParameterTypes_Success()
    {
        // Arrange
        await EnsureSkillsLoadedAsync();
        var input = "@task title='Fix critical bug' priority='critical' estimated_hours=4";

        // Act - 解析
        var parseSuccess = SkillCallParser.TryParse(input, out var skillCall);
        parseSuccess.Should().BeTrue();

        // 添加数组参数（模拟从 LLM 获取）
        skillCall!.Arguments["tags"] = new[] { "bug", "p0", "hotfix" };

        // Act - 执行
        var executeResult = _skillService.ExecuteSkill(skillCall.SkillName, skillCall.Arguments);

        // Assert
        executeResult.IsSuccess.Should().BeTrue();
        var output = executeResult.Value;
        output.Should().Contain("Fix critical bug");
        output.Should().Contain("CRITICAL");
        output.Should().Contain("4 小时");
        output.Should().Contain("#bug");
        output.Should().Contain("#p0");
        output.Should().Contain("#hotfix");
    }

    #endregion

    public void Dispose()
    {
        // 清理资源（如果需要）
    }
}
