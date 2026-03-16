using GeneralAgent.Infrastructure.LLM;

namespace GeneralAgent.Infrastructure.LLM.Tests;

public class LLMOptionsTests
{
    [Fact]
    public void LLMOptions_DefaultProvider_DefaultsToOllama()
    {
        // Arrange & Act
        var options = new LLMOptions();

        // Assert
        Assert.Equal("Ollama", options.DefaultProvider);
        Assert.NotNull(options.Providers);
        Assert.Empty(options.Providers);
    }

    [Fact]
    public void LLMProviderConfig_HasAllRequiredProperties()
    {
        // Arrange & Act
        var config = new LLMProviderConfig
        {
            Name = "Ollama",
            BaseUrl = "http://localhost:11434",
            DefaultModel = "llama3.2",
            TimeoutSeconds = 120
        };

        // Assert
        Assert.Equal("Ollama", config.Name);
        Assert.Equal("http://localhost:11434", config.BaseUrl);
        Assert.Equal("llama3.2", config.DefaultModel);
        Assert.Equal(120, config.TimeoutSeconds);
    }

    [Fact]
    public void LLMProviderConfig_TimeoutSeconds_DefaultsTo120()
    {
        // Arrange & Act
        var config = new LLMProviderConfig();

        // Assert
        Assert.Equal(120, config.TimeoutSeconds);
    }
}
