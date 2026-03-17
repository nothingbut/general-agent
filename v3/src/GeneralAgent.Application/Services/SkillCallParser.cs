using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 技能调用解析器
/// 解析 @skill 和 /skill 语法
/// </summary>
public static partial class SkillCallParser
{
    /// <summary>
    /// 技能调用信息
    /// </summary>
    public sealed record SkillCall
    {
        public required string SkillName { get; init; }
        public required Dictionary<string, object> Arguments { get; init; }
    }

    // 匹配 @skill 或 /skill 语法
    // 示例：@greeting user_name='张三'
    // 示例：/personal:reminder task='买牛奶' time='5pm'
    private static readonly Regex SkillCallRegex = GetSkillCallRegex();

    // 匹配参数：key='value' 或 key="value"
    private static readonly Regex ArgumentRegex = GetArgumentRegex();

    /// <summary>
    /// 尝试解析技能调用
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="skillCall">解析出的技能调用</param>
    /// <returns>是否是技能调用</returns>
    public static bool TryParse(string input, out SkillCall? skillCall)
    {
        skillCall = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmedInput = input.Trim();

        // 检查是否以 @ 或 / 开头
        if (!trimmedInput.StartsWith('@') && !trimmedInput.StartsWith('/'))
        {
            return false;
        }

        var match = SkillCallRegex.Match(trimmedInput);
        if (!match.Success)
        {
            return false;
        }

        var skillName = match.Groups["skill"].Value;
        var argsString = match.Groups["args"].Value;

        var arguments = ParseArguments(argsString);

        skillCall = new SkillCall
        {
            SkillName = skillName,
            Arguments = arguments
        };

        return true;
    }

    /// <summary>
    /// 解析参数字符串
    /// </summary>
    private static Dictionary<string, object> ParseArguments(string argsString)
    {
        var arguments = new Dictionary<string, object>();

        if (string.IsNullOrWhiteSpace(argsString))
        {
            return arguments;
        }

        var matches = ArgumentRegex.Matches(argsString);
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;

            // 值可能在 quoted 或 unquoted 组中
            var value = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["unquoted"].Value;

            // 尝试解析值类型
            arguments[key] = ParseValue(value);
        }

        return arguments;
    }

    /// <summary>
    /// 解析值类型（支持 string, int, bool）
    /// </summary>
    private static object ParseValue(string value)
    {
        // 尝试解析为 bool
        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        // 尝试解析为 int
        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        // 默认为 string
        return value;
    }

    [GeneratedRegex(@"^[@/](?<skill>[\w:.]+)(?:\s+(?<args>.*))?$")]
    private static partial Regex GetSkillCallRegex();

    // 匹配参数：
    // 1. key='value' 或 key="value" (带引号)
    // 2. key=value (不带引号，值可以是数字、布尔值等)
    [GeneratedRegex(@"(?<key>\w+)=(?:['""](?<quoted>[^'""]*)['""]|(?<unquoted>\S+))")]
    private static partial Regex GetArgumentRegex();
}
