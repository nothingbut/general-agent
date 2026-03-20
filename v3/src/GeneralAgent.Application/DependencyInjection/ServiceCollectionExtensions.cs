using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM.Serializers;
using GeneralAgent.Infrastructure.Skills.Converters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Application.DependencyInjection;

/// <summary>
/// Tool Calling 相关服务的依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Tool Calling 相关服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddToolCallingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 配置 ToolCallingConfig
        services.Configure<ToolCallingConfig>(
            configuration.GetSection("ToolCalling"));

        // 2. 核心服务（单例）
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<SkillToToolConverter>();

        // 3. 根据配置选择 IToolCallingListener
        var interactiveMode = configuration.GetValue<bool>("ToolCalling:InteractiveMode", true);
        if (interactiveMode)
        {
            services.AddSingleton<IToolCallingListener, ConsoleToolCallingListener>();
        }
        else
        {
            services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
        }

        // 4. 根据 LLM Provider 选择 IToolSerializer
        var llmProvider = configuration.GetValue<string>("LLM:DefaultProvider") ?? "Ollama";
        if (llmProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IToolSerializer, AnthropicToolSerializer>();
        }
        else
        {
            // OpenAI 格式兼容 Ollama, LMStudio, llama.cpp 等
            services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
        }

        // 5. Orchestrator（需要 ILLMClient，由 ILLMClientFactory 提供）
        services.AddSingleton<ToolCallingOrchestrator>(sp =>
        {
            var factory = sp.GetRequiredService<ILLMClientFactory>();
            var providerName = configuration.GetValue<string>("LLM:DefaultProvider");
            var llmClient = factory.GetClient(providerName);

            return new ToolCallingOrchestrator(
                sp.GetRequiredService<ToolExecutor>(),
                sp.GetRequiredService<ToolRegistry>(),
                llmClient,
                sp.GetRequiredService<IToolCallingListener>(),
                sp.GetRequiredService<IToolSerializer>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ToolCallingConfig>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToolCallingOrchestrator>>()
            );
        });

        return services;
    }
}
