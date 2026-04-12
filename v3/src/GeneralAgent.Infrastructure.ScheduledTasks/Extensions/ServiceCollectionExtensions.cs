using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using GeneralAgent.Infrastructure.ScheduledTasks.Repositories;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskScheduler = GeneralAgent.Infrastructure.ScheduledTasks.Services.TaskScheduler;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Extensions;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加计划任务服务
    /// </summary>
    /// <param name="enableBackgroundService">是否启用后台服务（自动启动调度器）</param>
    public static IServiceCollection AddScheduledTasks(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableBackgroundService = true)
    {
        // 配置选项
        services.Configure<ScheduledTasksOptions>(
            configuration.GetSection("ScheduledTasks"));

        // 注册 Repository
        services.AddSingleton<IScheduledTaskRepository, ScheduledTaskRepository>();
        services.AddSingleton<ITaskExecutionRepository, TaskExecutionRepository>();

        // 注册 Parser
        services.AddSingleton<ICronParser, CronParser>();
        services.AddSingleton<INaturalLanguageTimeParser, NaturalLanguageTimeParser>();

        // 注册 Service
        services.AddSingleton<ITaskExecutor, TaskExecutor>();
        services.AddSingleton<ITaskScheduler, TaskScheduler>();
        services.AddSingleton<ITaskManager, TaskManager>();

        // 可选：注册后台服务
        if (enableBackgroundService)
        {
            services.AddHostedService<ScheduledTasksBackgroundService>();
        }

        return services;
    }

    /// <summary>
    /// 添加计划任务服务（使用默认配置）
    /// </summary>
    /// <param name="configureOptions">配置选项委托</param>
    /// <param name="enableBackgroundService">是否启用后台服务（自动启动调度器）</param>
    public static IServiceCollection AddScheduledTasks(
        this IServiceCollection services,
        Action<ScheduledTasksOptions>? configureOptions = null,
        bool enableBackgroundService = true)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<ScheduledTasksOptions>(options => { });
        }

        // 注册 Repository
        services.AddSingleton<IScheduledTaskRepository, ScheduledTaskRepository>();
        services.AddSingleton<ITaskExecutionRepository, TaskExecutionRepository>();

        // 注册 Parser
        services.AddSingleton<ICronParser, CronParser>();
        services.AddSingleton<INaturalLanguageTimeParser, NaturalLanguageTimeParser>();

        // 注册 Service
        services.AddSingleton<ITaskExecutor, TaskExecutor>();
        services.AddSingleton<ITaskScheduler, TaskScheduler>();
        services.AddSingleton<ITaskManager, TaskManager>();

        // 可选：注册后台服务
        if (enableBackgroundService)
        {
            services.AddHostedService<ScheduledTasksBackgroundService>();
        }

        return services;
    }
}
