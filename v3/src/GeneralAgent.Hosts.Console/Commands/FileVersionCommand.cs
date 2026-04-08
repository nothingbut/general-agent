using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file versions/restore 命令 - 文件版本管理
/// </summary>
public static class FileVersionCommand
{
    /// <summary>
    /// 创建 file versions 命令
    /// </summary>
    public static Command CreateVersionsCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("versions", "查看文件的版本历史");

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
                var versionService = scope.ServiceProvider.GetRequiredService<IFileVersionService>();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取版本历史
                var versions = await versionService.GetVersionHistoryAsync(fileId);

                if (versions.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]文件不存在或没有版本历史: {fileId}[/]");
                    Environment.Exit(1);
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(versions, fileId);
                }
                else
                {
                    OutputTable(versions, fileId);
                }
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 操作失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 查看版本历史失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, formatOption);

        return command;
    }

    /// <summary>
    /// 创建 file restore 命令
    /// </summary>
    public static Command CreateRestoreCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("restore", "恢复文件到特定版本");

        // 参数：文件 ID
        var fileIdArgument = new Argument<string>(
            name: "file-id",
            description: "文件 ID");
        command.AddArgument(fileIdArgument);

        // 选项：目标版本号
        var versionOption = new Option<int>(
            aliases: new[] { "--version", "-v" },
            description: "要恢复到的版本号")
        { IsRequired = true };
        command.AddOption(versionOption);

        command.SetHandler(async (fileIdStr, targetVersion) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var versionService = scope.ServiceProvider.GetRequiredService<IFileVersionService>();
                var currentUserId = GetCurrentUserId();

                // 解析文件 ID
                if (!Guid.TryParse(fileIdStr, out var fileId))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {fileIdStr}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取版本历史以便显示信息
                var versions = await versionService.GetVersionHistoryAsync(fileId);
                var targetVersionInfo = versions.FirstOrDefault(v => v.Version == targetVersion);

                if (targetVersionInfo == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 版本不存在: v{targetVersion}[/]");
                    AnsiConsole.MarkupLine($"[dim]可用版本: {string.Join(", ", versions.Select(v => $"v{v.Version}"))}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 显示恢复信息
                var infoTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("属性")
                    .AddColumn("值");

                infoTable.AddRow("文件名", targetVersionInfo.FileName);
                infoTable.AddRow("目标版本", $"v{targetVersion}");
                infoTable.AddRow("版本创建时间", targetVersionInfo.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                infoTable.AddRow("文件大小", FormatFileSize(targetVersionInfo.FileSize));

                AnsiConsole.Write(infoTable);
                AnsiConsole.WriteLine();

                // 确认恢复操作
                if (!AnsiConsole.Confirm($"确定要将文件恢复到 [cyan]v{targetVersion}[/] 吗？", false))
                {
                    AnsiConsole.MarkupLine("[yellow]操作已取消[/]");
                    return;
                }

                // 执行恢复
                var restoredFile = await AnsiConsole.Status()
                    .StartAsync("正在恢复版本...", async ctx =>
                    {
                        return await versionService.RestoreVersionAsync(fileId, targetVersion, currentUserId);
                    });

                // 显示成功消息
                var panel = new Panel(new Markup($"""
                    [green]✓ 版本已恢复[/]

                    [cyan]文件 ID:[/] {restoredFile.Id}
                    [cyan]文件名:[/] {restoredFile.FileName}
                    [cyan]当前版本:[/] v{restoredFile.Version}
                    [cyan]恢复时间:[/] {restoredFile.UploadedAt:yyyy-MM-dd HH:mm:ss}

                    [dim]文件已恢复到 v{targetVersion} 的内容，并创建为新版本 v{restoredFile.Version}[/]
                    """))
                {
                    Header = new PanelHeader("版本恢复"),
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
                AnsiConsole.MarkupLine($"[red]✗ 恢复版本失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, fileIdArgument, versionOption);

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
    private static void OutputTable(List<Infrastructure.FileStorage.Models.UploadedFile> versions, Guid fileId)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("版本")
            .AddColumn("文件大小")
            .AddColumn("创建时间")
            .AddColumn("状态")
            .AddColumn("父版本");

        foreach (var version in versions.OrderBy(v => v.Version))
        {
            var statusText = version.IsLatest ? "[green]最新[/]" : "[dim]历史[/]";
            var parentText = version.ParentFileId.HasValue
                ? $"v{versions.FirstOrDefault(v => v.Id == version.ParentFileId.Value)?.Version ?? 0}"
                : "[dim]-[/]";

            table.AddRow(
                $"[cyan]v{version.Version}[/]",
                FormatFileSize(version.FileSize),
                version.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                statusText,
                parentText
            );
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader($"版本历史 - {versions.First().FileName}"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);

        AnsiConsole.WriteLine();
        var latestVersion = versions.FirstOrDefault(v => v.IsLatest);
        if (latestVersion != null)
        {
            AnsiConsole.MarkupLine($"当前最新版本: [cyan]v{latestVersion.Version}[/]");
        }
        AnsiConsole.MarkupLine($"共 [cyan]{versions.Count}[/] 个版本");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]使用 'agent file restore <file-id> --version <number>' 恢复到特定版本[/]");
        AnsiConsole.MarkupLine("[dim]使用 'agent file content <file-id> --version <number>' 查看特定版本内容[/]");
    }

    /// <summary>
    /// JSON 格式输出
    /// </summary>
    private static void OutputJson(List<Infrastructure.FileStorage.Models.UploadedFile> versions, Guid fileId)
    {
        var versionsData = versions.Select(v => new
        {
            v.Id,
            v.Version,
            v.FileName,
            FileSizeBytes = v.FileSize,
            v.IsLatest,
            v.ParentFileId,
            UploadedAt = v.UploadedAt.ToString("O")
        }).OrderBy(v => v.Version).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            FileId = fileId,
            FileName = versions.First().FileName,
            TotalVersions = versions.Count,
            LatestVersion = versions.FirstOrDefault(v => v.IsLatest)?.Version,
            Versions = versionsData
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        System.Console.WriteLine(json);
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

    #endregion
}
