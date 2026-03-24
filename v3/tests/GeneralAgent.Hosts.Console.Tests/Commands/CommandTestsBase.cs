using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// 命令测试基类
/// 提供最小化的 ServiceProvider 用于创建命令（不执行）
/// </summary>
public abstract class CommandTestsBase
{
    protected readonly IServiceProvider ServiceProvider;

    protected CommandTestsBase()
    {
        var services = new ServiceCollection();

        // 添加必要的服务（用于命令创建，不实际执行）
        // 这些服务在命令执行时才需要，创建命令时不需要

        ServiceProvider = services.BuildServiceProvider();
    }
}
