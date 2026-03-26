namespace GeneralAgent.Core.Models;

/// <summary>
/// 搜索查询模型（不可变）
/// </summary>
public sealed record SearchQuery
{
    /// <summary>
    /// 原始自然语言查询
    /// </summary>
    public string NaturalQuery { get; init; } = string.Empty;

    /// <summary>
    /// 结构化查询条件
    /// </summary>
    public SearchCriteria Criteria { get; init; } = new();

    /// <summary>
    /// 查询类型
    /// </summary>
    public SearchType Type { get; init; } = SearchType.Keyword;
}

/// <summary>
/// 搜索条件（不可变）
/// </summary>
public sealed record SearchCriteria
{
    /// <summary>
    /// 关键词（用于 LIKE 查询）
    /// </summary>
    public List<string> Keywords { get; init; } = new();

    /// <summary>
    /// 正则表达式模式
    /// </summary>
    public string? RegexPattern { get; init; }

    /// <summary>
    /// 精确短语
    /// </summary>
    public List<string> ExactPhrases { get; init; } = new();

    /// <summary>
    /// 角色过滤
    /// </summary>
    public MessageRole? Role { get; init; }

    /// <summary>
    /// 会话 ID 过滤
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// 时间范围过滤（起始时间）
    /// </summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>
    /// 时间范围过滤（结束时间）
    /// </summary>
    public DateTimeOffset? EndDate { get; init; }
}

/// <summary>
/// 搜索类型
/// </summary>
public enum SearchType
{
    /// <summary>
    /// 关键词搜索
    /// </summary>
    Keyword,

    /// <summary>
    /// 正则表达式
    /// </summary>
    Regex,

    /// <summary>
    /// 精确匹配
    /// </summary>
    Exact,

    /// <summary>
    /// 语义搜索（未来扩展）
    /// </summary>
    Semantic
}
