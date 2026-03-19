using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Skills.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Application;

/// <summary>
/// Application 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Application 层服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        // 注册技能系统
        services.AddSkills();

        // 注册工具系统核心组件
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<SkillToToolConverter>();
        services.AddSingleton<ToolExecutor>();

        // 注册 Tool Calling 相关服务
        services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();

        // 注册 ToolCallingOrchestrator（需要 ILLMClient 和 IToolSerializer）
        services.AddScoped<ToolCallingOrchestrator>();

        // 注册 ToolCallingConfig（如果未配置，使用默认值）
        services.AddOptions<ToolCallingConfig>()
            .Configure(config =>
            {
                // 使用默认值，可通过配置文件覆盖
            });

        // 注册技能服务
        services.AddSingleton<SkillService>();

        // 注册应用层服务（Scoped 生命周期）
        services.AddScoped<SessionService>();
        services.AddScoped<ConversationService>();

        return services;
    }
}
