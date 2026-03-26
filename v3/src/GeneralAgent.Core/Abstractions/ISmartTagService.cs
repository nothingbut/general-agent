using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 智能标签建议服务接口
/// 基于 LLM 为会话生成智能标签建议
/// </summary>
public interface ISmartTagService
{
    /// <summary>
    /// 根据会话标题生成标签建议（快速模式）
    /// </summary>
    /// <param name="title">会话标题</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>建议的标签列表（最多 3 个）</returns>
    Task<List<TagSuggestion>> SuggestFromTitleAsync(
        string title,
        CancellationToken ct = default);

    /// <summary>
    /// 根据会话内容生成标签建议（深度模式）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="messages">会话消息列表</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>建议的标签列表（最多 5 个）</returns>
    Task<List<TagSuggestion>> SuggestFromContentAsync(
        Guid sessionId,
        List<Message> messages,
        CancellationToken ct = default);

    /// <summary>
    /// 应用标签建议（去重、限额检查）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="suggestions">标签建议列表</param>
    /// <param name="ct">取消令牌</param>
    Task ApplySuggestionsAsync(
        Guid sessionId,
        List<TagSuggestion> suggestions,
        CancellationToken ct = default);
}

/// <summary>
/// 标签建议结果
/// </summary>
public sealed record TagSuggestion
{
    /// <summary>
    /// 标签名称
    /// </summary>
    public string Tag { get; init; }

    /// <summary>
    /// 标签表情符号（可选）
    /// </summary>
    public string? Emoji { get; init; }

    /// <summary>
    /// 标签颜色（可选）
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 创建标签建议，验证标签名称不为空
    /// </summary>
    /// <param name="tag">标签名称（非空）</param>
    /// <param name="emoji">表情符号（可选）</param>
    /// <param name="color">颜色十六进制代码（可选）</param>
    public TagSuggestion(string tag, string? emoji = null, string? color = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        Tag = tag.Trim().ToLowerInvariant();
        Emoji = emoji;
        Color = color;
    }
}
