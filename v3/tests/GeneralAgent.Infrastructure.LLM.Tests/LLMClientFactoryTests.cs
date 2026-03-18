using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Infrastructure.LLM.Tests;

/// <summary>
/// LLMClientFactory 单元测试
/// </summary>
public class LLMClientFactoryTests
{
    private readonly IHttpClientFactory _mockHttpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public LLMClientFactoryTests()
    {
        _mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        _loggerFactory = NullLoggerFactory.Instance;
    }

    private LLMClientFactory CreateFactory(LLMOptions options)
    {
        var optionsWrapper = Options.Create(options);
        return new LLMClientFactory(_mockHttpClientFactory, optionsWrapper, _loggerFactory);
    }

    [Fact]
    public void GetClient_使用默认提供商_成功返回客户端()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig
                {
                    Name = "Ollama",
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "qwen2.5:0.5b",
                    TimeoutSeconds = 120
                }
            }
        };

        _mockHttpClientFactory
            .CreateClient("LLM_Ollama").Returns(new HttpClient());

        var factory = CreateFactory(options);

        // Act
        var client = factory.GetClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal("Ollama", client.ProviderName);
    }

    [Fact]
    public void GetClient_指定提供商名称_成功返回客户端()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig
                {
                    Name = "Ollama",
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "qwen2.5:0.5b",
                    TimeoutSeconds = 120
                },
                ["LMStudio"] = new LLMProviderConfig
                {
                    Name = "LMStudio",
                    BaseUrl = "http://localhost:1234",
                    DefaultModel = "llama-3.2-1b",
                    TimeoutSeconds = 60
                }
            }
        };

        _mockHttpClientFactory
            .CreateClient("LLM_LMStudio").Returns(new HttpClient());

        var factory = CreateFactory(options);

        // Act
        var client = factory.GetClient("LMStudio");

        // Assert
        Assert.NotNull(client);
        Assert.Equal("LMStudio", client.ProviderName);
    }

    [Fact]
    public void GetClient_未配置提供商_抛出LLMException()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig
                {
                    Name = "Ollama",
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "qwen2.5:0.5b",
                    TimeoutSeconds = 120
                }
            }
        };

        var factory = CreateFactory(options);

        // Act & Assert
        var exception = Assert.Throws<LLMException>(() => factory.GetClient("NonExistent"));
        Assert.Contains("提供商 'NonExistent' 未配置", exception.Message);
    }

    [Fact]
    public void GetClient_未配置默认提供商_抛出LLMException()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "",
            Providers = new Dictionary<string, LLMProviderConfig>()
        };

        var factory = CreateFactory(options);

        // Act & Assert
        var exception = Assert.Throws<LLMException>(() => factory.GetClient());
        Assert.Contains("未配置默认 LLM 提供商", exception.Message);
    }

    [Fact]
    public void GetClient_DefaultProvider为null_抛出LLMException()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = null!,
            Providers = new Dictionary<string, LLMProviderConfig>()
        };

        var factory = CreateFactory(options);

        // Act & Assert
        var exception = Assert.Throws<LLMException>(() => factory.GetClient(null));
        Assert.Contains("未配置默认 LLM 提供商", exception.Message);
    }

    [Fact]
    public void GetClient_多次调用_返回同一实例()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig
                {
                    Name = "Ollama",
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "qwen2.5:0.5b",
                    TimeoutSeconds = 120
                }
            }
        };

        _mockHttpClientFactory
            .CreateClient("LLM_Ollama").Returns(new HttpClient());

        var factory = CreateFactory(options);

        // Act
        var client1 = factory.GetClient("Ollama");
        var client2 = factory.GetClient("Ollama");

        // Assert
        Assert.Same(client1, client2);
        _mockHttpClientFactory.Received(1).CreateClient("LLM_Ollama");
    }

    [Fact]
    public void GetClient_不同提供商_返回不同实例()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig
                {
                    Name = "Ollama",
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "qwen2.5:0.5b",
                    TimeoutSeconds = 120
                },
                ["LMStudio"] = new LLMProviderConfig
                {
                    Name = "LMStudio",
                    BaseUrl = "http://localhost:1234",
                    DefaultModel = "llama-3.2-1b",
                    TimeoutSeconds = 60
                }
            }
        };

        _mockHttpClientFactory
            .CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var factory = CreateFactory(options);

        // Act
        var client1 = factory.GetClient("Ollama");
        var client2 = factory.GetClient("LMStudio");

        // Assert
        Assert.NotSame(client1, client2);
        Assert.Equal("Ollama", client1.ProviderName);
        Assert.Equal("LMStudio", client2.ProviderName);
    }

    [Fact]
    public void GetAvailableProviders_返回所有提供商名称()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>
            {
                ["Ollama"] = new LLMProviderConfig { Name = "Ollama", BaseUrl = "http://localhost:11434" },
                ["LMStudio"] = new LLMProviderConfig { Name = "LMStudio", BaseUrl = "http://localhost:1234" },
                ["LlamaCpp"] = new LLMProviderConfig { Name = "LlamaCpp", BaseUrl = "http://localhost:8080" }
            }
        };

        var factory = CreateFactory(options);

        // Act
        var providers = factory.GetAvailableProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.Equal(3, providers.Count);
        Assert.Contains("Ollama", providers);
        Assert.Contains("LMStudio", providers);
        Assert.Contains("LlamaCpp", providers);
    }

    [Fact]
    public void GetAvailableProviders_无提供商配置_返回空列表()
    {
        // Arrange
        var options = new LLMOptions
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, LLMProviderConfig>()
        };

        var factory = CreateFactory(options);

        // Act
        var providers = factory.GetAvailableProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.Empty(providers);
    }
}
