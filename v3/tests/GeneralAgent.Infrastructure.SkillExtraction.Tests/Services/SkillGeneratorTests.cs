using FluentAssertions;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using GeneralAgent.Infrastructure.Skills.Parsers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Services;

/// <summary>
/// SkillGenerator 单元测试
/// </summary>
public class SkillGeneratorTests
{
    private readonly ISkillParser _skillParser;
    private readonly ILogger<SkillGenerator> _logger;
    private readonly SkillGenerator _generator;

    public SkillGeneratorTests()
    {
        _skillParser = new MarkdownSkillParser();
        _logger = Substitute.For<ILogger<SkillGenerator>>();
        _generator = new SkillGenerator(_skillParser, _logger);
    }

    [Fact]
    public async Task GenerateSkillFileAsync_简单建议_应该生成有效文件()
    {
        // Arrange
        var suggestion = new SkillSuggestion
        {
            Name = "greeting",
            Description = "向用户问候",
            Namespace = "personal",
            Template = "你好！今天有什么我可以帮助你的吗？",
            Parameters = new List<SkillParameterDefinition>(),
            Confidence = 0.8,
            Rationale = "用户多次请求问候",
            Occurrences = 3,
            ExampleMessages = new List<string> { "你好", "嗨" }
        };

        // Act
        var result = await _generator.GenerateSkillFileAsync(suggestion);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("---");
        result.Should().Contain("name: greeting");
        result.Should().Contain("description: 向用户问候");
        result.Should().Contain("namespace: personal");
        result.Should().Contain("你好！今天有什么我可以帮助你的吗？");
    }

    [Fact]
    public async Task GenerateSkillFileAsync_带参数的建议_应该生成参数定义()
    {
        // Arrange
        var suggestion = new SkillSuggestion
        {
            Name = "api-helper",
            Description = "查看 API 文档",
            Namespace = "dev",
            Template = "请帮我查看 {{api}} 的文档并生成示例代码。",
            Parameters = new List<SkillParameterDefinition>
            {
                new SkillParameterDefinition
                {
                    Name = "api",
                    Type = "string",
                    Required = true,
                    Description = "API 名称",
                    DefaultValue = null
                }
            },
            Confidence = 0.85,
            Rationale = "用户多次查询 API 文档",
            Occurrences = 5,
            ExampleMessages = new List<string>()
        };

        // Act
        var result = await _generator.GenerateSkillFileAsync(suggestion);

        // Assert
        result.Should().Contain("parameters:");
        result.Should().Contain("name: api");
        result.Should().Contain("type: string");
        result.Should().Contain("required: true");
        result.Should().Contain("description: API 名称");
        result.Should().Contain("{{api}}");
    }

    [Fact]
    public async Task ValidateSkillAsync_有效的技能文件_应该验证通过()
    {
        // Arrange
        var validSkill = """
        ---
        name: test-skill
        description: 测试技能
        parameters: []
        ---

        这是一个测试技能模板。
        """;

        // Act
        var result = await _generator.ValidateSkillAsync(validSkill);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateSkillAsync_缺少frontmatter_应该验证失败()
    {
        // Arrange
        var invalidSkill = "这只是一个普通文本文件，没有 YAML frontmatter。";

        // Act
        var result = await _generator.ValidateSkillAsync(invalidSkill);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Should().Contain("frontmatter");
    }

    [Fact]
    public async Task ValidateSkillAsync_缺少必填字段_应该验证失败()
    {
        // Arrange
        var invalidSkill = """
        ---
        name: test-skill
        ---

        缺少 description 字段
        """;

        // Act
        var result = await _generator.ValidateSkillAsync(invalidSkill);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateSkillFileAsync_然后Validate_应该验证通过()
    {
        // Arrange
        var suggestion = new SkillSuggestion
        {
            Name = "complete-test",
            Description = "完整测试技能",
            Namespace = "test",
            Template = "这是一个完整的测试模板：{{input}}",
            Parameters = new List<SkillParameterDefinition>
            {
                new SkillParameterDefinition
                {
                    Name = "input",
                    Type = "string",
                    Required = true,
                    Description = "输入参数",
                    DefaultValue = null
                }
            },
            Confidence = 0.9,
            Rationale = "完整性测试",
            Occurrences = 1,
            ExampleMessages = new List<string>()
        };

        // Act - 生成
        var generated = await _generator.GenerateSkillFileAsync(suggestion);

        // Act - 验证
        var validation = await _generator.ValidateSkillAsync(generated);

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }
}
