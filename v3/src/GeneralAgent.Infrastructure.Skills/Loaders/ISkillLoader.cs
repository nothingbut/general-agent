using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Loaders;

/// <summary>
/// 技能加载器接口
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// 从目录加载所有技能
    /// </summary>
    /// <param name="directoryPath">技能目录路径</param>
    /// <returns>加载的技能列表</returns>
    Task<Result<List<Skill>>> LoadFromDirectoryAsync(string directoryPath);
}
