using System.Text;
using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using MemoryEntity = GeneralAgent.Core.Models.Memory;

namespace GeneralAgent.Infrastructure.Memory.Services;

/// <summary>
/// 记忆提取服务实现 - 使用 LLM 从对话中提取记忆
/// </summary>
public sealed class MemoryExtractionService : IMemoryExtractionService
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMemoryRepository _memoryRepository;
    private readonly ILogger<MemoryExtractionService> _logger;

    private const string ExtractionSystemPrompt = """
        你是一个记忆提取助手。你的任务是从用户的消息中识别并提取有价值的记忆信息。

        记忆类型说明：
        - User: 用户的个人信息、偏好、习惯、背景
        - Feedback: 用户对工作方式的反馈、建议、纠正
        - Project: 项目相关的信息、决策、进展、目标
        - Reference: 外部资源、文档、链接的引用
        - Knowledge: 领域知识、技术概念、最佳实践

        提取原则：
        1. 只提取明确、有价值的信息
        2. 置信度 < 0.6 的建议不要返回
        3. 为每个记忆生成清晰的名称、描述和标签
        4. 检测是否与现有记忆重复

        响应格式（JSON）：
        {
          "suggestions": [
            {
              "type": "User|Feedback|Project|Reference|Knowledge",
              "name": "memory_name",
              "description": "简短描述",
              "content": "完整内容",
              "confidence": 0.0-1.0,
              "tags": ["tag1", "tag2"],
              "rationale": "提取原因"
            }
          ]
        }

        如果消息中没有可提取的记忆，返回 {"suggestions": []}
        """;

    public MemoryExtractionService(
        ILLMClientFactory llmFactory,
        IMemoryRepository memoryRepository,
        ILogger<MemoryExtractionService> logger)
    {
        _llmFactory = llmFactory;
        _memoryRepository = memoryRepository;
        _logger = logger;
    }

    public async Task<List<MemorySuggestion>> ExtractFromMessageAsync(
        string messageContent,
        string? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
        {
            return new List<MemorySuggestion>();
        }

        try
        {
            // 构建提示词
            var userPrompt = new StringBuilder();
            userPrompt.AppendLine("请从以下消息中提取记忆：");
            userPrompt.AppendLine();
            userPrompt.AppendLine($"消息内容：{messageContent}");

            if (!string.IsNullOrWhiteSpace(conversationContext))
            {
                userPrompt.AppendLine();
                userPrompt.AppendLine($"对话上下文：{conversationContext}");
            }

            // 调用 LLM
            var client = _llmFactory.GetClient();
            var request = new CompletionRequest
            {
                Model = "qwen2.5:0.5b", // 使用配置的默认模型
                Messages = new[]
                {
                    new ChatMessage { Role = "user", Content = userPrompt.ToString() }
                },
                SystemPrompt = ExtractionSystemPrompt,
                Temperature = 0.3, // 降低温度以获得更一致的结果
                MaxTokens = 2000
            };

            var response = await client.CompleteAsync(request, cancellationToken);

            // 解析 JSON 响应
            var suggestions = ParseSuggestions(response.Content);

            _logger.LogInformation(
                "从消息中提取了 {Count} 个记忆建议",
                suggestions.Count);

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取记忆时发生错误");
            return new List<MemorySuggestion>();
        }
    }

    public async Task<MemoryEntity?> CreateMemoryFromSuggestionAsync(
        MemorySuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查置信度阈值
            if (suggestion.Confidence < 0.6)
            {
                _logger.LogInformation(
                    "记忆建议 '{Name}' 置信度过低 ({Confidence})，已拒绝",
                    suggestion.Name,
                    suggestion.Confidence);
                return null;
            }

            // 检查是否已存在同名记忆
            if (await _memoryRepository.NameExistsAsync(suggestion.Name, suggestion.Type, cancellationToken))
            {
                _logger.LogWarning(
                    "记忆 '{Name}' 已存在，已跳过",
                    suggestion.Name);
                return null;
            }

            // 创建记忆（包含标签）
            var memory = MemoryEntity.Create(
                suggestion.Type,
                suggestion.Name,
                suggestion.Description,
                suggestion.Content,
                suggestion.Tags.ToList());

            // 保存到仓储
            await _memoryRepository.SaveAsync(memory, cancellationToken);

            _logger.LogInformation(
                "成功创建记忆 '{Name}' (Type: {Type})",
                memory.Name,
                memory.Type);

            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从建议创建记忆时发生错误");
            return null;
        }
    }

    public async Task<List<MemorySuggestion>> ExtractFromConversationAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
        {
            return new List<MemorySuggestion>();
        }

        // 将对话历史组合成一个上下文
        var context = new StringBuilder();
        foreach (var message in conversationHistory)
        {
            context.AppendLine($"{message.Role}: {message.Content}");
        }

        // 提取最后一条用户消息
        var lastUserMessage = conversationHistory
            .LastOrDefault(m => m.Role == "user");

        if (lastUserMessage == null)
        {
            return new List<MemorySuggestion>();
        }

        return await ExtractFromMessageAsync(
            lastUserMessage.Content,
            context.ToString(),
            cancellationToken);
    }

    private List<MemorySuggestion> ParseSuggestions(string llmResponse)
    {
        try
        {
            // 尝试提取 JSON（LLM 可能在响应中包含额外的文本）
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("LLM 响应中未找到有效的 JSON");
                return new List<MemorySuggestion>();
            }

            var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<ExtractionResult>(jsonContent, options);

            if (result?.Suggestions == null)
            {
                return new List<MemorySuggestion>();
            }

            // 转换为 MemorySuggestion
            var suggestions = new List<MemorySuggestion>();
            foreach (var dto in result.Suggestions)
            {
                if (!Enum.TryParse<MemoryType>(dto.Type, true, out var memoryType))
                {
                    _logger.LogWarning("无效的记忆类型: {Type}", dto.Type);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.Name) ||
                    string.IsNullOrWhiteSpace(dto.Content))
                {
                    continue;
                }

                suggestions.Add(new MemorySuggestion
                {
                    Type = memoryType,
                    Name = dto.Name.Trim(),
                    Description = dto.Description?.Trim() ?? "",
                    Content = dto.Content.Trim(),
                    Confidence = dto.Confidence,
                    Tags = dto.Tags ?? Array.Empty<string>(),
                    Rationale = dto.Rationale
                });
            }

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 LLM 响应时发生错误");
            return new List<MemorySuggestion>();
        }
    }

    // DTO 用于反序列化
    private sealed class ExtractionResult
    {
        public List<SuggestionDto>? Suggestions { get; set; }
    }

    private sealed class SuggestionDto
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Content { get; set; } = "";
        public double Confidence { get; set; }
        public string[]? Tags { get; set; }
        public string? Rationale { get; set; }
    }
}
