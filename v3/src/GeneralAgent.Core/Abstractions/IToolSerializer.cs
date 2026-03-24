using System.Text.Json.Nodes;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 工具序列化器（LLM 提供商格式适配）
/// </summary>
public interface IToolSerializer
{
    /// <summary>
    /// 序列化工具定义为 LLM 格式
    /// </summary>
    JsonObject SerializeToolDefinition(ToolDefinition toolDef);

    /// <summary>
    /// 序列化多个工具定义
    /// </summary>
    JsonArray SerializeTools(IEnumerable<ToolDefinition> tools);
}
