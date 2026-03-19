using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Converters;

/// <summary>
/// 技能到工具定义的转换器
/// 将 Skill 对象转换为 LLM Function Calling 的 ToolDefinition 格式
/// </summary>
public sealed class SkillToToolConverter
{
    /// <summary>
    /// 将技能转换为工具定义
    /// </summary>
    /// <param name="skill">技能对象</param>
    /// <returns>工具定义</returns>
    public ToolDefinition Convert(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var inputSchema = BuildJsonSchema(skill.Parameters);

        return new ToolDefinition
        {
            Name = skill.FullName,
            Description = skill.Description,
            InputSchema = inputSchema
        };
    }

    /// <summary>
    /// 构建参数的 JSON Schema
    /// </summary>
    /// <param name="parameters">技能参数列表</param>
    /// <returns>JSON Schema 对象</returns>
    private JsonObject BuildJsonSchema(IReadOnlyList<SkillParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var param in parameters)
        {
            // 构建属性定义
            var propDef = new JsonObject
            {
                ["type"] = MapType(param.Type),
                ["description"] = param.Description ?? ""
            };

            // 添加默认值（如果存在）
            if (param.DefaultValue != null)
            {
                propDef["default"] = ConvertDefaultValue(param.DefaultValue, param.Type);
            }

            properties[param.Name] = propDef;

            // 记录必填参数
            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        // 构建 Schema 对象
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        // 只有在有必填参数时才添加 required 字段
        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    /// <summary>
    /// 将技能参数类型映射到 JSON Schema 类型
    /// </summary>
    /// <param name="skillType">技能参数类型</param>
    /// <returns>JSON Schema 类型</returns>
    private string MapType(string skillType)
    {
        return skillType.ToLower() switch
        {
            "string" => "string",
            "number" or "int" or "integer" => "number",
            "bool" or "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "string" // 默认回退到 string
        };
    }

    /// <summary>
    /// 转换默认值为合适的 JsonNode 类型
    /// </summary>
    /// <param name="defaultValue">默认值</param>
    /// <param name="type">参数类型</param>
    /// <returns>JsonNode 对象</returns>
    private JsonNode? ConvertDefaultValue(object defaultValue, string type)
    {
        var jsonType = MapType(type);

        return jsonType switch
        {
            "number" => ConvertToNumber(defaultValue),
            "boolean" => ConvertToBoolean(defaultValue),
            _ => JsonValue.Create(defaultValue.ToString())
        };
    }

    /// <summary>
    /// 转换为数字类型
    /// </summary>
    private JsonNode? ConvertToNumber(object value)
    {
        return value switch
        {
            int intVal => JsonValue.Create(intVal),
            long longVal => JsonValue.Create(longVal),
            double doubleVal => JsonValue.Create(doubleVal),
            float floatVal => JsonValue.Create(floatVal),
            decimal decimalVal => JsonValue.Create(decimalVal),
            string strVal when int.TryParse(strVal, out var parsed) => JsonValue.Create(parsed),
            string strVal when double.TryParse(strVal, out var parsed) => JsonValue.Create(parsed),
            _ => JsonValue.Create(0)
        };
    }

    /// <summary>
    /// 转换为布尔类型
    /// </summary>
    private JsonNode? ConvertToBoolean(object value)
    {
        return value switch
        {
            bool boolVal => JsonValue.Create(boolVal),
            string strVal when bool.TryParse(strVal, out var parsed) => JsonValue.Create(parsed),
            _ => JsonValue.Create(false)
        };
    }
}
