using System.Threading.Channels;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 后台任务服务（处理标签生成等异步任务）
/// </summary>
public sealed class BackgroundTaskService : BackgroundService
{
    private readonly Channel<TagSuggestionTask> _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundTaskService> _logger;

    public BackgroundTaskService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundTaskService> logger)
    {
        _taskQueue = Channel.CreateUnbounded<TagSuggestionTask>();
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 入队标签建议任务
    /// </summary>
    public async Task EnqueueTagSuggestionAsync(Guid sessionId)
    {
        var task = new TagSuggestionTask(sessionId, DateTime.UtcNow);
        await _taskQueue.Writer.WriteAsync(task);
        _logger.LogDebug("标签建议任务已入队: {SessionId}", sessionId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("后台任务服务已启动");

        await foreach (var task in _taskQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessTagSuggestionAsync(task, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理标签建议任务失败: {SessionId}", task.SessionId);
            }
        }

        _logger.LogInformation("后台任务服务已停止");
    }

    private async Task ProcessTagSuggestionAsync(TagSuggestionTask task, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var tagService = scope.ServiceProvider.GetRequiredService<SmartTagService>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        _logger.LogInformation("开始处理标签建议: {SessionId}", task.SessionId);

        // 1. 获取会话和消息
        var session = await sessionRepo.GetByIdAsync(task.SessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("会话不存在: {SessionId}", task.SessionId);
            return;
        }

        var messages = await messageRepo.GetBySessionAsync(task.SessionId, ct);
        if (messages.Count == 0)
        {
            _logger.LogDebug("会话无消息，跳过标签建议: {SessionId}", task.SessionId);
            return;
        }

        // 2. 生成标签建议
        var suggestions = await tagService.SuggestFromContentAsync(task.SessionId, messages, ct);
        if (suggestions.Count == 0)
        {
            _logger.LogDebug("未生成标签建议: {SessionId}", task.SessionId);
            return;
        }

        // 3. 应用建议
        await tagService.ApplySuggestionsAsync(task.SessionId, suggestions, ct);

        _logger.LogInformation(
            "标签建议处理完成: {SessionId}, 建议数: {Count}",
            task.SessionId,
            suggestions.Count
        );
    }

    public override void Dispose()
    {
        _taskQueue.Writer.Complete();
        base.Dispose();
    }
}

/// <summary>
/// 标签建议任务
/// </summary>
internal sealed record TagSuggestionTask(Guid SessionId, DateTime EnqueuedAt);
