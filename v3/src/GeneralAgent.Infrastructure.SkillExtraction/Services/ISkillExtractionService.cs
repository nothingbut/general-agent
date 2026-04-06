using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能提取服务接口 - 从对话历史中识别重复性任务模式
/// </summary>
public interface ISkillExtractionService
{
    /// <summary>
    /// 从会话中提取技能建议
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="lookbackMessages">回溯分析的消息数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能建议列表</returns>
    Task<List<SkillSuggestion>> ExtractFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从消息列表中提取技能建议
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能建议列表</returns>
    Task<List<SkillSuggestion>> ExtractFromMessagesAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}
