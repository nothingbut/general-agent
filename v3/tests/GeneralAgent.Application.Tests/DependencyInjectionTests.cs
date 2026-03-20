using GeneralAgent.Application;
using GeneralAgent.Application.Services;
using GeneralAgent.Infrastructure;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeneralAgent.Application.Tests;

/// <summary>
/// Application 层 DI 配置测试
/// </summary>
public class DependencyInjectionTests
{
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        // 创建模拟配置
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "LLM:DefaultProvider", "mock" },
                { "LLM:Providers:mock:Name", "mock" },
                { "LLM:Providers:mock:BaseUrl", "http://localhost" },
                { "LLM:Providers:mock:TimeoutSeconds", "30" },
                { "ToolCalling:Enabled", "true" },
                { "ToolCalling:MaxRounds", "3" },
                { "ToolCalling:InteractiveMode", "false" },
                { "ToolCalling:AutoExtendBy", "5" },
                { "ToolCalling:AbsoluteMaxRounds", "20" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);

        // 按正确顺序注册依赖
        services.AddInfrastructure("Data Source=:memory:");
        services.AddLLMInfrastructure(config);
        services.AddApplicationLayer(config);

        return services;
    }

    [Fact]
    public void AddApplicationLayer_Should_Register_SessionService()
    {
        // Arrange
        var services = CreateServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var sessionService = serviceProvider.GetService<SessionService>();

        // Assert
        Assert.NotNull(sessionService);
    }

    [Fact]
    public void AddApplicationLayer_Should_Register_ConversationService()
    {
        // Arrange
        var services = CreateServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var conversationService = serviceProvider.GetService<ConversationService>();

        // Assert
        Assert.NotNull(conversationService);
    }

    [Fact]
    public void AddApplicationLayer_Should_Use_Scoped_Lifecycle_For_SessionService()
    {
        // Arrange
        var services = CreateServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var scope1 = serviceProvider.CreateScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<SessionService>();

        var scope2 = serviceProvider.CreateScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<SessionService>();

        // Assert：不同 Scope 中的实例应该不同
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void AddApplicationLayer_Should_Use_Scoped_Lifecycle_For_ConversationService()
    {
        // Arrange
        var services = CreateServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var scope1 = serviceProvider.CreateScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<ConversationService>();

        var scope2 = serviceProvider.CreateScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<ConversationService>();

        // Assert：不同 Scope 中的实例应该不同
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void AddApplicationLayer_Should_Return_ServiceCollection_For_Chaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ToolCalling:Enabled", "true" }
            })
            .Build();

        // Act
        var result = services.AddApplicationLayer(config);

        // Assert：应该返回相同的 IServiceCollection，支持链式调用
        Assert.Same(services, result);
    }
}
