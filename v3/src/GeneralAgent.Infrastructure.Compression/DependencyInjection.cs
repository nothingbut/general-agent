using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Compression.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.Compression;

/// <summary>
/// 压缩服务的依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加压缩服务到 DI 容器
    /// </summary>
    public static IServiceCollection AddCompression(this IServiceCollection services)
    {
        // 注册 Token 计数器
        services.AddSingleton<ITokenCounter, TokenCounter>();

        // 注册压缩策略
        services.AddSingleton<ICompressionStrategy, SlidingWindowStrategy>();
        services.AddSingleton<ICompressionStrategy, HierarchicalStrategy>();
        services.AddSingleton<ICompressionStrategy, SemanticStrategy>();

        // 注册编排器
        services.AddSingleton<ICompressionOrchestrator, CompressionOrchestrator>();

        // 注册压缩服务
        services.AddScoped<CompressionService>();

        return services;
    }
}
