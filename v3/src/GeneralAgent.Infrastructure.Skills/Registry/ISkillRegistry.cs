using System.Collections.Generic;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Registry;

/// <summary>
/// 技能注册表接口
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// 注册单个技能
    /// </summary>
    Result<bool> Register(Skill skill);

    /// <summary>
    /// 批量注册技能
    /// </summary>
    /// <returns>成功注册的数量</returns>
    Result<int> RegisterMany(IEnumerable<Skill> skills);

    /// <summary>
    /// 根据完整名称获取技能（包含命名空间）
    /// </summary>
    Skill? GetByFullName(string fullName);

    /// <summary>
    /// 根据名称获取技能
    /// </summary>
    /// <param name="name">技能名称</param>
    /// <param name="namespaceName">可选的命名空间</param>
    Skill? GetByName(string name, string? namespaceName = null);

    /// <summary>
    /// 获取指定命名空间下的所有技能
    /// </summary>
    IReadOnlyList<Skill> GetSkillsByNamespace(string namespaceName);

    /// <summary>
    /// 获取所有已注册的技能
    /// </summary>
    IReadOnlyList<Skill> GetAllSkills();

    /// <summary>
    /// 清空所有技能
    /// </summary>
    void Clear();
}
