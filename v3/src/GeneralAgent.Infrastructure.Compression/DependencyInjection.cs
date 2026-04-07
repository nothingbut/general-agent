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
    /// <param name="services">服务集合</param>
    /// <param name="enableCaching">是否启用压缩结果缓存（默认 false）</param>
    /// <param name="cacheDuration">缓存持续时间（默认 1 小时）</param>
    public static IServiceCollection AddCompression(
        this IServiceCollection services,
        bool enableCaching = false,
        TimeSpan? cacheDuration = null)
    {
        // 注册 Token 计数器
        services.AddSingleton<ITokenCounter, TokenCounter>();

        // 注册压缩策略
        services.AddSingleton<ICompressionStrategy, SlidingWindowStrategy>();
        services.AddSingleton<ICompressionStrategy, HierarchicalStrategy>();
        services.AddSingleton<ICompressionStrategy, SemanticStrategy>();

        // 注册编排器（根据是否启用缓存选择实现）
        if (enableCaching)
        {
            // 启用缓存：使用装饰器模式
            // 1. 先注册内部实现
            services.AddSingleton<CompressionOrchestrator>();

            // 2. 注册缓存装饰器，包装内部实现
            services.AddSingleton<ICompressionOrchestrator>(provider =>
            {
                var inner = provider.GetRequiredService<CompressionOrchestrator>();
                var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedCompressionOrchestrator>>();

                return new CachedCompressionOrchestrator(inner, cache, logger, cacheDuration);
            });
        }
        else
        {
            // 不启用缓存：直接注册
            services.AddSingleton<ICompressionOrchestrator, CompressionOrchestrator>();
        }

        // 注册压缩服务
        services.AddScoped<CompressionService>();

        return services;
    }
}
