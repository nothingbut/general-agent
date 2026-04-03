using GeneralAgent.Infrastructure.FileStorage.Parsers;
using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeneralAgent.Infrastructure.FileStorage.Extensions;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加文件存储服务
    /// </summary>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        Action<FileStorageOptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<FileStorageOptions>(_ => { });
        }

        // 注册仓储
        services.TryAddSingleton<FileRepository>();

        // 注册文件处理器
        services.TryAddSingleton<IFileProcessor, TextFileProcessor>();
        services.TryAddSingleton<IFileProcessor, CodeFileProcessor>();
        services.TryAddSingleton<IFileProcessor, JsonFileProcessor>();

        // 注册处理器服务
        services.TryAddSingleton<FileProcessorService>();

        // 注册文件存储服务
        services.TryAddSingleton<FileStorageService>();

        // 注册文件引用解析器
        services.TryAddSingleton<FileReferenceParser>();

        return services;
    }
}
