using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using GeneralAgent.Infrastructure.Skills.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeneralAgent.Infrastructure.SkillExtraction.Extensions;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加技能提取服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项（可选）</param>
    /// <param name="enableCaching">是否启用缓存（默认 true）</param>
    public static IServiceCollection AddSkillExtraction(
        this IServiceCollection services,
        Action<SkillExtractionOptions>? configureOptions = null,
        bool enableCaching = true)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<SkillExtractionOptions>(_ => { });
        }

        // 注册技能解析器（如果尚未注册）
        services.TryAddSingleton<ISkillParser, MarkdownSkillParser>();

        // 注册技能提取服务（根据是否启用缓存选择实现）
        if (enableCaching)
        {
            // 确保 IMemoryCache 已注册
            services.AddMemoryCache();

            // 注册内部服务
            services.TryAddSingleton<SkillExtractionService>();

            // 注册缓存装饰器
            services.TryAddSingleton<ISkillExtractionService>(provider =>
            {
                var innerService = provider.GetRequiredService<SkillExtractionService>();
                var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedSkillExtractionService>>();
                return new CachedSkillExtractionService(innerService, cache, logger);
            });
        }
        else
        {
            services.TryAddSingleton<ISkillExtractionService, SkillExtractionService>();
        }

        // 注册技能生成器
        services.TryAddSingleton<ISkillGenerator, SkillGenerator>();

        // 注册技能写入器
        services.TryAddSingleton<ISkillWriter, SkillWriter>();

        // 注册提取历史仓储（默认使用内存实现）
        services.TryAddSingleton<IExtractionHistoryRepository, InMemoryExtractionHistoryRepository>();

        // 注册用户交互（默认使用测试实现，可在应用层替换为真实实现）
        services.TryAddSingleton<IUserInteraction, TestUserInteraction>();

        // 注册编排器
        services.TryAddSingleton<ISkillExtractionOrchestrator, SkillExtractionOrchestrator>();

        // 注册历史服务
        services.TryAddSingleton<IExtractionHistoryService, ExtractionHistoryService>();

        return services;
    }
}
