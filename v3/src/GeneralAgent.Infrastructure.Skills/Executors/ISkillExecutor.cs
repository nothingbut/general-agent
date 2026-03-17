using System.Collections.Generic;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器接口
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// 执行技能
    /// </summary>
    /// <param name="skill">要执行的技能</param>
    /// <param name="arguments">参数字典</param>
    /// <returns>渲染后的文本结果</returns>
    Result<string> Execute(Skill skill, Dictionary<string, object> arguments);
}
