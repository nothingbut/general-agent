namespace GeneralAgent.Infrastructure.Skills.Tests.Models;

using GeneralAgent.Infrastructure.Skills.Models;

public class SkillTests
{
    [Fact]
    public void Skill_ShouldSupportContextConfig()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test template",
            Parameters = new List<SkillParameter>(),
            RequiresContext = true,
            ContextConfig = new ContextConfig
            {
                MaxMessages = 10,
                Roles = new[] { "user", "assistant" },
                IncludeSystemMessages = false
            }
        };

        // Assert
        Assert.True(skill.RequiresContext);
        Assert.NotNull(skill.ContextConfig);
        Assert.Equal(10, skill.ContextConfig.MaxMessages);
        Assert.Equal(2, skill.ContextConfig.Roles!.Length);
    }

    [Fact]
    public void Skill_ShouldSupportReturnToLLM()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test",
            Parameters = new List<SkillParameter>(),
            ReturnToLLM = false
        };

        // Assert
        Assert.False(skill.ReturnToLLM);
    }

    [Fact]
    public void Skill_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var skill = new Skill
        {
            Name = "test",
            Description = "Test",
            Template = "Test",
            Parameters = new List<SkillParameter>()
        };

        // Assert
        Assert.False(skill.RequiresContext); // default false
        Assert.Null(skill.ContextConfig);
        Assert.True(skill.ReturnToLLM); // default true
    }
}
