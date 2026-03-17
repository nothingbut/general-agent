using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// chat 命令 - 在指定会话中发送消息
/// </summary>
public static class ChatCommand
{
    /// <summary>
    /// 创建 chat 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("chat", "在指定会话中发送消息");

        // 参数：会话 ID
        var sessionIdArgument = new Argument<string>(
            name: "session-id",
            description: "会话 ID（支持短格式，如前8位）");
        command.AddArgument(sessionIdArgument);

        // 参数：消息内容
        var messageArgument = new Argument<string>(
            name: "message",
            description: "要发送的消息内容");
        command.AddArgument(messageArgument);

        // 选项：提供商
        var providerOption = new Option<string?>(
            aliases: new[] { "--provider", "-p" },
            description: "LLM 提供商（默认使用配置文件中的默认提供商）");
        command.AddOption(providerOption);

        // 选项：流式输出
        var streamOption = new Option<bool>(
            aliases: new[] { "--stream", "-s" },
            getDefaultValue: () => true,
            description: "启用流式输出");
        command.AddOption(streamOption);

        command.SetHandler(async (sessionIdStr, message, provider, stream) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                // 解析会话 ID（支持短格式）
                Guid sessionId;
                if (Guid.TryParse(sessionIdStr, out var fullId))
                {
                    sessionId = fullId;
                }
                else
                {
                    // 尝试通过前缀匹配
                    var sessions = await sessionService.ListSessionsAsync(limit: 100);
                    var matchedSession = sessions.Items.FirstOrDefault(s =>
                        s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase));

                    if (matchedSession == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
                        Environment.Exit(1);
                        return;
                    }

                    sessionId = matchedSession.Id;
                }

                // 验证会话存在
                var session = await sessionService.GetSessionAsync(sessionId);
                if (session == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                    Environment.Exit(1);
                    return;
                }

                // 确定提供商
                var currentProvider = provider ?? configuration["LLM:DefaultProvider"] ?? "Ollama";

                // 显示会话信息
                AnsiConsole.MarkupLine($"[dim]会话: {session.Title} ({sessionId.ToString()[..8]}...)[/]");
                AnsiConsole.MarkupLine($"[dim]提供商: {currentProvider}[/]");
                AnsiConsole.WriteLine();

                // 显示用户消息
                AnsiConsole.MarkupLine($"[bold blue]You>[/] {message}");

                // 发送消息并获取响应
                AnsiConsole.Write(new Markup("[bold green]Assistant>[/] "));

                if (stream)
                {
                    // 流式输出
                    await foreach (var content in conversationService.SendMessageStreamAsync(
                        sessionId, message, currentProvider))
                    {
                        AnsiConsole.Write(content);
                    }
                    AnsiConsole.WriteLine();
                }
                else
                {
                    // 非流式输出
                    var response = await conversationService.SendMessageAsync(
                        sessionId, message, currentProvider);
                    AnsiConsole.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]✗ 发送消息失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, sessionIdArgument, messageArgument, providerOption, streamOption);

        return command;
    }
}
