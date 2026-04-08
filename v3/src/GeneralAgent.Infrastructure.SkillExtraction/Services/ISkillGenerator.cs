using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能生成器接口 - 从建议生成技能定义文件
/// </summary>
public interface ISkillGenerator
{
    /// <summary>
    /// 从技能建议生成技能文件内容
    /// </summary>
    /// <param name="suggestion">技能建议</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能文件内容（YAML frontmatter + Markdown）</returns>
    Task<string> GenerateSkillFileAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证技能文件内容
    /// </summary>
    /// <param name="skillContent">技能文件内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<ValidationResult> ValidateSkillAsync(
        string skillContent,
        CancellationToken cancellationToken = default);
}
