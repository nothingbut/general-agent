using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 智能标签建议服务
/// 基于 LLM 为会话生成智能标签建议
/// </summary>
public sealed class SmartTagService : ISmartTagService
{
    private readonly ILLMClient _llmClient;
    private readonly ISessionTagRepository _tagRepository;
    private readonly ILogger<SmartTagService> _logger;

    // 配置常量
    private const int MaxTagsPerSession = 5;
    private const int MaxSuggestionsFromTitle = 3;
    private const int MaxSuggestionsFromContent = 5;
    private const int TitleModeTimeoutSeconds = 3;
    private const int ContentModeTimeoutSeconds = 10;

    /// <summary>
    /// 初始化 SmartTagService
    /// </summary>
    public SmartTagService(
        ILLMClient llmClient,
        ISessionTagRepository tagRepository,
        ILogger<SmartTagService> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据会话标题生成标签建议（快速模式）
    /// </summary>
    public async Task<List<TagSuggestion>> SuggestFromTitleAsync(
        string title,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        _logger.LogDebug("快速模式：基于标题生成标签建议 - {Title}", title);

        // 调用 LLM 生成建议
        try
        {
            var response = await CallLLMForTitleMode(title, ct);
            var suggestions = ParseLLMResponse(response);

            // 限制返回数量
            var limitedSuggestions = suggestions
                .Take(MaxSuggestionsFromTitle)
                .ToList();

            _logger.LogDebug("生成 {Count} 个标签建议", limitedSuggestions.Count);
            return limitedSuggestions;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("快速模式超时或被取消，返回空建议");
            return new List<TagSuggestion>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM 返回无效 JSON，返回空建议");
            return new List<TagSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成标签建议出错，返回空建议");
            return new List<TagSuggestion>();
        }
    }

    /// <summary>
    /// 根据会话内容生成标签建议（深度模式）
    /// </summary>
    public async Task<List<TagSuggestion>> SuggestFromContentAsync(
        Guid sessionId,
        List<Message> messages,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        _logger.LogDebug("深度模式：基于 {Count} 条消息生成标签建议", messages.Count);

        // 调用 LLM 生成建议
        try
        {
            var response = await CallLLMForContentMode(messages, ct);
            var suggestions = ParseLLMResponse(response);

            // 限制返回数量
            var limitedSuggestions = suggestions
                .Take(MaxSuggestionsFromContent)
                .ToList();

            _logger.LogDebug("生成 {Count} 个标签建议", limitedSuggestions.Count);
            return limitedSuggestions;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("深度模式超时或被取消，返回空建议");
            return new List<TagSuggestion>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM 返回无效 JSON，返回空建议");
            return new List<TagSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成标签建议出错，返回空建议");
            return new List<TagSuggestion>();
        }
    }

    /// <summary>
    /// 应用标签建议（去重、限额检查）
    /// </summary>
    public async Task ApplySuggestionsAsync(
        Guid sessionId,
        List<TagSuggestion> suggestions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        // 1. 获取现有标签
        var existingTags = await _tagRepository.GetBySessionAsync(sessionId, ct);
        var existingTagNames = existingTags.Select(t => t.Tag.ToLowerInvariant()).ToHashSet();

        // 2. 过滤重复和超限
        var availableSlots = MaxTagsPerSession - existingTags.Count;
        if (availableSlots <= 0)
        {
            _logger.LogWarning("会话 {SessionId} 标签数量已达上限 {Max}", sessionId, MaxTagsPerSession);
            return;
        }

        var newSuggestions = suggestions
            .Where(s => !existingTagNames.Contains(s.Tag.ToLowerInvariant()))
            .Take(availableSlots)
            .ToList();

        // 3. 添加标签
        foreach (var suggestion in newSuggestions)
        {
            var tag = SessionTag.Create(
                sessionId,
                suggestion.Tag,
                TagSource.Auto,
                suggestion.Color,
                suggestion.Emoji
            );
            await _tagRepository.AddAsync(tag, ct);
        }

        _logger.LogInformation(
            "为会话 {SessionId} 添加了 {Count} 个自动标签",
            sessionId,
            newSuggestions.Count
        );
    }

    /// <summary>
    /// 调用 LLM 进行快速模式标签生成（基于标题）
    /// </summary>
    private async Task<string> CallLLMForTitleMode(string title, CancellationToken ct)
    {
        var prompt = BuildTitlePrompt(title);
        var systemPrompt = "你是一个标签生成器。为会话标题生成 1-3 个相关的标签（包含表情符号和颜色）。只返回 JSON，不要其他解释。";

        // 创建带超时的取消令牌源
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TitleModeTimeoutSeconds));
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
            MaxTokens = 300
        };

        var response = await _llmClient.CompleteAsync(request, linkedCts.Token);
        return response.Content;
    }

    /// <summary>
    /// 调用 LLM 进行深度模式标签生成（基于内容）
    /// </summary>
    private async Task<string> CallLLMForContentMode(List<Message> messages, CancellationToken ct)
    {
        var prompt = BuildContentPrompt(messages);
        var systemPrompt = "你是一个标签生成器。根据对话内容生成 1-5 个相关的标签（包含表情符号和颜色）。只返回 JSON，不要其他解释。";

        // 创建带超时的取消令牌源
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ContentModeTimeoutSeconds));
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
            MaxTokens = 800
        };

        var response = await _llmClient.CompleteAsync(request, linkedCts.Token);
        return response.Content;
    }

    /// <summary>
    /// 构建快速模式提示词（基于标题）
    /// </summary>
    private static string BuildTitlePrompt(string title)
    {
        return @"为以下会话标题生成 1-3 个相关标签。

会话标题: " + title + @"

要求：
- 标签应简洁、准确、小写、用连字符分隔单词
- 标签应反映主题、技术栈或讨论领域
- 为每个标签选择合适的表情符号和颜色（十六进制）

返回 JSON 格式：
{
  ""tags"": [
    {""name"": ""标签名"", ""emoji"": ""🐍"", ""color"": ""#3776AB""}
  ]
}

示例：
- 标题：""讨论 Python 异步编程最佳实践""
  JSON: {""tags"":[{""name"":""python"",""emoji"":""🐍"",""color"":""#3776AB""},{""name"":""async"",""emoji"":""⚡"",""color"":""#F59E0B""}]}

仅返回 JSON，不要其他内容。";
    }

    /// <summary>
    /// 构建深度模式提示词（基于内容）
    /// </summary>
    private static string BuildContentPrompt(List<Message> messages)
    {
        // 处理空消息列表的边缘情况
        if (messages.Count == 0)
        {
            return @"为以下对话内容生成 1-5 个相关标签。

对话内容: (空对话)

返回 JSON 格式：
{
  ""tags"": []
}";
        }

        var contentSummary = string.Join("\n", messages.Take(10).Select(m =>
            $"{m.Role}: {(m.Content.Length > 200 ? m.Content.Substring(0, 200) + "..." : m.Content)}"
        ));

        return @"为以下对话内容生成 1-5 个相关标签。

对话内容:
" + contentSummary + @"

要求：
- 标签应简洁、准确、小写、用连字符分隔单词
- 标签应反映讨论的主题、技术栈、问题类型或领域
- 为每个标签选择合适的表情符号和颜色（十六进制）

返回 JSON 格式：
{
  ""tags"": [
    {""name"": ""标签名"", ""emoji"": ""🐛"", ""color"": ""#EF4444""}
  ]
}

仅返回 JSON，不要其他内容。";
    }

    /// <summary>
    /// 解析 LLM 响应为标签建议列表
    /// </summary>
    private static List<TagSuggestion> ParseLLMResponse(string response)
    {
        // 清理可能的 Markdown 代码块
        var json = CleanJsonResponse(response);

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var suggestions = new List<TagSuggestion>();

        // 解析标签数组
        if (root.TryGetProperty("tags", out var tagsArray) &&
            tagsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var tagElement in tagsArray.EnumerateArray())
            {
                if (tagElement.TryGetProperty("name", out var nameElement))
                {
                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string? emoji = null;
                    if (tagElement.TryGetProperty("emoji", out var emojiElement))
                    {
                        emoji = emojiElement.GetString();
                    }

                    string? color = null;
                    if (tagElement.TryGetProperty("color", out var colorElement))
                    {
                        color = colorElement.GetString();
                    }

                    suggestions.Add(new TagSuggestion(name, emoji, color));
                }
            }
        }

        return suggestions;
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
            json = json.Substring(7).Trim();
        }
        // 移除 ``` 开头
        else if (json.StartsWith("```"))
        {
            json = json.Substring(3).Trim();
        }

        // 移除 ``` 结尾（确保长度足够）
        if (json.EndsWith("```") && json.Length >= 3)
        {
            json = json.Substring(0, json.Length - 3);
        }

        return json.Trim();
    }
}
