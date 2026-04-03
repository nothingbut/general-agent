using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file content 命令 - 显示文件内容
/// </summary>
public static class FileContentCommand
{
    /// <summary>
    /// 创建 file content 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("content", "显示文件内容");

        // 参数：文件 ID
        var idArgument = new Argument<string>(
            name: "id",
            description: "文件 ID（可以是完整 GUID 或前几位）");
        command.AddArgument(idArgument);

        // 选项：行数限制
        var linesOption = new Option<int?>(
            aliases: new[] { "--lines", "-n" },
            description: "限制显示的行数");
        command.AddOption(linesOption);

        command.SetHandler(async (idInput, maxLines) =>
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

                // 获取文件
                var file = await fileStorage.GetFileAsync(fileId);

                if (file == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]未找到文件: {fileId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 读取文件内容
                var processedContent = await fileStorage.ReadFileContentAsync(file);

                // 显示文件头部信息
                var headerPanel = new Panel(new Markup($"""
                    [cyan]文件名:[/] {file.FileName}
                    [cyan]类型:[/] {file.FileType}
                    [cyan]大小:[/] {FormatFileSize(file.FileSize)}
                    {(processedContent.IsTruncated ? $"[yellow]⚠ 内容已截断（原始: {processedContent.OriginalLength} 字符，显示: {processedContent.ProcessedLength} 字符）[/]" : "")}
                    """))
                {
                    Header = new PanelHeader("文件信息"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(headerPanel);
                AnsiConsole.WriteLine();

                // 处理内容
                var content = processedContent.Content;

                // 如果指定了行数限制
                if (maxLines.HasValue && maxLines.Value > 0)
                {
                    var lines = content.Split('\n');
                    if (lines.Length > maxLines.Value)
                    {
                        content = string.Join('\n', lines.Take(maxLines.Value));
                        content += $"\n\n... [显示前 {maxLines.Value} 行，共 {lines.Length} 行] ...";
                    }
                }

                // 显示内容（语法高亮）
                var language = GetLanguageForSyntaxHighlight(file.FileType);

                // 显示内容（暂时不使用语法高亮，因为 Spectre.Console 的 Syntax 类需要特定配置）
                var panel = new Panel(content)
                {
                    Header = new PanelHeader($"文件内容 ({GetLanguageForSyntaxHighlight(file.FileType) ?? "text"})"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey)
                };
                AnsiConsole.Write(panel);

                // 显示元数据
                if (processedContent.Metadata != null && processedContent.Metadata.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    var metadataPanel = new Panel(FormatMetadata(processedContent.Metadata))
                    {
                        Header = new PanelHeader("元数据"),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Grey)
                    };
                    AnsiConsole.Write(metadataPanel);
                }
            }
            catch (FileNotFoundException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 文件不存在: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 读取文件内容失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, idArgument, linesOption);

        return command;
    }

    /// <summary>
    /// 根据文件类型获取语法高亮语言
    /// </summary>
    private static string? GetLanguageForSyntaxHighlight(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".rs" => "rust",
            ".go" => "go",
            ".java" => "java",
            ".cpp" or ".c" or ".h" => "cpp",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".xml" => "xml",
            ".html" => "html",
            ".css" => "css",
            ".sh" or ".bash" => "bash",
            ".md" or ".markdown" => "markdown",
            _ => null
        };
    }

    /// <summary>
    /// 格式化元数据
    /// </summary>
    private static string FormatMetadata(Dictionary<string, object> metadata)
    {
        var lines = new List<string>();

        foreach (var kvp in metadata)
        {
            lines.Add($"[cyan]{kvp.Key}:[/] {kvp.Value}");
        }

        return string.Join("\n", lines);
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
