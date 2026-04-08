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

        // 基础文件操作命令
        command.AddCommand(FileUploadCommand.Create(serviceProvider));
        command.AddCommand(FileListCommand.Create(serviceProvider));
        command.AddCommand(FileShowCommand.Create(serviceProvider));
        command.AddCommand(FileContentCommand.Create(serviceProvider));
        command.AddCommand(FileDeleteCommand.Create(serviceProvider));

        // 全局文件库命令
        command.AddCommand(FileLibraryCommand.Create(serviceProvider));

        // 权限管理命令
        command.AddCommand(FilePermissionCommand.CreateShareCommand(serviceProvider));
        command.AddCommand(FilePermissionCommand.CreateRevokeCommand(serviceProvider));
        command.AddCommand(FilePermissionCommand.CreateAccessCommand(serviceProvider));
        command.AddCommand(FilePermissionCommand.CreatePermissionsCommand(serviceProvider));

        // 版本管理命令
        command.AddCommand(FileVersionCommand.CreateVersionsCommand(serviceProvider));
        command.AddCommand(FileVersionCommand.CreateRestoreCommand(serviceProvider));

        return command;
    }
}
