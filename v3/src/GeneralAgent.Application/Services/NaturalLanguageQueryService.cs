using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 自然语言查询服务
/// 将用户的自然语言查询转换为结构化的搜索条件
/// </summary>
public sealed class NaturalLanguageQueryService
{
    private readonly ILLMClient _llmClient;
    private readonly ISearchQueryCache _queryCache;
    private readonly ILogger<NaturalLanguageQueryService> _logger;

    /// <summary>
    /// 初始化 NaturalLanguageQueryService
    /// </summary>
    public NaturalLanguageQueryService(
        ILLMClient llmClient,
        ISearchQueryCache queryCache,
        ILogger<NaturalLanguageQueryService> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _queryCache = queryCache ?? throw new ArgumentNullException(nameof(queryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 解析自然语言查询为结构化搜索条件
    /// </summary>
    /// <param name="naturalQuery">自然语言查询</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>解析后的搜索查询</returns>
    public async Task<SearchQuery> ParseQueryAsync(
        string naturalQuery,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalQuery);

        // 1. 检查缓存
        var cachedQuery = _queryCache.Get(naturalQuery);
        if (cachedQuery != null)
        {
            _logger.LogDebug("查询缓存命中: {Query}", naturalQuery);
            return cachedQuery;
        }

        _logger.LogDebug("缓存未命中，调用 LLM 解析查询: {Query}", naturalQuery);

        // 2. 调用 LLM 解析
        try
        {
            var response = await CallLLMForQueryParsing(naturalQuery, ct);
            var criteria = ParseLLMResponse(response);

            var query = new SearchQuery
            {
                NaturalQuery = naturalQuery,
                Criteria = criteria,
                Type = DetermineSearchType(criteria)
            };

            // 3. 缓存结果
            _queryCache.Set(naturalQuery, query);
            _logger.LogDebug("查询已缓存: {Query}", naturalQuery);

            return query;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("查询解析超时或被取消，回退到关键词搜索: {Query}", naturalQuery);
            return FallbackToKeywordSearch(naturalQuery);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM 返回无效 JSON，回退到关键词搜索: {Query}", naturalQuery);
            return FallbackToKeywordSearch(naturalQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析查询出错，回退到关键词搜索: {Query}", naturalQuery);
            return FallbackToKeywordSearch(naturalQuery);
        }
    }

    /// <summary>
    /// 调用 LLM 进行查询解析
    /// </summary>
    private async Task<string> CallLLMForQueryParsing(string naturalQuery, CancellationToken ct)
    {
        var prompt = BuildParsePrompt(naturalQuery);
        var systemPrompt = "你是一个搜索查询解析器。将用户的自然语言查询转换为结构化的搜索条件。只返回 JSON，不要其他解释。";

        // 创建带超时的取消令牌源
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var request = new CompletionRequest
        {
            Model = "default",
            Messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            },
            SystemPrompt = systemPrompt,
            Temperature = 0.3,
            MaxTokens = 500
        };

        var response = await _llmClient.CompleteAsync(request, linkedCts.Token);
        return response.Content;
    }

    /// <summary>
    /// 构建 LLM 提示词
    /// </summary>
    private static string BuildParsePrompt(string naturalQuery)
    {
        return @"将用户查询转换为 JSON 格式的搜索条件。

用户查询: " + naturalQuery + @"

返回 JSON（包含以下可选字段）：
{
  ""keywords"": [""关键词1"", ""关键词2""],
  ""exactPhrases"": [""精确短语""],
  ""regexPattern"": ""正则表达式（如果需要）"",
  ""role"": ""User"" | ""Assistant"" | ""System"" | null,
  ""startDate"": ""ISO8601日期"" | null,
  ""endDate"": ""ISO8601日期"" | null
}

示例：
- 查询：""查找昨天关于 Python 的讨论""
  JSON: {""keywords"":[""Python""], ""startDate"":""2026-03-24T00:00:00Z""}

- 查询：""搜索我问的所有问题""
  JSON: {""keywords"":[], ""role"":""User""}

- 查询：""包含 'bug fix' 的消息""
  JSON: {""exactPhrases"":[""bug fix""]}

仅返回 JSON，不要其他内容。";
    }

    /// <summary>
    /// 解析 LLM 响应为结构化条件
    /// </summary>
    private static SearchCriteria ParseLLMResponse(string response)
    {
        // 清理可能的 Markdown 代码块
        var json = CleanJsonResponse(response);

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var criteria = new SearchCriteria();

        // 解析关键词
        if (root.TryGetProperty("keywords", out var keywords) &&
            keywords.ValueKind != JsonValueKind.Null)
        {
            var keywordList = keywords.EnumerateArray()
                .Select(k => k.GetString()!)
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            criteria = criteria with { Keywords = keywordList };
        }

        // 解析精确短语
        if (root.TryGetProperty("exactPhrases", out var phrases) &&
            phrases.ValueKind != JsonValueKind.Null)
        {
            var phraseList = phrases.EnumerateArray()
                .Select(p => p.GetString()!)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            criteria = criteria with { ExactPhrases = phraseList };
        }

        // 解析正则表达式
        if (root.TryGetProperty("regexPattern", out var regex) &&
            regex.ValueKind != JsonValueKind.Null)
        {
            var pattern = regex.GetString();
            if (!string.IsNullOrEmpty(pattern))
            {
                criteria = criteria with { RegexPattern = pattern };
            }
        }

        // 解析角色
        if (root.TryGetProperty("role", out var role) &&
            role.ValueKind != JsonValueKind.Null)
        {
            var roleStr = role.GetString();
            if (!string.IsNullOrEmpty(roleStr) &&
                Enum.TryParse<MessageRole>(roleStr, ignoreCase: true, out var roleEnum))
            {
                criteria = criteria with { Role = roleEnum };
            }
        }

        // 解析开始日期
        if (root.TryGetProperty("startDate", out var startDate) &&
            startDate.ValueKind != JsonValueKind.Null)
        {
            var dateStr = startDate.GetString();
            if (!string.IsNullOrEmpty(dateStr) &&
                DateTimeOffset.TryParse(dateStr, out var start))
            {
                criteria = criteria with { StartDate = start };
            }
        }

        // 解析结束日期
        if (root.TryGetProperty("endDate", out var endDate) &&
            endDate.ValueKind != JsonValueKind.Null)
        {
            var dateStr = endDate.GetString();
            if (!string.IsNullOrEmpty(dateStr) &&
                DateTimeOffset.TryParse(dateStr, out var end))
            {
                criteria = criteria with { EndDate = end };
            }
        }

        return criteria;
    }

    /// <summary>
    /// 清理 JSON 响应（移除 Markdown 代码块）
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        var json = response.Trim();

        // 移除 ```json 开头
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Substring(7);
        }
        // 移除 ``` 开头
        else if (json.StartsWith("```"))
        {
            json = json.Substring(3);
        }

        // 移除 ``` 结尾
        if (json.EndsWith("```"))
        {
            json = json.Substring(0, json.Length - 3);
        }

        return json.Trim();
    }

    /// <summary>
    /// 判断搜索类型
    /// </summary>
    private static SearchType DetermineSearchType(SearchCriteria criteria)
    {
        // 优先级：Regex > Exact > Keyword
        if (!string.IsNullOrEmpty(criteria.RegexPattern))
            return SearchType.Regex;

        if (criteria.ExactPhrases.Count > 0)
            return SearchType.Exact;

        return SearchType.Keyword;
    }

    /// <summary>
    /// 回退到关键词搜索
    /// </summary>
    private static SearchQuery FallbackToKeywordSearch(string query)
    {
        return new SearchQuery
        {
            NaturalQuery = query,
            Criteria = new SearchCriteria
            {
                Keywords = new List<string> { query }
            },
            Type = SearchType.Keyword
        };
    }
}
