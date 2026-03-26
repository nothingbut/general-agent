namespace GeneralAgent.Hosts.Console.Services;

/// <summary>
/// 搜索服务接口
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// 使用自然语言查询进行搜索
    /// </summary>
    /// <param name="naturalQuery">自然语言查询</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>搜索结果列表</returns>
    Task<List<SearchResult>> SearchWithNaturalLanguageAsync(
        string naturalQuery,
        CancellationToken ct = default);
}

/// <summary>
/// 搜索结果（不可变）
/// </summary>
public sealed record SearchResult(
    Guid MessageId,
    string SessionTitle,
    string Content,
    DateTime CreatedAt);
