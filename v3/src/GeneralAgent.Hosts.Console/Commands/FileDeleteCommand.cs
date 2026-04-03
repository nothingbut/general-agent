using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file delete 命令 - 删除文件
/// </summary>
public static class FileDeleteCommand
{
    /// <summary>
    /// 创建 file delete 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("delete", "删除文件");

        // 参数：文件 ID
        var idArgument = new Argument<string>(
            name: "id",
            description: "文件 ID（可以是完整 GUID 或前几位）");
        command.AddArgument(idArgument);

        // 选项：跳过确认
        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "跳过确认提示");
        command.AddOption(forceOption);

        command.SetHandler(async (idInput, force) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var fileStorage = scope.ServiceProvider.GetRequiredService<FileStorageService>();

                // 尝试解析为 GUID
                Guid fileId;
                if (Guid.TryParse(idInput, out var parsedGuid))
                {
                    fileId = parsedGuid;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ 无效的文件 ID: {idInput}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 先获取文件信息（用于确认和显示）
                var file = await fileStorage.GetFileAsync(fileId);

                if (file == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]未找到文件: {fileId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 确认删除
                if (!force)
                {
                    var confirmPanel = new Panel(new Markup($"""
                        [yellow]⚠ 即将删除文件：[/]

                        [cyan]文件名:[/] {file.FileName}
                        [cyan]文件类型:[/] {file.FileType}
                        [cyan]文件大小:[/] {FormatFileSize(file.FileSize)}
                        [cyan]上传时间:[/] {file.UploadedAt:yyyy-MM-dd HH:mm:ss}

                        [red]此操作无法撤销！[/]
                        """))
                    {
                        Header = new PanelHeader("确认删除"),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Yellow)
                    };

                    AnsiConsole.Write(confirmPanel);
                    AnsiConsole.WriteLine();

                    var confirmed = AnsiConsole.Confirm("确定要删除此文件吗？", false);

                    if (!confirmed)
                    {
                        AnsiConsole.MarkupLine("[dim]已取消删除[/]");
                        return;
                    }
                }

                // 执行删除
                await AnsiConsole.Status()
                    .StartAsync("正在删除文件...", async ctx =>
                    {
                        var deleted = await fileStorage.DeleteFileAsync(fileId);

                        if (deleted)
                        {
                            ctx.Status("删除完成");
                            AnsiConsole.MarkupLine($"[green]✓ 文件已删除: {file.FileName}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]文件删除失败（可能已被删除）[/]");
                        }
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 删除文件失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, idArgument, forceOption);

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
