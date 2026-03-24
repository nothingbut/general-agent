using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// config 命令组 - 配置管理
/// </summary>
public static class ConfigCommand
{
    /// <summary>
    /// 创建 config 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("config", "配置管理命令");

        // 添加子命令
        command.AddCommand(ConfigShowCommand.Create(serviceProvider));
        command.AddCommand(ConfigSetCommand.Create(serviceProvider));
        command.AddCommand(ConfigResetCommand.Create(serviceProvider));

        return command;
    }
}
