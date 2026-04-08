using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能提取编排器实现 - 协调整个提取流程
/// </summary>
public sealed class SkillExtractionOrchestrator : ISkillExtractionOrchestrator
{
    private readonly ISkillExtractionService _extractionService;
    private readonly ISkillGenerator _skillGenerator;
    private readonly ISkillWriter _skillWriter;
    private readonly IUserInteraction _userInteraction;
    private readonly IExtractionHistoryRepository _historyRepository;
    private readonly ILogger<SkillExtractionOrchestrator> _logger;

    public SkillExtractionOrchestrator(
        ISkillExtractionService extractionService,
        ISkillGenerator skillGenerator,
        ISkillWriter skillWriter,
        IUserInteraction userInteraction,
        IExtractionHistoryRepository historyRepository,
        ILogger<SkillExtractionOrchestrator> logger)
    {
        _extractionService = extractionService;
        _skillGenerator = skillGenerator;
        _skillWriter = skillWriter;
        _userInteraction = userInteraction;
        _historyRepository = historyRepository;
        _logger = logger;
    }

    public async Task<List<string>> ExtractAndCreateFromSessionAsync(
        string sessionId,
        int lookbackMessages = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从会话 {SessionId} 提取技能", sessionId);

        var createdSkills = new List<string>();

        try
        {
            // 1. 从会话提取建议
            await _userInteraction.ShowMessageAsync("正在分析对话历史...", cancellationToken);

            var suggestions = await _extractionService.ExtractFromSessionAsync(
                sessionId, lookbackMessages, cancellationToken);

            if (suggestions.Count == 0)
            {
                await _userInteraction.ShowMessageAsync(
                    "未发现明显的重复任务模式。建议在更多对话后再尝试。", cancellationToken);
                return createdSkills;
            }

            await _userInteraction.ShowMessageAsync(
                $"找到 {suggestions.Count} 个潜在的技能模式", cancellationToken);

            // 2. 让用户选择建议
            while (true)
            {
                var selectedIndex = await _userInteraction.PromptForSelectionAsync(
                    suggestions, cancellationToken);

                if (selectedIndex < 0 || selectedIndex >= suggestions.Count)
                {
                    // 用户取消或选择无效
                    break;
                }

                var selectedSuggestion = suggestions[selectedIndex];

                // 3. 创建选中的技能
                var skillPath = await CreateSkillFromSuggestionAsync(
                    selectedSuggestion, sessionId, cancellationToken);

                if (skillPath != null)
                {
                    createdSkills.Add(skillPath);
                }

                // 移除已处理的建议
                suggestions = suggestions.Where((_, i) => i != selectedIndex).ToList();

                if (suggestions.Count == 0)
                {
                    break;
                }
            }

            if (createdSkills.Count > 0)
            {
                await _userInteraction.ShowSuccessAsync(
                    $"成功创建 {createdSkills.Count} 个技能", cancellationToken);
            }

            return createdSkills;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取技能时发生错误");
            await _userInteraction.ShowErrorAsync($"提取失败: {ex.Message}", cancellationToken);
            throw;
        }
    }

    public async Task<string?> CreateSkillFromSuggestionAsync(
        SkillSuggestion suggestion,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("创建技能: {SkillName}", suggestion.FullName);

        try
        {
            // 1. 生成技能文件内容
            var skillContent = await _skillGenerator.GenerateSkillFileAsync(
                suggestion, cancellationToken);

            // 2. 验证生成的内容
            var validation = await _skillGenerator.ValidateSkillAsync(
                skillContent, cancellationToken);

            if (!validation.IsValid)
            {
                await _userInteraction.ShowErrorAsync(
                    $"生成的技能文件无效: {string.Join(", ", validation.Errors)}",
                    cancellationToken);

                await RecordHistoryAsync(suggestion, EditAction.Reject, sessionId,
                    "生成内容验证失败", cancellationToken);

                return null;
            }

            // 3. 提示用户确认或编辑
            var editResult = await _userInteraction.PromptForActionAsync(
                suggestion, cancellationToken);

            switch (editResult.Action)
            {
                case EditAction.Accept:
                    // 直接保存
                    return await SaveSkillAsync(
                        suggestion, skillContent, sessionId, EditAction.Accept, cancellationToken);

                case EditAction.Edit:
                    // 进入编辑模式
                    var editedContent = await _userInteraction.EditContentAsync(
                        editResult.EditedContent ?? skillContent, cancellationToken);

                    if (editedContent == null)
                    {
                        await _userInteraction.ShowMessageAsync("已取消编辑", cancellationToken);
                        await RecordHistoryAsync(suggestion, EditAction.Reject, sessionId,
                            "用户取消编辑", cancellationToken);
                        return null;
                    }

                    // 验证编辑后的内容
                    var editValidation = await _skillGenerator.ValidateSkillAsync(
                        editedContent, cancellationToken);

                    if (!editValidation.IsValid)
                    {
                        await _userInteraction.ShowErrorAsync(
                            $"编辑后的内容无效: {string.Join(", ", editValidation.Errors)}",
                            cancellationToken);

                        await RecordHistoryAsync(suggestion, EditAction.Reject, sessionId,
                            "编辑后验证失败", cancellationToken);

                        return null;
                    }

                    return await SaveSkillAsync(
                        suggestion, editedContent, sessionId, EditAction.Edit, cancellationToken);

                case EditAction.Reject:
                    // 用户拒绝
                    await _userInteraction.ShowMessageAsync("已拒绝创建此技能", cancellationToken);
                    await RecordHistoryAsync(suggestion, EditAction.Reject, sessionId,
                        editResult.RejectionReason ?? "用户拒绝", cancellationToken);
                    return null;

                default:
                    throw new InvalidOperationException($"未知的操作: {editResult.Action}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建技能 {SkillName} 时发生错误", suggestion.FullName);
            await _userInteraction.ShowErrorAsync($"创建失败: {ex.Message}", cancellationToken);
            throw;
        }
    }

    private async Task<string> SaveSkillAsync(
        SkillSuggestion suggestion,
        string content,
        string? sessionId,
        EditAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            // 保存技能文件
            var filePath = await _skillWriter.SaveSkillAsync(
                suggestion.Namespace,
                suggestion.Name,
                content,
                cancellationToken);

            await _userInteraction.ShowSuccessAsync(
                $"技能已保存: {filePath}", cancellationToken);

            // 记录历史
            await RecordHistoryAsync(suggestion, action, sessionId, null, cancellationToken);

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存技能文件失败: {SkillName}", suggestion.FullName);
            await _userInteraction.ShowErrorAsync($"保存失败: {ex.Message}", cancellationToken);
            throw;
        }
    }

    private async Task RecordHistoryAsync(
        SkillSuggestion suggestion,
        EditAction action,
        string? sessionId,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = new ExtractionRecord
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId,
                SkillName = suggestion.Name,
                SkillNamespace = suggestion.Namespace,
                Action = action,
                Confidence = suggestion.Confidence,
                Occurrences = suggestion.Occurrences,
                RejectionReason = rejectionReason,
                Metadata = new Dictionary<string, object>
                {
                    ["Rationale"] = suggestion.Rationale,
                    ["ExampleMessages"] = suggestion.ExampleMessages
                }
            };

            await _historyRepository.CreateAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            // 记录失败不应阻止主流程
            _logger.LogWarning(ex, "记录提取历史失败");
        }
    }
}
