namespace GeneralAgent.Infrastructure.SkillExtraction.Models;

/// <summary>
/// 技能验证结果
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误消息列表
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// 警告消息列表
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// 创建验证成功的结果
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// 创建验证失败的结果
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = new List<string>(errors)
    };

    /// <summary>
    /// 创建带警告的验证结果
    /// </summary>
    public static ValidationResult WithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = new List<string>(warnings)
    };
}
