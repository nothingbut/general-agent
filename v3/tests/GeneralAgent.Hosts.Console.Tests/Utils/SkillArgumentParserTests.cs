using GeneralAgent.Hosts.Console.Utils;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Hosts.Console.Tests.Utils;

/// <summary>
/// SkillArgumentParser 测试
/// </summary>
public class SkillArgumentParserTests
{
    [Fact]
    public void Parse_WithRequiredStringParameter_ShouldSucceed()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "name",
                    Type = "string",
                    Required = true,
                    Description = "User name"
                }
            }
        };

        var args = new[] { "name=Alice" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Alice", result.Value!["name"]);
    }

    [Fact]
    public void Parse_WithMissingRequiredParameter_ShouldFail()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "name",
                    Type = "string",
                    Required = true,
                    Description = "User name"
                }
            }
        };

        var args = Array.Empty<string>();

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("缺少必填参数", result.Error!);
    }

    [Fact]
    public void Parse_WithOptionalParameterAndDefaultValue_ShouldUseDefault()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "count",
                    Type = "int",
                    Required = false,
                    Description = "Count",
                    DefaultValue = 10
                }
            }
        };

        var args = Array.Empty<string>();

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(10, result.Value!["count"]);
    }

    [Fact]
    public void Parse_WithIntParameter_ShouldConvertType()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "age",
                    Type = "int",
                    Required = true,
                    Description = "Age"
                }
            }
        };

        var args = new[] { "age=25" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(25, result.Value!["age"]);
    }

    [Fact]
    public void Parse_WithBoolParameter_ShouldConvertType()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "enabled",
                    Type = "bool",
                    Required = true,
                    Description = "Enabled"
                }
            }
        };

        var args = new[] { "enabled=true" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(true, result.Value!["enabled"]);
    }

    [Fact]
    public void Parse_WithInvalidTypeValue_ShouldFail()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "age",
                    Type = "int",
                    Required = true,
                    Description = "Age"
                }
            }
        };

        var args = new[] { "age=not_a_number" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("格式错误", result.Error!);
    }

    [Fact]
    public void Parse_WithExtraParameter_ShouldFail()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "name",
                    Type = "string",
                    Required = true,
                    Description = "Name"
                }
            }
        };

        var args = new[] { "name=Alice", "extra=value" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("未知参数", result.Error!);
    }

    [Fact]
    public void Parse_WithQuotedValue_ShouldRemoveQuotes()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "message",
                    Type = "string",
                    Required = true,
                    Description = "Message"
                }
            }
        };

        var args = new[] { "message=\"Hello World\"" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello World", result.Value!["message"]);
    }

    [Fact]
    public void Parse_WithMultipleParameters_ShouldParseAll()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "name",
                    Type = "string",
                    Required = true,
                    Description = "Name"
                },
                new SkillParameter
                {
                    Name = "age",
                    Type = "int",
                    Required = true,
                    Description = "Age"
                },
                new SkillParameter
                {
                    Name = "active",
                    Type = "bool",
                    Required = false,
                    DefaultValue = true,
                    Description = "Active"
                }
            }
        };

        var args = new[] { "name=Bob", "age=30" };

        // Act
        var result = SkillArgumentParser.Parse(skill, args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal("Bob", result.Value!["name"]);
        Assert.Equal(30, result.Value!["age"]);
        Assert.Equal(true, result.Value!["active"]);
    }

    [Fact]
    public void BuildUsageHint_WithRequiredAndOptionalParameters_ShouldGenerateCorrectHint()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "test",
            Description = "Test skill",
            Template = "Template",
            Parameters = new List<SkillParameter>
            {
                new SkillParameter
                {
                    Name = "required",
                    Type = "string",
                    Required = true,
                    Description = "Required param"
                },
                new SkillParameter
                {
                    Name = "optional",
                    Type = "int",
                    Required = false,
                    Description = "Optional param"
                }
            }
        };

        // Act
        var hint = SkillArgumentParser.BuildUsageHint(skill);

        // Assert
        Assert.Contains("required=<string>", hint);
        Assert.Contains("[optional=<int>]", hint);
    }
}
