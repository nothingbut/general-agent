using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能提取编排器接口 - 协调整个提取流程
/// </summary>
public interface ISkillExtractionOrchestrator
{
    /// <summary>
    /// 从会话提取并创建技能（完整流程）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="lookbackMessages">回溯消息数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的技能路径列表</returns>
    Task<List<string>> ExtractAndCreateFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从单个建议创建技能
    /// </summary>
    /// <param name="suggestion">技能建议</param>
    /// <param name="sessionId">会话 ID（用于记录）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的技能路径（null 表示用户取消）</returns>
    Task<string?> CreateSkillFromSuggestionAsync(
        SkillSuggestion suggestion,
        string? sessionId = null,
        CancellationToken cancellationToken = default);
}
