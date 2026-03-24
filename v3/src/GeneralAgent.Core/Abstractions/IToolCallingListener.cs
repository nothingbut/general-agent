using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// Tool Calling 用户交互接口
/// 负责在达到最大轮数时与用户交互，决定是否继续执行
/// </summary>
public interface IToolCallingListener
{
    /// <summary>
    /// 当达到最大轮数时调用
    /// </summary>
    /// <param name="currentRounds">当前已执行的轮数</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="toolCalls">待执行的工具调用列表</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>用户决策</returns>
    Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default);
}
