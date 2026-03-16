using FluentAssertions;
using GeneralAgent.Core.Exceptions;

namespace GeneralAgent.Core.Tests.Exceptions;

public class LLMExceptionTests
{
    [Fact]
    public void Create_WithMessage_ShouldSetMessage()
    {
        // Arrange & Act
        var ex = new LLMException("Test error");

        // Assert
        ex.Message.Should().Be("Test error");
        ex.ProviderName.Should().BeNull();
        ex.ErrorType.Should().Be(LLMErrorType.Unknown);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_ShouldSetAll()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");

        // Act
        var ex = new LLMException(
            "Network error",
            "Ollama",
            LLMErrorType.NetworkError,
            innerEx);

        // Assert
        ex.Message.Should().Be("Network error");
        ex.ProviderName.Should().Be("Ollama");
        ex.ErrorType.Should().Be(LLMErrorType.NetworkError);
        ex.InnerException.Should().Be(innerEx);
    }

    [Fact]
    public void LLMException_InheritsFromAgentException()
    {
        // Arrange & Act
        var ex = new LLMException("Test");

        // Assert
        ex.Should().BeAssignableTo<AgentException>();
    }

    [Theory]
    [InlineData(LLMErrorType.NetworkError)]
    [InlineData(LLMErrorType.TimeoutError)]
    [InlineData(LLMErrorType.AuthenticationError)]
    [InlineData(LLMErrorType.ModelNotFound)]
    [InlineData(LLMErrorType.RateLimitError)]
    [InlineData(LLMErrorType.ServerError)]
    [InlineData(LLMErrorType.Unknown)]
    public void ErrorType_AllValuesAreValid(LLMErrorType errorType)
    {
        // Arrange & Act
        var ex = new LLMException("Test", errorType: errorType);

        // Assert
        ex.ErrorType.Should().Be(errorType);
    }

    [Fact]
    public void Create_WithNullProvider_ShouldBeNull()
    {
        // Arrange & Act
        var ex = new LLMException(
            "Test error",
            providerName: null,
            LLMErrorType.NetworkError);

        // Assert
        ex.ProviderName.Should().BeNull();
    }

    [Fact]
    public void Create_WithProviderName_ShouldBeSet()
    {
        // Arrange & Act
        var ex = new LLMException(
            "Test error",
            providerName: "Anthropic");

        // Assert
        ex.ProviderName.Should().Be("Anthropic");
    }

    [Fact]
    public void LLMException_CanBeThrownAndCaught()
    {
        // Arrange
        var thrownException = new LLMException(
            "API call failed",
            "OpenAI",
            LLMErrorType.NetworkError);

        // Act & Assert
        try
        {
            throw thrownException;
        }
        catch (LLMException caught)
        {
            caught.Message.Should().Be("API call failed");
            caught.ProviderName.Should().Be("OpenAI");
            caught.ErrorType.Should().Be(LLMErrorType.NetworkError);
        }
    }

    [Fact]
    public void LLMException_CanBeCaughtAsAgentException()
    {
        // Arrange
        var thrownException = new LLMException("Test error");

        // Act & Assert
        try
        {
            throw thrownException;
        }
        catch (AgentException caught)
        {
            caught.Message.Should().Be("Test error");
        }
    }
}
