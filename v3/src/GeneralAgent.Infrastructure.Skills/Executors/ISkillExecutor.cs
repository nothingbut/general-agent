using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器接口
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// 执行技能（异步）
    /// </summary>
    /// <param name="skill">要执行的技能</param>
    /// <param name="arguments">参数字典</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="providerName">LLM 提供商名称（可选，默认使用配置的默认提供商）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>LLM 响应结果</returns>
    Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default);

    /// <summary>
    /// 流式执行技能
    /// </summary>
    /// <param name="skill">要执行的技能</param>
    /// <param name="arguments">参数字典</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="providerName">LLM 提供商名称（可选，默认使用配置的默认提供商）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应块</returns>
    IAsyncEnumerable<string> ExecuteStreamAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default);
}
