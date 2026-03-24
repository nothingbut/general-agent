using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Models;
using FluentAssertions;

namespace GeneralAgent.Infrastructure.Skills.Tests.Converters;

/// <summary>
/// SkillToToolConverter 单元测试
/// </summary>
public class SkillToToolConverterTests
{
    private readonly SkillToToolConverter _converter;

    public SkillToToolConverterTests()
    {
        _converter = new SkillToToolConverter();
    }

    [Fact]
    public void Convert_BasicSkill_ShouldCreateToolDefinition()
    {
        // Arrange
        var skill = new Skill
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

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        toolDef.Name.Should().Be("personal:greeting");
        toolDef.Description.Should().Be("向用户问候");
        toolDef.InputSchema.Should().NotBeNull();

        var schema = toolDef.InputSchema;
        schema["type"]?.GetValue<string>().Should().Be("object");

        var properties = schema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties.Should().ContainKey("user_name");

        var userNameProp = properties!["user_name"]?.AsObject();
        userNameProp.Should().NotBeNull();
        userNameProp!["type"]?.GetValue<string>().Should().Be("string");
        userNameProp["description"]?.GetValue<string>().Should().Be("用户名称");

        var required = schema["required"]?.AsArray();
        required.Should().NotBeNull();
        required.Should().HaveCount(1);
        required![0]?.GetValue<string>().Should().Be("user_name");
    }

    [Fact]
    public void Convert_MultipleTypes_ShouldMapCorrectly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "multi_type",
            Description = "多种类型参数",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "str_param", Type = "string", Required = true, Description = "字符串参数" },
                new() { Name = "num_param", Type = "number", Required = true, Description = "数字参数" },
                new() { Name = "bool_param", Type = "boolean", Required = true, Description = "布尔参数" },
                new() { Name = "arr_param", Type = "array", Required = true, Description = "数组参数" },
                new() { Name = "obj_param", Type = "object", Required = true, Description = "对象参数" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();

        properties!["str_param"]?["type"]?.GetValue<string>().Should().Be("string");
        properties["num_param"]?["type"]?.GetValue<string>().Should().Be("number");
        properties["bool_param"]?["type"]?.GetValue<string>().Should().Be("boolean");
        properties["arr_param"]?["type"]?.GetValue<string>().Should().Be("array");
        properties["obj_param"]?["type"]?.GetValue<string>().Should().Be("object");
    }

    [Fact]
    public void Convert_TypeVariations_ShouldMapCorrectly()
    {
        // Arrange - 测试类型名称的各种写法
        var skill = new Skill
        {
            Name = "type_variations",
            Description = "类型变体",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "int_param", Type = "int", Required = true, Description = "整数" },
                new() { Name = "integer_param", Type = "integer", Required = true, Description = "整数2" },
                new() { Name = "bool_param", Type = "bool", Required = true, Description = "布尔" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();

        properties!["int_param"]?["type"]?.GetValue<string>().Should().Be("number");
        properties["integer_param"]?["type"]?.GetValue<string>().Should().Be("number");
        properties["bool_param"]?["type"]?.GetValue<string>().Should().Be("boolean");
    }

    [Fact]
    public void Convert_RequiredParams_ShouldIncludeInRequired()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "required_test",
            Description = "必填参数测试",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "param1", Type = "string", Required = true, Description = "必填1" },
                new() { Name = "param2", Type = "string", Required = false, Description = "可选" },
                new() { Name = "param3", Type = "string", Required = true, Description = "必填2" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var required = toolDef.InputSchema["required"]?.AsArray();
        required.Should().NotBeNull();
        required.Should().HaveCount(2);

        var requiredList = required!.Select(n => n?.GetValue<string>()).ToList();
        requiredList.Should().Contain("param1");
        requiredList.Should().Contain("param3");
        requiredList.Should().NotContain("param2");
    }

    [Fact]
    public void Convert_OptionalParams_ShouldNotIncludeInRequired()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "optional_test",
            Description = "可选参数测试",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "param1", Type = "string", Required = false, Description = "可选1" },
                new() { Name = "param2", Type = "string", Required = false, Description = "可选2" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var schema = toolDef.InputSchema;

        // 当没有必填参数时，required 字段应该不存在或为空
        if (schema.ContainsKey("required"))
        {
            var required = schema["required"]?.AsArray();
            required.Should().BeEmpty();
        }
    }

    [Fact]
    public void Convert_WithDefaults_ShouldIncludeDefaultValues()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "defaults_test",
            Description = "默认值测试",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "str_param", Type = "string", Required = false, DefaultValue = "默认值", Description = "字符串" },
                new() { Name = "num_param", Type = "number", Required = false, DefaultValue = 42, Description = "数字" },
                new() { Name = "bool_param", Type = "boolean", Required = false, DefaultValue = true, Description = "布尔" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();

        properties!["str_param"]?["default"]?.GetValue<string>().Should().Be("默认值");
        properties["num_param"]?["default"]?.GetValue<int>().Should().Be(42);
        properties["bool_param"]?["default"]?.GetValue<bool>().Should().Be(true);
    }

    [Fact]
    public void Convert_WithNamespace_ShouldUseFullName()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Namespace = "personal",
            Description = "问候技能",
            Template = "测试",
            Parameters = new List<SkillParameter>()
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        toolDef.Name.Should().Be("personal:greeting");
    }

    [Fact]
    public void Convert_WithoutNamespace_ShouldUseNameOnly()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "测试",
            Parameters = new List<SkillParameter>()
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        toolDef.Name.Should().Be("greeting");
    }

    [Fact]
    public void Convert_NoParameters_ShouldCreateEmptySchema()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "simple",
            Description = "简单技能",
            Template = "测试",
            Parameters = new List<SkillParameter>()
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var schema = toolDef.InputSchema;
        schema["type"]?.GetValue<string>().Should().Be("object");

        var properties = schema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties.Should().BeEmpty();

        // 没有参数时，不应该有 required 字段或为空
        if (schema.ContainsKey("required"))
        {
            var required = schema["required"]?.AsArray();
            required.Should().BeEmpty();
        }
    }

    [Fact]
    public void Convert_UnknownType_ShouldFallbackToString()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "unknown_type",
            Description = "未知类型",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "weird_param", Type = "weird_type", Required = true, Description = "奇怪的类型" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties!["weird_param"]?["type"]?.GetValue<string>().Should().Be("string");
    }

    [Fact]
    public void Convert_CaseInsensitiveTypes_ShouldMapCorrectly()
    {
        // Arrange - 测试大小写不敏感
        var skill = new Skill
        {
            Name = "case_test",
            Description = "大小写测试",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "upper_string", Type = "STRING", Required = true, Description = "大写" },
                new() { Name = "mixed_number", Type = "Number", Required = true, Description = "混合" },
                new() { Name = "lower_bool", Type = "boolean", Required = true, Description = "小写" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();

        properties!["upper_string"]?["type"]?.GetValue<string>().Should().Be("string");
        properties["mixed_number"]?["type"]?.GetValue<string>().Should().Be("number");
        properties["lower_bool"]?["type"]?.GetValue<string>().Should().Be("boolean");
    }

    [Fact]
    public void Convert_ParameterWithoutDescription_ShouldUseEmptyString()
    {
        // Arrange
        var skill = new Skill
        {
            Name = "no_desc",
            Description = "无描述参数",
            Template = "测试",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "param1", Type = "string", Required = true } // Description = null
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        var properties = toolDef.InputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties!["param1"]?["description"]?.GetValue<string>().Should().Be("");
    }

    [Fact]
    public void Convert_ComplexSkill_ShouldGenerateCorrectSchema()
    {
        // Arrange - 综合测试
        var skill = new Skill
        {
            Name = "reminder",
            Namespace = "personal",
            Description = "创建提醒",
            Template = "提醒：{{ task }} 在 {{ time }}",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "task", Type = "string", Required = true, Description = "任务内容" },
                new() { Name = "time", Type = "string", Required = true, Description = "提醒时间" },
                new() { Name = "priority", Type = "string", Required = false, DefaultValue = "medium", Description = "优先级" },
                new() { Name = "repeat", Type = "boolean", Required = false, DefaultValue = false, Description = "是否重复" }
            }
        };

        // Act
        var toolDef = _converter.Convert(skill);

        // Assert
        toolDef.Name.Should().Be("personal:reminder");
        toolDef.Description.Should().Be("创建提醒");

        var schema = toolDef.InputSchema;
        schema["type"]?.GetValue<string>().Should().Be("object");

        var properties = schema["properties"]?.AsObject();
        properties.Should().HaveCount(4);
        properties!["task"]?["type"]?.GetValue<string>().Should().Be("string");
        properties["time"]?["type"]?.GetValue<string>().Should().Be("string");
        properties["priority"]?["type"]?.GetValue<string>().Should().Be("string");
        properties["priority"]?["default"]?.GetValue<string>().Should().Be("medium");
        properties["repeat"]?["type"]?.GetValue<string>().Should().Be("boolean");
        properties["repeat"]?["default"]?.GetValue<bool>().Should().Be(false);

        var required = schema["required"]?.AsArray();
        required.Should().HaveCount(2);
        var requiredList = required!.Select(n => n?.GetValue<string>()).ToList();
        requiredList.Should().Contain("task");
        requiredList.Should().Contain("time");
    }
}
