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

                // 显示文件详情
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]属性[/]")
                    .AddColumn("[bold]值[/]");

                table.AddRow("[cyan]文件 ID[/]", file.Id.ToString());
                table.AddRow("[cyan]会话 ID[/]", file.SessionId);
                table.AddRow("[cyan]文件名[/]", file.FileName);
                table.AddRow("[cyan]文件类型[/]", file.FileType);
                table.AddRow("[cyan]文件大小[/]", FormatFileSize(file.FileSize));
                table.AddRow("[cyan]MIME 类型[/]", file.MimeType ?? "[dim]未知[/]");
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
                var panel = new Panel(new Markup($"""
                    [dim]在对话中引用此文件：[/]
                    • [green]@file:{file.FileName}[/]
                    • [green]@file:{file.Id}[/]

                    [dim]查看文件内容：[/]
                    • [green]agent file content {file.Id}[/]
                    """))
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
