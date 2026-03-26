using System.Diagnostics;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Hosts.Console.Services;

/// <summary>
/// 搜索服务实现
/// 集成自然语言查询解析和消息搜索
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly NaturalLanguageQueryService _nlQueryService;
    private readonly IMessageRepository _messageRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SearchService> _logger;

    /// <summary>
    /// 初始化 SearchService
    /// </summary>
    public SearchService(
        NaturalLanguageQueryService nlQueryService,
        IMessageRepository messageRepository,
        ISessionRepository sessionRepository,
        ILogger<SearchService> logger)
    {
        _nlQueryService = nlQueryService ?? throw new ArgumentNullException(nameof(nlQueryService));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 使用自然语言查询进行搜索
    /// </summary>
    public async Task<List<SearchResult>> SearchWithNaturalLanguageAsync(
        string naturalQuery,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalQuery);

        var sw = Stopwatch.StartNew();

        try
        {
            // 1. 解析自然语言查询
            _logger.LogInformation("开始解析查询: {Query}", naturalQuery);
            var searchQuery = await _nlQueryService.ParseQueryAsync(naturalQuery, ct);

            _logger.LogInformation(
                "查询解析完成: Type={Type}, Keywords={Keywords}",
                searchQuery.Type,
                string.Join(", ", searchQuery.Criteria.Keywords));

            // 2. 执行搜索（简化实现，返回空列表）
            // TODO: 在后续任务中实现真正的 FTS5 搜索
            var results = new List<SearchResult>();

            sw.Stop();
            _logger.LogInformation(
                "搜索完成: 耗时 {ElapsedMs}ms, 结果数 {Count}",
                sw.ElapsedMilliseconds,
                results.Count);

            return results;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "搜索失败: Query={Query}, 耗时 {ElapsedMs}ms",
                naturalQuery,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
