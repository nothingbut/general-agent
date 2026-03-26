using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// 标签管理命令
/// 支持添加、移除、列出和生成标签
/// </summary>
public sealed class TagCommand
{
    private readonly ISessionTagRepository _tagRepository;
    private readonly ISmartTagService _tagService;

    /// <summary>
    /// 初始化 TagCommand
    /// </summary>
    public TagCommand(
        ISessionTagRepository tagRepository,
        ISmartTagService tagService)
    {
        _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
        _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
    }

    /// <summary>
    /// 添加标签到会话
    /// </summary>
    public async Task ExecuteAddAsync(
        Guid sessionId,
        string tagName,
        string? emoji,
        string? color,
        CancellationToken ct = default)
    {
        try
        {
            var tag = SessionTag.Create(sessionId, tagName, TagSource.User, color, emoji);
            await _tagRepository.AddAsync(tag, ct);
            AnsiConsole.MarkupLine($"[green]✓ 标签已添加:[/] {FormatTag(tag)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ 添加标签失败: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    /// <summary>
    /// 从会话移除标签
    /// </summary>
    public async Task ExecuteRemoveAsync(
        Guid sessionId,
        string tagName,
        CancellationToken ct = default)
    {
        try
        {
            await _tagRepository.RemoveAsync(sessionId, tagName, ct);
            AnsiConsole.MarkupLine($"[green]✓ 标签已移除:[/] {tagName}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ 移除标签失败: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    /// <summary>
    /// 列出标签（会话标签或全局标签）
    /// </summary>
    public async Task ExecuteListAsync(
        Guid? sessionId,
        CancellationToken ct = default)
    {
        try
        {
            if (sessionId.HasValue)
            {
                await ListSessionTagsAsync(sessionId.Value, ct);
            }
            else
            {
                await ListAllTagsAsync(ct);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ 列出标签失败: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    /// <summary>
    /// 生成并应用标签建议
    /// </summary>
    public async Task ExecuteSuggestAsync(
        Guid sessionId,
        string sessionTitle,
        CancellationToken ct = default)
    {
        try
        {
            AnsiConsole.MarkupLine("[cyan]💡 生成标签建议中...[/]");

            var suggestions = await _tagService.SuggestFromTitleAsync(sessionTitle, ct);

            if (suggestions.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ 未生成标签建议[/]");
                return;
            }

            // 显示建议表格
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("标签");
            table.AddColumn("Emoji");
            table.AddColumn("颜色");

            foreach (var suggestion in suggestions)
            {
                table.AddRow(
                    suggestion.Tag,
                    suggestion.Emoji ?? "-",
                    suggestion.Color ?? "-"
                );
            }

            AnsiConsole.Write(table);

            // 询问是否应用
            if (AnsiConsole.Confirm("是否应用这些标签?"))
            {
                await _tagService.ApplySuggestionsAsync(sessionId, suggestions, ct);
                AnsiConsole.MarkupLine("[green]✓ 标签已应用[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ 标签建议失败: {ex.Message.EscapeMarkup()}[/]");
        }
    }

    /// <summary>
    /// 列出会话的所有标签
    /// </summary>
    private async Task ListSessionTagsAsync(Guid sessionId, CancellationToken ct)
    {
        var tags = await _tagRepository.GetBySessionAsync(sessionId, ct);

        if (tags.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ 该会话没有标签[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]标签列表 ({tags.Count}):[/]");
        foreach (var tag in tags)
        {
            AnsiConsole.MarkupLine($"  {FormatTag(tag)} [dim]{tag.Source}[/]");
        }
    }

    /// <summary>
    /// 列出所有标签及其使用统计
    /// </summary>
    private async Task ListAllTagsAsync(CancellationToken ct)
    {
        var statistics = await _tagRepository.GetTagStatisticsAsync(ct);

        if (statistics.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ 没有任何标签[/]");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("标签");
        table.AddColumn("使用次数");

        // 按使用次数降序排列
        foreach (var (tag, count) in statistics.OrderByDescending(kv => kv.Value))
        {
            table.AddRow(tag, count.ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[cyan]总计 {statistics.Count} 个标签[/]");
    }

    /// <summary>
    /// 格式化标签显示（包含 Emoji 和颜色）
    /// </summary>
    private static string FormatTag(SessionTag tag)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(tag.Emoji))
        {
            parts.Add(tag.Emoji);
        }

        parts.Add(tag.Tag);

        if (!string.IsNullOrEmpty(tag.Color))
        {
            parts.Add($"[{tag.Color}]●[/]");
        }

        return string.Join(" ", parts);
    }
}
