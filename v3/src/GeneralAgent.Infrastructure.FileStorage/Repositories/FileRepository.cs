using GeneralAgent.Infrastructure.FileStorage.Migrations;
using GeneralAgent.Infrastructure.FileStorage.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.FileStorage.Repositories;

/// <summary>
/// 文件元数据仓储（SQLite）
/// </summary>
public class FileRepository
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<FileRepository> _logger;
    private readonly string _connectionString;

    public FileRepository(
        IOptions<FileStorageOptions> options,
        ILogger<FileRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionString = $"Data Source={_options.DatabasePath}";

        // 确保数据库和表存在
        EnsureDatabaseInitialized();
    }

    /// <summary>
    /// 保存文件元数据
    /// </summary>
    public async Task<UploadedFile> SaveAsync(UploadedFile file, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO uploaded_files (
                id, session_id, file_name, file_path, file_type,
                file_size, mime_type, uploaded_at, summary, tags, metadata,
                owner_id, access_level, version, parent_file_id, updated_at, is_latest
            ) VALUES (
                @Id, @SessionId, @FileName, @FilePath, @FileType,
                @FileSize, @MimeType, @UploadedAt, @Summary, @Tags, @Metadata,
                @OwnerId, @AccessLevel, @Version, @ParentFileId, @UpdatedAt, @IsLatest
            )";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", file.Id.ToString());
        command.Parameters.AddWithValue("@SessionId", file.SessionId);
        command.Parameters.AddWithValue("@FileName", file.FileName);
        command.Parameters.AddWithValue("@FilePath", file.FilePath);
        command.Parameters.AddWithValue("@FileType", file.FileType);
        command.Parameters.AddWithValue("@FileSize", file.FileSize);
        command.Parameters.AddWithValue("@MimeType", (object?)file.MimeType ?? DBNull.Value);
        command.Parameters.AddWithValue("@UploadedAt", file.UploadedAt.ToString("O"));
        command.Parameters.AddWithValue("@Summary", (object?)file.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("@Tags", (object?)file.Tags ?? DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", (object?)file.Metadata ?? DBNull.Value);
        command.Parameters.AddWithValue("@OwnerId", file.OwnerId);
        command.Parameters.AddWithValue("@AccessLevel", (int)file.AccessLevel);
        command.Parameters.AddWithValue("@Version", file.Version);
        command.Parameters.AddWithValue("@ParentFileId", file.ParentFileId.HasValue ? file.ParentFileId.Value.ToString() : (object)DBNull.Value);
        command.Parameters.AddWithValue("@UpdatedAt", file.UpdatedAt.HasValue ? file.UpdatedAt.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsLatest", file.IsLatest ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("文件元数据已保存: {FileName} (ID: {FileId})", file.FileName, file.Id);
        return file;
    }

    /// <summary>
    /// 根据 ID 获取文件
    /// </summary>
    public async Task<UploadedFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM uploaded_files WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToUploadedFile(reader);
        }

        return null;
    }

    /// <summary>
    /// 根据文件名和会话 ID 获取文件
    /// </summary>
    public async Task<List<UploadedFile>> GetByFileNameAsync(
        string fileName,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM uploaded_files
            WHERE file_name = @FileName AND session_id = @SessionId
            ORDER BY uploaded_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FileName", fileName);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 列出会话的所有文件
    /// </summary>
    public async Task<List<UploadedFile>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM uploaded_files
            WHERE session_id = @SessionId
            ORDER BY uploaded_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 更新文件元数据
    /// </summary>
    public async Task<UploadedFile> UpdateAsync(UploadedFile file, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE uploaded_files SET
                summary = @Summary,
                tags = @Tags,
                metadata = @Metadata,
                access_level = @AccessLevel,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", file.Id.ToString());
        command.Parameters.AddWithValue("@Summary", (object?)file.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("@Tags", (object?)file.Tags ?? DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", (object?)file.Metadata ?? DBNull.Value);
        command.Parameters.AddWithValue("@AccessLevel", (int)file.AccessLevel);
        command.Parameters.AddWithValue("@UpdatedAt", file.UpdatedAt.HasValue ? file.UpdatedAt.Value.ToString("O") : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("文件元数据已更新: {FileName} (ID: {FileId})", file.FileName, file.Id);
        return file;
    }

    /// <summary>
    /// 删除文件元数据
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM uploaded_files WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation("文件元数据已删除: ID {FileId}", id);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据所有者列出文件
    /// </summary>
    public async Task<List<UploadedFile>> ListByOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM uploaded_files
            WHERE owner_id = @OwnerId AND is_latest = 1
            ORDER BY uploaded_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@OwnerId", ownerId);

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 根据访问级别列出文件
    /// </summary>
    public async Task<List<UploadedFile>> ListByAccessLevelAsync(
        FileAccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM uploaded_files
            WHERE access_level = @AccessLevel AND is_latest = 1
            ORDER BY uploaded_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@AccessLevel", (int)accessLevel);

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 搜索文件（按名称、标签、摘要）
    /// </summary>
    public async Task<List<UploadedFile>> SearchAsync(
        string keyword,
        string? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT * FROM uploaded_files
            WHERE is_latest = 1 AND (
                file_name LIKE @Keyword OR
                tags LIKE @Keyword OR
                summary LIKE @Keyword
            )";

        if (!string.IsNullOrEmpty(ownerId))
        {
            sql += " AND owner_id = @OwnerId";
        }

        sql += " ORDER BY uploaded_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Keyword", $"%{keyword}%");

        if (!string.IsNullOrEmpty(ownerId))
        {
            command.Parameters.AddWithValue("@OwnerId", ownerId);
        }

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 获取文件的所有版本
    /// </summary>
    public async Task<List<UploadedFile>> GetVersionsAsync(
        Guid rootFileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // 使用递归 CTE 查找版本链
        const string sql = @"
            WITH RECURSIVE version_chain AS (
                -- 基础情况：根文件
                SELECT * FROM uploaded_files WHERE id = @RootFileId
                UNION ALL
                -- 递归情况：子版本
                SELECT f.* FROM uploaded_files f
                INNER JOIN version_chain vc ON f.parent_file_id = vc.id
            )
            SELECT * FROM version_chain
            ORDER BY version ASC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@RootFileId", rootFileId.ToString());

        var files = new List<UploadedFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(MapToUploadedFile(reader));
        }

        return files;
    }

    /// <summary>
    /// 获取最新版本的文件
    /// </summary>
    public async Task<UploadedFile?> GetLatestVersionAsync(
        Guid rootFileId,
        CancellationToken cancellationToken = default)
    {
        var versions = await GetVersionsAsync(rootFileId, cancellationToken);
        return versions.FirstOrDefault(f => f.IsLatest);
    }

    /// <summary>
    /// 标记旧版本为非最新
    /// </summary>
    public async Task MarkAsNotLatestAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE uploaded_files SET is_latest = 0 WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", fileId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 初始化数据库和表结构
    /// </summary>
    private void EnsureDatabaseInitialized()
    {
        // 确保数据库目录存在
        var dbDirectory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
            _logger.LogInformation("创建数据库目录: {Directory}", dbDirectory);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // 创建基础表结构
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS uploaded_files (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                file_name TEXT NOT NULL,
                file_path TEXT NOT NULL,
                file_type TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                mime_type TEXT,
                uploaded_at TEXT NOT NULL,
                summary TEXT,
                tags TEXT,
                metadata TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_session_id ON uploaded_files(session_id);
            CREATE INDEX IF NOT EXISTS idx_file_name ON uploaded_files(file_name);
            CREATE INDEX IF NOT EXISTS idx_uploaded_at ON uploaded_files(uploaded_at);
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        command.ExecuteNonQuery();

        _logger.LogDebug("数据库基础表已初始化: {DatabasePath}", _options.DatabasePath);

        // 应用数据库迁移
        var migrationLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseMigrationManager>();
        var migrationManager = new DatabaseMigrationManager(_options.DatabasePath, migrationLogger);
        migrationManager.ApplyMigrationsAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 映射数据库记录到模型
    /// </summary>
    private static UploadedFile MapToUploadedFile(SqliteDataReader reader)
    {
        return new UploadedFile
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            SessionId = reader.GetString(reader.GetOrdinal("session_id")),
            FileName = reader.GetString(reader.GetOrdinal("file_name")),
            FilePath = reader.GetString(reader.GetOrdinal("file_path")),
            FileType = reader.GetString(reader.GetOrdinal("file_type")),
            FileSize = reader.GetInt64(reader.GetOrdinal("file_size")),
            MimeType = reader.IsDBNull(reader.GetOrdinal("mime_type"))
                ? null
                : reader.GetString(reader.GetOrdinal("mime_type")),
            UploadedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("uploaded_at"))),
            Summary = reader.IsDBNull(reader.GetOrdinal("summary"))
                ? null
                : reader.GetString(reader.GetOrdinal("summary")),
            Tags = reader.IsDBNull(reader.GetOrdinal("tags"))
                ? null
                : reader.GetString(reader.GetOrdinal("tags")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("metadata"))
                ? null
                : reader.GetString(reader.GetOrdinal("metadata")),
            OwnerId = reader.GetString(reader.GetOrdinal("owner_id")),
            AccessLevel = (FileAccessLevel)reader.GetInt32(reader.GetOrdinal("access_level")),
            Version = reader.GetInt32(reader.GetOrdinal("version")),
            ParentFileId = reader.IsDBNull(reader.GetOrdinal("parent_file_id"))
                ? null
                : Guid.Parse(reader.GetString(reader.GetOrdinal("parent_file_id"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            IsLatest = reader.GetInt32(reader.GetOrdinal("is_latest")) == 1
        };
    }
}
