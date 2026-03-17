using GeneralAgent.Core.Common;

namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能参数定义
/// </summary>
public sealed record SkillParameter
{
    public required string Name { get; init; }
    public required string Type { get; init; }  // string, int, bool, array
    public required bool Required { get; init; }
    public string? Description { get; init; }
    public object? DefaultValue { get; init; }

    /// <summary>
    /// 验证参数值
    /// </summary>
    public Result<object> Validate(object? value)
    {
        // 必填检查
        if (Required && value == null)
        {
            return Result<object>.Failure($"参数 '{Name}' 是必填的");
        }

        // 类型检查（简化版）
        if (value != null && !IsValidType(value))
        {
            return Result<object>.Failure(
                $"参数 '{Name}' 类型不匹配，期望 {Type}");
        }

        return Result<object>.Success(value ?? DefaultValue!);
    }

    private bool IsValidType(object value)
    {
        return Type.ToLower() switch
        {
            "string" => value is string,
            "int" => value is int or long,
            "bool" => value is bool,
            "array" => value is System.Collections.IEnumerable,
            _ => true
        };
    }
}
