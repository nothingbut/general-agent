using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ToolRegistry 单元测试
/// </summary>
public sealed class ToolRegistryTests
{
    private readonly ILogger<ToolRegistry> _mockLogger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _mockLogger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_mockLogger);
    }

    #region Register Tests

    [Fact]
    public void Register_ShouldAddTool()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test description");

        // Act
        _registry.Register(tool);

        // Assert
        var retrieved = _registry.GetTool("test_tool");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("test_tool");
        _registry.Count.Should().Be(1);
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
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Description 2");
        _registry.Count.Should().Be(1);
    }

    [Fact]
    public void Register_ShouldThrowArgumentNullException_WhenToolIsNull()
    {
        // Act & Assert
        var action = () => _registry.Register(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_ShouldLogDebugMessage()
    {
        // Arrange
        var tool = CreateMockTool("test_tool", "Test description");

        // Act
        _registry.Register(tool);

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Register_ShouldLogWarningWhenOverwriting()
    {
        // Arrange
        var tool1 = CreateMockTool("tool", "Description 1");
        var tool2 = CreateMockTool("tool", "Description 2");
        _registry.Register(tool1);

        // Act
        _registry.Register(tool2);

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region RegisterMany Tests

    [Fact]
    public void RegisterMany_ShouldAddMultipleTools()
    {
        // Arrange
        var tools = new List<ITool>
        {
            CreateMockTool("tool1", "Tool 1"),
            CreateMockTool("tool2", "Tool 2"),
            CreateMockTool("tool3", "Tool 3")
        };

        // Act
        _registry.RegisterMany(tools);

        // Assert
        _registry.Count.Should().Be(3);
        _registry.GetTool("tool1").Should().NotBeNull();
        _registry.GetTool("tool2").Should().NotBeNull();
        _registry.GetTool("tool3").Should().NotBeNull();
    }

    [Fact]
    public void RegisterMany_WithEmptyCollection_ShouldNotAddAnything()
    {
        // Arrange
        var tools = new List<ITool>();

        // Act
        _registry.RegisterMany(tools);

        // Assert
        _registry.Count.Should().Be(0);
    }

    #endregion

    #region GetTool Tests

    [Fact]
    public void GetTool_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var tool = _registry.GetTool("non_existent");

        // Assert
        tool.Should().BeNull();
    }

    [Fact]
    public void GetTool_ShouldReturnCorrectTool()
    {
        // Arrange
        var tool = CreateMockTool("my_tool", "My description");
        _registry.Register(tool);

        // Act
        var retrieved = _registry.GetTool("my_tool");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("my_tool");
        retrieved!.Description.Should().Be("My description");
    }

    #endregion

    #region GetAllTools Tests

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
        tools.Should().HaveCount(3);
        tools.Should().Contain(t => t.Name == "tool1");
        tools.Should().Contain(t => t.Name == "tool2");
        tools.Should().Contain(t => t.Name == "tool3");
    }

    [Fact]
    public void GetAllTools_ShouldReturnEmptyList_WhenNoToolsRegistered()
    {
        // Act
        var tools = _registry.GetAllTools();

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public void GetAllTools_ShouldReturnReadOnlyList()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Tool 1"));

        // Act
        var tools = _registry.GetAllTools();

        // Assert
        tools.Should().BeAssignableTo<IReadOnlyList<ITool>>();
    }

    #endregion

    #region GetToolsByNamespace Tests

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
        personalTools.Should().HaveCount(2);
        personalTools.Should().AllSatisfy(t => t.Name.Should().StartWith("personal:"));
    }

    [Fact]
    public void GetToolsByNamespace_ShouldReturnEmptyList_WhenNoMatchingNamespace()
    {
        // Arrange
        _registry.Register(CreateMockTool("personal:greeting", "Greeting"));
        _registry.Register(CreateMockTool("personal:reminder", "Reminder"));

        // Act
        var tools = _registry.GetToolsByNamespace("nonexistent");

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public void GetToolsByNamespace_ShouldReturnReadOnlyList()
    {
        // Arrange
        _registry.Register(CreateMockTool("personal:greeting", "Greeting"));

        // Act
        var tools = _registry.GetToolsByNamespace("personal");

        // Assert
        tools.Should().BeAssignableTo<IReadOnlyList<ITool>>();
    }

    [Fact]
    public void GetToolsByNamespace_ShouldExcludeToolsWithoutNamespace()
    {
        // Arrange
        _registry.Register(CreateMockTool("personal:greeting", "Greeting"));
        _registry.Register(CreateMockTool("tool_without_namespace", "Tool"));

        // Act
        var personalTools = _registry.GetToolsByNamespace("personal");

        // Assert
        personalTools.Should().HaveCount(1);
        personalTools.Should().OnlyContain(t => t.Name == "personal:greeting");
    }

    #endregion

    #region Unregister Tests

    [Fact]
    public void Unregister_ShouldRemoveTool()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool", "Test"));

        // Act
        var removed = _registry.Unregister("tool");

        // Assert
        removed.Should().BeTrue();
        _registry.GetTool("tool").Should().BeNull();
        _registry.Count.Should().Be(0);
    }

    [Fact]
    public void Unregister_ShouldReturnFalse_WhenToolNotFound()
    {
        // Act
        var removed = _registry.Unregister("non_existent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Unregister_ShouldLogDebugMessage()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool", "Test"));
        _mockLogger.ClearReceivedCalls();

        // Act
        _registry.Unregister("tool");

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_ShouldRemoveAllTools()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Test 1"));
        _registry.Register(CreateMockTool("tool2", "Test 2"));

        // Act
        _registry.Clear();

        // Assert
        _registry.Count.Should().Be(0);
        _registry.GetAllTools().Should().BeEmpty();
    }

    [Fact]
    public void Clear_ShouldLogInformationMessage()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool1", "Test 1"));
        _mockLogger.ClearReceivedCalls();

        // Act
        _registry.Clear();

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Count Property Tests

    [Fact]
    public void Count_ShouldReturnZero_WhenNoToolsRegistered()
    {
        // Act & Assert
        _registry.Count.Should().Be(0);
    }

    [Fact]
    public void Count_ShouldIncrementWhenToolAdded()
    {
        // Arrange & Act
        _registry.Register(CreateMockTool("tool1", "Test 1"));
        var count1 = _registry.Count;

        _registry.Register(CreateMockTool("tool2", "Test 2"));
        var count2 = _registry.Count;

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(2);
    }

    [Fact]
    public void Count_ShouldNotChangeWhenOverwriting()
    {
        // Arrange
        _registry.Register(CreateMockTool("tool", "Description 1"));
        var count1 = _registry.Count;

        // Act
        _registry.Register(CreateMockTool("tool", "Description 2"));
        var count2 = _registry.Count;

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(1);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentOperations_ShouldBeSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        const int threadCount = 10;
        const int operationsPerThread = 10;

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    var toolName = $"tool_{threadId}_{j}";
                    var tool = CreateMockTool(toolName, $"Description {j}");
                    _registry.Register(tool);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        _registry.Count.Should().Be(threadCount * operationsPerThread);
    }

    #endregion

    #region Helper Methods

    private static ITool CreateMockTool(string name, string description)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        return tool;
    }

    #endregion
}
