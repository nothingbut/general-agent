using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;
using FluentAssertions;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Skills.Tests;

/// <summary>
/// SkillTool 适配器单元测试
/// </summary>
public class SkillToolTests
{
    private readonly ISkillExecutor _executorMock;
    private readonly SkillToToolConverter _converter;

    public SkillToolTests()
    {
        _executorMock = Substitute.For<ISkillExecutor>();
        _converter = new SkillToToolConverter();
    }

    [Fact]
    public void SkillTool_ShouldImplementITool()
    {
        // Arrange
        var skill = CreateTestSkill();

        // Act
        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Assert
        skillTool.Should().BeAssignableTo<ITool>();
        skillTool.Name.Should().Be(skill.FullName);
        skillTool.Description.Should().Be(skill.Description);
    }

    [Fact]
    public void Name_WithNamespace_ShouldReturnFullName()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Namespace = "personal",
            Description = "问候技能",
            Template = "你好！",
            Parameters = Array.Empty<SkillParameter>()
        };

        // Act
        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Assert
        skillTool.Name.Should().Be("personal:greeting");
    }

    [Fact]
    public void Name_WithoutNamespace_ShouldReturnNameOnly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "你好！",
            Parameters = Array.Empty<SkillParameter>()
        };

        // Act
        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Assert
        skillTool.Name.Should().Be("greeting");
    }

    [Fact]
    public void GetDefinition_ShouldDelegateToConverter()
    {
        // Arrange
        var skill = CreateTestSkill();
        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var definition = skillTool.GetDefinition();

        // Assert
        definition.Should().NotBeNull();
        definition.Name.Should().Be(skill.FullName);
        definition.Description.Should().Be(skill.Description);
        definition.InputSchema.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDelegateToExecutor()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "张三"
        };
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = "TestProvider"
        };

        var expectedResult = Result<string>.Success("执行成功");
        _executorMock.ExecuteAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            sessionId,
            "TestProvider",
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var result = await skillTool.ExecuteAsync(arguments, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("执行成功");

        await _executorMock.Received(1).ExecuteAsync(
            skill,
            Arg.Is<Dictionary<string, object>>(d => d["user_name"].ToString() == "张三"),
            sessionId,
            "TestProvider",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapContext()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var providerName = "CustomProvider";
        var arguments = new Dictionary<string, object>();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = providerName
        };

        var expectedResult = Result<string>.Success("OK");
        _executorMock.ExecuteAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        await skillTool.ExecuteAsync(arguments, context);

        // Assert - 验证 SessionId 和 ProviderName 正确传递
        await _executorMock.Received(1).ExecuteAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            sessionId,
            providerName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNullProviderName_ShouldPassNull()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var arguments = new Dictionary<string, object>();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = null
        };

        var expectedResult = Result<string>.Success("OK");
        _executorMock.ExecuteAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        await skillTool.ExecuteAsync(arguments, context);

        // Assert
        await _executorMock.Received(1).ExecuteAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            sessionId,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldConvertArguments()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();

        // 使用 IReadOnlyDictionary 作为参数（模拟 ITool 接口调用）
        IReadOnlyDictionary<string, object> readOnlyArgs = new Dictionary<string, object>
        {
            ["user_name"] = "李四",
            ["age"] = 25
        };

        var context = new ToolExecutionContext
        {
            SessionId = sessionId
        };

        var expectedResult = Result<string>.Success("OK");
        _executorMock.ExecuteAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        await skillTool.ExecuteAsync(readOnlyArgs, context);

        // Assert - 验证转换为 Dictionary
        await _executorMock.Received(1).ExecuteAsync(
            skill,
            Arg.Is<Dictionary<string, object>>(d =>
                d.ContainsKey("user_name") &&
                d["user_name"].ToString() == "李四" &&
                d.ContainsKey("age") &&
                (int)d["age"] == 25),
            sessionId,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutorFails_ShouldReturnFailure()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var arguments = new Dictionary<string, object>();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId
        };

        var expectedResult = Result<string>.Failure("执行失败：参数无效");
        _executorMock.ExecuteAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var result = await skillTool.ExecuteAsync(arguments, context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("执行失败：参数无效");
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldDelegateToExecutor()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var arguments = new Dictionary<string, object>
        {
            ["question"] = "什么是AI？"
        };
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = "StreamProvider"
        };

        var streamChunks = new[] { "AI ", "是 ", "人工智能。" };
        _executorMock.ExecuteStreamAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            sessionId,
            "StreamProvider",
            Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(streamChunks));

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in skillTool.ExecuteStreamAsync(arguments, context))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(3);
        chunks[0].Should().Be("AI ");
        chunks[1].Should().Be("是 ");
        chunks[2].Should().Be("人工智能。");

        // 验证 ExecuteStreamAsync 被调用（通过检查返回值被访问）
        _executorMock.Received(1).ExecuteStreamAsync(
            skill,
            Arg.Is<Dictionary<string, object>>(d => d["question"].ToString() == "什么是AI？"),
            sessionId,
            "StreamProvider",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStreamAsync_WithCancellation_ShouldPassToken()
    {
        // Arrange
        var skill = CreateTestSkill();
        var sessionId = Guid.NewGuid();
        var arguments = new Dictionary<string, object>();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId
        };
        var cts = new CancellationTokenSource();

        var streamChunks = new[] { "chunk1", "chunk2" };
        _executorMock.ExecuteStreamAsync(
            Arg.Any<Skill>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(streamChunks));

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in skillTool.ExecuteStreamAsync(arguments, context, cts.Token))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(2);

        // 验证 ExecuteStreamAsync 被调用（通过检查返回值被访问）
        _executorMock.Received(1).ExecuteStreamAsync(
            skill,
            Arg.Any<Dictionary<string, object>>(),
            sessionId,
            Arg.Any<string?>(),
            cts.Token);
    }

    [Fact]
    public void GetDefinition_WithComplexSkill_ShouldReturnFullDefinition()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "reminder",
            Namespace = "personal",
            Description = "创建提醒",
            Template = "提醒：{{ task }} 在 {{ time }}",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "task",
                    Type = "string",
                    Required = true,
                    Description = "任务内容"
                },
                new()
                {
                    Name = "time",
                    Type = "string",
                    Required = true,
                    Description = "提醒时间"
                },
                new()
                {
                    Name = "priority",
                    Type = "string",
                    Required = false,
                    DefaultValue = "medium",
                    Description = "优先级"
                }
            }
        };

        var skillTool = new SkillTool(skill, _executorMock, _converter);

        // Act
        var definition = skillTool.GetDefinition();

        // Assert
        definition.Name.Should().Be("personal:reminder");
        definition.Description.Should().Be("创建提醒");

        var properties = definition.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties.Should().HaveCount(3);
        properties!["task"].Should().NotBeNull();
        properties["time"].Should().NotBeNull();
        properties["priority"].Should().NotBeNull();

        var required = definition.InputSchema["required"]?.AsArray();
        required.Should().NotBeNull();
        required.Should().HaveCount(2);
    }

    // 辅助方法：创建测试用的技能
    private Skill CreateTestSkill()
    {
        return new Skill
        {
            Name = "greeting",
            Namespace = "personal",
            Description = "向用户问候",
            Template = "你好 {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true,
                    Description = "用户名称"
                }
            }
        };
    }

    // 辅助方法：将数组转换为异步枚举
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
