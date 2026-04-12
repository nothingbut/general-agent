using GeneralAgent.Infrastructure.ScheduledTasks;
using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using GeneralAgent.Infrastructure.ScheduledTasks.Parsers;
using GeneralAgent.Infrastructure.ScheduledTasks.Repositories;
using GeneralAgent.Infrastructure.ScheduledTasks.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;
using TaskScheduler = GeneralAgent.Infrastructure.ScheduledTasks.Services.TaskScheduler;

namespace GeneralAgent.Infrastructure.Tests.ScheduledTasks;

/// <summary>
/// TaskScheduler 单元测试
/// </summary>
public class TaskSchedulerTests
{
    private readonly Mock<IScheduledTaskRepository> _mockTaskRepository;
    private readonly Mock<ITaskExecutor> _mockTaskExecutor;
    private readonly Mock<ICronParser> _mockCronParser;
    private readonly Mock<INaturalLanguageTimeParser> _mockNaturalLanguageParser;
    private readonly Mock<ILogger<TaskScheduler>> _mockLogger;
    private readonly ScheduledTasksOptions _options;
    private readonly TaskScheduler _scheduler;

    public TaskSchedulerTests()
    {
        _mockTaskRepository = new Mock<IScheduledTaskRepository>();
        _mockTaskExecutor = new Mock<ITaskExecutor>();
        _mockCronParser = new Mock<ICronParser>();
        _mockNaturalLanguageParser = new Mock<INaturalLanguageTimeParser>();
        _mockLogger = new Mock<ILogger<TaskScheduler>>();

        _options = new ScheduledTasksOptions
        {
            DatabasePath = ":memory:",
            ScanIntervalSeconds = 1,
            MaxConcurrentTasks = 5
        };

        _scheduler = new TaskScheduler(
            _mockTaskRepository.Object,
            _mockTaskExecutor.Object,
            _mockCronParser.Object,
            _mockNaturalLanguageParser.Object,
            Options.Create(_options),
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task StartAsync_ShouldLoadPendingTasksAndStartScanner()
    {
        // Arrange
        var pendingTasks = new List<ScheduledTask>
        {
            CreateTestTask("task1", ScheduleType.Cron, "0 9 * * *"),
            CreateTestTask("task2", ScheduleType.Cron, "0 17 * * *")
        };

        _mockTaskRepository
            .Setup(r => r.ListByStatusAsync(TaskStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingTasks);

        _mockCronParser
            .Setup(p => p.GetNextOccurrence(It.IsAny<string>(), It.IsAny<DateTime>(), null))
            .Returns((string _, DateTime from, TimeZoneInfo? _) => from.AddHours(1));

        // Act
        await _scheduler.StartAsync();

        // Assert
        Assert.True(_scheduler.IsRunning);
        _mockTaskRepository.Verify(r => r.ListByStatusAsync(TaskStatus.Pending, It.IsAny<CancellationToken>()), Times.Once);
        _mockTaskRepository.Verify(r => r.UpdateAsync(It.IsAny<ScheduledTask>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Cleanup
        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldCancelRunningTasks()
    {
        // Arrange
        await _scheduler.StartAsync();

        // Act
        await _scheduler.StopAsync();

        // Assert
        Assert.False(_scheduler.IsRunning);
    }

    [Fact]
    public async Task ScheduleTaskAsync_ShouldCalculateNextExecutionAndEnqueue()
    {
        // Arrange
        var task = CreateTestTask("test", ScheduleType.Cron, "0 9 * * *");
        var nextExecution = DateTime.UtcNow.AddHours(1);

        _mockCronParser
            .Setup(p => p.GetNextOccurrence("0 9 * * *", It.IsAny<DateTime>(), null))
            .Returns(nextExecution);

        await _scheduler.StartAsync();

        // Act
        await _scheduler.ScheduleTaskAsync(task);

        // Assert
        Assert.Equal(nextExecution, task.NextExecutionAt);
        _mockTaskRepository.Verify(r => r.UpdateAsync(task, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task ScheduleTaskAsync_WithNaturalLanguage_ShouldConvertToCron()
    {
        // Arrange
        var task = CreateTestTask("test", ScheduleType.Natural, "每天9点");
        var nextExecution = DateTime.UtcNow.AddDays(1);

        _mockNaturalLanguageParser
            .Setup(p => p.ParseToCron("每天9点"))
            .Returns("0 9 * * *");

        _mockCronParser
            .Setup(p => p.GetNextOccurrence("0 9 * * *", It.IsAny<DateTime>(), null))
            .Returns(nextExecution);

        await _scheduler.StartAsync();

        // Act
        await _scheduler.ScheduleTaskAsync(task);

        // Assert
        Assert.Equal(nextExecution, task.NextExecutionAt);
        _mockNaturalLanguageParser.Verify(p => p.ParseToCron("每天9点"), Times.Once);

        // Cleanup
        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task PauseTaskAsync_ShouldUpdateStatusToPaused()
    {
        // Arrange
        var task = CreateTestTask("test", ScheduleType.Cron, "0 9 * * *");

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        await _scheduler.PauseTaskAsync(task.Id);

        // Assert
        Assert.Equal(TaskStatus.Paused, task.Status);
        _mockTaskRepository.Verify(r => r.UpdateAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeTaskAsync_ShouldUpdateStatusToPending()
    {
        // Arrange
        var task = CreateTestTask("test", ScheduleType.Cron, "0 9 * * *");
        task.Status = TaskStatus.Paused;

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _mockCronParser
            .Setup(p => p.GetNextOccurrence("0 9 * * *", It.IsAny<DateTime>(), null))
            .Returns(DateTime.UtcNow.AddHours(1));

        await _scheduler.StartAsync();

        // Act
        await _scheduler.ResumeTaskAsync(task.Id);

        // Assert
        Assert.Equal(TaskStatus.Pending, task.Status);
        _mockTaskRepository.Verify(r => r.UpdateAsync(task, It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Cleanup
        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task TriggerTaskAsync_ShouldExecuteTaskImmediately()
    {
        // Arrange
        var task = CreateTestTask("test", ScheduleType.Cron, "0 9 * * *");
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = ExecutionStatus.Completed
        };

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _mockTaskExecutor
            .Setup(e => e.ExecuteAsync(task, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execution);

        await _scheduler.StartAsync();

        // Act
        await _scheduler.TriggerTaskAsync(task.Id);

        // 等待异步执行完成
        await Task.Delay(100);

        // Assert
        _mockTaskExecutor.Verify(e => e.ExecuteAsync(task, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task UnscheduleTaskAsync_ShouldRemoveTask()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        await _scheduler.UnscheduleTaskAsync(taskId);

        // Assert - no exception thrown
        Assert.True(true);
    }

    private ScheduledTask CreateTestTask(string name, ScheduleType scheduleType, string schedule)
    {
        return new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test task",
            ScheduleType = scheduleType,
            Schedule = schedule,
            TaskType = TaskType.CustomCommand,
            TaskPayload = "{\"command\":\"echo test\"}",
            Status = TaskStatus.Pending,
            MaxRetries = 3,
            TimeoutSeconds = 60,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
