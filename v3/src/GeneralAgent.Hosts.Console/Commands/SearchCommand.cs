using GeneralAgent.Hosts.Console.Services;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// 搜索命令
/// </summary>
public sealed class SearchCommand
{
    private readonly ISearchService _searchService;

    public SearchCommand(ISearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    public async Task ExecuteAsync(string query, CancellationToken ct = default)
    {
        AnsiConsole.MarkupLine($"[cyan]🔍 搜索:[/] {query}");

        var results = await _searchService.SearchWithNaturalLanguageAsync(query, ct);

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ 未找到匹配结果[/]");
            return;
        }

        // 显示结果表格
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("会话")
            .AddColumn("内容摘要")
            .AddColumn("时间");

        foreach (var result in results)
        {
            var contentPreview = result.Content.Length > 60
                ? result.Content.Substring(0, 60) + "..."
                : result.Content;

            table.AddRow(
                result.SessionTitle,
                contentPreview.EscapeMarkup(),
                result.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[green]✓ 找到 {results.Count} 条结果[/]");
    }
}
