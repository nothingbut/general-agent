using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Parsers;

/// <summary>
/// 技能解析器接口
/// </summary>
public interface ISkillParser
{
    /// <summary>
    /// 解析技能文件内容
    /// </summary>
    Result<Skill> Parse(string content);
}
