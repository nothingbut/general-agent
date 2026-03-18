using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Abstractions;

public class IToolTests
{
    [Fact]
    public void ITool_ShouldHaveRequiredProperties()
    {
        // Arrange
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("test_tool");
        mockTool.Description.Returns("Test tool description");

        // Assert
        Assert.NotNull(mockTool.Name);
        Assert.NotNull(mockTool.Description);
    }

    [Fact]
    public async Task ITool_ExecuteAsync_ShouldReturnResult()
    {
        // Arrange
        var mockTool = Substitute.For<ITool>();
        mockTool.ExecuteAsync(
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("output"));

        // Act
        var result = await mockTool.ExecuteAsync(
            new Dictionary<string, object>(),
            new ToolExecutionContext { SessionId = Guid.NewGuid() },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("output", result.Value);
    }
}
