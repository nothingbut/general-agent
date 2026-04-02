using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using MemoryEntity = GeneralAgent.Core.Models.Memory;

namespace GeneralAgent.Infrastructure.Memory.Services;

/// <summary>
/// 记忆检索服务实现 - 语义搜索和相关记忆推荐
/// </summary>
public sealed class MemoryRetrievalService : IMemoryRetrievalService
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMemoryRepository _memoryRepository;
    private readonly ILogger<MemoryRetrievalService> _logger;
    private readonly IEmbeddingClient? _embeddingClient;
    private readonly IVectorRepository? _vectorRepository;

    /// <summary>
    /// 降级到 LLM 评分时触发的事件
    /// </summary>
    public event Action<string>? OnFallbackToLLMScoring;

    private const string RelevanceSystemPrompt = """
        你是一个记忆相关性评估助手。你的任务是评估记忆内容与查询的相关性。

        评分标准：
        - 1.0: 高度相关，直接回答查询或提供关键信息
        - 0.7-0.9: 相关，提供有用的背景或间接信息
        - 0.4-0.6: 部分相关，有一些相关概念或主题
        - 0.1-0.3: 弱相关，仅有模糊的连接
        - 0.0: 不相关

        响应格式（JSON）：
        {
          "score": 0.0-1.0,
          "reason": "评分理由"
        }
        """;

    private const string ImportanceSystemPrompt = """
        你是一个记忆重要性评估助手。评估记忆的重要性和价值。

        评分标准：
        - 0.9-1.0: 核心信息，关键决策，重要偏好
        - 0.7-0.8: 重要信息，常用知识，有价值的反馈
        - 0.5-0.6: 一般信息，可能有用的参考
        - 0.3-0.4: 次要信息，很少使用
        - 0.0-0.2: 不重要或过时的信息

        考虑因素：
        - 信息的时效性
        - 使用频率
        - 对决策的影响
        - 独特性和不可替代性

        响应格式（JSON）：
        {
          "score": 0.0-1.0,
          "reason": "评分理由"
        }
        """;

    public MemoryRetrievalService(
        ILLMClientFactory llmFactory,
        IMemoryRepository memoryRepository,
        ILogger<MemoryRetrievalService> logger,
        IEmbeddingClient? embeddingClient = null,
        IVectorRepository? vectorRepository = null)
    {
        _llmFactory = llmFactory;
        _memoryRepository = memoryRepository;
        _logger = logger;
        _embeddingClient = embeddingClient;
        _vectorRepository = vectorRepository;
    }

    public async Task<List<MemoryEntity>> SearchBySemanticAsync(
        string query,
        int topK = 5,
        MemoryType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<MemoryEntity>();
        }

        // 1. 检查 Qdrant 健康状态（30秒缓存）
        var isHealthy = _vectorRepository != null && _embeddingClient != null &&
                        await _vectorRepository.IsHealthyAsync(cancellationToken);

        if (isHealthy)
        {
            // 🚀 快速路径：向量搜索
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // 生成查询向量
                var queryVector = await _embeddingClient!.GenerateEmbeddingAsync(query, cancellationToken);

                // 构建过滤条件
                Dictionary<string, object>? filters = null;
                if (typeFilter.HasValue)
                {
                    filters = new() { ["type"] = typeFilter.Value.ToString() };
                }

                // 向量相似度搜索
                var vectorResults = await _vectorRepository!.SearchAsync(
                    queryVector,
                    topK,
                    filters,
                    cancellationToken);

                // 加载完整记忆实体（批量加载优化）
                var memoryIds = vectorResults.Select(r => r.MemoryId).ToList();
                var memories = await _memoryRepository.GetByIdsAsync(memoryIds, cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation(
                    "✅ 向量搜索 '{Query}' 返回 {Count} 个结果（耗时 {ElapsedMs}ms）",
                    query, memories.Count, stopwatch.ElapsedMilliseconds);

                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向量搜索失败，降级到 LLM 评分");
                // 继续执行降级逻辑
            }
        }

        // 🐢 慢速路径：降级到 LLM 评分（原有逻辑）
        _logger.LogWarning("向量数据库不可用或搜索失败，使用 LLM 评分（较慢）");

        // 触发降级通知
        OnFallbackToLLMScoring?.Invoke(
            "⚠️ 向量搜索不可用，使用 LLM 评分（较慢，50-100秒）\n" +
            "提示：启动 Qdrant 以获得 1000-10000 倍的性能提升（10-50ms）\n" +
            "  docker run -p 6333:6333 qdrant/qdrant");

        var stopwatch2 = Stopwatch.StartNew();

        try
        {
            // 获取所有记忆（或按类型过滤）
            var allMemories = typeFilter.HasValue
                ? await _memoryRepository.GetByTypeAsync(typeFilter.Value, cancellationToken)
                : await _memoryRepository.GetAllAsync(cancellationToken);

            if (allMemories.Count == 0)
            {
                return new List<MemoryEntity>();
            }

            // 计算每个记忆的相关性评分
            var scoredMemories = new List<(MemoryEntity Memory, double Score)>();

            foreach (var memory in allMemories)
            {
                var score = await CalculateRelevanceScoreAsync(
                    query,
                    memory,
                    cancellationToken);

                if (score > 0.3) // 过滤掉低相关性的记忆
                {
                    scoredMemories.Add((memory, score));
                }
            }

            // 按相关性排序并返回 topK
            var results = scoredMemories
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Memory)
                .ToList();

            stopwatch2.Stop();

            _logger.LogInformation(
                "⚠️ LLM 评分搜索 '{Query}' 返回 {Count} 个结果（耗时 {ElapsedMs}ms）",
                query, results.Count, stopwatch2.ElapsedMilliseconds);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语义搜索时发生错误");
            return new List<MemoryEntity>();
        }
    }

    public async Task<List<MemoryEntity>> GetRelevantMemoriesAsync(
        string context,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        // 使用语义搜索获取相关记忆
        return await SearchBySemanticAsync(
            context,
            topK,
            typeFilter: null,
            cancellationToken);
    }

    public async Task<List<MemoryEntity>> HybridSearchAsync(
        string query,
        int topK = 5,
        double keywordWeight = 0.3,
        double semanticWeight = 0.7,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<MemoryEntity>();
        }

        if (Math.Abs(keywordWeight + semanticWeight - 1.0) > 0.01)
        {
            throw new ArgumentException("关键词权重和语义权重之和必须为 1.0");
        }

        try
        {
            // 关键词搜索
            var keywordResults = await _memoryRepository.SearchAsync(
                query,
                type: null,
                cancellationToken);

            // 语义搜索
            var semanticResults = await SearchBySemanticAsync(
                query,
                topK * 2, // 获取更多候选以便混合
                typeFilter: null,
                cancellationToken);

            // 计算混合评分
            var scoredMemories = new Dictionary<Guid, (MemoryEntity Memory, double Score)>();

            // 关键词评分（基于排名）
            for (int i = 0; i < keywordResults.Count; i++)
            {
                var memory = keywordResults[i];
                var score = keywordWeight * (1.0 - (double)i / keywordResults.Count);

                scoredMemories[memory.Id] = (memory, score);
            }

            // 添加语义评分
            for (int i = 0; i < semanticResults.Count; i++)
            {
                var memory = semanticResults[i];
                var score = semanticWeight * (1.0 - (double)i / semanticResults.Count);

                if (scoredMemories.ContainsKey(memory.Id))
                {
                    var existing = scoredMemories[memory.Id];
                    scoredMemories[memory.Id] = (existing.Memory, existing.Score + score);
                }
                else
                {
                    scoredMemories[memory.Id] = (memory, score);
                }
            }

            // 按混合评分排序并返回 topK
            var results = scoredMemories.Values
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Memory)
                .ToList();

            _logger.LogInformation(
                "混合搜索 '{Query}' 返回 {Count} 个结果 (Keyword: {KW}, Semantic: {SW})",
                query,
                results.Count,
                keywordWeight,
                semanticWeight);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "混合搜索时发生错误");
            return new List<MemoryEntity>();
        }
    }

    public async Task<double> CalculateImportanceScoreAsync(
        MemoryEntity memory,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _llmFactory.GetClient();

            var prompt = new StringBuilder();
            prompt.AppendLine("请评估以下记忆的重要性：");
            prompt.AppendLine();
            prompt.AppendLine($"类型：{memory.Type}");
            prompt.AppendLine($"名称：{memory.Name}");
            prompt.AppendLine($"描述：{memory.Description}");
            prompt.AppendLine($"内容：{memory.Content}");
            prompt.AppendLine($"创建时间：{memory.CreatedAt:yyyy-MM-dd}");
            prompt.AppendLine($"更新时间：{memory.UpdatedAt:yyyy-MM-dd}");

            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt.AppendLine();
                prompt.AppendLine($"评估上下文：{context}");
            }

            var request = new CompletionRequest
            {
                Model = "qwen2.5:0.5b",
                Messages = new[]
                {
                    new ChatMessage { Role = "user", Content = prompt.ToString() }
                },
                SystemPrompt = ImportanceSystemPrompt,
                Temperature = 0.2,
                MaxTokens = 500
            };

            var response = await client.CompleteAsync(request, cancellationToken);
            var score = ParseScoreResponse(response.Content);

            _logger.LogDebug(
                "记忆 '{Name}' 重要性评分: {Score}",
                memory.Name,
                score);

            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算重要性评分时发生错误");
            return 0.5; // 返回中等评分作为默认值
        }
    }

    private async Task<double> CalculateRelevanceScoreAsync(
        string query,
        MemoryEntity memory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _llmFactory.GetClient();

            var prompt = new StringBuilder();
            prompt.AppendLine("请评估以下记忆与查询的相关性：");
            prompt.AppendLine();
            prompt.AppendLine($"查询：{query}");
            prompt.AppendLine();
            prompt.AppendLine("记忆信息：");
            prompt.AppendLine($"类型：{memory.Type}");
            prompt.AppendLine($"名称：{memory.Name}");
            prompt.AppendLine($"描述：{memory.Description}");
            prompt.AppendLine($"内容：{memory.Content}");

            var request = new CompletionRequest
            {
                Model = "qwen2.5:0.5b",
                Messages = new[]
                {
                    new ChatMessage { Role = "user", Content = prompt.ToString() }
                },
                SystemPrompt = RelevanceSystemPrompt,
                Temperature = 0.2,
                MaxTokens = 500
            };

            var response = await client.CompleteAsync(request, cancellationToken);
            return ParseScoreResponse(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算相关性评分时发生错误");
            return 0.0;
        }
    }

    private double ParseScoreResponse(string llmResponse)
    {
        try
        {
            // 尝试提取 JSON
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("LLM 响应中未找到有效的 JSON");
                return 0.5;
            }

            var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<ScoreResult>(jsonContent, options);

            if (result?.Score == null)
            {
                return 0.5;
            }

            // 确保评分在 0.0-1.0 范围内
            return Math.Clamp(result.Score.Value, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析评分响应时发生错误");
            return 0.5;
        }
    }

    private sealed class ScoreResult
    {
        public double? Score { get; set; }
        public string? Reason { get; set; }
    }
}
