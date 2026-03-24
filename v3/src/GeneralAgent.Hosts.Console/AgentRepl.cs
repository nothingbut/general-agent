using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
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
    private readonly LLMOptions _llmOptions;
    private readonly ILogger<AgentRepl> _logger;

    private Guid _currentSessionId = Guid.Empty;
    private string _currentProvider = string.Empty;

    public AgentRepl(
        SessionService sessionService,
        ConversationService conversationService,
        IMessageRepository messageRepository,
        SkillService skillService,
        IOptions<LLMOptions> llmOptions,
        ILogger<AgentRepl> logger)
    {
        _sessionService = sessionService;
        _conversationService = conversationService;
        _messageRepository = messageRepository;
        _skillService = skillService;
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

            default:
                AnsiConsole.MarkupLine($"[red]未知命令: {command}[/]");
                AnsiConsole.MarkupLine($"[dim]提示: 输入 /help 查看可用命令[/]");
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

        // 其他
        table.AddRow("", "");
        table.AddRow("[bold yellow]其他[/]", "");
        table.AddRow("[cyan]/clear[/]", "清屏");
        table.AddRow("[cyan]/help[/]", "显示此帮助信息");
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

    /// <summary>
    /// 切换会话
    /// </summary>
    private async Task SwitchSessionAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]用法: /session <session-id>[/]");
            AnsiConsole.MarkupLine("[dim]提示: 使用 /list 查看所有会话[/]");
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
                    AnsiConsole.MarkupLine($"[red]未找到会话: {sessionIdStr}[/]");
                    return;
                }

                if (matchingSessions.Count > 1)
                {
                    AnsiConsole.MarkupLine($"[yellow]找到多个匹配的会话，请使用更长的 ID：[/]");
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
                AnsiConsole.MarkupLine($"[red]会话不存在: {sessionId}[/]");
                return;
            }

            // 切换会话
            _currentSessionId = sessionId;
            AnsiConsole.MarkupLine($"[green]✓ 已切换到会话: {session.Title}[/]");
            AnsiConsole.MarkupLine($"  ID: [cyan]{session.Id.ToString()[..8]}...[/]");
            AnsiConsole.MarkupLine($"  创建时间: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换会话失败");
            AnsiConsole.MarkupLine($"[red]切换会话失败: {ex.Message}[/]");
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
                    AnsiConsole.MarkupLine("[red]没有活动会话[/]");
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
                        AnsiConsole.MarkupLine($"[red]未找到会话: {sessionIdStr}[/]");
                        return;
                    }

                    if (matchingSessions.Count > 1)
                    {
                        AnsiConsole.MarkupLine($"[yellow]找到多个匹配的会话，请使用更长的 ID[/]");
                        return;
                    }

                    sessionId = matchingSessions[0].Id;
                }
            }

            // 获取会话信息
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                AnsiConsole.MarkupLine($"[red]会话不存在: {sessionId}[/]");
                return;
            }

            // 确认删除
            var confirm = AnsiConsole.Confirm(
                $"确定要删除会话 [cyan]{session.Title}[/] ([dim]{session.Id.ToString()[..8]}...[/]) 吗？",
                false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]已取消删除[/]");
                return;
            }

            // 删除会话
            await _sessionService.DeleteSessionAsync(sessionId);
            AnsiConsole.MarkupLine($"[green]✓ 已删除会话: {session.Title}[/]");

            // 如果删除的是当前会话，清除当前会话 ID
            if (sessionId == _currentSessionId)
            {
                _currentSessionId = Guid.Empty;
                AnsiConsole.MarkupLine("[yellow]当前会话已清除，请使用 /new 创建新会话或 /session 切换到其他会话[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败");
            AnsiConsole.MarkupLine($"[red]删除会话失败: {ex.Message}[/]");
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
                    AnsiConsole.MarkupLine($"[yellow]命名空间 '{namespaceName}' 中没有技能[/]");
                    return;
                }
            }
            else
            {
                skills = _skillService.GetAllSkills();

                if (skills.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]没有加载任何技能[/]");
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
            AnsiConsole.MarkupLine("\n[dim]提示: 使用 /skill <name> 查看技能详情[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示技能列表失败");
            AnsiConsole.MarkupLine($"[red]显示技能列表失败: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// 显示技能详情
    /// </summary>
    private void ShowSkillInfo(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]用法: /skill <skill-name>[/]");
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
                AnsiConsole.MarkupLine($"[red]未找到技能: {skillName}[/]");
                AnsiConsole.MarkupLine("[dim]提示: 使用 /skills 查看所有可用技能[/]");
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
                AnsiConsole.MarkupLine("[dim]提示: 使用 /skill <name> --template 查看提示词模板[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示技能详情失败");
            AnsiConsole.MarkupLine($"[red]显示技能详情失败: {ex.Message}[/]");
        }
    }
}
