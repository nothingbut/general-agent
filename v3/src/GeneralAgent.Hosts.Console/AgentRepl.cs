using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Hosts.Console.Repl;
using GeneralAgent.Hosts.Console.Commands;
using GeneralAgent.Hosts.Console.Services;
using GeneralAgent.Infrastructure.LLM;
using GeneralAgent.Infrastructure.Skills.Models;
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
    private readonly SkillService _skillService;
    private readonly ContextCompressionService _contextCompressionService;
    private readonly SearchCommand _searchCommand;
    private readonly TagCommand _tagCommand;
    private readonly LLMOptions _llmOptions;
    private readonly ILogger<AgentRepl> _logger;
    private readonly ReplHistoryManager _historyManager;
    private readonly AutoCompletionHandler _completionHandler;
    private readonly MultiLineInputHandler _multiLineHandler;
    private readonly AliasManager _aliasManager;

    private Guid _currentSessionId = Guid.Empty;
    private string _currentProvider = string.Empty;

    public AgentRepl(
        SessionService sessionService,
        ConversationService conversationService,
        IMessageRepository messageRepository,
        SkillService skillService,
        ContextCompressionService contextCompressionService,
        SearchCommand searchCommand,
        TagCommand tagCommand,
        IOptions<LLMOptions> llmOptions,
        ILogger<AgentRepl> logger)
    {
        _sessionService = sessionService;
        _conversationService = conversationService;
        _messageRepository = messageRepository;
        _skillService = skillService;
        _contextCompressionService = contextCompressionService;
        _searchCommand = searchCommand;
        _tagCommand = tagCommand;
        _llmOptions = llmOptions.Value;
        _logger = logger;

        // 初始化 REPL 组件
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDir = Path.Combine(homeDir, ".agent");

        // 初始化历史管理器
        var historyPath = Path.Combine(agentDir, "repl_history.txt");
        _historyManager = new ReplHistoryManager(historyPath, logger: logger);

        // 初始化自动补全处理器
        _completionHandler = new AutoCompletionHandler(sessionService, skillService, logger);

        // 初始化多行输入处理器
        _multiLineHandler = new MultiLineInputHandler(logger);

        // 初始化别名管理器
        var aliasPath = Path.Combine(agentDir, "aliases.json");
        _aliasManager = new AliasManager(aliasPath, logger);
    }

    /// <summary>
    /// 启动 REPL 主循环
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // 初始化提供商
        _currentProvider = _llmOptions.DefaultProvider;

        // 加载历史记录
        var history = _historyManager.LoadHistory();
        ReadLine.HistoryEnabled = true;
        foreach (var item in history)
        {
            ReadLine.AddHistory(item);
        }

        _logger.LogInformation("已加载 {Count} 条历史记录", history.Count);

        // 设置自动补全处理器
        ReadLine.AutoCompletionHandler = _completionHandler;
        _logger.LogInformation("已启用自动补全功能");

        // 显示欢迎信息
        DisplayWelcome();

        // 创建默认会话
        await CreateNewSessionAsync("默认会话");

        // 主循环
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 使用 ReadLine 获取初始输入（支持历史和自动补全）
                var initialInput = ReadLine.Read("You> ");

                // 处理 Ctrl+D (EOF)
                if (initialInput == null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]✓ 检测到 Ctrl+D，退出 REPL[/]");
                    break;
                }

                // 处理空输入
                if (string.IsNullOrWhiteSpace(initialInput))
                {
                    continue;
                }

                // 处理多行输入（如果检测到多行标记）
                var input = _multiLineHandler.ProcessInput(initialInput, prompt => ReadLine.Read(prompt));

                // 处理空输入（多行输入可能为空）
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                // 添加到历史
                _historyManager.AddHistoryItem(initialInput); // 只添加初始输入，不添加完整多行内容

                // 显示多行输入统计（如果是多行）
                if (_multiLineHandler.IsMultiLineStart(initialInput))
                {
                    var stats = _multiLineHandler.GetInputStats(input);
                    AnsiConsole.MarkupLine($"[dim]→ 已接收多行输入: {stats.Format()}[/]");
                }

                // 处理命令
                if (initialInput.StartsWith('/'))
                {
                    // 解析别名
                    var resolvedInput = _aliasManager.ResolveAlias(initialInput);
                    var shouldExit = await HandleCommandAsync(resolvedInput);
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
                AnsiConsole.MarkupLine($"[red]✗ 错误: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[dim]💡 提示: REPL 将继续运行，你可以重试或使用 /help 查看帮助[/]");
            }
        }

        AnsiConsole.MarkupLine("\n[green]✓ 再见！[/]");
    }

    /// <summary>
    /// 显示欢迎信息
    /// </summary>
    private void DisplayWelcome()
    {
        AnsiConsole.Clear();
        var panel = new Panel(
            new Markup("[bold yellow]General Agent V3 - Console REPL[/]\n\n" +
                      $"当前提供商: [cyan]{_currentProvider}[/]\n\n" +
                      "输入 [bold]/help[/] 查看可用命令\n" +
                      "[dim]快捷键: ↑↓ 浏览历史 | Tab 自动补全 | Ctrl+C 取消输入 | Ctrl+D 退出[/]"))
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

            case "session":
                await SwitchSessionAsync(args);
                return false;

            case "delete":
                await DeleteSessionAsync(args);
                return false;

            case "switch":
                // 为了向后兼容，/switch 用于切换提供商
                await SwitchProviderAsync(args);
                return false;

            case "provider":
                ShowCurrentProvider();
                return false;

            case "history":
                await ShowHistoryAsync();
                return false;

            case "skills":
                ShowSkills(args);
                return false;

            case "skill":
                ShowSkillInfo(args);
                return false;

            case "clear":
                AnsiConsole.Clear();
                DisplayWelcome();
                return false;

            case "search":
                await SearchAsync(args);
                return false;

            case "tag":
                await HandleTagCommandAsync(args);
                return false;

            case "alias":
                HandleAliasCommand(args);
                return false;

            case "context":
                await HandleContextCommandAsync(args);
                return false;

            default:
                AnsiConsole.MarkupLine($"[red]✗ 未知命令: {command}[/]");
                AnsiConsole.MarkupLine($"[dim]💡 提示: 输入 /help 查看可用命令[/]");
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
            AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
            AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /new 创建新会话[/]");
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
            AnsiConsole.MarkupLine($"\n[red]✗ 错误: {ex.Message}[/]");
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

        // 会话管理
        table.AddRow("[bold yellow]会话管理[/]", "");
        table.AddRow("[cyan]/new [[title]][/]", "创建新会话（可选标题）");
        table.AddRow("[cyan]/list[/]", "列出所有会话");
        table.AddRow("[cyan]/session <id>[/]", "切换到指定会话（支持短 ID）");
        table.AddRow("[cyan]/delete [[id]][/]", "删除会话（默认当前会话）");
        table.AddRow("[cyan]/history[/]", "显示当前会话历史");

        // 技能管理
        table.AddRow("", "");
        table.AddRow("[bold yellow]技能管理[/]", "");
        table.AddRow("[cyan]/skills [[namespace]][/]", "列出技能（可选命名空间过滤）");
        table.AddRow("[cyan]/skill <name>[/]", "显示技能详情");

        // LLM 配置
        table.AddRow("", "");
        table.AddRow("[bold yellow]LLM 配置[/]", "");
        table.AddRow("[cyan]/switch <provider>[/]", "切换 LLM 提供商");
        table.AddRow("[cyan]/provider[/]", "显示当前提供商");

        // 搜索
        table.AddRow("", "");
        table.AddRow("[bold yellow]搜索功能[/] [green](V3.1 新增)[/]", "");
        table.AddRow("[cyan]/search <查询>[/]", "使用自然语言搜索消息内容");

        // 标签管理
        table.AddRow("", "");
        table.AddRow("[bold yellow]标签管理[/] [green](V3.1 新增)[/]", "");
        table.AddRow("[cyan]/tag add <标签> [[--emoji 🐍]] [[--color #FF0000]][/]", "为当前会话添加标签");
        table.AddRow("[cyan]/tag remove <标签>[/]", "从当前会话移除标签");
        table.AddRow("[cyan]/tag list[/]", "列出当前会话的标签");
        table.AddRow("[cyan]/tag list --all[/]", "列出所有标签及使用统计");
        table.AddRow("[cyan]/tag suggest[/]", "基于会话标题生成智能标签建议");

        // 别名
        table.AddRow("", "");
        table.AddRow("[bold yellow]别名[/]", "");
        table.AddRow("[cyan]/alias[/]", "列出所有别名");
        table.AddRow("[cyan]/alias add <别名> <命令>[/]", "添加别名");
        table.AddRow("[cyan]/alias remove <别名>[/]", "移除别名");

        // 上下文压缩
        table.AddRow("", "");
        table.AddRow("[bold yellow]上下文压缩[/] [green](V3 Phase 6 新增)[/]", "");
        table.AddRow("[cyan]/context status[/]", "查看当前上下文状态");
        table.AddRow("[cyan]/context compress [[strategy]][/]", "手动压缩上下文");
        table.AddRow("[cyan]/context config[/]", "查看/修改压缩配置");
        table.AddRow("[cyan]/context history[[limit]][/]", "查看压缩历史");

        // 其他
        table.AddRow("", "");
        table.AddRow("[bold yellow]其他[/]", "");
        table.AddRow("[cyan]/clear[/]", "清屏");
        table.AddRow("[cyan]/help[/]", "显示此帮助信息");
        table.AddRow("[cyan]/exit[/]", "退出 REPL");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // 添加快捷键说明
        var shortcutPanel = new Panel(
            new Markup("[bold]常用快捷键：[/]\n" +
                      "[cyan]↑/↓[/] - 浏览命令历史\n" +
                      "[cyan]Tab[/] - 自动补全命令和会话 ID\n" +
                      "[cyan]Ctrl+C[/] - 取消当前输入\n" +
                      "[cyan]Ctrl+D[/] - 退出 REPL（或使用 /exit）\n" +
                      "[cyan]/clear[/] - 清屏\n" +
                      "[cyan]\"\"\"[/] - 开始多行输入（以单独的 \"\"\" 结束）"))
        {
            Header = new PanelHeader("[yellow]快捷键[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(shortcutPanel);
    }

    /// <summary>
    /// 显示当前提供商
    /// </summary>
    private void ShowCurrentProvider()
    {
        var availableProviders = string.Join(", ", _llmOptions.Providers.Keys);
        AnsiConsole.MarkupLine($"[blue]ℹ[/] 当前提供商: [cyan]{_currentProvider}[/]");
        AnsiConsole.MarkupLine($"[dim]  可用提供商: {availableProviders}[/]");
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
            AnsiConsole.MarkupLine($"[green]✓ 已创建新会话: {session.Title}[/]");
            AnsiConsole.MarkupLine($"  [dim]ID: {session.Id.ToString()[..8]}...[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            AnsiConsole.MarkupLine($"[red]✗ 创建会话失败: {ex.Message}[/]");
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
                AnsiConsole.MarkupLine("[yellow]⚠ 没有会话[/]");
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
            AnsiConsole.MarkupLine($"[red]✗ 列出会话失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 切换提供商
    /// </summary>
    private async Task SwitchProviderAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗ 用法: /switch <provider>[/]");
            ShowCurrentProvider();
            return;
        }

        var providerName = args[0];

        // 验证提供商是否存在
        if (!_llmOptions.Providers.ContainsKey(providerName))
        {
            AnsiConsole.MarkupLine($"[red]✗ 未知提供商: {providerName}[/]");
            ShowCurrentProvider();
            return;
        }

        _currentProvider = providerName;
        AnsiConsole.MarkupLine($"[green]✓ 已切换到提供商: {_currentProvider}[/]");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 显示当前会话历史
    /// </summary>
    private async Task ShowHistoryAsync()
    {
        if (_currentSessionId == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[red]✗ 没有活动会话[/]");
            AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /new 创建新会话[/]");
            return;
        }

        try
        {
            var messages = await _messageRepository.GetBySessionAsync(_currentSessionId);

            if (messages.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ 会话历史为空[/]");
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
            AnsiConsole.MarkupLine($"[red]✗ 获取会话历史失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 切换会话
    /// </summary>
    private async Task SwitchSessionAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗ 用法: /session <session-id>[/]");
            AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /list 查看所有会话[/]");
            return;
        }

        try
        {
            var sessionIdStr = args[0];
            Guid sessionId;

            // 解析会话 ID（支持短格式）
            if (Guid.TryParse(sessionIdStr, out var fullId))
            {
                sessionId = fullId;
            }
            else
            {
                // 短格式：查找匹配的会话
                var pagedResult = await _sessionService.ListSessionsAsync(100, 0);
                var matchingSessions = pagedResult.Items
                    .Where(s => s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingSessions.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
                    return;
                }

                if (matchingSessions.Count > 1)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ 找到多个匹配的会话，请使用更长的 ID：[/]");
                    foreach (var s in matchingSessions)
                    {
                        AnsiConsole.MarkupLine($"  - [cyan]{s.Id.ToString()[..8]}...[/] {s.Title}");
                    }
                    return;
                }

                sessionId = matchingSessions[0].Id;
            }

            // 验证会话存在
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                return;
            }

            // 切换会话
            _currentSessionId = sessionId;
            AnsiConsole.MarkupLine($"[green]✓ 已切换到会话: {session.Title}[/]");
            AnsiConsole.MarkupLine($"  [dim]ID: [cyan]{session.Id.ToString()[..8]}...[/][/]");
            AnsiConsole.MarkupLine($"  [dim]创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换会话失败");
            AnsiConsole.MarkupLine($"[red]✗ 切换会话失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    private async Task DeleteSessionAsync(string[] args)
    {
        try
        {
            Guid sessionId;

            // 如果没有提供参数，删除当前会话
            if (args.Length == 0)
            {
                if (_currentSessionId == Guid.Empty)
                {
                    AnsiConsole.MarkupLine("[red]✗ 没有活动会话[/]");
            AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /new 创建新会话[/]");
                    return;
                }
                sessionId = _currentSessionId;
            }
            else
            {
                var sessionIdStr = args[0];

                // 解析会话 ID（支持短格式）
                if (Guid.TryParse(sessionIdStr, out var fullId))
                {
                    sessionId = fullId;
                }
                else
                {
                    // 短格式：查找匹配的会话
                    var pagedResult = await _sessionService.ListSessionsAsync(100, 0);
                    var matchingSessions = pagedResult.Items
                        .Where(s => s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (matchingSessions.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
                        return;
                    }

                    if (matchingSessions.Count > 1)
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠ 找到多个匹配的会话，请使用更长的 ID[/]");
                        return;
                    }

                    sessionId = matchingSessions[0].Id;
                }
            }

            // 获取会话信息
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                return;
            }

            // 确认删除
            var confirm = AnsiConsole.Confirm(
                $"确定要删除会话 [cyan]{session.Title}[/] ([dim]{session.Id.ToString()[..8]}...[/]) 吗？",
                false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ 已取消删除[/]");
                return;
            }

            // 删除会话
            await _sessionService.DeleteSessionAsync(sessionId);
            AnsiConsole.MarkupLine($"[green]✓ 已删除会话: {session.Title}[/]");

            // 如果删除的是当前会话，清除当前会话 ID
            if (sessionId == _currentSessionId)
            {
                _currentSessionId = Guid.Empty;
                AnsiConsole.MarkupLine("[yellow]⚠ 当前会话已清除[/]");
                AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /new 创建新会话或 /session 切换到其他会话[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败");
            AnsiConsole.MarkupLine($"[red]✗ 删除会话失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示技能列表
    /// </summary>
    private void ShowSkills(string[] args)
    {
        try
        {
            IReadOnlyList<Skill> skills;

            // 如果提供了命名空间参数，过滤技能
            if (args.Length > 0)
            {
                var namespaceName = args[0];
                skills = _skillService.GetSkillsByNamespace(namespaceName);

                if (skills.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ 命名空间 '{namespaceName}' 中没有技能[/]");
                    return;
                }
            }
            else
            {
                skills = _skillService.GetAllSkills();

                if (skills.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ 没有加载任何技能[/]");
                    return;
                }
            }

            // 按命名空间分组
            var groupedSkills = skills
                .GroupBy(s => s.Namespace ?? "(无命名空间)")
                .OrderBy(g => g.Key);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("完整名称")
                .AddColumn("描述")
                .AddColumn("参数数量");

            foreach (var group in groupedSkills)
            {
                // 命名空间标题行
                table.AddRow(
                    $"[bold yellow]{group.Key}[/]",
                    "",
                    "");

                // 技能行
                foreach (var skill in group.OrderBy(s => s.Name))
                {
                    var fullName = $"[cyan]{skill.FullName}[/]";
                    var description = skill.Description.Length > 50
                        ? skill.Description[..47] + "..."
                        : skill.Description;
                    var paramCount = skill.Parameters.Count.ToString();

                    table.AddRow($"  {fullName}", description, paramCount);
                }
            }

            AnsiConsole.MarkupLine($"[bold]已加载 {skills.Count} 个技能：[/]");
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[dim]💡 提示: 使用 /skill <name> 查看技能详情[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示技能列表失败");
            AnsiConsole.MarkupLine($"[red]✗ 显示技能列表失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示技能详情
    /// </summary>
    private void ShowSkillInfo(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗ 用法: /skill <skill-name>[/]");
            AnsiConsole.MarkupLine("[dim]示例: /skill personal:greeting[/]");
            return;
        }

        try
        {
            var skillName = args[0];
            var allSkills = _skillService.GetAllSkills();

            // 查找技能（支持完整名称和简短名称）
            var skill = allSkills.FirstOrDefault(s => s.FullName.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                ?? allSkills.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ 未找到技能: {skillName}[/]");
                AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /skills 查看所有可用技能[/]");
                return;
            }

            // 显示技能信息
            var panel = new Panel(
                new Markup($"[bold cyan]{skill.FullName}[/]\n\n" +
                          $"[bold]描述：[/]\n{skill.Description}\n\n" +
                          $"[bold]命名空间：[/] {skill.Namespace ?? "(无)"}\n" +
                          $"[bold]需要上下文：[/] {(skill.RequiresContext ? "是" : "否")}\n" +
                          $"[bold]返回给 LLM：[/] {(skill.ReturnToLLM ? "是" : "否")}"))
            {
                Header = new PanelHeader($"[yellow]技能详情[/]"),
                Border = BoxBorder.Double,
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);

            // 显示参数表格
            if (skill.Parameters.Count > 0)
            {
                AnsiConsole.WriteLine();
                var paramTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("参数名")
                    .AddColumn("类型")
                    .AddColumn("必需")
                    .AddColumn("描述");

                foreach (var param in skill.Parameters)
                {
                    paramTable.AddRow(
                        $"[cyan]{param.Name}[/]",
                        param.Type,
                        param.Required ? "[green]是[/]" : "[dim]否[/]",
                        param.Description ?? "-");
                }

                AnsiConsole.MarkupLine("[bold]参数：[/]");
                AnsiConsole.Write(paramTable);
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]此技能没有参数[/]");
            }

            // 显示模板（可选）
            if (args.Length > 1 && args[1] == "--template")
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]提示词模板：[/]");
                var templatePanel = new Panel(skill.Template)
                {
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 0)
                };
                AnsiConsole.Write(templatePanel);
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]💡 提示: 使用 /skill <name> --template 查看提示词模板[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示技能详情失败");
            AnsiConsole.MarkupLine($"[red]✗ 显示技能详情失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 搜索功能
    /// </summary>
    private async Task SearchAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗ 用法: /search <查询>[/]");
            AnsiConsole.MarkupLine("[dim]示例: /search 查找昨天关于 Python 的讨论[/]");
            return;
        }

        var query = string.Join(' ', args);

        try
        {
            await _searchCommand.ExecuteAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索失败");
            AnsiConsole.MarkupLine($"[red]✗ 搜索失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 处理别名命令
    /// </summary>
    private void HandleAliasCommand(string[] args)
    {
        if (args.Length == 0)
        {
            // 显示所有别名
            var aliases = _aliasManager.GetAllAliases();
            if (aliases.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ 没有配置任何别名[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("别名")
                .AddColumn("命令");

            foreach (var (alias, command) in aliases.OrderBy(x => x.Key))
            {
                table.AddRow($"[cyan]{alias}[/]", command);
            }

            AnsiConsole.MarkupLine($"[bold]已配置 {aliases.Count} 个别名：[/]");
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[dim]💡 提示: 使用 /alias add <别名> <命令> 添加新别名[/]");
            return;
        }

        var subCommand = args[0].ToLower();

        try
        {
            switch (subCommand)
            {
                case "list":
                    // 递归调用自己显示列表
                    HandleAliasCommand(Array.Empty<string>());
                    break;

                case "add":
                    if (args.Length < 3)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 用法: /alias add <别名> <命令>[/]");
                        AnsiConsole.MarkupLine("[dim]示例: /alias add n new[/]");
                        return;
                    }

                    var newAlias = args[1];
                    var newCommand = args[2];

                    _aliasManager.AddAlias(newAlias, newCommand);
                    _aliasManager.SaveAliases();
                    AnsiConsole.MarkupLine($"[green]✓ 已添加别名: {newAlias} -> {newCommand}[/]");
                    break;

                case "remove":
                case "delete":
                case "rm":
                    if (args.Length < 2)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 用法: /alias remove <别名>[/]");
                        AnsiConsole.MarkupLine("[dim]示例: /alias remove n[/]");
                        return;
                    }

                    var aliasToRemove = args[1];
                    if (_aliasManager.RemoveAlias(aliasToRemove))
                    {
                        _aliasManager.SaveAliases();
                        AnsiConsole.MarkupLine($"[green]✓ 已移除别名: {aliasToRemove}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠ 别名不存在: {aliasToRemove}[/]");
                    }
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]✗ 未知子命令: {subCommand}[/]");
                    AnsiConsole.MarkupLine("[dim]💡 提示: 可用命令: list, add, remove[/]");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理别名命令失败");
            AnsiConsole.MarkupLine($"[red]✗ 错误: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 处理标签命令
    /// </summary>
    private async Task HandleTagCommandAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            ShowTagHelp();
            return;
        }

        var subCommand = args[0].ToLower();

        try
        {
            switch (subCommand)
            {
                case "add":
                    if (args.Length < 2)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 用法: /tag add <标签> [[--emoji 🐍]] [[--color #FF0000]][/]");
                        return;
                    }

                    if (_currentSessionId == Guid.Empty)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
                        return;
                    }

                    var tagName = args[1];
                    string? emoji = null;
                    string? color = null;

                    // 解析可选参数
                    for (int i = 2; i < args.Length; i++)
                    {
                        if (args[i] == "--emoji" && i + 1 < args.Length)
                        {
                            emoji = args[i + 1];
                            i++;
                        }
                        else if (args[i] == "--color" && i + 1 < args.Length)
                        {
                            color = args[i + 1];
                            i++;
                        }
                    }

                    await _tagCommand.ExecuteAddAsync(_currentSessionId, tagName, emoji, color, ct);
                    break;

                case "remove":
                case "rm":
                    if (args.Length < 2)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 用法: /tag remove <标签>[/]");
                        return;
                    }

                    if (_currentSessionId == Guid.Empty)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
                        return;
                    }

                    await _tagCommand.ExecuteRemoveAsync(_currentSessionId, args[1], ct);
                    break;

                case "list":
                case "ls":
                    // 检查是否有 --all 标志
                    bool showAll = args.Length > 1 && args[1] == "--all";

                    if (showAll)
                    {
                        // 列出所有标签
                        await _tagCommand.ExecuteListAsync(null, ct);
                    }
                    else
                    {
                        // 列出当前会话标签
                        if (_currentSessionId == Guid.Empty)
                        {
                            AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
                            return;
                        }
                        await _tagCommand.ExecuteListAsync(_currentSessionId, ct);
                    }
                    break;

                case "suggest":
                    if (_currentSessionId == Guid.Empty)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
                        return;
                    }

                    // 获取当前会话标题
                    var session = await _sessionService.GetSessionAsync(_currentSessionId, ct);
                    if (session == null)
                    {
                        AnsiConsole.MarkupLine("[red]✗ 会话不存在[/]");
                        return;
                    }

                    var sessionTitle = session.Title ?? "未命名会话";
                    await _tagCommand.ExecuteSuggestAsync(_currentSessionId, sessionTitle, ct);
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]✗ 未知子命令: {subCommand}[/]");
                    ShowTagHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理标签命令失败");
            AnsiConsole.MarkupLine($"[red]✗ 错误: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示标签命令帮助
    /// </summary>
    private void ShowTagHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("命令")
            .AddColumn("说明");

        table.AddRow("[cyan]/tag add <标签> [[--emoji 🐍]] [[--color #FF0000]][/]", "添加标签到当前会话");
        table.AddRow("[cyan]/tag remove <标签>[/]", "从当前会话移除标签");
        table.AddRow("[cyan]/tag list[/]", "列出当前会话的标签");
        table.AddRow("[cyan]/tag list --all[/]", "列出所有标签及使用统计");
        table.AddRow("[cyan]/tag suggest[/]", "基于会话标题生成标签建议");

        AnsiConsole.MarkupLine("[bold yellow]标签命令：[/]");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // 示例
        var examplePanel = new Panel(
            new Markup("[bold]示例：[/]\n" +
                      "[cyan]/tag add python --emoji 🐍 --color #3776AB[/]\n" +
                      "[cyan]/tag remove python[/]\n" +
                      "[cyan]/tag list[/]\n" +
                      "[cyan]/tag list --all[/]\n" +
                      "[cyan]/tag suggest[/]"))
        {
            Header = new PanelHeader("[yellow]示例[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(examplePanel);
    }

    /// <summary>
    /// 处理上下文压缩命令
    /// </summary>
    private async Task HandleContextCommandAsync(string[] args, CancellationToken ct = default)
    {
        if (_currentSessionId == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[red]✗ 错误: 没有活动会话[/]");
            return;
        }

        if (args.Length == 0)
        {
            ShowContextHelp();
            return;
        }

        var subCommand = args[0].ToLower();

        try
        {
            switch (subCommand)
            {
                case "status":
                    await ShowContextStatusAsync(ct);
                    break;

                case "compress":
                    var strategy = args.Length > 1 ? args[1] : null;
                    await CompressContextAsync(strategy, ct);
                    break;

                case "config":
                    await HandleContextConfigAsync(args.Skip(1).ToArray(), ct);
                    break;

                case "history":
                    var limit = args.Length > 1 && int.TryParse(args[1], out var l) ? l : 10;
                    await ShowCompressionHistoryAsync(limit, ct);
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]✗ 未知子命令: {subCommand}[/]");
                    ShowContextHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理上下文命令失败");
            AnsiConsole.MarkupLine($"[red]✗ 错误: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示上下文状态
    /// </summary>
    private async Task ShowContextStatusAsync(CancellationToken ct = default)
    {
        var status = await _contextCompressionService.GetContextStatusAsync(_currentSessionId, ct);

        var panel = new Panel(
            new Markup($"[bold]会话 ID:[/] {_currentSessionId.ToString()[..8]}...\n" +
                      $"[bold]消息数量:[/] [cyan]{status.MessageCount}[/] 条\n" +
                      $"[bold]当前 Token 数:[/] [cyan]{status.CurrentTokens}[/] tokens\n" +
                      $"[bold]压缩阈值:[/] {status.CompressionThreshold} tokens\n" +
                      $"[bold]Token 使用率:[/] {FormatTokenUsageRatio(status.TokenUsageRatio)}\n" +
                      $"[bold]自动压缩:[/] {(status.AutoCompressionEnabled ? "[green]已启用[/]" : "[red]已禁用[/]")}\n" +
                      $"[bold]默认策略:[/] [yellow]{status.DefaultStrategy}[/]\n" +
                      $"[bold]是否需要压缩:[/] {(status.ShouldCompress ? "[yellow]是[/]" : "[green]否[/]")}\n" +
                      $"[bold]最后压缩时间:[/] {(status.LastCompressionAt.HasValue ? status.LastCompressionAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "[dim]从未压缩[/]")}"))
        {
            Header = new PanelHeader("[yellow]上下文状态[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);

        if (status.ShouldCompress)
        {
            AnsiConsole.MarkupLine("\n[yellow]⚠ 提示: 上下文已达到压缩阈值，建议使用 /context compress 进行压缩[/]");
        }
    }

    /// <summary>
    /// 格式化 Token 使用率
    /// </summary>
    private string FormatTokenUsageRatio(double ratio)
    {
        var percentage = (int)(ratio * 100);
        var color = percentage >= 100 ? "red" :
                   percentage >= 80 ? "yellow" :
                   percentage >= 60 ? "cyan" : "green";

        var bar = new string('█', Math.Min(percentage / 5, 20));
        var empty = new string('░', Math.Max(0, 20 - percentage / 5));

        return $"[{color}]{percentage}%[/] [{color}]{bar}[/][dim]{empty}[/]";
    }

    /// <summary>
    /// 压缩上下文
    /// </summary>
    private async Task CompressContextAsync(string? strategy, CancellationToken ct = default)
    {
        AnsiConsole.MarkupLine("[yellow]⏳ 正在压缩上下文...[/]");

        var result = await _contextCompressionService.CompressSessionMessagesAsync(
            _currentSessionId,
            strategy,
            ct);

        if (result.Success)
        {
            var stats = result.Stats;

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("指标")
                .AddColumn("值");

            table.AddRow("使用策略", $"[yellow]{stats.StrategyUsed}[/]");
            table.AddRow("原始消息数", $"{stats.OriginalMessageCount} 条");
            table.AddRow("压缩后消息数", $"[cyan]{stats.CompressedMessageCount}[/] 条");
            table.AddRow("原始 Token 数", $"{stats.OriginalTokens} tokens");
            table.AddRow("压缩后 Token 数", $"[cyan]{stats.CompressedTokens}[/] tokens");
            table.AddRow("压缩比率", $"[green]{stats.CompressionRatio:P2}[/]");
            table.AddRow("节省 Token", $"[green]{stats.TokensSaved}[/] tokens");
            table.AddRow("压缩耗时", $"{stats.DurationMs}ms");

            AnsiConsole.MarkupLine("[green]✓ 压缩成功[/]");
            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ 压缩失败: {result.ErrorMessage}[/]");
        }
    }

    /// <summary>
    /// 处理配置子命令
    /// </summary>
    private async Task HandleContextConfigAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            // 显示当前配置
            var config = await _contextCompressionService.GetOrCreateConfigAsync(_currentSessionId, ct);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("配置项")
                .AddColumn("当前值");

            table.AddRow("自动压缩", config.AutoCompressionEnabled ? "[green]已启用[/]" : "[red]已禁用[/]");
            table.AddRow("压缩阈值", $"{config.AutoCompressionThreshold} tokens");
            table.AddRow("默认策略", $"[yellow]{config.DefaultStrategy}[/]");

            AnsiConsole.MarkupLine("[bold]当前压缩配置：[/]");
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[dim]💡 提示: 使用 /context config <key> <value> 修改配置[/]");
            AnsiConsole.MarkupLine("[dim]可用配置项: auto-enabled, threshold, strategy[/]");
            return;
        }

        var configKey = args[0].ToLower();
        var configValue = args.Length > 1 ? args[1] : null;

        if (configValue == null)
        {
            AnsiConsole.MarkupLine("[red]✗ 用法: /context config <key> <value>[/]");
            AnsiConsole.MarkupLine("[dim]示例: /context config threshold 2500[/]");
            return;
        }

        switch (configKey)
        {
            case "auto-enabled":
            case "auto":
                if (bool.TryParse(configValue, out var enabled))
                {
                    await _contextCompressionService.UpdateCompressionConfigAsync(
                        _currentSessionId,
                        autoCompressionEnabled: enabled,
                        cancellationToken: ct);
                    AnsiConsole.MarkupLine($"[green]✓ 已{(enabled ? "启用" : "禁用")}自动压缩[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ 无效的布尔值，请使用 true 或 false[/]");
                }
                break;

            case "threshold":
                if (int.TryParse(configValue, out var threshold) && threshold > 0)
                {
                    await _contextCompressionService.UpdateCompressionConfigAsync(
                        _currentSessionId,
                        autoCompressionThreshold: threshold,
                        cancellationToken: ct);
                    AnsiConsole.MarkupLine($"[green]✓ 已设置压缩阈值为 {threshold} tokens[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ 无效的阈值，请输入正整数[/]");
                }
                break;

            case "strategy":
                await _contextCompressionService.UpdateCompressionConfigAsync(
                    _currentSessionId,
                    defaultStrategy: configValue,
                    cancellationToken: ct);
                AnsiConsole.MarkupLine($"[green]✓ 已设置默认策略为 {configValue}[/]");
                break;

            default:
                AnsiConsole.MarkupLine($"[red]✗ 未知配置项: {configKey}[/]");
                AnsiConsole.MarkupLine("[dim]可用配置项: auto-enabled, threshold, strategy[/]");
                break;
        }
    }

    /// <summary>
    /// 显示压缩历史
    /// </summary>
    private async Task ShowCompressionHistoryAsync(int limit, CancellationToken ct = default)
    {
        var histories = await _contextCompressionService.GetCompressionHistoryAsync(_currentSessionId, limit, ct);

        if (histories.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ 该会话没有压缩历史记录[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("时间")
            .AddColumn("策略")
            .AddColumn("原始/压缩")
            .AddColumn("Token 节省")
            .AddColumn("压缩比率")
            .AddColumn("耗时");

        foreach (var h in histories)
        {
            table.AddRow(
                h.CompressedAt.ToLocalTime().ToString("MM-dd HH:mm"),
                $"[yellow]{h.StrategyUsed}[/]",
                $"{h.OriginalMessageCount}→{h.CompressedMessageCount}",
                $"[green]{h.OriginalTokens - h.CompressedTokens}[/]",
                $"{h.CompressionRatio:P0}",
                $"{h.DurationMs}ms"
            );
        }

        AnsiConsole.MarkupLine($"[bold]压缩历史（最近 {histories.Count} 条）：[/]");
        AnsiConsole.Write(table);

        // 显示统计汇总
        var stats = await _contextCompressionService.GetCompressionStatsAsync(_currentSessionId, ct);
        AnsiConsole.MarkupLine(
            $"\n[dim]总计: {stats.TotalCompressions} 次压缩，平均压缩比率 {stats.AverageCompressionRatio:P2}，" +
            $"累计节省 {stats.TotalTokensSaved} tokens，最常用策略: {stats.MostUsedStrategy}[/]");
    }

    /// <summary>
    /// 显示上下文命令帮助
    /// </summary>
    private void ShowContextHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("命令")
            .AddColumn("说明");

        table.AddRow("[cyan]/context status[/]", "查看当前会话的上下文状态");
        table.AddRow("[cyan]/context compress [[strategy]][/]", "手动压缩上下文（可选指定策略）");
        table.AddRow("[cyan]/context config[/]", "查看压缩配置");
        table.AddRow("[cyan]/context config <key> <value>[/]", "修改压缩配置");
        table.AddRow("[cyan]/context history [[limit]][/]", "查看压缩历史记录（默认 10 条）");

        AnsiConsole.MarkupLine("[bold yellow]上下文压缩命令：[/]");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // 可用策略
        var strategyPanel = new Panel(
            new Markup("[bold]可用压缩策略：[/]\n" +
                      "[yellow]sliding_window[/] - 滑动窗口（保留最近 N 条消息）\n" +
                      "[yellow]hierarchical[/] - 层级压缩（近期详细 + 中期关键点 + 旧消息摘要）\n" +
                      "[yellow]semantic[/] - 语义压缩（使用 LLM 生成摘要）"))
        {
            Header = new PanelHeader("[yellow]压缩策略[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(strategyPanel);

        AnsiConsole.WriteLine();

        // 示例
        var examplePanel = new Panel(
            new Markup("[bold]示例：[/]\n" +
                      "[cyan]/context status[/] - 查看上下文状态\n" +
                      "[cyan]/context compress[/] - 使用默认策略压缩\n" +
                      "[cyan]/context compress hierarchical[/] - 使用层级策略压缩\n" +
                      "[cyan]/context config threshold 2500[/] - 设置压缩阈值为 2500 tokens\n" +
                      "[cyan]/context config auto-enabled true[/] - 启用自动压缩\n" +
                      "[cyan]/context history 20[/] - 查看最近 20 条压缩历史"))
        {
            Header = new PanelHeader("[yellow]示例[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(examplePanel);
    }
}
