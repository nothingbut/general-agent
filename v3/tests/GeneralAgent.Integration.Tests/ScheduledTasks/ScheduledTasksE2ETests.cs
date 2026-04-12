using GeneralAgent.Infrastructure.ScheduledTasks;
using GeneralAgent.Infrastructure.ScheduledTasks.Extensions;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Integration.Tests.ScheduledTasks;

/// <summary>
/// 计划任务 E2E 集成测试
/// </summary>
public class ScheduledTasksE2ETests : IAsyncLifetime
{
    private IHost? _host;
    private ITaskManager? _taskManager;
    private ITaskScheduler? _scheduler;
    private string? _testDbPath;

    public async Task InitializeAsync()
    {
        // 创建临时测试数据库
        _testDbPath = Path.Combine(Path.GetTempPath(), $"scheduled_tasks_test_{Guid.NewGuid()}.db");

        // 创建测试 Host
        var builder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ScheduledTasks:DatabasePath", _testDbPath },
                    { "ScheduledTasks:ScanIntervalSeconds", "1" },
                    { "ScheduledTasks:MaxConcurrentTasks", "5" }
                });
            })
            .ConfigureServices((context, services) =>
            {
                // 不启用后台服务，手动控制启动/停止
                services.AddScheduledTasks(context.Configuration, enableBackgroundService: false);
            });

        _host = builder.Build();
        await _host.StartAsync();

        _taskManager = _host.Services.GetRequiredService<ITaskManager>();
        _scheduler = _host.Services.GetRequiredService<ITaskScheduler>();

        // 手动启动调度器
        await _scheduler.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_scheduler != null)
        {
            await _scheduler.StopAsync();
        }

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // 删除临时测试数据库
        if (_testDbPath != null && File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task CreateTask_WithCronSchedule_ShouldSucceed()
    {
        // Arrange
        var taskName = "Test Cron Task";
        var cronSchedule = "0 9 * * *"; // 每天 9:00

        // Act
        var task = await _taskManager!.CreateTaskAsync(
            name: taskName,
            description: "测试 Cron 任务",
            scheduleType: ScheduleType.Cron,
            schedule: cronSchedule,
            taskType: TaskType.CustomCommand,
            taskPayload: "{\"command\":\"echo test\"}"
        );

        // Assert
        Assert.NotNull(task);
        Assert.Equal(taskName, task.Name);
        Assert.Equal(cronSchedule, task.Schedule);
        Assert.Equal(ScheduleType.Cron, task.ScheduleType);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.NotNull(task.NextExecutionAt);
    }

    [Fact]
    public async Task CreateTask_WithNaturalLanguage_ShouldSucceed()
    {
        // Arrange
        var taskName = "Test Natural Language Task";
        var schedule = "每天9:00";

        // Act
        var task = await _taskManager!.CreateTaskAsync(
            name: taskName,
            description: "测试自然语言任务",
            scheduleType: ScheduleType.Natural,
            schedule: schedule,
            taskType: TaskType.MemoryReminder,
            taskPayload: "{\"message\":\"测试提醒\"}"
        );

        // Assert
        Assert.NotNull(task);
        Assert.Equal(taskName, task.Name);
        Assert.Equal(schedule, task.Schedule);
        Assert.Equal(ScheduleType.Natural, task.ScheduleType);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.NotNull(task.NextExecutionAt);
    }

    [Fact]
    public async Task ListTasks_ShouldReturnAllTasks()
    {
        // Arrange
        await _taskManager!.CreateTaskAsync(
            "Task 1", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{}");
        await _taskManager.CreateTaskAsync(
            "Task 2", "", ScheduleType.Cron, "0 10 * * *",
            TaskType.CustomCommand, "{}");

        // Act
        var tasks = await _taskManager.ListTasksAsync();

        // Assert
        Assert.True(tasks.Count >= 2);
    }

    [Fact]
    public async Task ListTasks_WithStatusFilter_ShouldReturnFilteredTasks()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "Pending Task", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{}");

        // Act
        var pendingTasks = await _taskManager.ListTasksAsync(status: TaskStatus.Pending);

        // Assert
        Assert.Contains(pendingTasks, t => t.Id == task.Id);
    }

    [Fact]
    public async Task UpdateTask_ShouldUpdateSchedule()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "Update Test", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{}");
        var newSchedule = "0 10 * * *";

        // Act
        var updatedTask = await _taskManager.UpdateTaskAsync(
            task.Id, schedule: newSchedule);

        // Assert
        Assert.Equal(newSchedule, updatedTask.Schedule);
    }

    [Fact]
    public async Task PauseAndResumeTask_ShouldChangeStatus()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "Pause Test", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{}");

        // Act - Pause
        await _taskManager.PauseTaskAsync(task.Id);
        var pausedTask = await _taskManager.GetTaskAsync(task.Id);

        // Assert - Paused
        Assert.NotNull(pausedTask);
        Assert.Equal(TaskStatus.Paused, pausedTask.Status);

        // Act - Resume
        await _taskManager.ResumeTaskAsync(task.Id);
        var resumedTask = await _taskManager.GetTaskAsync(task.Id);

        // Assert - Resumed
        Assert.NotNull(resumedTask);
        Assert.Equal(TaskStatus.Pending, resumedTask.Status);
    }

    [Fact]
    public async Task TriggerTask_ShouldExecuteImmediately()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "Trigger Test", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{\"command\":\"echo test\"}");

        // Act
        var execution = await _taskManager.TriggerTaskAsync(task.Id);

        // Assert
        Assert.NotNull(execution);
        Assert.Equal(task.Id, execution.TaskId);
        Assert.True(
            execution.Status == ExecutionStatus.Completed ||
            execution.Status == ExecutionStatus.Failed);

        // 验证任务执行次数增加
        var updatedTask = await _taskManager.GetTaskAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.True(updatedTask.ExecutionCount > 0);
    }

    [Fact]
    public async Task GetExecutionHistory_ShouldReturnHistory()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "History Test", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{\"command\":\"echo test\"}");

        // 执行任务
        await _taskManager.TriggerTaskAsync(task.Id);

        // Act
        var history = await _taskManager.GetExecutionHistoryAsync(task.Id);

        // Assert
        Assert.NotEmpty(history);
        Assert.Contains(history, h => h.TaskId == task.Id);
    }

    [Fact]
    public async Task DeleteTask_ShouldRemoveTask()
    {
        // Arrange
        var task = await _taskManager!.CreateTaskAsync(
            "Delete Test", "", ScheduleType.Cron, "0 9 * * *",
            TaskType.CustomCommand, "{}");

        // Act
        await _taskManager.DeleteTaskAsync(task.Id);

        // Assert
        var deletedTask = await _taskManager.GetTaskAsync(task.Id);
        Assert.Null(deletedTask);
    }

    [Fact]
    public async Task CreateTask_WithInvalidSchedule_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _taskManager!.CreateTaskAsync(
                "Invalid Task", "", ScheduleType.Cron, "invalid cron",
                TaskType.CustomCommand, "{}");
        });
    }

    [Fact]
    public async Task CreateTask_WithNegativeRetries_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _taskManager!.CreateTaskAsync(
                "Invalid Task", "", ScheduleType.Cron, "0 9 * * *",
                TaskType.CustomCommand, "{}", maxRetries: -1);
        });
    }

    [Fact]
    public async Task SchedulerLifecycle_StartAndStop_ShouldSucceed()
    {
        // Act - 已经在 InitializeAsync 中启动

        // Assert - 调度器应该在运行
        Assert.True(_scheduler!.IsRunning);

        // Act - 停止
        await _scheduler.StopAsync();

        // Assert - 调度器应该已停止
        Assert.False(_scheduler.IsRunning);

        // 重新启动以便清理
        await _scheduler.StartAsync();
    }
}
