using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 用户交互接口 - 抽象用户输入输出
/// </summary>
public interface IUserInteraction
{
    /// <summary>
    /// 显示技能建议并获取用户决策
    /// </summary>
    /// <param name="suggestion">技能建议</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户编辑结果</returns>
    Task<EditResult> PromptForActionAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 显示技能列表并让用户选择
    /// </summary>
    /// <param name="suggestions">建议列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户选择的建议索引（-1 表示取消）</returns>
    Task<int> PromptForSelectionAsync(
        IReadOnlyList<SkillSuggestion> suggestions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 允许用户编辑技能内容
    /// </summary>
    /// <param name="initialContent">初始内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的内容（null 表示取消）</returns>
    Task<string?> EditContentAsync(
        string initialContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 显示信息消息
    /// </summary>
    Task ShowMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 显示错误消息
    /// </summary>
    Task ShowErrorAsync(string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 显示成功消息
    /// </summary>
    Task ShowSuccessAsync(string message, CancellationToken cancellationToken = default);
}
