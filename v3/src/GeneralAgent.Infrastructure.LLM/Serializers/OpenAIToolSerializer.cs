using System.Text.Json.Nodes;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Infrastructure.LLM.Serializers;

/// <summary>
/// OpenAI Function Calling 格式序列化器
/// </summary>
public sealed class OpenAIToolSerializer : IToolSerializer
{
    /// <summary>
    /// 序列化工具定义为 OpenAI Function Calling 格式
    /// </summary>
    /// <param name="toolDef">工具定义</param>
    /// <returns>OpenAI 格式的 JSON 对象</returns>
    public JsonObject SerializeToolDefinition(ToolDefinition toolDef)
    {
        ArgumentNullException.ThrowIfNull(toolDef);

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = toolDef.Name,
                ["description"] = toolDef.Description,
                ["parameters"] = toolDef.InputSchema
            }
        };
    }

    /// <summary>
    /// 序列化多个工具定义为 OpenAI Function Calling 格式数组
    /// </summary>
    /// <param name="tools">工具定义集合</param>
    /// <returns>OpenAI 格式的 JSON 数组</returns>
    public JsonArray SerializeTools(IEnumerable<ToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var array = new JsonArray();
        foreach (var tool in tools)
        {
            array.Add(SerializeToolDefinition(tool));
        }
        return array;
    }
}
