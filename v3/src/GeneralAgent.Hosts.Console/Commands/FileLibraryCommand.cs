using System.CommandLine;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// file library 命令组 - 全局文件库管理
/// </summary>
public static class FileLibraryCommand
{
    /// <summary>
    /// 创建 file library 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("library", "全局文件库管理（跨会话文件访问）");

        // 添加子命令
        command.AddCommand(CreateListCommand(serviceProvider));
        command.AddCommand(CreateSearchCommand(serviceProvider));
        command.AddCommand(CreateOwnedCommand(serviceProvider));
        command.AddCommand(CreateSharedCommand(serviceProvider));
        command.AddCommand(CreatePublicCommand(serviceProvider));

        return command;
    }

    /// <summary>
    /// library list - 列出用户可访问的所有文件
    /// </summary>
    private static Command CreateListCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "列出用户可访问的所有文件（跨会话）");

        // 选项：访问级别过滤
        var levelOption = new Option<string?>(
            aliases: new[] { "--level", "-l" },
            description: "按访问级别过滤 (private, shared, public)");
        command.AddOption(levelOption);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (levelStr, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var libraryService = scope.ServiceProvider.GetRequiredService<IFileLibraryService>();
                var userId = GetCurrentUserId();

                // 解析访问级别
                FileAccessLevel? filterLevel = null;
                if (!string.IsNullOrEmpty(levelStr))
                {
                    if (Enum.TryParse<FileAccessLevel>(levelStr, true, out var level))
                    {
                        filterLevel = level;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 无效的访问级别: {levelStr}[/]");
                        AnsiConsole.MarkupLine("[dim]有效值: private, shared, public[/]");
                        Environment.Exit(1);
                        return;
                    }
                }

                // 获取文件列表
                var files = await libraryService.ListAccessibleFilesAsync(userId, filterLevel);

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]没有可访问的文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(files, userId);
                }
                else
                {
                    OutputTable(files, userId, filterLevel);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 列出文件失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, levelOption, formatOption);

        return command;
    }

    /// <summary>
    /// library search - 搜索文件
    /// </summary>
    private static Command CreateSearchCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("search", "搜索文件（按名称、标签、摘要）");

        // 参数：搜索关键词
        var keywordArgument = new Argument<string>(
            name: "keyword",
            description: "搜索关键词");
        command.AddArgument(keywordArgument);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (keyword, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var libraryService = scope.ServiceProvider.GetRequiredService<IFileLibraryService>();
                var userId = GetCurrentUserId();

                // 搜索文件
                var files = await libraryService.SearchFilesAsync(userId, keyword);

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]未找到包含 '{keyword}' 的文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(files, userId);
                }
                else
                {
                    OutputTable(files, userId, null, keyword);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 搜索失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, keywordArgument, formatOption);

        return command;
    }

    /// <summary>
    /// library owned - 列出用户拥有的文件
    /// </summary>
    private static Command CreateOwnedCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("owned", "列出用户拥有的所有文件");

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
                var libraryService = scope.ServiceProvider.GetRequiredService<IFileLibraryService>();
                var userId = GetCurrentUserId();

                // 获取拥有的文件
                var files = await libraryService.ListOwnedFilesAsync(userId);

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]您还没有上传任何文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(files, userId);
                }
                else
                {
                    OutputTable(files, userId, null, null, "您拥有的文件");
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
    /// library shared - 列出共享给用户的文件
    /// </summary>
    private static Command CreateSharedCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("shared", "列出他人共享给您的文件");

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
                var libraryService = scope.ServiceProvider.GetRequiredService<IFileLibraryService>();
                var userId = GetCurrentUserId();

                // 获取共享文件
                var files = await libraryService.ListSharedFilesAsync(userId);

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]没有人共享文件给您[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(files, userId);
                }
                else
                {
                    OutputTable(files, userId, null, null, "共享给您的文件");
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
    /// library public - 列出所有公开文件
    /// </summary>
    private static Command CreatePublicCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("public", "列出所有公开文件");

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
                var libraryService = scope.ServiceProvider.GetRequiredService<IFileLibraryService>();

                // 获取公开文件
                var files = await libraryService.ListPublicFilesAsync();

                if (files.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]没有公开文件[/]");
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    OutputJson(files, null);
                }
                else
                {
                    OutputTable(files, null, null, null, "公开文件");
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
    private static void OutputTable(
        List<UploadedFile> files,
        string? currentUserId,
        FileAccessLevel? filterLevel,
        string? searchKeyword = null,
        string? title = null)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("文件 ID")
            .AddColumn("文件名")
            .AddColumn("所有者")
            .AddColumn("访问级别")
            .AddColumn("大小")
            .AddColumn("上传时间");

        foreach (var file in files.OrderByDescending(f => f.UploadedAt))
        {
            var ownerDisplay = file.OwnerId == currentUserId ? "[green]您[/]" : file.OwnerId;
            var levelColor = file.AccessLevel switch
            {
                FileAccessLevel.Private => "red",
                FileAccessLevel.Shared => "yellow",
                FileAccessLevel.Public => "green",
                _ => "white"
            };

            table.AddRow(
                $"[cyan]{file.Id.ToString()[..8]}...[/]",
                file.FileName,
                ownerDisplay,
                $"[{levelColor}]{file.AccessLevel}[/]",
                FormatFileSize(file.FileSize),
                file.UploadedAt.ToString("yyyy-MM-dd HH:mm")
            );
        }

        // 显示标题
        if (!string.IsNullOrEmpty(title))
        {
            var panel = new Panel(table)
            {
                Header = new PanelHeader(title),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(panel);
        }
        else
        {
            AnsiConsole.Write(table);
        }

        // 显示统计信息
        AnsiConsole.WriteLine();
        if (!string.IsNullOrEmpty(searchKeyword))
        {
            AnsiConsole.MarkupLine($"共找到 [cyan]{files.Count}[/] 个包含 '{searchKeyword}' 的文件");
        }
        else if (filterLevel.HasValue)
        {
            AnsiConsole.MarkupLine($"共 [cyan]{files.Count}[/] 个 {filterLevel.Value} 文件");
        }
        else
        {
            AnsiConsole.MarkupLine($"共 [cyan]{files.Count}[/] 个文件");
        }

        // 显示使用提示
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]使用 'agent file show <ID>' 查看文件详情[/]");
        AnsiConsole.MarkupLine("[dim]使用 'agent file share <ID> --user <user-id>' 共享文件[/]");
    }

    /// <summary>
    /// JSON 格式输出
    /// </summary>
    private static void OutputJson(List<UploadedFile> files, string? currentUserId)
    {
        var filesData = files.Select(f => new
        {
            f.Id,
            f.FileName,
            f.OwnerId,
            IsOwner = f.OwnerId == currentUserId,
            AccessLevel = f.AccessLevel.ToString(),
            FileSizeBytes = f.FileSize,
            f.MimeType,
            UploadedAt = f.UploadedAt.ToString("O"),
            f.Summary,
            f.Tags,
            f.SessionId
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Total = files.Count,
            Files = filesData
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
