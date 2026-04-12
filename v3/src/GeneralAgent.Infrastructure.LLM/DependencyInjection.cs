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
        if (llmOptions?.Providers is not null && llmOptions.Providers.Count > 0)
        {
            foreach (var provider in llmOptions.Providers.Values)
            {
                services.AddHttpClient($"LLM_{provider.Name}", client =>
                {
                    client.BaseAddress = new Uri(provider.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds);
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            }
        }

        // 确保基础 HttpClient 服务始终可用（即使没有提供商配置）
        // 这样 IHttpClientFactory 可以被解析，Factory 可以正常创建
        if (!services.Any(sd => sd.ServiceType == typeof(IHttpClientFactory)))
        {
            services.AddHttpClient();
        }

        // 注册工厂（单例）
        services.AddSingleton<ILLMClientFactory, LLMClientFactory>();

        // 注册默认 ILLMClient（Scoped）
        // 使用 factory 创建默认提供商的客户端
        services.AddScoped<ILLMClient>(provider =>
        {
            var factory = provider.GetRequiredService<ILLMClientFactory>();
            return factory.GetClient(); // 使用默认提供商
        });

        return services;
    }
}
