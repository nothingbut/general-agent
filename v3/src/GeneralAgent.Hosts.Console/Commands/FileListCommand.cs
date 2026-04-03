using System.CommandLine;
using GeneralAgent.Hosts.Console.Utils;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file list 命令 - 列出当前会话的所有文件
/// </summary>
public static class FileListCommand
{
    /// <summary>
    /// 创建 file list 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "列出当前会话的所有文件");

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var fileStorage = scope.ServiceProvider.GetRequiredService<FileStorageService>();

                // 获取当前会话
                var currentSessionId = await SessionSelector.GetCurrentSessionIdAsync();
                if (!currentSessionId.HasValue)
                {
                    AnsiConsole.MarkupLine("[red]✗ 没有活跃的会话，请先创建或切换到一个会话[/]");
                    AnsiConsole.MarkupLine("[dim]提示: 使用 'agent new' 创建新会话或 'agent switch <ID>' 切换会话[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取文件列表
                var files = await fileStorage.ListFilesAsync(currentSessionId.Value.ToString());

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]当前会话没有上传的文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var filesData = files.Select(f => new
                    {
                        f.Id,
                        f.FileName,
                        f.FileType,
                        FileSizeBytes = f.FileSize,
                        f.MimeType,
                        UploadedAt = f.UploadedAt.ToString("O"),
                        f.Summary,
                        f.Tags
                    }).ToList();

                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SessionId = currentSessionId.Value.ToString(),
                        Total = files.Count,
                        Files = filesData
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("文件 ID")
                        .AddColumn("文件名")
                        .AddColumn("类型")
                        .AddColumn("大小")
                        .AddColumn("上传时间");

                    foreach (var file in files.OrderByDescending(f => f.UploadedAt))
                    {
                        table.AddRow(
                            $"[cyan]{file.Id.ToString()[..8]}...[/]",
                            file.FileName,
                            file.FileType,
                            FormatFileSize(file.FileSize),
                            file.UploadedAt.ToString("yyyy-MM-dd HH:mm")
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n共 [cyan]{files.Count}[/] 个文件");

                    // 显示使用提示
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用 'agent file show <ID>' 查看文件详情[/]");
                    AnsiConsole.MarkupLine("[dim]使用 'agent file content <ID>' 查看文件内容[/]");
                    AnsiConsole.MarkupLine("[dim]在对话中使用 '@file:<文件名>' 或 '@file:<ID>' 引用文件[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 列出文件失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, formatOption);

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
