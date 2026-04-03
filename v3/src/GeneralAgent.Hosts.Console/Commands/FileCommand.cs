using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file 命令组 - 文件管理
/// </summary>
public static class FileCommand
{
    /// <summary>
    /// 创建 file 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("file", "文件管理命令");

        // 添加子命令
        command.AddCommand(FileUploadCommand.Create(serviceProvider));
        command.AddCommand(FileListCommand.Create(serviceProvider));
        command.AddCommand(FileShowCommand.Create(serviceProvider));
        command.AddCommand(FileContentCommand.Create(serviceProvider));
        command.AddCommand(FileDeleteCommand.Create(serviceProvider));

        return command;
    }
}
