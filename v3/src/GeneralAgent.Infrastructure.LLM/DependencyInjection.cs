using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// Infrastructure.LLM 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 LLM 基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLLMInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));

        // 为每个提供商注册 HttpClient
        var llmOptions = configuration.GetSection("LLM").Get<LLMOptions>();
        if (llmOptions?.Providers is not null)
        {
            foreach (var providerName in llmOptions.Providers.Keys)
            {
                services.AddHttpClient($"LLM_{providerName}")
                    .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            }
        }

        // 注册工厂（单例）
        services.AddSingleton<ILLMClientFactory, LLMClientFactory>();

        return services;
    }
}
