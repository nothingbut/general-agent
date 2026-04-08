using GeneralAgent.Infrastructure.FileStorage.Models;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件权限服务接口
/// </summary>
public interface IFilePermissionService
{
    /// <summary>
    /// 授予权限
    /// </summary>
    Task GrantPermissionAsync(
        Guid fileId,
        string userId,
        string grantedBy,
        PermissionType permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销权限
    /// </summary>
    Task RevokePermissionAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出文件的所有权限
    /// </summary>
    Task<List<FilePermission>> ListPermissionsAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新文件访问级别
    /// </summary>
    Task UpdateAccessLevelAsync(
        Guid fileId,
        string ownerId,
        FileAccessLevel newLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查用户是否有文件访问权限
    /// </summary>
    Task<bool> HasAccessAsync(
        Guid fileId,
        string userId,
        PermissionType requiredPermission = PermissionType.Read,
        CancellationToken cancellationToken = default);
}
