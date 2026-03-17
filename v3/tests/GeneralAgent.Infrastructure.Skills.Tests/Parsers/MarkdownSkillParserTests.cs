using GeneralAgent.Infrastructure.Skills.Parsers;
using GeneralAgent.Infrastructure.Skills.Models;
using FluentAssertions;

namespace GeneralAgent.Infrastructure.Skills.Tests.Parsers;

public class MarkdownSkillParserTests
{
    private readonly MarkdownSkillParser _parser = new();

    [Fact]
    public void Parse_ValidMarkdown_ReturnsSkill()
    {
        // Arrange
        var markdown = """
            ---
            name: greeting
            description: 向用户问候
            parameters:
              - name: user_name
                type: string
                required: true
                description: 用户名称
            ---

            你好 {user_name}！今天有什么我可以帮助你的吗？
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("greeting");
        result.Value.Description.Should().Be("向用户问候");
        result.Value.Parameters.Should().HaveCount(1);
        result.Value.Template.Trim().Should().Be("你好 {user_name}！今天有什么我可以帮助你的吗？");
    }

    [Fact]
    public void Parse_MissingFrontmatter_ReturnsFailure()
    {
        // Arrange
        var markdown = "这只是普通内容，没有 YAML frontmatter";

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("frontmatter");
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsFailure()
    {
        // Arrange
        var markdown = """
            ---
            name: test
            parameters: [invalid yaml
            ---
            content
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_MissingRequiredFields_ReturnsFailure()
    {
        // Arrange
        var markdown = """
            ---
            name: test
            ---
            content
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("description");
    }

    [Fact]
    public void Parse_WithNamespace_SetsFullName()
    {
        // Arrange
        var markdown = """
            ---
            name: greeting
            description: 问候技能
            namespace: personal
            parameters: []
            ---
            你好！
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Namespace.Should().Be("personal");
        result.Value.FullName.Should().Be("personal:greeting");
    }

    [Fact]
    public void Parse_WithoutNamespace_FullNameEqualsName()
    {
        // Arrange
        var markdown = """
            ---
            name: greeting
            description: 问候技能
            parameters: []
            ---
            你好！
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Namespace.Should().BeNull();
        result.Value.FullName.Should().Be("greeting");
    }
}
