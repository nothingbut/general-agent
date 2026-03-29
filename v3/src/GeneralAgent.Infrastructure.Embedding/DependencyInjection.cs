using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.Embedding;

/// <summary>
/// Embedding 基础设施层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Embedding 基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddEmbeddingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<EmbeddingOptions>(
            configuration.GetSection(EmbeddingOptions.SectionName));

        // 注册 HttpClient
        services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(
            (serviceProvider, client) =>
            {
                var options = configuration
                    .GetSection(EmbeddingOptions.SectionName)
                    .Get<EmbeddingOptions>();

                if (options is not null)
                {
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                }
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}
