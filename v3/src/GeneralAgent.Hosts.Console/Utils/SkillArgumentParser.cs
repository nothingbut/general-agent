using System.Text.Json;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Hosts.Console.Utils;

/// <summary>
/// 技能参数解析器
/// </summary>
public static class SkillArgumentParser
{
    /// <summary>
    /// 从命令行参数解析技能参数
    /// </summary>
    /// <param name="skill">技能定义</param>
    /// <param name="args">命令行参数（key=value 格式）</param>
    /// <returns>解析后的参数字典</returns>
    public static Result<Dictionary<string, object>> Parse(
        Skill skill,
        IEnumerable<string> args)
    {
        var result = new Dictionary<string, object>();
        var errors = new List<string>();

        // 解析命令行参数
        var providedArgs = ParseCommandLineArgs(args);

        // 验证每个技能参数
        foreach (var param in skill.Parameters)
        {
            if (providedArgs.TryGetValue(param.Name, out var rawValue))
            {
                // 提供了参数，验证并转换类型
                var parseResult = ParseAndValidateValue(param, rawValue);
                if (parseResult.IsSuccess)
                {
                    result[param.Name] = parseResult.Value!;
                }
                else
                {
                    errors.Add(parseResult.Error!);
                }
            }
            else
            {
                // 未提供参数
                if (param.Required)
                {
                    errors.Add($"缺少必填参数: {param.Name}");
                }
                else if (param.DefaultValue != null)
                {
                    result[param.Name] = param.DefaultValue!;
                }
            }
        }

        // 检查是否有多余的参数
        var validParamNames = new HashSet<string>(skill.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var extraArgs = providedArgs.Keys.Where(k => !validParamNames.Contains(k)).ToList();
        if (extraArgs.Count > 0)
        {
            errors.Add($"未知参数: {string.Join(", ", extraArgs)}");
        }

        if (errors.Count > 0)
        {
            return Result<Dictionary<string, object>>.Failure(
                string.Join("; ", errors));
        }

        return Result<Dictionary<string, object>>.Success(result);
    }

    /// <summary>
    /// 解析命令行参数（支持 key=value 格式）
    /// </summary>
    private static Dictionary<string, string> ParseCommandLineArgs(IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            var parts = arg.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // 移除引号
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                result[key] = value;
            }
            else
            {
                // 如果不是 key=value 格式，跳过（可能是位置参数）
                continue;
            }
        }

        return result;
    }

    /// <summary>
    /// 解析并验证参数值
    /// </summary>
    private static Result<object> ParseAndValidateValue(SkillParameter param, string rawValue)
    {
        try
        {
            var value = param.Type.ToLower() switch
            {
                "string" => rawValue,
                "int" => int.Parse(rawValue),
                "bool" => bool.Parse(rawValue),
                "array" => ParseArray(rawValue),
                _ => rawValue
            };

            // 使用技能参数自带的验证
            return param.Validate(value);
        }
        catch (FormatException)
        {
            return Result<object>.Failure(
                $"参数 '{param.Name}' 值格式错误，期望类型: {param.Type}");
        }
        catch (Exception ex)
        {
            return Result<object>.Failure(
                $"参数 '{param.Name}' 解析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析数组值（JSON 格式）
    /// </summary>
    private static object ParseArray(string rawValue)
    {
        try
        {
            // 尝试解析 JSON 数组
            return JsonSerializer.Deserialize<List<string>>(rawValue)
                ?? new List<string>();
        }
        catch
        {
            // 如果不是 JSON 格式，尝试按逗号分割
            return rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
        }
    }

    /// <summary>
    /// 构建参数使用提示
    /// </summary>
    public static string BuildUsageHint(Skill skill)
    {
        var hints = new List<string>();

        foreach (var param in skill.Parameters)
        {
            var hint = param.Required
                ? $"{param.Name}=<{param.Type}>"
                : $"[{param.Name}=<{param.Type}>]";
            hints.Add(hint);
        }

        return string.Join(" ", hints);
    }
}
