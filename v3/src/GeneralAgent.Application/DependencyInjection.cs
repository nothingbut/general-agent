using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;

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
        // 注册应用层服务（Scoped 生命周期）
        services.AddScoped<SessionService>();
        services.AddScoped<ConversationService>();

        return services;
    }
}
