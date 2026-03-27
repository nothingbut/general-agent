using GeneralAgent.Core.Abstractions;
using GeneralAgent.Infrastructure.Memory.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.Memory;

/// <summary>
/// 依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加记忆系统服务
    /// </summary>
    public static IServiceCollection AddMemoryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置选项
        services.Configure<MemoryOptions>(
            configuration.GetSection(MemoryOptions.SectionName));

        // 注册仓储
        services.AddSingleton<IMemoryRepository, MemoryRepository>();
        services.AddSingleton<IMemoryIndexManager, MemoryIndexManager>();

        return services;
    }
}
