using GeneralAgent.Infrastructure.SkillExtraction.Models;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 测试用的用户交互实现 - 预先配置响应
/// </summary>
public sealed class TestUserInteraction : IUserInteraction
{
    private readonly Queue<EditResult> _actionResponses = new();
    private readonly Queue<int> _selectionResponses = new();
    private readonly Queue<string?> _editResponses = new();
    private readonly List<string> _messages = new();
    private readonly List<string> _errors = new();
    private readonly List<string> _successes = new();

    /// <summary>
    /// 配置下一次 PromptForAction 的响应
    /// </summary>
    public void ConfigureNextAction(EditResult result)
    {
        _actionResponses.Enqueue(result);
    }

    /// <summary>
    /// 配置下一次 PromptForSelection 的响应
    /// </summary>
    public void ConfigureNextSelection(int index)
    {
        _selectionResponses.Enqueue(index);
    }

    /// <summary>
    /// 配置下一次 EditContent 的响应
    /// </summary>
    public void ConfigureNextEdit(string? content)
    {
        _editResponses.Enqueue(content);
    }

    /// <summary>
    /// 获取所有显示的消息
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// 获取所有显示的错误
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// 获取所有显示的成功消息
    /// </summary>
    public IReadOnlyList<string> Successes => _successes;

    public Task<EditResult> PromptForActionAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        if (_actionResponses.Count == 0)
        {
            throw new InvalidOperationException(
                "没有配置的 Action 响应。请先调用 ConfigureNextAction()");
        }

        return Task.FromResult(_actionResponses.Dequeue());
    }

    public Task<int> PromptForSelectionAsync(
        IReadOnlyList<SkillSuggestion> suggestions,
        CancellationToken cancellationToken = default)
    {
        if (_selectionResponses.Count == 0)
        {
            throw new InvalidOperationException(
                "没有配置的 Selection 响应。请先调用 ConfigureNextSelection()");
        }

        return Task.FromResult(_selectionResponses.Dequeue());
    }

    public Task<string?> EditContentAsync(
        string initialContent,
        CancellationToken cancellationToken = default)
    {
        if (_editResponses.Count == 0)
        {
            throw new InvalidOperationException(
                "没有配置的 Edit 响应。请先调用 ConfigureNextEdit()");
        }

        return Task.FromResult(_editResponses.Dequeue());
    }

    public Task ShowMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string error, CancellationToken cancellationToken = default)
    {
        _errors.Add(error);
        return Task.CompletedTask;
    }

    public Task ShowSuccessAsync(string message, CancellationToken cancellationToken = default)
    {
        _successes.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清空所有记录（用于测试）
    /// </summary>
    public void Clear()
    {
        _actionResponses.Clear();
        _selectionResponses.Clear();
        _editResponses.Clear();
        _messages.Clear();
        _errors.Clear();
        _successes.Clear();
    }
}
