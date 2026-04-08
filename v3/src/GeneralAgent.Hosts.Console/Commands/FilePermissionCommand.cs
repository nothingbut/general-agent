using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file share/revoke/access/permissions 命令 - 文件权限管理
/// </summary>
public static class FilePermissionCommand
{
    /// <summary>
    /// 创建 file share 命令
    /// </summary>
    public static Command CreateShareCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("share", "共享文件给其他用户");

        // 参数：文件 ID
        var fileIdArgument = new Argument<string>(
            name: "file-id",
            description: "文件 ID");
        command.AddArgument(fileIdArgument);

        // 选项：目标用户 ID
        var userOption = new Option<string>(
            aliases: new[] { "--user", "-u" },
            description: "目标用户 ID")
        { IsRequired = true };
        command.AddOption(userOption);

        // 选项：权限类型
        var permissionOption = new Option<string>(
            aliases: new[] { "--permission", "-p" },
            getDefaultValue: () => "read",
            description: "权限类型 (read, write)");
        command.AddOption(permissionOption);

        command.SetHandler(async (fileIdStr, targetUserId, permissionStr) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var permissionService = scope.ServiceProvider.GetRequiredService<IFilePermissionService>();
                var currentUserId = GetCurrentUserId();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 解析权限类型
                if (!Enum.TryParse<PermissionType>(permissionStr, true, out var permission))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的权限类型: {permissionStr}[/]");
                    AnsiConsole.MarkupLine("[dim]有效值: read, write[/]");
                    Environment.Exit(1);
                    return;
                }

                // 授予权限
                await AnsiConsole.Status()
                    .StartAsync("正在授予权限...", async ctx =>
                    {
                        await permissionService.GrantPermissionAsync(
                            fileId, targetUserId, currentUserId, permission);
                    });

                // 显示成功消息
                var permissionColor = permission == PermissionType.Write ? "yellow" : "green";
                var panel = new Panel(new Markup($"""
                    [green]✓ 权限已授予[/]

                    [cyan]文件 ID:[/] {fileId}
                    [cyan]目标用户:[/] {targetUserId}
                    [cyan]权限类型:[/] [{permissionColor}]{permission}[/]

                    [dim]用户 {targetUserId} 现在可以{(permission == PermissionType.Write ? "读写" : "读取")}该文件[/]
                    """))
                {
                    Header = new PanelHeader("权限管理"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Green)
                };

                AnsiConsole.Write(panel);
            }
            catch (UnauthorizedAccessException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 权限不足: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 操作失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 授予权限失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, userOption, permissionOption);

        return command;
    }

    /// <summary>
    /// 创建 file revoke 命令
    /// </summary>
    public static Command CreateRevokeCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("revoke", "撤销用户对文件的访问权限");

        // 参数：文件 ID
        var fileIdArgument = new Argument<string>(
            name: "file-id",
            description: "文件 ID");
        command.AddArgument(fileIdArgument);

        // 选项：目标用户 ID
        var userOption = new Option<string>(
            aliases: new[] { "--user", "-u" },
            description: "目标用户 ID")
        { IsRequired = true };
        command.AddOption(userOption);

        command.SetHandler(async (fileIdStr, targetUserId) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var permissionService = scope.ServiceProvider.GetRequiredService<IFilePermissionService>();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 确认撤销操作
                if (!AnsiConsole.Confirm($"确定要撤销用户 [cyan]{targetUserId}[/] 对文件的访问权限吗？", false))
                {
                    AnsiConsole.MarkupLine("[yellow]操作已取消[/]");
                    return;
                }

                // 撤销权限
                await AnsiConsole.Status()
                    .StartAsync("正在撤销权限...", async ctx =>
                    {
                        await permissionService.RevokePermissionAsync(fileId, targetUserId);
                    });

                // 显示成功消息
                AnsiConsole.MarkupLine($"[green]✓ 已撤销用户 {targetUserId} 对文件 {fileId} 的访问权限[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 撤销权限失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, userOption);

        return command;
    }

    /// <summary>
    /// 创建 file access 命令
    /// </summary>
    public static Command CreateAccessCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("access", "修改文件访问级别");

        // 参数：文件 ID
        var fileIdArgument = new Argument<string>(
            name: "file-id",
            description: "文件 ID");
        command.AddArgument(fileIdArgument);

        // 选项：访问级别
        var levelOption = new Option<string>(
            aliases: new[] { "--level", "-l" },
            description: "访问级别 (private, shared, public)")
        { IsRequired = true };
        command.AddOption(levelOption);

        command.SetHandler(async (fileIdStr, levelStr) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var permissionService = scope.ServiceProvider.GetRequiredService<IFilePermissionService>();
                var currentUserId = GetCurrentUserId();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 解析访问级别
                if (!Enum.TryParse<FileAccessLevel>(levelStr, true, out var level))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的访问级别: {levelStr}[/]");
                    AnsiConsole.MarkupLine("[dim]有效值: private, shared, public[/]");
                    Environment.Exit(1);
                    return;
                }

                // 显示警告信息
                if (level == FileAccessLevel.Private)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ 将文件改为私有会删除所有现有的权限记录[/]");
                    if (!AnsiConsole.Confirm("确定要继续吗？", false))
                    {
                        AnsiConsole.MarkupLine("[yellow]操作已取消[/]");
                        return;
                    }
                }

                // 更新访问级别
                await AnsiConsole.Status()
                    .StartAsync("正在更新访问级别...", async ctx =>
                    {
                        await permissionService.UpdateAccessLevelAsync(fileId, currentUserId, level);
                    });

                // 显示成功消息
                var levelColor = level switch
                {
                    FileAccessLevel.Private => "red",
                    FileAccessLevel.Shared => "yellow",
                    FileAccessLevel.Public => "green",
                    _ => "white"
                };

                var description = level switch
                {
                    FileAccessLevel.Private => "只有您可以访问",
                    FileAccessLevel.Shared => "可以授权特定用户访问",
                    FileAccessLevel.Public => "所有人都可以读取",
                    _ => ""
                };

                var panel = new Panel(new Markup($"""
                    [green]✓ 访问级别已更新[/]

                    [cyan]文件 ID:[/] {fileId}
                    [cyan]新的访问级别:[/] [{levelColor}]{level}[/]

                    [dim]{description}[/]
                    """))
                {
                    Header = new PanelHeader("访问级别管理"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Green)
                };

                AnsiConsole.Write(panel);
            }
            catch (UnauthorizedAccessException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 权限不足: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 操作失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 更新访问级别失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, levelOption);

        return command;
    }

    /// <summary>
    /// 创建 file permissions 命令
    /// </summary>
    public static Command CreatePermissionsCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("permissions", "查看文件的权限列表");

        // 参数：文件 ID
        var fileIdArgument = new Argument<string>(
            name: "file-id",
            description: "文件 ID");
        command.AddArgument(fileIdArgument);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (fileIdStr, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var permissionService = scope.ServiceProvider.GetRequiredService<IFilePermissionService>();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取权限列表
                var permissions = await permissionService.ListPermissionsAsync(fileId);

                if (permissions.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]该文件没有授予任何用户权限[/]");
                    AnsiConsole.MarkupLine("[dim]提示: 使用 'agent file share <file-id> --user <user-id>' 共享文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(permissions, fileId);
                }
                else
                {
                    OutputTable(permissions, fileId);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 查看权限失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, formatOption);

        return command;
    }

    #region 辅助方法

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    private static string GetCurrentUserId()
    {
        // 优先使用环境变量
        var userId = Environment.GetEnvironmentVariable("AGENT_USER_ID");
        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        // 使用系统用户名作为默认值
        return Environment.UserName ?? "default-user";
    }

    /// <summary>
    /// 表格格式输出
    /// </summary>
    private static void OutputTable(List<FilePermission> permissions, Guid fileId)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("用户 ID")
            .AddColumn("权限类型")
            .AddColumn("授予者")
            .AddColumn("授予时间");

        foreach (var permission in permissions.OrderByDescending(p => p.GrantedAt))
        {
            var permissionColor = permission.Permission == PermissionType.Write ? "yellow" : "green";

            table.AddRow(
                permission.UserId,
                $"[{permissionColor}]{permission.Permission}[/]",
                permission.GrantedBy,
                permission.GrantedAt.ToString("yyyy-MM-dd HH:mm")
            );
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader($"文件权限列表 - {fileId}"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"共 [cyan]{permissions.Count}[/] 个用户有访问权限");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]使用 'agent file revoke <file-id> --user <user-id>' 撤销权限[/]");
    }

    /// <summary>
    /// JSON 格式输出
    /// </summary>
    private static void OutputJson(List<FilePermission> permissions, Guid fileId)
    {
        var permissionsData = permissions.Select(p => new
        {
            p.Id,
            p.FileId,
            p.UserId,
            Permission = p.Permission.ToString(),
            p.GrantedBy,
            GrantedAt = p.GrantedAt.ToString("O")
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            FileId = fileId,
            Total = permissions.Count,
            Permissions = permissionsData
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        System.Console.WriteLine(json);
    }

    #endregion
}
