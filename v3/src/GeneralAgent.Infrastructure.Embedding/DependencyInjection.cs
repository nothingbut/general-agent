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

        // 提前读取配置（避免每次解析重复反序列化）
        var embeddingOptions = configuration
            .GetSection(EmbeddingOptions.SectionName)
            .Get<EmbeddingOptions>() ?? new EmbeddingOptions();

        // 注册 HttpClient
        services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(client =>
        {
            // 验证 BaseUrl 是有效的 URI
            if (!Uri.TryCreate(embeddingOptions.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    $"Invalid Embedding BaseUrl: '{embeddingOptions.BaseUrl}'. Must be a valid absolute URI.");
            }

            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromSeconds(embeddingOptions.TimeoutSeconds);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}
