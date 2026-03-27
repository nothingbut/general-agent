using GeneralAgent.Core.Abstractions;
using GeneralAgent.Infrastructure.Compression.Services;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure;

/// <summary>
/// Infrastructure 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Infrastructure 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">SQLite 连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // 注册 DbContext
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite(connectionString));

        // 注册 Repositories
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<ICompressionHistoryRepository, CompressionHistoryRepository>();
        services.AddScoped<ICompressionConfigRepository, CompressionConfigRepository>();

        return services;
    }
}
