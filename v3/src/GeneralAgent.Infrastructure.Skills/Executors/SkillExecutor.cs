using System;
using System.Collections.Generic;
using System.Linq;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器实现
/// 使用 Scriban 模板引擎渲染技能模板
/// </summary>
public class SkillExecutor : ISkillExecutor
{
    private readonly ILogger<SkillExecutor> _logger;

    public SkillExecutor(ILogger<SkillExecutor> logger)
    {
        _logger = logger;
    }

    public Result<string> Execute(Skill skill, Dictionary<string, object> arguments)
    {
        try
        {
            _logger.LogDebug("执行技能: {SkillName}", skill.FullName);

            // 1. 验证和准备参数
            var validationResult = ValidateAndPrepareArguments(skill, arguments);
            if (!validationResult.IsSuccess)
            {
                return Result<string>.Failure(validationResult.Error!);
            }

            var preparedArgs = validationResult.Value!;

            // 2. 解析模板
            var template = Template.Parse(skill.Template);
            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                _logger.LogError("模板解析失败: {Errors}", errors);
                return Result<string>.Failure($"模板解析失败: {errors}");
            }

            // 3. 创建模板上下文
            var scriptObject = new ScriptObject();
            foreach (var (key, value) in preparedArgs)
            {
                scriptObject.Add(key, value);
            }

            var context = new TemplateContext();

            // 启用内置函数（用于过滤器等）
            context.BuiltinObject.Import(typeof(Scriban.Functions.StringFunctions));
            context.BuiltinObject.Import(typeof(Scriban.Functions.MathFunctions));

            context.PushGlobal(scriptObject);

            // 4. 渲染模板
            string output;
            try
            {
                output = template.Render(context);
            }
            catch (Exception renderEx)
            {
                _logger.LogError(renderEx, "模板渲染失败");
                return Result<string>.Failure($"模板渲染失败: {renderEx.Message}");
            }

            _logger.LogDebug("技能执行成功: {SkillName}, 输出长度: {Length}",
                skill.FullName, output.Length);

            return Result<string>.Success(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行技能失败: {SkillName}", skill.FullName);
            return Result<string>.Failure($"执行技能失败: {ex.Message}");
        }
    }

    private Result<Dictionary<string, object>> ValidateAndPrepareArguments(
        Skill skill,
        Dictionary<string, object> arguments)
    {
        var preparedArgs = new Dictionary<string, object>(arguments);

        // 验证所有参数
        foreach (var parameter in skill.Parameters)
        {
            // 获取参数值（如果存在）
            var hasValue = arguments.TryGetValue(parameter.Name, out var value);

            // 验证参数
            var validationResult = parameter.Validate(hasValue ? value : null);

            if (!validationResult.IsSuccess)
            {
                _logger.LogWarning("参数验证失败: {Parameter} - {Error}",
                    parameter.Name, validationResult.Error);
                return Result<Dictionary<string, object>>.Failure(validationResult.Error!);
            }

            // 使用验证后的值（可能包含默认值）
            if (validationResult.Value != null)
            {
                preparedArgs[parameter.Name] = validationResult.Value;
            }
        }

        return Result<Dictionary<string, object>>.Success(preparedArgs);
    }
}
