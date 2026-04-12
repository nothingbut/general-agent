using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Services;

/// <summary>
/// 计划任务后台服务 - 负责启动和停止任务调度器
/// </summary>
public class ScheduledTasksBackgroundService : BackgroundService
{
    private readonly ITaskScheduler _scheduler;
    private readonly ILogger<ScheduledTasksBackgroundService> _logger;

    public ScheduledTasksBackgroundService(
        ITaskScheduler scheduler,
        ILogger<ScheduledTasksBackgroundService> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// 启动后台服务
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("计划任务后台服务正在启动...");

        try
        {
            // 启动任务调度器
            await _scheduler.StartAsync(stoppingToken);

            _logger.LogInformation("计划任务后台服务已启动");

            // 保持服务运行，直到收到停止信号
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 服务正常停止
            _logger.LogInformation("计划任务后台服务正在停止...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计划任务后台服务发生错误");
            throw;
        }
    }

    /// <summary>
    /// 停止后台服务
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止计划任务后台服务...");

        try
        {
            // 停止任务调度器
            await _scheduler.StopAsync(cancellationToken);

            _logger.LogInformation("计划任务后台服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止计划任务后台服务时发生错误");
            throw;
        }

        await base.StopAsync(cancellationToken);
    }
}
