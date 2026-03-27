using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 记忆检索服务接口 - 语义搜索和相关记忆推荐
/// </summary>
public interface IMemoryRetrievalService
{
    /// <summary>
    /// 语义相似度搜索
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回的记忆数量</param>
    /// <param name="typeFilter">记忆类型过滤（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相似度排序的记忆列表</returns>
    Task<List<Memory>> SearchBySemanticAsync(
        string query,
        int topK = 5,
        MemoryType? typeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取与当前上下文相关的记忆
    /// </summary>
    /// <param name="context">当前上下文（可以是用户问题或对话摘要）</param>
    /// <param name="topK">返回的记忆数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>相关记忆列表</returns>
    Task<List<Memory>> GetRelevantMemoriesAsync(
        string context,
        int topK = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 混合检索：结合关键词和语义搜索
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回的记忆数量</param>
    /// <param name="keywordWeight">关键词权重（0.0-1.0，默认 0.3）</param>
    /// <param name="semanticWeight">语义权重（0.0-1.0，默认 0.7）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按综合得分排序的记忆列表</returns>
    Task<List<Memory>> HybridSearchAsync(
        string query,
        int topK = 5,
        double keywordWeight = 0.3,
        double semanticWeight = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算记忆的重要性评分
    /// </summary>
    /// <param name="memory">记忆实体</param>
    /// <param name="context">评分上下文（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重要性评分（0.0-1.0）</returns>
    Task<double> CalculateImportanceScoreAsync(
        Memory memory,
        string? context = null,
        CancellationToken cancellationToken = default);
}
