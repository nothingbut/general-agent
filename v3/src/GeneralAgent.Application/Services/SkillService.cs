using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 技能服务
/// 负责技能的加载、管理和执行
/// 自动将加载的技能注册为工具，支持 LLM Function Calling
/// </summary>
public sealed class SkillService
{
    private readonly ISkillLoader _loader;
    private readonly ISkillRegistry _registry;
    private readonly ISkillExecutor _executor;
    private readonly ToolRegistry _toolRegistry;
    private readonly SkillToToolConverter _converter;
    private readonly ILogger<SkillService> _logger;
    private bool _initialized;

    public SkillService(
        ISkillLoader loader,
        ISkillRegistry registry,
        ISkillExecutor executor,
        ToolRegistry toolRegistry,
        SkillToToolConverter converter,
        ILogger<SkillService> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 从指定目录加载技能
    /// 加载的技能会自动注册到 SkillRegistry 和 ToolRegistry
    /// </summary>
    public async Task<Result<int>> LoadSkillsAsync(string skillsDirectory, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("开始加载技能，目录: {Directory}", skillsDirectory);

            // 检查目录是否存在
            if (!Directory.Exists(skillsDirectory))
            {
                _logger.LogWarning("技能目录不存在: {Directory}", skillsDirectory);
                return Result<int>.Failure($"技能目录不存在: {skillsDirectory}");
            }

            // 加载技能文件
            var loadResult = await _loader.LoadFromDirectoryAsync(skillsDirectory);
            if (!loadResult.IsSuccess)
            {
                return Result<int>.Failure(loadResult.Error!);
            }

            var skills = loadResult.Value!;

            // 注册技能到 SkillRegistry
            var registerResult = _registry.RegisterMany(skills);
            if (!registerResult.IsSuccess)
            {
                return Result<int>.Failure(registerResult.Error!);
            }

            // 将每个技能注册为工具到 ToolRegistry
            foreach (var skill in skills)
            {
                var skillTool = new SkillTool(skill, _executor, _converter);
                _toolRegistry.Register(skillTool);

                _logger.LogDebug("注册 skill 为 tool: {SkillName}", skill.FullName);
            }

            _initialized = true;
            _logger.LogInformation("成功加载 {Count} 个技能，并注册为工具", registerResult.Value);

            return Result<int>.Success(registerResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载技能失败");
            return Result<int>.Failure($"加载技能失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行技能
    /// </summary>
    /// <param name="skillName">技能名称（可以包含命名空间，如 "personal:greeting" 或 "greeting"）</param>
    /// <param name="arguments">参数字典</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="providerName">LLM 提供商名称（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<Result<string>> ExecuteSkillAsync(
        string skillName,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            return Result<string>.Failure("技能系统未初始化，请先调用 LoadSkillsAsync");
        }

        try
        {
            _logger.LogDebug("执行技能: {SkillName}", skillName);

            // 查找技能
            var skill = FindSkill(skillName);
            if (skill == null)
            {
                _logger.LogWarning("技能不存在: {SkillName}", skillName);
                return Result<string>.Failure($"技能不存在: {skillName}");
            }

            // 执行技能
            var result = await _executor.ExecuteAsync(skill, arguments, sessionId, providerName, ct);

            if (result.IsSuccess)
            {
                _logger.LogDebug("技能执行成功: {SkillName}", skillName);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行技能失败: {SkillName}", skillName);
            return Result<string>.Failure($"执行技能失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有已加载的技能
    /// </summary>
    public IReadOnlyList<Skill> GetAllSkills()
    {
        return _registry.GetAllSkills();
    }

    /// <summary>
    /// 根据命名空间获取技能
    /// </summary>
    public IReadOnlyList<Skill> GetSkillsByNamespace(string namespaceName)
    {
        return _registry.GetSkillsByNamespace(namespaceName);
    }

    /// <summary>
    /// 查找技能（支持完整名称和简短名称）
    /// </summary>
    private Skill? FindSkill(string skillName)
    {
        // 尝试作为完整名称查找（包含命名空间，如 "personal:greeting"）
        var skill = _registry.GetByFullName(skillName);
        if (skill != null)
        {
            return skill;
        }

        // 尝试解析命名空间
        var parts = skillName.Split(':', 2);
        if (parts.Length == 2)
        {
            // 格式: "namespace:name"
            return _registry.GetByName(parts[1], parts[0]);
        }

        // 简短名称查找
        return _registry.GetByName(skillName);
    }
}
