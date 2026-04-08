using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Migrations;

/// <summary>
/// 数据库迁移管理器
/// </summary>
public class DatabaseMigrationManager
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMigrationManager> _logger;

    public DatabaseMigrationManager(string databasePath, ILogger<DatabaseMigrationManager> logger)
    {
        _connectionString = $"Data Source={databasePath}";
        _logger = logger;
    }

    /// <summary>
    /// 应用所有待执行的迁移
    /// </summary>
    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 确保 schema_migrations 表存在
        await EnsureSchemaMigrationsTableAsync(connection, cancellationToken);

        // 获取已应用的迁移版本
        var appliedVersions = await GetAppliedMigrationsAsync(connection, cancellationToken);

        // 定义所有迁移
        var migrations = new List<Migration>
        {
            new Migration(
                Version: 1,
                Name: "AddCrossSessionAccessFields",
                Sql: GetMigration001Sql())
        };

        // 应用未执行的迁移
        foreach (var migration in migrations.Where(m => !appliedVersions.Contains(m.Version)))
        {
            _logger.LogInformation("应用数据库迁移: {Version} - {Name}", migration.Version, migration.Name);
            await ApplyMigrationAsync(connection, migration, cancellationToken);
            _logger.LogInformation("数据库迁移 {Version} 应用成功", migration.Version);
        }
    }

    /// <summary>
    /// 确保 schema_migrations 表存在
    /// </summary>
    private async Task EnsureSchemaMigrationsTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at TEXT NOT NULL
            );";

        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 获取已应用的迁移版本列表
    /// </summary>
    private async Task<HashSet<int>> GetAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT version FROM schema_migrations";

        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var versions = new HashSet<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    /// <summary>
    /// 应用单个迁移
    /// </summary>
    private async Task ApplyMigrationAsync(
        SqliteConnection connection,
        Migration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 执行迁移 SQL
            await using var command = new SqliteCommand(migration.Sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // 记录迁移版本
            const string insertSql = @"
                INSERT INTO schema_migrations (version, name, applied_at)
                VALUES (@Version, @Name, @AppliedAt)";

            await using var insertCommand = new SqliteCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("@Version", migration.Version);
            insertCommand.Parameters.AddWithValue("@Name", migration.Name);
            insertCommand.Parameters.AddWithValue("@AppliedAt", DateTime.UtcNow.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 获取迁移 001 的 SQL
    /// </summary>
    private static string GetMigration001Sql()
    {
        return @"
            -- 添加跨会话访问字段到 uploaded_files 表
            ALTER TABLE uploaded_files ADD COLUMN owner_id TEXT NOT NULL DEFAULT 'system';
            ALTER TABLE uploaded_files ADD COLUMN access_level INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE uploaded_files ADD COLUMN version INTEGER NOT NULL DEFAULT 1;
            ALTER TABLE uploaded_files ADD COLUMN parent_file_id TEXT NULL;
            ALTER TABLE uploaded_files ADD COLUMN updated_at TEXT NULL;
            ALTER TABLE uploaded_files ADD COLUMN is_latest INTEGER NOT NULL DEFAULT 1;

            -- 创建索引以提高查询性能
            CREATE INDEX IF NOT EXISTS idx_owner_id ON uploaded_files(owner_id);
            CREATE INDEX IF NOT EXISTS idx_access_level ON uploaded_files(access_level);
            CREATE INDEX IF NOT EXISTS idx_parent_file_id ON uploaded_files(parent_file_id);
            CREATE INDEX IF NOT EXISTS idx_is_latest ON uploaded_files(is_latest);

            -- 创建文件权限表
            CREATE TABLE IF NOT EXISTS file_permissions (
                id TEXT PRIMARY KEY,
                file_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                permission INTEGER NOT NULL,
                granted_at TEXT NOT NULL,
                granted_by TEXT NOT NULL,
                FOREIGN KEY (file_id) REFERENCES uploaded_files(id) ON DELETE CASCADE
            );

            -- 创建权限表索引
            CREATE INDEX IF NOT EXISTS idx_file_id ON file_permissions(file_id);
            CREATE INDEX IF NOT EXISTS idx_user_id ON file_permissions(user_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_file_user ON file_permissions(file_id, user_id);
        ";
    }

    private record Migration(int Version, string Name, string Sql);
}
