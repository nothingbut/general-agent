namespace GeneralAgent.Core.Models;

/// <summary>
/// Token 使用统计
/// </summary>
public sealed record TokenUsage
{
    /// <summary>
    /// 提示词 token 数
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// 生成内容 token 数
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// 总 token 数（自动计算：PromptTokens + CompletionTokens）
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
