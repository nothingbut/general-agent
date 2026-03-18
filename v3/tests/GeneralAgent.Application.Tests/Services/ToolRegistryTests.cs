using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Tests.Services;

public class ToolRegistryTests
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _logger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_logger);
    }

    [Fact]
    public void Register_ShouldAddTool()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test description");

        // Act
        _registry.Register(tool);

        // Assert
        var retrieved = _registry.GetTool("test_tool");
        Assert.NotNull(retrieved);
        Assert.Equal("test_tool", retrieved.Name);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void Register_ShouldOverwriteExistingTool()
    {
        // Arrange
        var tool1 = CreateMockTool("tool", "Description 1");
        var tool2 = CreateMockTool("tool", "Description 2");

        // Act
        _registry.Register(tool1);
        _registry.Register(tool2);

        // Assert
        var retrieved = _registry.GetTool("tool");
        Assert.NotNull(retrieved);
        Assert.Equal("Description 2", retrieved.Description);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void GetTool_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var tool = _registry.GetTool("non_existent");

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void GetAllTools_ShouldReturnAllRegisteredTools()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Tool 1"));
        _registry.Register(CreateMockTool("tool2", "Tool 2"));
        _registry.Register(CreateMockTool("tool3", "Tool 3"));

        // Act
        var tools = _registry.GetAllTools();

        // Assert
        Assert.Equal(3, tools.Count);
    }

    [Fact]
    public void GetToolsByNamespace_ShouldFilterCorrectly()
    {
        // Arrange
        _registry.Register(CreateMockTool("personal:greeting", "Greeting"));
        _registry.Register(CreateMockTool("personal:reminder", "Reminder"));
        _registry.Register(CreateMockTool("productivity:task", "Task"));

        // Act
        var personalTools = _registry.GetToolsByNamespace("personal");

        // Assert
        Assert.Equal(2, personalTools.Count);
        Assert.All(personalTools, t => Assert.StartsWith("personal:", t.Name));
    }

    [Fact]
    public void Unregister_ShouldRemoveTool()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool", "Test"));

        // Act
        var removed = _registry.Unregister("tool");

        // Assert
        Assert.True(removed);
        Assert.Null(_registry.GetTool("tool"));
        Assert.Equal(0, _registry.Count);
    }

    [Fact]
    public void Clear_ShouldRemoveAllTools()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Test 1"));
        _registry.Register(CreateMockTool("tool2", "Test 2"));

        // Act
        _registry.Clear();

        // Assert
        Assert.Equal(0, _registry.Count);
        Assert.Empty(_registry.GetAllTools());
    }

    private ITool CreateMockTool(string name, string description)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        return tool;
    }
}
