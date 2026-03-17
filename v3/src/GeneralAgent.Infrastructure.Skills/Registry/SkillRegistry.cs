using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.Skills.Registry;

/// <summary>
/// 技能注册表实现（线程安全）
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, Skill> _skillsByFullName;
    private readonly ILogger<SkillRegistry> _logger;

    public SkillRegistry(ILogger<SkillRegistry> logger)
    {
        _skillsByFullName = new ConcurrentDictionary<string, Skill>();
        _logger = logger;
    }

    public Result<bool> Register(Skill skill)
    {
        var fullName = skill.FullName;

        var added = _skillsByFullName.TryAdd(fullName, skill);

        if (!added)
        {
            _logger.LogWarning("技能已存在，无法注册: {FullName}", fullName);
            return Result<bool>.Failure($"技能 '{fullName}' 已存在");
        }

        _logger.LogDebug("成功注册技能: {FullName}", fullName);
        return Result<bool>.Success(true);
    }

    public Result<int> RegisterMany(IEnumerable<Skill> skills)
    {
        var successCount = 0;

        foreach (var skill in skills)
        {
            var result = Register(skill);
            if (result.IsSuccess)
            {
                successCount++;
            }
        }

        _logger.LogInformation("批量注册完成，成功注册 {Count} 个技能", successCount);
        return Result<int>.Success(successCount);
    }

    public Skill? GetByFullName(string fullName)
    {
        _skillsByFullName.TryGetValue(fullName, out var skill);
        return skill;
    }

    public Skill? GetByName(string name, string? namespaceName = null)
    {
        // 如果指定了命名空间，直接构造完整名称查找
        if (!string.IsNullOrEmpty(namespaceName))
        {
            var fullName = $"{namespaceName}:{name}";
            return GetByFullName(fullName);
        }

        // 未指定命名空间，先查找没有命名空间的技能
        var skillWithoutNamespace = GetByFullName(name);
        if (skillWithoutNamespace != null)
        {
            return skillWithoutNamespace;
        }

        // 如果没有找到，返回任意一个匹配名称的技能
        return _skillsByFullName.Values
            .FirstOrDefault(s => s.Name == name);
    }

    public IReadOnlyList<Skill> GetSkillsByNamespace(string namespaceName)
    {
        return _skillsByFullName.Values
            .Where(s => s.Namespace == namespaceName)
            .ToList();
    }

    public IReadOnlyList<Skill> GetAllSkills()
    {
        return _skillsByFullName.Values.ToList();
    }

    public void Clear()
    {
        _skillsByFullName.Clear();
        _logger.LogInformation("已清空所有技能");
    }
}
