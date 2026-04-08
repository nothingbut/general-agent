using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file show 命令 - 显示文件详细信息
/// </summary>
public static class FileShowCommand
{
    /// <summary>
    /// 创建 file show 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("show", "显示文件的详细信息");

        // 参数：文件 ID
        var idArgument = new Argument<string>(
            name: "id",
            description: "文件 ID（可以是完整 GUID 或前几位）");
        command.AddArgument(idArgument);

        command.SetHandler(async (idInput) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var fileStorage = scope.ServiceProvider.GetRequiredService<FileStorageService>();
                var permissionService = scope.ServiceProvider.GetRequiredService<IFilePermissionService>();
                var versionService = scope.ServiceProvider.GetRequiredService<IFileVersionService>();

                // 尝试解析为 GUID
                Guid fileId;
                if (Guid.TryParse(idInput, out var parsedGuid))
                {
                    fileId = parsedGuid;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {idInput}[/]");
                    AnsiConsole.MarkupLine("[dim]提示: 请提供完整的 GUID 或使用 'agent file list' 查看所有文件[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取文件
                var file = await fileStorage.GetFileAsync(fileId);

                if (file == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]未找到文件: {fileId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取权限信息
                var permissions = await permissionService.ListPermissionsAsync(fileId);

                // 获取版本信息
                List<Infrastructure.FileStorage.Models.UploadedFile> versions;
                try
                {
                    versions = await versionService.GetVersionHistoryAsync(fileId);
                }
                catch
                {
                    versions = new List<Infrastructure.FileStorage.Models.UploadedFile>();
                }

                // 显示文件详情
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]属性[/]")
                    .AddColumn("[bold]值[/]");

                table.AddRow("[cyan]文件 ID[/]", file.Id.ToString());
                table.AddRow("[cyan]会话 ID[/]", file.SessionId);
                table.AddRow("[cyan]文件名[/]", file.FileName);
                table.AddRow("[cyan]所有者[/]", file.OwnerId);

                var levelColor = file.AccessLevel switch
                {
                    Infrastructure.FileStorage.Models.FileAccessLevel.Private => "red",
                    Infrastructure.FileStorage.Models.FileAccessLevel.Shared => "yellow",
                    Infrastructure.FileStorage.Models.FileAccessLevel.Public => "green",
                    _ => "white"
                };
                table.AddRow("[cyan]访问级别[/]", $"[{levelColor}]{file.AccessLevel}[/]");

                table.AddRow("[cyan]文件类型[/]", file.FileType);
                table.AddRow("[cyan]文件大小[/]", FormatFileSize(file.FileSize));
                table.AddRow("[cyan]MIME 类型[/]", file.MimeType ?? "[dim]未知[/]");

                if (versions.Count > 0)
                {
                    table.AddRow("[cyan]当前版本[/]", $"v{file.Version} {(file.IsLatest ? "[green](最新)[/]" : "[dim](历史)[/]")}");
                    table.AddRow("[cyan]版本总数[/]", versions.Count.ToString());
                }

                if (permissions.Count > 0)
                {
                    table.AddRow("[cyan]授权用户数[/]", permissions.Count.ToString());
                }

                table.AddRow("[cyan]上传时间[/]", file.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                table.AddRow("[cyan]存储路径[/]", file.FilePath);

                if (!string.IsNullOrEmpty(file.Summary))
                {
                    table.AddRow("[cyan]文件摘要[/]", file.Summary);
                }

                if (!string.IsNullOrEmpty(file.Tags))
                {
                    table.AddRow("[cyan]标签[/]", file.Tags);
                }

                AnsiConsole.Write(table);

                // 显示引用提示
                AnsiConsole.WriteLine();
                var tips = new List<string>
                {
                    "[dim]在对话中引用此文件：[/]",
                    "• [green]@file:" + file.FileName + "[/]",
                    "• [green]@file:" + file.Id + "[/]",
                    "",
                    "[dim]查看文件内容：[/]",
                    "• [green]agent file content " + file.Id + "[/]"
                };

                if (versions.Count > 1)
                {
                    tips.Add("");
                    tips.Add("[dim]版本管理：[/]");
                    tips.Add("• [green]agent file versions " + file.Id + "[/] - 查看版本历史");
                    tips.Add("• [green]agent file restore " + file.Id + " --version <number>[/] - 恢复到特定版本");
                }

                if (file.AccessLevel == Infrastructure.FileStorage.Models.FileAccessLevel.Private ||
                    file.AccessLevel == Infrastructure.FileStorage.Models.FileAccessLevel.Shared)
                {
                    tips.Add("");
                    tips.Add("[dim]权限管理：[/]");
                    tips.Add("• [green]agent file share " + file.Id + " --user <user-id>[/] - 共享文件");
                    tips.Add("• [green]agent file permissions " + file.Id + "[/] - 查看权限列表");
                    tips.Add("• [green]agent file access " + file.Id + " --level <private|shared|public>[/] - 修改访问级别");
                }

                var panel = new Panel(new Markup(string.Join("\n", tips)))
                {
                    Header = new PanelHeader("使用提示"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(panel);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 查看文件失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, idArgument);

        return command;
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
