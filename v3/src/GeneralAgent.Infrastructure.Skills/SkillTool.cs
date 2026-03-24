using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills;

/// <summary>
/// 技能工具适配器
/// 将 Skill 包装为 ITool 接口实现，委托执行逻辑给 ISkillExecutor
/// </summary>
public sealed class SkillTool : ITool
{
    private readonly Skill _skill;
    private readonly ISkillExecutor _executor;
    private readonly SkillToToolConverter _converter;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="skill">技能对象</param>
    /// <param name="executor">技能执行器</param>
    /// <param name="converter">技能到工具定义的转换器</param>
    public SkillTool(Skill skill, ISkillExecutor executor, SkillToToolConverter converter)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(converter);

        _skill = skill;
        _executor = executor;
        _converter = converter;
    }

    /// <summary>
    /// 工具名称（使用技能的完整名称）
    /// </summary>
    public string Name => _skill.FullName;

    /// <summary>
    /// 工具描述（使用技能的描述）
    /// </summary>
    public string Description => _skill.Description;

    /// <summary>
    /// 获取工具定义（委托给转换器）
    /// </summary>
    /// <returns>工具定义</returns>
    public ToolDefinition GetDefinition()
    {
        return _converter.Convert(_skill);
    }

    /// <summary>
    /// 执行工具（非流式）
    /// </summary>
    /// <param name="arguments">工具参数字典</param>
    /// <param name="context">工具执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<Result<string>> ExecuteAsync(
        IReadOnlyDictionary<string, object> arguments,
        ToolExecutionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        // 转换 IReadOnlyDictionary 为 Dictionary（防御性拷贝）
        var args = new Dictionary<string, object>(arguments);

        return await _executor.ExecuteAsync(
            _skill,
            args,
            context.SessionId,
            context.ProviderName,
            ct);
    }

    /// <summary>
    /// 执行工具（流式）
    /// </summary>
    /// <param name="arguments">工具参数字典</param>
    /// <param name="context">工具执行上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>异步枚举，逐个返回结果块</returns>
    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        IReadOnlyDictionary<string, object> arguments,
        ToolExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        // 转换 IReadOnlyDictionary 为 Dictionary（防御性拷贝）
        var args = new Dictionary<string, object>(arguments);

        await foreach (var chunk in _executor.ExecuteStreamAsync(
            _skill,
            args,
            context.SessionId,
            context.ProviderName,
            ct))
        {
            yield return chunk;
        }
    }
}
