using System.CommandLine;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Hosts.Console.Utils;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file upload 命令 - 上传文件
/// </summary>
public static class FileUploadCommand
{
    /// <summary>
    /// 创建 file upload 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("upload", "上传文件到当前会话");

        // 参数：文件路径
        var pathArgument = new Argument<string>(
            name: "path",
            description: "要上传的文件路径");
        command.AddArgument(pathArgument);

        // 选项：是否保存到记忆
        var toMemoryOption = new Option<bool>(
            aliases: new[] { "--to-memory", "-m" },
            description: "自动提取文件内容到记忆系统");
        command.AddOption(toMemoryOption);

        command.SetHandler(async (path, toMemory) =>
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

                // 展开 ~ 路径
                if (path.StartsWith("~/"))
                {
                    path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        path[2..]);
                }
                else if (!Path.IsPathRooted(path))
                {
                    // 转换为绝对路径
                    path = Path.GetFullPath(path);
                }

                // 检查文件是否存在
                if (!File.Exists(path))
                {
                    AnsiConsole.MarkupLine($"[red]✗ 文件不存在: {path}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 显示上传进度
                var uploadedFile = await AnsiConsole.Status()
                    .StartAsync("正在上传文件...", async ctx =>
                    {
                        var file = await fileStorage.UploadFileAsync(
                            path,
                            currentSessionId.Value.ToString());

                        ctx.Status("上传完成");

                        // 显示上传结果
                        var panel = new Panel(new Markup($"""
                            [green]✓ 文件已上传[/]

                            [cyan]文件 ID:[/] {file.Id}
                            [cyan]文件名:[/] {file.FileName}
                            [cyan]文件类型:[/] {file.FileType}
                            [cyan]文件大小:[/] {FormatFileSize(file.FileSize)}
                            [cyan]MIME 类型:[/] {file.MimeType ?? "[dim]未知[/]"}
                            [cyan]上传时间:[/] {file.UploadedAt:yyyy-MM-dd HH:mm:ss}

                            [dim]使用 '@file:{file.FileName}' 或 '@file:{file.Id}' 引用此文件[/]
                            """))
                        {
                            Header = new PanelHeader("上传成功"),
                            Border = BoxBorder.Rounded,
                            BorderStyle = new Style(Color.Green)
                        };

                        AnsiConsole.Write(panel);
                        return file;
                    });

                // 如果指定了 --to-memory，提取文件内容到记忆
                if (toMemory)
                {
                    await AnsiConsole.Status()
                        .StartAsync("正在提取文件内容到记忆...", async ctx =>
                        {
                            var memoryService = scope.ServiceProvider.GetService<IMemoryExtractionService>();

                            if (memoryService == null)
                            {
                                AnsiConsole.MarkupLine("[yellow]⚠ 记忆服务未启用，跳过记忆提取[/]");
                                return;
                            }

                            try
                            {
                                // 读取文件内容
                                var content = await fileStorage.ReadFileContentAsync(uploadedFile);

                                // 构建提取上下文
                                var context = $"文件名: {uploadedFile.FileName}\n" +
                                              $"文件类型: {uploadedFile.FileType}\n" +
                                              $"上传时间: {uploadedFile.UploadedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                                              $"文件内容:\n{content.Content}";

                                // 提取记忆建议
                                var suggestions = await memoryService.ExtractFromMessageAsync(
                                    context,
                                    conversationContext: $"用户上传了文件 {uploadedFile.FileName}",
                                    cancellationToken: default);

                                if (suggestions.Count == 0)
                                {
                                    AnsiConsole.MarkupLine("[dim]未从文件中提取到记忆[/]");
                                    return;
                                }

                                // 自动保存高置信度的记忆（> 0.7）
                                var savedCount = 0;
                                foreach (var suggestion in suggestions.Where(s => s.Confidence > 0.7))
                                {
                                    var memory = await memoryService.CreateMemoryFromSuggestionAsync(
                                        suggestion,
                                        cancellationToken: default);

                                    if (memory != null)
                                    {
                                        savedCount++;
                                        AnsiConsole.MarkupLine(
                                            $"[green]✓[/] 已保存记忆: [cyan]{memory.Name}[/] ({suggestion.Type})");
                                    }
                                }

                                if (savedCount > 0)
                                {
                                    AnsiConsole.MarkupLine($"\n[green]✓ 成功保存 {savedCount} 条记忆[/]");
                                }
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[yellow]⚠ 记忆提取失败: {ex.Message}[/]");
                            }
                        });
                }
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 文件验证失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 上传失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 上传文件时发生错误: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, pathArgument, toMemoryOption);

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
