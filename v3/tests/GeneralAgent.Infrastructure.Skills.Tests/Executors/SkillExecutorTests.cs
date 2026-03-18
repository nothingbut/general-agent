using System.Collections.Generic;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Skills.Tests.Executors;

public class SkillExecutorTests
{
    private readonly SkillExecutor _executor;
    private readonly ILogger<SkillExecutor> _loggerMock;

    public SkillExecutorTests()
    {
        _loggerMock = Substitute.For<ILogger<SkillExecutor>>();
        _executor = new SkillExecutor(_loggerMock);
    }

    [Fact]
    public void Execute_SimpleTemplate_RendersCorrectly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "你好 {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "张三"
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("你好 张三！");
    }

    [Fact]
    public void Execute_MissingRequiredParameter_ReturnsFailure()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "你好 {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>();

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("user_name");
        result.Error.Should().Contain("必填");
    }

    [Fact]
    public void Execute_WithDefaultValue_UsesDefault()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "{{ greeting }} {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "greeting",
                    Type = "string",
                    Required = false,
                    DefaultValue = "你好"
                },
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "李四"
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("你好 李四！");
    }

    [Fact]
    public void Execute_ComplexTemplate_WithLoops()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "list_items",
            Description = "列出项目",
            Template = """
                项目列表：
                {{ for item in items }}
                - {{ item }}
                {{ end }}
                """,
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "items",
                    Type = "array",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["items"] = new[] { "任务1", "任务2", "任务3" }
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("任务1");
        result.Value.Should().Contain("任务2");
        result.Value.Should().Contain("任务3");
    }

    [Fact]
    public void Execute_WithConditionals_RendersCorrectly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "conditional",
            Description = "条件渲染",
            Template = """
                {{ if is_vip }}
                尊贵的 {{ name }} 用户
                {{ else }}
                普通用户 {{ name }}
                {{ end }}
                """,
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "name",
                    Type = "string",
                    Required = true
                },
                new()
                {
                    Name = "is_vip",
                    Type = "bool",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["name"] = "王五",
            ["is_vip"] = true
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("尊贵的 王五 用户");
    }

    [Fact]
    public void Execute_InvalidTemplatedSyntax_ReturnsFailure()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "invalid",
            Description = "无效模板",
            Template = "{{ unclosed tag",
            Parameters = new List<SkillParameter>()
        };

        var arguments = new Dictionary<string, object>();

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("模板");
    }

    [Fact]
    public void Execute_TypeMismatch_ReturnsFailure()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "测试",
            Template = "{{ value }}",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "value",
                    Type = "int",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["value"] = "not_a_number"
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("类型");
    }

    [Fact]
    public void Execute_EmptyArguments_WithOptionalParams_Succeeds()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "optional",
            Description = "可选参数",
            Template = "Hello World!",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "optional_param",
                    Type = "string",
                    Required = false,
                    DefaultValue = "default"
                }
            }
        };

        var arguments = new Dictionary<string, object>();

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello World!");
    }

    [Fact]
    public void Execute_NestedObjectAccess_RendersCorrectly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "nested",
            Description = "嵌套对象",
            Template = "用户: {{ user.name }}, 年龄: {{ user.age }}",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user",
                    Type = "object",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["user"] = new Dictionary<string, object>
            {
                ["name"] = "赵六",
                ["age"] = 25
            }
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("赵六");
        result.Value.Should().Contain("25");
    }

    [Fact]
    public void Execute_MultipleParameters_AllReplaced()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "multi",
            Description = "多参数",
            Template = "{{ param1 }}, {{ param2 }}, {{ param3 }}",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "param1", Type = "string", Required = true },
                new() { Name = "param2", Type = "string", Required = true },
                new() { Name = "param3", Type = "string", Required = true }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["param1"] = "A",
            ["param2"] = "B",
            ["param3"] = "C"
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("A, B, C");
    }

    [Fact]
    public void Execute_WithFilters_AppliesFilters()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "filters",
            Description = "过滤器",
            Template = "{{ name | upcase }}, {{ count | plus 10 }}",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "name", Type = "string", Required = true },
                new() { Name = "count", Type = "int", Required = true }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["name"] = "hello",
            ["count"] = 5
        };

        // Act
        var result = _executor.Execute(skill, arguments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("HELLO");
        result.Value.Should().Contain("15");
    }
}
