using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace GeneralAgent.Infrastructure.VectorDB;

/// <summary>
/// VectorDB 基础设施层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 VectorDB 基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddVectorDB(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<VectorDBOptions>(
            configuration.GetSection(VectorDBOptions.SectionName));

        // 提前读取配置（避免每次解析重复反序列化）
        var vectorDBOptions = configuration
            .GetSection(VectorDBOptions.SectionName)
            .Get<VectorDBOptions>() ?? new VectorDBOptions();

        // 注册 QdrantClient
        services.AddSingleton<QdrantClient>(sp =>
        {
            // 验证 Url 是有效的 URI
            if (!Uri.TryCreate(vectorDBOptions.Url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"Invalid VectorDB Url: '{vectorDBOptions.Url}'. Must be a valid absolute URI.");
            }

            // QdrantClient 使用 gRPC，需要主机和端口
            // REST API: 6333, gRPC: 6334
            var host = uri.Host;
            var port = uri.Port == 6333 ? 6334 : uri.Port; // 如果是 REST 端口，转换为 gRPC 端口

            return new QdrantClient(host, port);
        });

        // 注册 IQdrantClient 包装器
        services.AddSingleton<IQdrantClient>(sp =>
        {
            var client = sp.GetRequiredService<QdrantClient>();
            return new QdrantClientWrapper(client);
        });

        // 注册向量存储库
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();

        return services;
    }
}
