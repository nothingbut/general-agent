using System.CommandLine;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Hosts.Console.Utils;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// export 命令 - 导出会话
/// </summary>
public static class ExportCommand
{
    /// <summary>
    /// 创建 export 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("export", "导出会话");

        // 参数：会话 ID
        var sessionIdArgument = new Argument<string>(
            name: "session-id",
            description: "会话 ID（支持短格式，如前8位）");
        command.AddArgument(sessionIdArgument);

        // 选项：导出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "markdown",
            description: "导出格式 (json, markdown, text)");
        command.AddOption(formatOption);

        // 选项：输出文件
        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "输出文件路径（默认输出到标准输出）");
        command.AddOption(outputOption);

        command.SetHandler(async (sessionIdStr, format, outputPath) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                // 解析会话 ID
                var sessionId = await SessionSelector.ResolveSessionIdAsync(sessionIdStr, sessionService);
                if (!sessionId.HasValue)
                {
                    Environment.Exit(1);
                    return;
                }

                // 获取会话
                var session = await sessionService.GetSessionAsync(sessionId.Value);
                if (session == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 获取消息
                var messages = await messageRepository.GetBySessionAsync(sessionId.Value);

                // 导出
                string exportedContent;
                try
                {
                    exportedContent = ExportHelper.Export(format, session, messages);
                }
                catch (ArgumentException ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ {ex.Message}[/]");
                    AnsiConsole.MarkupLine($"[yellow]支持的格式: json, markdown, text[/]");
                    Environment.Exit(1);
                    return;
                }

                // 输出
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    // 输出到标准输出
                    System.Console.WriteLine(exportedContent);
                }
                else
                {
                    // 输出到文件
                    // 如果没有扩展名，自动添加
                    if (!Path.HasExtension(outputPath))
                    {
                        outputPath += ExportHelper.GetFileExtension(format);
                    }

                    await File.WriteAllTextAsync(outputPath, exportedContent);

                    AnsiConsole.MarkupLine($"[green]✓[/] 会话已导出到: [cyan]{outputPath}[/]");
                    AnsiConsole.MarkupLine($"  格式: {format}");
                    AnsiConsole.MarkupLine($"  消息数: {messages.Count()}");
                    AnsiConsole.MarkupLine($"  文件大小: {new FileInfo(outputPath).Length / 1024.0:F2} KB");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 导出会话失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, sessionIdArgument, formatOption, outputOption);

        return command;
    }
}
