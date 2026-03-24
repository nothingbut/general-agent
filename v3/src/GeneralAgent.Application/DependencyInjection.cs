using GeneralAgent.Application.DependencyInjection;
using GeneralAgent.Application.Services;
using GeneralAgent.Infrastructure.Skills;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Application;

/// <summary>
/// Application 层依赖注入扩展
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// 添加 Application 层服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置（用于 Tool Calling 服务）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddApplicationLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册技能系统
        services.AddSkills();

        // 注册技能服务
        services.AddSingleton<SkillService>();

        // 注册 Tool Calling 相关服务
        services.AddToolCallingServices(configuration);

        // 注册应用层服务（Scoped 生命周期）
        services.AddScoped<SessionService>();
        services.AddScoped<ConversationService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        return services;
    }
}
