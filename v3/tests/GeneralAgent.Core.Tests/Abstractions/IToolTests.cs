using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Moq;
using System.Text.Json.Nodes;

namespace GeneralAgent.Core.Tests.Abstractions;

/// <summary>
/// ITool 接口契约测试
/// 验证所有 ITool 实现必须满足的要求
/// </summary>
public class IToolTests
{
    [Fact]
    public void ITool_ShouldHaveNameProperty()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");

        // Act
        var name = mockTool.Object.Name;

        // Assert
        name.Should().NotBeNullOrEmpty();
        name.Should().Be("test_tool");
    }

    [Fact]
    public void ITool_ShouldHaveDescriptionProperty()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Description).Returns("Test tool description");

        // Act
        var description = mockTool.Object.Description;

        // Assert
        description.Should().NotBeNullOrEmpty();
        description.Should().Be("Test tool description");
    }

    [Fact]
    public void ITool_GetDefinition_ShouldReturnToolDefinition()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var expectedDefinition = new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test tool description",
            InputSchema = new JsonObject { ["type"] = "object" }
        };
        mockTool.Setup(t => t.GetDefinition()).Returns(expectedDefinition);

        // Act
        var definition = mockTool.Object.GetDefinition();

        // Assert
        definition.Should().NotBeNull();
        definition.Name.Should().Be("test_tool");
        definition.Description.Should().Be("Test tool description");
        definition.InputSchema.Should().NotBeNull();
    }

    [Fact]
    public async Task ITool_ExecuteAsync_ShouldReturnResult()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var sessionId = Guid.NewGuid();
        var context = new ToolExecutionContext { SessionId = sessionId };
        var arguments = new Dictionary<string, object> { ["key"] = "value" };
        var expectedResult = Result<string>.Success("execution output");

        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await mockTool.Object.ExecuteAsync(arguments, context, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("execution output");
    }

    [Fact]
    public async Task ITool_ExecuteAsync_CanReturnFailure()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var sessionId = Guid.NewGuid();
        var context = new ToolExecutionContext { SessionId = sessionId };
        var arguments = new Dictionary<string, object>();
        var expectedResult = Result<string>.Failure("Tool execution failed");

        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await mockTool.Object.ExecuteAsync(arguments, context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Tool execution failed");
    }

    [Fact]
    public async Task ITool_ExecuteAsync_ShouldReceiveContext()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var sessionId = Guid.NewGuid();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = "test_provider"
        };

        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("output"));

        // Act
        await mockTool.Object.ExecuteAsync(
            new Dictionary<string, object>(),
            context,
            CancellationToken.None);

        // Assert
        mockTool.Verify(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.Is<ToolExecutionContext>(c => c.SessionId == sessionId && c.ProviderName == "test_provider"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ITool_ExecuteAsync_ShouldSupportCancellation()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        using var cts = new CancellationTokenSource();
        var context = new ToolExecutionContext { SessionId = Guid.NewGuid() };

        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("output"));

        // Act
        await mockTool.Object.ExecuteAsync(
            new Dictionary<string, object>(),
            context,
            cts.Token);

        // Assert
        mockTool.Verify(t => t.ExecuteAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ITool_ExecuteStreamAsync_ShouldYieldResults()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var sessionId = Guid.NewGuid();
        var context = new ToolExecutionContext { SessionId = sessionId };

        var streamResults = new[] { "chunk1", "chunk2", "chunk3" };
        mockTool.Setup(t => t.ExecuteStreamAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .Returns(GetAsyncEnumerable(streamResults));

        // Act
        var results = new List<string>();
        await foreach (var chunk in mockTool.Object.ExecuteStreamAsync(
            new Dictionary<string, object>(),
            context,
            CancellationToken.None))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().ContainInOrder(streamResults);
    }

    [Fact]
    public async Task ITool_ExecuteStreamAsync_CanReturnEmptyStream()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var context = new ToolExecutionContext { SessionId = Guid.NewGuid() };

        mockTool.Setup(t => t.ExecuteStreamAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .Returns(GetAsyncEnumerable(Array.Empty<string>()));

        // Act
        var results = new List<string>();
        await foreach (var chunk in mockTool.Object.ExecuteStreamAsync(
            new Dictionary<string, object>(),
            context,
            CancellationToken.None))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ITool_ExecuteStreamAsync_ShouldReceiveContext()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        var sessionId = Guid.NewGuid();
        var context = new ToolExecutionContext
        {
            SessionId = sessionId,
            ProviderName = "stream_provider"
        };

        mockTool.Setup(t => t.ExecuteStreamAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<ToolExecutionContext>(),
            It.IsAny<CancellationToken>()))
            .Returns(GetAsyncEnumerable(new[] { "output" }));

        // Act
        var results = new List<string>();
        await foreach (var chunk in mockTool.Object.ExecuteStreamAsync(
            new Dictionary<string, object>(),
            context,
            CancellationToken.None))
        {
            results.Add(chunk);
        }

        // Assert
        mockTool.Verify(t => t.ExecuteStreamAsync(
            It.IsAny<Dictionary<string, object>>(),
            It.Is<ToolExecutionContext>(c => c.SessionId == sessionId && c.ProviderName == "stream_provider"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 辅助方法：创建异步枚举器
    private static async IAsyncEnumerable<string> GetAsyncEnumerable(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            yield return await Task.FromResult(item);
        }
    }
}
