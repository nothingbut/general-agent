using System.Text;
using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能提取服务实现 - 使用 LLM 识别对话中的重复性任务模式
/// </summary>
public sealed class SkillExtractionService : ISkillExtractionService
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<SkillExtractionService> _logger;

    private const string ExtractionSystemPrompt = """
        你是一个技能提取助手。分析对话历史，识别重复性任务模式并生成技能建议。

        识别标准：
        1. 任务至少出现 2-3 次
        2. 任务有明确的步骤和输入输出
        3. 任务可以参数化（有变化的部分）
        4. 任务足够复杂，值得创建技能

        技能命名规则：
        - 使用小写字母和连字符（如 api-helper）
        - 名称清晰描述功能
        - 长度 10-30 字符

        命名空间建议：
        - dev: 开发相关（代码、API、工具）
        - productivity: 生产力工具（任务、笔记、提醒）
        - personal: 个人助手（问候、日程、习惯）
        - analysis: 数据分析（统计、报表、可视化）
        - writing: 写作辅助（文档、邮件、博客）

        输出格式（JSON）：
        {
          "suggestions": [
            {
              "name": "skill-name",
              "namespace": "category",
              "description": "简短描述（一句话）",
              "template": "提示词模板（使用 {{param}} 占位符）",
              "parameters": [
                {
                  "name": "param1",
                  "type": "string|number|boolean",
                  "required": true,
                  "description": "参数说明"
                }
              ],
              "confidence": 0.0-1.0,
              "rationale": "为什么建议这个技能（1-2句话）",
              "occurrences": 出现次数,
              "exampleMessages": ["示例消息1", "示例消息2"]
            }
          ]
        }

        如果没有识别到合适的模式（置信度 < 0.6），返回 {"suggestions": []}
        """;

    public SkillExtractionService(
        ILLMClientFactory llmFactory,
        IMessageRepository messageRepository,
        ILogger<SkillExtractionService> logger)
    {
        _llmFactory = llmFactory;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<List<SkillSuggestion>> ExtractFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid))
        {
            _logger.LogWarning("无效的会话 ID: {SessionId}", sessionId);
            return new List<SkillSuggestion>();
        }

        _logger.LogInformation("从会话 {SessionId} 提取技能建议（回溯 {Count} 条消息）",
            sessionId, lookbackMessages);

        // 加载会话的最近 N 条消息
        var messages = await _messageRepository.GetRecentAsync(sessionGuid, lookbackMessages, cancellationToken);
        if (messages == null || messages.Count == 0)
        {
            _logger.LogWarning("会话 {SessionId} 没有消息", sessionId);
            return new List<SkillSuggestion>();
        }

        _logger.LogDebug("加载了 {Count} 条消息进行分析", messages.Count);

        return await ExtractFromMessagesAsync(messages, cancellationToken);
    }

    public async Task<List<SkillSuggestion>> ExtractFromMessagesAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages == null || messages.Count < 2)
        {
            _logger.LogWarning("消息数量不足，无法提取技能");
            return new List<SkillSuggestion>();
        }

        _logger.LogInformation("开始分析 {Count} 条消息", messages.Count);

        try
        {
            // 构建分析提示词
            var userPrompt = BuildAnalysisPrompt(messages);

            // 调用 LLM
            var client = _llmFactory.GetClient();
            var request = new CompletionRequest
            {
                Model = "qwen2.5:0.5b", // 使用配置的默认模型
                Messages = new[]
                {
                    new ChatMessage { Role = "user", Content = userPrompt }
                },
                SystemPrompt = ExtractionSystemPrompt,
                Temperature = 0.3, // 降低温度以获得更一致的结果
                MaxTokens = 4000
            };

            _logger.LogDebug("发送 LLM 请求...");
            var response = await client.CompleteAsync(request, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                _logger.LogWarning("LLM 返回空响应");
                return new List<SkillSuggestion>();
            }

            _logger.LogDebug("收到 LLM 响应: {Length} 字符", response.Content.Length);

            // 解析 JSON 响应
            var suggestions = ParseSuggestions(response.Content);

            // 过滤低置信度建议
            var filtered = suggestions
                .Where(s => s.Confidence >= 0.6)
                .OrderByDescending(s => s.Confidence)
                .ToList();

            _logger.LogInformation("提取到 {Total} 个建议，过滤后剩余 {Filtered} 个（置信度 >= 0.6）",
                suggestions.Count, filtered.Count);

            return filtered;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析 LLM 响应 JSON 失败");
            return new List<SkillSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取技能建议时发生错误");
            return new List<SkillSuggestion>();
        }
    }

    private string BuildAnalysisPrompt(IReadOnlyList<Message> messages)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("请分析以下对话历史，识别重复性任务模式：");
        prompt.AppendLine();

        // 添加对话历史
        foreach (var message in messages)
        {
            var role = message.Role switch
            {
                MessageRole.User => "用户",
                MessageRole.Assistant => "助手",
                MessageRole.System => "系统",
                _ => "未知"
            };

            // 截断过长的消息
            var content = message.Content.Length > 500
                ? message.Content[..500] + "..."
                : message.Content;

            prompt.AppendLine($"[{role}]: {content}");
            prompt.AppendLine();
        }

        prompt.AppendLine("---");
        prompt.AppendLine("请提取重复性任务模式并生成技能建议（JSON 格式）。");

        return prompt.ToString();
    }

    private List<SkillSuggestion> ParseSuggestions(string json)
    {
        var suggestions = new List<SkillSuggestion>();

        try
        {
            // 尝试提取 JSON 对象（可能包含在 markdown 代码块中）
            var jsonContent = ExtractJsonFromMarkdown(json);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = JsonSerializer.Deserialize<ExtractionResponse>(jsonContent, options);

            if (response?.Suggestions == null)
            {
                _logger.LogWarning("响应中没有 suggestions 字段");
                return suggestions;
            }

            foreach (var dto in response.Suggestions)
            {
                try
                {
                    var suggestion = new SkillSuggestion
                    {
                        Name = dto.Name ?? "unnamed",
                        Description = dto.Description ?? "",
                        Namespace = dto.Namespace ?? "general",
                        Template = dto.Template ?? "",
                        Parameters = dto.Parameters?.Select(p => new SkillParameterDefinition
                        {
                            Name = p.Name ?? "param",
                            Type = p.Type ?? "string",
                            Required = p.Required,
                            Description = p.Description ?? "",
                            DefaultValue = p.DefaultValue
                        }).ToList() ?? new List<SkillParameterDefinition>(),
                        Confidence = dto.Confidence,
                        Rationale = dto.Rationale ?? "",
                        Occurrences = dto.Occurrences,
                        ExampleMessages = dto.ExampleMessages?.ToList() ?? new List<string>()
                    };

                    suggestions.Add(suggestion);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析单个建议失败");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析建议列表失败");
        }

        return suggestions;
    }

    private string ExtractJsonFromMarkdown(string text)
    {
        // 如果文本包含在 ```json 代码块中，提取出来
        var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = text.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = text.IndexOf("```", jsonStart);
            if (jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }
        }

        // 如果文本包含在 ``` 代码块中
        jsonStart = text.IndexOf("```");
        if (jsonStart >= 0)
        {
            jsonStart = text.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = text.IndexOf("```", jsonStart);
            if (jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }
        }

        // 否则直接返回原文
        return text.Trim();
    }

    // DTO 类用于 JSON 反序列化
    private sealed class ExtractionResponse
    {
        public List<SuggestionDto>? Suggestions { get; set; }
    }

    private sealed class SuggestionDto
    {
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public string? Description { get; set; }
        public string? Template { get; set; }
        public List<ParameterDto>? Parameters { get; set; }
        public double Confidence { get; set; }
        public string? Rationale { get; set; }
        public int Occurrences { get; set; }
        public List<string>? ExampleMessages { get; set; }
    }

    private sealed class ParameterDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public bool Required { get; set; }
        public string? Description { get; set; }
        public string? DefaultValue { get; set; }
    }
}
