using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Parsers;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.Skills;

/// <summary>
/// Skills 系统依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加技能系统服务
    /// </summary>
    public static IServiceCollection AddSkills(this IServiceCollection services)
    {
        // 注册核心服务
        services.AddSingleton<ISkillParser, MarkdownSkillParser>();
        services.AddSingleton<ISkillLoader, FileSystemSkillLoader>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<ISkillExecutor, SkillExecutor>();

        return services;
    }
}
