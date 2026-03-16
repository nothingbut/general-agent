using FluentAssertions;
using GeneralAgent.Core.Common;

namespace GeneralAgent.Core.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Act
        var result = Result<int>.Failure("Error message");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be("Error message");
    }

    [Fact]
    public void Match_WhenSuccess_ShouldCallSuccessFunc()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error}");

        // Assert
        output.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_WhenFailure_ShouldCallFailureFunc()
    {
        // Arrange
        var result = Result<int>.Failure("Something went wrong");

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error}");

        // Assert
        output.Should().Be("Failure: Something went wrong");
    }
}
