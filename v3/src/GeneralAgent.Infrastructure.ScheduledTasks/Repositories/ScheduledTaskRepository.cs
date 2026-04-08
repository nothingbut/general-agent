using GeneralAgent.Infrastructure.ScheduledTasks.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskStatus = GeneralAgent.Infrastructure.ScheduledTasks.Models.TaskStatus;

namespace GeneralAgent.Infrastructure.ScheduledTasks.Repositories;

/// <summary>
/// 计划任务仓储实现（SQLite）
/// </summary>
public class ScheduledTaskRepository : IScheduledTaskRepository
{
    private readonly ScheduledTasksOptions _options;
    private readonly ILogger<ScheduledTaskRepository> _logger;
    private readonly string _connectionString;

    public ScheduledTaskRepository(
        IOptions<ScheduledTasksOptions> options,
        ILogger<ScheduledTaskRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionString = $"Data Source={_options.DatabasePath}";

        // 确保数据库和表存在
        EnsureDatabaseInitialized();
    }

    /// <summary>
    /// 创建任务
    /// </summary>
    public async Task<ScheduledTask> CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO scheduled_tasks (
                id, name, description, owner_id,
                schedule, schedule_type, start_at, end_at,
                task_type, task_payload, max_retries, timeout_seconds,
                status, created_at, updated_at, last_execution_at, next_execution_at, execution_count,
                tags, metadata
            ) VALUES (
                @Id, @Name, @Description, @OwnerId,
                @Schedule, @ScheduleType, @StartAt, @EndAt,
                @TaskType, @TaskPayload, @MaxRetries, @TimeoutSeconds,
                @Status, @CreatedAt, @UpdatedAt, @LastExecutionAt, @NextExecutionAt, @ExecutionCount,
                @Tags, @Metadata
            )";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", task.Id.ToString());
        command.Parameters.AddWithValue("@Name", task.Name);
        command.Parameters.AddWithValue("@Description", task.Description);
        command.Parameters.AddWithValue("@OwnerId", task.OwnerId);
        command.Parameters.AddWithValue("@Schedule", task.Schedule);
        command.Parameters.AddWithValue("@ScheduleType", (int)task.ScheduleType);
        command.Parameters.AddWithValue("@StartAt", task.StartAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EndAt", task.EndAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TaskType", (int)task.TaskType);
        command.Parameters.AddWithValue("@TaskPayload", task.TaskPayload);
        command.Parameters.AddWithValue("@MaxRetries", task.MaxRetries);
        command.Parameters.AddWithValue("@TimeoutSeconds", task.TimeoutSeconds);
        command.Parameters.AddWithValue("@Status", (int)task.Status);
        command.Parameters.AddWithValue("@CreatedAt", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", task.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastExecutionAt", task.LastExecutionAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@NextExecutionAt", task.NextExecutionAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ExecutionCount", task.ExecutionCount);
        command.Parameters.AddWithValue("@Tags", task.Tags ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", task.Metadata ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("计划任务已创建: {TaskName} (ID: {TaskId})", task.Name, task.Id);
        return task;
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    public async Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE scheduled_tasks SET
                name = @Name,
                description = @Description,
                schedule = @Schedule,
                schedule_type = @ScheduleType,
                start_at = @StartAt,
                end_at = @EndAt,
                task_type = @TaskType,
                task_payload = @TaskPayload,
                max_retries = @MaxRetries,
                timeout_seconds = @TimeoutSeconds,
                status = @Status,
                updated_at = @UpdatedAt,
                last_execution_at = @LastExecutionAt,
                next_execution_at = @NextExecutionAt,
                execution_count = @ExecutionCount,
                tags = @Tags,
                metadata = @Metadata
            WHERE id = @Id";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", task.Id.ToString());
        command.Parameters.AddWithValue("@Name", task.Name);
        command.Parameters.AddWithValue("@Description", task.Description);
        command.Parameters.AddWithValue("@Schedule", task.Schedule);
        command.Parameters.AddWithValue("@ScheduleType", (int)task.ScheduleType);
        command.Parameters.AddWithValue("@StartAt", task.StartAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EndAt", task.EndAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TaskType", (int)task.TaskType);
        command.Parameters.AddWithValue("@TaskPayload", task.TaskPayload);
        command.Parameters.AddWithValue("@MaxRetries", task.MaxRetries);
        command.Parameters.AddWithValue("@TimeoutSeconds", task.TimeoutSeconds);
        command.Parameters.AddWithValue("@Status", (int)task.Status);
        command.Parameters.AddWithValue("@UpdatedAt", task.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastExecutionAt", task.LastExecutionAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@NextExecutionAt", task.NextExecutionAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ExecutionCount", task.ExecutionCount);
        command.Parameters.AddWithValue("@Tags", task.Tags ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", task.Metadata ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("计划任务已更新: {TaskName} (ID: {TaskId})", task.Name, task.Id);
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    public async Task DeleteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM scheduled_tasks WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", taskId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("计划任务已删除: ID: {TaskId}", taskId);
    }

    /// <summary>
    /// 根据 ID 获取任务
    /// </summary>
    public async Task<ScheduledTask?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM scheduled_tasks WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", taskId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToScheduledTask(reader);
        }

        return null;
    }

    /// <summary>
    /// 列出用户的所有任务
    /// </summary>
    public async Task<List<ScheduledTask>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM scheduled_tasks WHERE owner_id = @OwnerId ORDER BY created_at DESC";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@OwnerId", ownerId);

        var tasks = new List<ScheduledTask>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(MapToScheduledTask(reader));
        }

        return tasks;
    }

    /// <summary>
    /// 按状态列出任务
    /// </summary>
    public async Task<List<ScheduledTask>> ListByStatusAsync(TaskStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM scheduled_tasks WHERE status = @Status ORDER BY created_at DESC";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Status", (int)status);

        var tasks = new List<ScheduledTask>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(MapToScheduledTask(reader));
        }

        return tasks;
    }

    /// <summary>
    /// 获取待执行的任务
    /// </summary>
    public async Task<List<ScheduledTask>> GetPendingTasksAsync(DateTime before, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM scheduled_tasks
            WHERE status = @Status
            AND next_execution_at IS NOT NULL
            AND next_execution_at <= @Before
            ORDER BY next_execution_at ASC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Status", (int)TaskStatus.Pending);
        command.Parameters.AddWithValue("@Before", before.ToString("O"));

        var tasks = new List<ScheduledTask>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(MapToScheduledTask(reader));
        }

        return tasks;
    }

    /// <summary>
    /// 列出用户的指定状态的任务
    /// </summary>
    public async Task<List<ScheduledTask>> ListByOwnerAndStatusAsync(string ownerId, TaskStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM scheduled_tasks WHERE owner_id = @OwnerId AND status = @Status ORDER BY created_at DESC";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@OwnerId", ownerId);
        command.Parameters.AddWithValue("@Status", (int)status);

        var tasks = new List<ScheduledTask>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(MapToScheduledTask(reader));
        }

        return tasks;
    }

    /// <summary>
    /// 映射 SqliteDataReader 到 ScheduledTask
    /// </summary>
    private ScheduledTask MapToScheduledTask(SqliteDataReader reader)
    {
        return new ScheduledTask
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Description = reader.GetString(reader.GetOrdinal("description")),
            OwnerId = reader.GetString(reader.GetOrdinal("owner_id")),
            Schedule = reader.GetString(reader.GetOrdinal("schedule")),
            ScheduleType = (ScheduleType)reader.GetInt32(reader.GetOrdinal("schedule_type")),
            StartAt = reader.IsDBNull(reader.GetOrdinal("start_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("start_at"))),
            EndAt = reader.IsDBNull(reader.GetOrdinal("end_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("end_at"))),
            TaskType = (TaskType)reader.GetInt32(reader.GetOrdinal("task_type")),
            TaskPayload = reader.GetString(reader.GetOrdinal("task_payload")),
            MaxRetries = reader.GetInt32(reader.GetOrdinal("max_retries")),
            TimeoutSeconds = reader.GetInt32(reader.GetOrdinal("timeout_seconds")),
            Status = (TaskStatus)reader.GetInt32(reader.GetOrdinal("status")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            LastExecutionAt = reader.IsDBNull(reader.GetOrdinal("last_execution_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_execution_at"))),
            NextExecutionAt = reader.IsDBNull(reader.GetOrdinal("next_execution_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("next_execution_at"))),
            ExecutionCount = reader.GetInt32(reader.GetOrdinal("execution_count")),
            Tags = reader.IsDBNull(reader.GetOrdinal("tags")) ? null : reader.GetString(reader.GetOrdinal("tags")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("metadata")) ? null : reader.GetString(reader.GetOrdinal("metadata"))
        };
    }

    /// <summary>
    /// 确保数据库已初始化
    /// </summary>
    private void EnsureDatabaseInitialized()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // 读取并执行迁移脚本
        var assembly = typeof(ScheduledTaskRepository).Assembly;
        var resourceName = "GeneralAgent.Infrastructure.ScheduledTasks.Migrations.001_CreateScheduledTasksTables.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("未找到数据库迁移脚本，将手动创建表");
            CreateTablesManually(connection);
            return;
        }

        using var reader = new StreamReader(stream);
        var migrationSql = reader.ReadToEnd();

        using var command = new SqliteCommand(migrationSql, connection);
        command.ExecuteNonQuery();

        _logger.LogInformation("计划任务数据库初始化完成");
    }

    /// <summary>
    /// 手动创建表（如果嵌入式资源不可用）
    /// </summary>
    private void CreateTablesManually(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS scheduled_tasks (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT NOT NULL,
                owner_id TEXT NOT NULL,
                schedule TEXT NOT NULL,
                schedule_type INTEGER NOT NULL,
                start_at TEXT NULL,
                end_at TEXT NULL,
                task_type INTEGER NOT NULL,
                task_payload TEXT NOT NULL,
                max_retries INTEGER NOT NULL DEFAULT 3,
                timeout_seconds INTEGER NOT NULL DEFAULT 300,
                status INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NULL,
                last_execution_at TEXT NULL,
                next_execution_at TEXT NULL,
                execution_count INTEGER NOT NULL DEFAULT 0,
                tags TEXT NULL,
                metadata TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_owner_id ON scheduled_tasks(owner_id);
            CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_status ON scheduled_tasks(status);
            CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_next_execution ON scheduled_tasks(next_execution_at);
            CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_created_at ON scheduled_tasks(created_at);

            CREATE TABLE IF NOT EXISTS task_executions (
                id TEXT PRIMARY KEY,
                task_id TEXT NOT NULL,
                started_at TEXT NOT NULL,
                completed_at TEXT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                output TEXT NULL,
                error TEXT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0,
                duration_ms INTEGER NULL,
                metadata TEXT NULL,
                FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_task_executions_task_id ON task_executions(task_id);
            CREATE INDEX IF NOT EXISTS idx_task_executions_started_at ON task_executions(started_at);
            CREATE INDEX IF NOT EXISTS idx_task_executions_status ON task_executions(status);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
