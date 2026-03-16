using GeneralAgent.Core.Abstractions;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.LLM.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddLLMInfrastructure_RegistersFactoryAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:DefaultProvider"] = "Ollama"
            })
            .Build();

        // Act
        services.AddLLMInfrastructure(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var factory1 = provider.GetService<ILLMClientFactory>();
        var factory2 = provider.GetService<ILLMClientFactory>();

        Assert.NotNull(factory1);
        Assert.Same(factory1, factory2); // Singleton check
    }

    [Fact]
    public void AddLLMInfrastructure_BindsOptionsFromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:DefaultProvider"] = "LMStudio",
                ["LLM:Providers:LMStudio:BaseUrl"] = "http://localhost:1234",
                ["LLM:Providers:LMStudio:DefaultModel"] = "llama3.2",
                ["LLM:Providers:LMStudio:TimeoutSeconds"] = "300"
            })
            .Build();

        // Act
        services.AddLLMInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LLMOptions>>().Value;

        // Assert
        Assert.Equal("LMStudio", options.DefaultProvider);
        Assert.Contains("LMStudio", options.Providers.Keys);
        Assert.Equal("http://localhost:1234", options.Providers["LMStudio"].BaseUrl);
        Assert.Equal("llama3.2", options.Providers["LMStudio"].DefaultModel);
        Assert.Equal(300, options.Providers["LMStudio"].TimeoutSeconds);
    }

    [Fact]
    public void AddLLMInfrastructure_RegistersNamedHttpClientsForEachProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:DefaultProvider"] = "Ollama",
                ["LLM:Providers:Ollama:BaseUrl"] = "http://localhost:11434",
                ["LLM:Providers:LMStudio:BaseUrl"] = "http://localhost:1234"
            })
            .Build();

        // Act
        services.AddLLMInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        // Assert - Should not throw
        var ollamaClient = httpClientFactory.CreateClient("LLM_Ollama");
        var lmStudioClient = httpClientFactory.CreateClient("LLM_LMStudio");

        Assert.NotNull(ollamaClient);
        Assert.NotNull(lmStudioClient);
    }

    [Fact]
    public void AddLLMInfrastructure_HandlesEmptyProvidersGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:DefaultProvider"] = "Ollama"
            })
            .Build();

        // Act & Assert - should not throw
        services.AddLLMInfrastructure(config);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetService<ILLMClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddLLMInfrastructure_HandlesNullConfigurationSectionGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert - should not throw
        services.AddLLMInfrastructure(config);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetService<ILLMClientFactory>();
        Assert.NotNull(factory);

        var options = provider.GetRequiredService<IOptions<LLMOptions>>().Value;
        Assert.NotNull(options);
        Assert.Equal("Ollama", options.DefaultProvider); // Default value
    }
}
