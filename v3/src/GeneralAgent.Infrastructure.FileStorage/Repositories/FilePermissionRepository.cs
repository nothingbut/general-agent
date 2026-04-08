using GeneralAgent.Infrastructure.FileStorage.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.FileStorage.Repositories;

/// <summary>
/// 文件权限仓储（SQLite）
/// </summary>
public class FilePermissionRepository : IFilePermissionRepository
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<FilePermissionRepository> _logger;
    private readonly string _connectionString;

    public FilePermissionRepository(
        IOptions<FileStorageOptions> options,
        ILogger<FilePermissionRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionString = $"Data Source={_options.DatabasePath}";
    }

    /// <summary>
    /// 保存权限记录
    /// </summary>
    public async Task<FilePermission> SaveAsync(FilePermission permission, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO file_permissions (
                id, file_id, user_id, permission, granted_at, granted_by
            ) VALUES (
                @Id, @FileId, @UserId, @Permission, @GrantedAt, @GrantedBy
            )";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", permission.Id.ToString());
        command.Parameters.AddWithValue("@FileId", permission.FileId.ToString());
        command.Parameters.AddWithValue("@UserId", permission.UserId);
        command.Parameters.AddWithValue("@Permission", (int)permission.Permission);
        command.Parameters.AddWithValue("@GrantedAt", permission.GrantedAt.ToString("O"));
        command.Parameters.AddWithValue("@GrantedBy", permission.GrantedBy);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("文件权限已保存: FileId={FileId}, UserId={UserId}, Permission={Permission}",
            permission.FileId, permission.UserId, permission.Permission);

        return permission;
    }

    /// <summary>
    /// 根据 ID 获取权限
    /// </summary>
    public async Task<FilePermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM file_permissions WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToFilePermission(reader);
        }

        return null;
    }

    /// <summary>
    /// 获取文件的所有权限
    /// </summary>
    public async Task<List<FilePermission>> ListByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM file_permissions
            WHERE file_id = @FileId
            ORDER BY granted_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FileId", fileId.ToString());

        var permissions = new List<FilePermission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            permissions.Add(MapToFilePermission(reader));
        }

        return permissions;
    }

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    public async Task<List<FilePermission>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM file_permissions
            WHERE user_id = @UserId
            ORDER BY granted_at DESC";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var permissions = new List<FilePermission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            permissions.Add(MapToFilePermission(reader));
        }

        return permissions;
    }

    /// <summary>
    /// 检查用户是否有文件权限
    /// </summary>
    public async Task<FilePermission?> GetByFileAndUserAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT * FROM file_permissions
            WHERE file_id = @FileId AND user_id = @UserId";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FileId", fileId.ToString());
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToFilePermission(reader);
        }

        return null;
    }

    /// <summary>
    /// 更新权限
    /// </summary>
    public async Task<FilePermission> UpdateAsync(FilePermission permission, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE file_permissions SET
                permission = @Permission
            WHERE id = @Id";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", permission.Id.ToString());
        command.Parameters.AddWithValue("@Permission", (int)permission.Permission);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("文件权限已更新: Id={Id}, Permission={Permission}",
            permission.Id, permission.Permission);

        return permission;
    }

    /// <summary>
    /// 删除权限
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM file_permissions WHERE id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation("文件权限已删除: Id={Id}", id);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 删除文件的所有权限
    /// </summary>
    public async Task<int> DeleteByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM file_permissions WHERE file_id = @FileId";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FileId", fileId.ToString());

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("文件权限已批量删除: FileId={FileId}, Count={Count}", fileId, rowsAffected);

        return rowsAffected;
    }

    /// <summary>
    /// 删除特定用户对文件的权限
    /// </summary>
    public async Task<bool> DeleteByFileAndUserAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM file_permissions WHERE file_id = @FileId AND user_id = @UserId";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FileId", fileId.ToString());
        command.Parameters.AddWithValue("@UserId", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation("文件权限已删除: FileId={FileId}, UserId={UserId}", fileId, userId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 映射数据库记录到模型
    /// </summary>
    private static FilePermission MapToFilePermission(SqliteDataReader reader)
    {
        return new FilePermission
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            FileId = Guid.Parse(reader.GetString(reader.GetOrdinal("file_id"))),
            UserId = reader.GetString(reader.GetOrdinal("user_id")),
            Permission = (PermissionType)reader.GetInt32(reader.GetOrdinal("permission")),
            GrantedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("granted_at"))),
            GrantedBy = reader.GetString(reader.GetOrdinal("granted_by"))
        };
    }
}
