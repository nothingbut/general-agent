using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Repositories;

/// <summary>
/// 任务执行记录仓储实现（SQLite）
/// </summary>
public class TaskExecutionRepository : ITaskExecutionRepository
{
    private readonly ScheduledTasksOptions _options;
    private readonly ILogger<TaskExecutionRepository> _logger;
    private readonly string _connectionString;

    public TaskExecutionRepository(
        IOptions<ScheduledTasksOptions> options,
        ILogger<TaskExecutionRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionString = $"Data Source={_options.DatabasePath}";
    }

    /// <summary>
    /// 创建执行记录
    /// </summary>
    public async Task<TaskExecution> CreateAsync(TaskExecution execution, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO task_executions (
                id, task_id, started_at, completed_at, status,
                output, error, retry_count, duration_ms, metadata
            ) VALUES (
                @Id, @TaskId, @StartedAt, @CompletedAt, @Status,
                @Output, @Error, @RetryCount, @DurationMs, @Metadata
            )";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", execution.Id.ToString());
        command.Parameters.AddWithValue("@TaskId", execution.TaskId.ToString());
        command.Parameters.AddWithValue("@StartedAt", execution.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@CompletedAt", execution.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)execution.Status);
        command.Parameters.AddWithValue("@Output", execution.Output ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Error", execution.Error ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RetryCount", execution.RetryCount);
        command.Parameters.AddWithValue("@DurationMs", execution.DurationMs ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", execution.Metadata ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("任务执行记录已创建: TaskId={TaskId}, ExecutionId={ExecutionId}", execution.TaskId, execution.Id);
        return execution;
    }

    /// <summary>
    /// 更新执行记录
    /// </summary>
    public async Task UpdateAsync(TaskExecution execution, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE task_executions SET
                completed_at = @CompletedAt,
                status = @Status,
                output = @Output,
                error = @Error,
                retry_count = @RetryCount,
                duration_ms = @DurationMs,
                metadata = @Metadata
            WHERE id = @Id";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", execution.Id.ToString());
        command.Parameters.AddWithValue("@CompletedAt", execution.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)execution.Status);
        command.Parameters.AddWithValue("@Output", execution.Output ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Error", execution.Error ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RetryCount", execution.RetryCount);
        command.Parameters.AddWithValue("@DurationMs", execution.DurationMs ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", execution.Metadata ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("任务执行记录已更新: ExecutionId={ExecutionId}, Status={Status}", execution.Id, execution.Status);
    }

    /// <summary>
    /// 获取任务的执行历史
    /// </summary>
    public async Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int? limit = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = "SELECT * FROM task_executions WHERE task_id = @TaskId ORDER BY started_at DESC";
        if (limit.HasValue)
        {
            sql += $" LIMIT {limit.Value}";
        }

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@TaskId", taskId.ToString());

        var executions = new List<TaskExecution>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            executions.Add(MapToTaskExecution(reader));
        }

        return executions;
    }

    /// <summary>
    /// 获取任务的最新执行记录
    /// </summary>
    public async Task<TaskExecution?> GetLatestByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM task_executions WHERE task_id = @TaskId ORDER BY started_at DESC LIMIT 1";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@TaskId", taskId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToTaskExecution(reader);
        }

        return null;
    }

    /// <summary>
    /// 根据 ID 获取执行记录
    /// </summary>
    public async Task<TaskExecution?> GetByIdAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM task_executions WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", executionId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToTaskExecution(reader);
        }

        return null;
    }

    /// <summary>
    /// 映射 SqliteDataReader 到 TaskExecution
    /// </summary>
    private TaskExecution MapToTaskExecution(SqliteDataReader reader)
    {
        return new TaskExecution
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            TaskId = Guid.Parse(reader.GetString(reader.GetOrdinal("task_id"))),
            StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("started_at"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("completed_at"))),
            Status = (ExecutionStatus)reader.GetInt32(reader.GetOrdinal("status")),
            Output = reader.IsDBNull(reader.GetOrdinal("output")) ? null : reader.GetString(reader.GetOrdinal("output")),
            Error = reader.IsDBNull(reader.GetOrdinal("error")) ? null : reader.GetString(reader.GetOrdinal("error")),
            RetryCount = reader.GetInt32(reader.GetOrdinal("retry_count")),
            DurationMs = reader.IsDBNull(reader.GetOrdinal("duration_ms")) ? null : reader.GetInt64(reader.GetOrdinal("duration_ms")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("metadata")) ? null : reader.GetString(reader.GetOrdinal("metadata"))
        };
    }
}
