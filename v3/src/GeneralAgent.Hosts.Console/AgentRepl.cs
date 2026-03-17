using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console;

/// <summary>
/// Agent REPL (Read-Eval-Print Loop) 实现
/// 提供交互式对话界面
/// </summary>
public class AgentRepl
{
    private readonly SessionService _sessionService;
    private readonly ConversationService _conversationService;
    private readonly IMessageRepository _messageRepository;
    private readonly LLMOptions _llmOptions;
    private readonly ILogger<AgentRepl> _logger;

    private Guid _currentSessionId = Guid.Empty;
    private string _currentProvider = string.Empty;

    public AgentRepl(
        SessionService sessionService,
        ConversationService conversationService,
        IMessageRepository messageRepository,
        IOptions<LLMOptions> llmOptions,
        ILogger<AgentRepl> logger)
    {
        _sessionService = sessionService;
        _conversationService = conversationService;
        _messageRepository = messageRepository;
        _llmOptions = llmOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 启动 REPL 主循环
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // 初始化提供商
        _currentProvider = _llmOptions.DefaultProvider;

        // 显示欢迎信息
        DisplayWelcome();

        // 创建默认会话
        await CreateNewSessionAsync("默认会话");

        // 主循环
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 显示提示符
                var input = AnsiConsole.Prompt(
                    new TextPrompt<string>("[bold blue]You>[/]")
                        .AllowEmpty());

                // 处理空输入
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                // 处理命令
                if (input.StartsWith('/'))
                {
                    var shouldExit = await HandleCommandAsync(input);
                    if (shouldExit)
                    {
                        break;
                    }
                    continue;
                }

                // 处理普通对话
                await HandleConversationAsync(input, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "REPL 循环发生错误");
                AnsiConsole.MarkupLine($"[red]错误: {ex.Message}[/]");
            }
        }

        AnsiConsole.MarkupLine("\n[green]再见！[/]");
    }

    /// <summary>
    /// 显示欢迎信息
    /// </summary>
    private void DisplayWelcome()
    {
        AnsiConsole.Clear();
        var panel = new Panel(
            new Markup("[bold yellow]General Agent V3 - Console REPL[/]\n\n" +
                      $"当前提供商: [cyan]{_currentProvider}[/]\n" +
                      "输入 [bold]/help[/] 查看可用命令"))
        {
            Border = BoxBorder.Double,
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 处理用户命令
    /// </summary>
    /// <returns>是否应该退出 REPL</returns>
    private async Task<bool> HandleCommandAsync(string input)
    {
        var parts = input.TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var command = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();

        switch (command)
        {
            case "help":
                ShowHelp();
                return false;

            case "exit":
            case "quit":
                return true;

            case "new":
                await CreateNewSessionAsync(string.Join(' ', args));
                return false;

            case "list":
                await ListSessionsAsync();
                return false;

            case "switch":
                await SwitchProviderAsync(args);
                return false;

            case "provider":
                ShowCurrentProvider();
                return false;

            case "history":
                await ShowHistoryAsync();
                return false;

            default:
                AnsiConsole.MarkupLine($"[red]未知命令: {command}[/]");
                return false;
        }
    }

    /// <summary>
    /// 处理对话
    /// </summary>
    private async Task HandleConversationAsync(string userMessage, CancellationToken cancellationToken)
    {
        if (_currentSessionId == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[red]错误: 没有活动会话，请先使用 /new 创建会话[/]");
            return;
        }

        try
        {
            // 显示 "思考中..." 并开始流式输出
            AnsiConsole.Write(new Markup("[bold green]Assistant>[/] "));

            await foreach (var content in _conversationService.SendMessageStreamAsync(
                _currentSessionId, userMessage, _currentProvider, cancellationToken))
            {
                AnsiConsole.Write(content);
            }

            AnsiConsole.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "对话处理失败");
            AnsiConsole.MarkupLine($"\n[red]错误: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示帮助信息
    /// </summary>
    private void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("命令").Centered())
            .AddColumn(new TableColumn("说明").LeftAligned());

        table.AddRow("[cyan]/help[/]", "显示此帮助信息");
        table.AddRow("[cyan]/new [title][/]", "创建新会话（可选标题）");
        table.AddRow("[cyan]/list[/]", "列出所有会话");
        table.AddRow("[cyan]/switch <provider>[/]", "切换 LLM 提供商");
        table.AddRow("[cyan]/provider[/]", "显示当前提供商");
        table.AddRow("[cyan]/history[/]", "显示当前会话历史");
        table.AddRow("[cyan]/exit[/]", "退出 REPL");

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// 显示当前提供商
    /// </summary>
    private void ShowCurrentProvider()
    {
        var availableProviders = string.Join(", ", _llmOptions.Providers.Keys);
        AnsiConsole.MarkupLine($"当前提供商: [cyan]{_currentProvider}[/]");
        AnsiConsole.MarkupLine($"可用提供商: [dim]{availableProviders}[/]");
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    private async Task CreateNewSessionAsync(string title)
    {
        try
        {
            var defaultTitle = string.IsNullOrWhiteSpace(title) ? "新会话" : title;
            var session = await _sessionService.CreateSessionAsync(defaultTitle);
            _currentSessionId = session.Id;
            AnsiConsole.MarkupLine($"[green]已创建新会话: {session.Title} (ID: {session.Id})[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            AnsiConsole.MarkupLine($"[red]创建会话失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 列出所有会话
    /// </summary>
    private async Task ListSessionsAsync()
    {
        try
        {
            var pagedResult = await _sessionService.ListSessionsAsync(limit: 10);

            if (pagedResult.Total == 0)
            {
                AnsiConsole.MarkupLine("[yellow]没有会话[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("ID")
                .AddColumn("标题")
                .AddColumn("消息数")
                .AddColumn("创建时间");

            foreach (var session in pagedResult.Items)
            {
                var messageCount = await _messageRepository.CountAsync(session.Id);
                var isCurrent = session.Id == _currentSessionId ? "[green]*[/]" : " ";
                table.AddRow(
                    $"{isCurrent} {session.Id.ToString()[..8]}...",
                    session.Title ?? "无标题",
                    messageCount.ToString(),
                    session.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                );
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出会话失败");
            AnsiConsole.MarkupLine($"[red]列出会话失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 切换提供商
    /// </summary>
    private async Task SwitchProviderAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]用法: /switch <provider>[/]");
            ShowCurrentProvider();
            return;
        }

        var providerName = args[0];

        // 验证提供商是否存在
        if (!_llmOptions.Providers.ContainsKey(providerName))
        {
            AnsiConsole.MarkupLine($"[red]未知提供商: {providerName}[/]");
            ShowCurrentProvider();
            return;
        }

        _currentProvider = providerName;
        AnsiConsole.MarkupLine($"[green]已切换到提供商: {_currentProvider}[/]");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 显示当前会话历史
    /// </summary>
    private async Task ShowHistoryAsync()
    {
        if (_currentSessionId == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[red]没有活动会话[/]");
            return;
        }

        try
        {
            var messages = await _messageRepository.GetBySessionAsync(_currentSessionId);

            if (messages.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]会话历史为空[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[bold]会话历史 (共 {messages.Count} 条消息):[/]");
            AnsiConsole.WriteLine();

            foreach (var message in messages)
            {
                var roleColor = message.Role switch
                {
                    MessageRole.User => "blue",
                    MessageRole.Assistant => "green",
                    MessageRole.System => "yellow",
                    _ => "white"
                };

                var roleLabel = message.Role switch
                {
                    MessageRole.User => "You",
                    MessageRole.Assistant => "Assistant",
                    MessageRole.System => "System",
                    _ => message.Role.ToString()
                };

                AnsiConsole.MarkupLine($"[bold {roleColor}]{roleLabel}>[/] {message.Content}");
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话历史失败");
            AnsiConsole.MarkupLine($"[red]获取会话历史失败: {ex.Message}[/]");
        }
    }
}
