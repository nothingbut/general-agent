using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 记忆提取服务接口 - 从对话中自动提取记忆
/// </summary>
public interface IMemoryExtractionService
{
    /// <summary>
    /// 从消息内容中提取记忆建议
    /// </summary>
    /// <param name="messageContent">用户消息内容</param>
    /// <param name="conversationContext">对话上下文（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提取的记忆建议列表</returns>
    Task<List<MemorySuggestion>> ExtractFromMessageAsync(
        string messageContent,
        string? conversationContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从记忆建议创建记忆实体
    /// </summary>
    /// <param name="suggestion">记忆建议</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的记忆实体，如果建议被拒绝则返回 null</returns>
    Task<Memory?> CreateMemoryFromSuggestionAsync(
        MemorySuggestion suggestion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量从对话历史中提取记忆
    /// </summary>
    /// <param name="conversationHistory">对话历史消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提取的记忆建议列表</returns>
    Task<List<MemorySuggestion>> ExtractFromConversationAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}
